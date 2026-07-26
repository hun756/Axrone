/**
 * A from-scratch GLSL ES preprocessor that powers the toolkit's variant system.
 *
 * It implements the subset of the C preprocessor that shader authors actually
 * rely on:
 *   - object-like and function-like macros (`#define`, `#undef`)
 *   - conditional inclusion (`#if`, `#ifdef`, `#ifndef`, `#elif`, `#else`,
 *     `#endif`) with full constant-expression evaluation (`defined()`,
 *     arithmetic, bitwise, logical and relational operators)
 *   - `#error`, `#pragma`, `#version`, `#extension`, `#line` passthrough
 *   - line-continuation (backslash) and comment stripping (line and block comments)
 *   - optional `#line` marker emission so driver-side GLSL compile errors map
 *     back to the authored source
 *
 * The preprocessor operates on the fully compiled shader source produced by
 * `compileRenderShaderEffect`, so it composes with the existing render-core
 * compiler instead of replacing it. Recoverable issues are reported through a
 * {@link ShaderDiagnosticSink}; `#error` and structural problems (unbalanced
 * conditionals) throw a {@link ShaderPreprocessError} that carries the report.
 */

import {
    type ShaderDiagnostic,
    type ShaderDiagnosticSink,
    createDiagnosticSink,
} from './diagnostics';

export interface ShaderPreprocessOptions {
    /**
     * Macro values made available as if `#define`d before evaluation.
     * Booleans become `1`/`0`, numbers and strings are used verbatim.
     */
    readonly defines?: Readonly<Record<string, string | number | boolean>>;
    /** Logical name used in diagnostics and `#line` markers. */
    readonly sourceId?: string;
    /** Emit `#line` markers so driver errors map back to authored lines. */
    readonly preserveLineMarkers?: boolean;
    /** Sink to collect diagnostics into. A fresh one is created when omitted. */
    readonly sink?: ShaderDiagnosticSink;
}

export interface ShaderPreprocessResult {
    readonly code: string;
    readonly diagnostics: readonly ShaderDiagnostic[];
}

export class ShaderPreprocessError extends Error {
    readonly diagnostics: readonly ShaderDiagnostic[];
    constructor(message: string, diagnostics: readonly ShaderDiagnostic[]) {
        super(message);
        this.name = 'ShaderPreprocessError';
        this.diagnostics = diagnostics;
    }
}

interface ShaderMacro {
    readonly name: string;
    readonly params: readonly string[] | undefined;
    readonly body: string;
}

const isIdentStart = (char: string): boolean => /[A-Za-z_$]/.test(char);
const isIdentPart = (char: string): boolean => /[A-Za-z0-9_$]/.test(char);

const stripComments = (source: string): string => {
    let state: 'code' | 'line' | 'block' = 'code';
    let result = '';

    for (let index = 0; index < source.length; index += 1) {
        const char = source[index];
        const next = source[index + 1];

        if (state === 'code') {
            if (char === '/' && next === '/') {
                state = 'line';
                result += '  ';
                index += 1;
                continue;
            }
            if (char === '/' && next === '*') {
                state = 'block';
                result += '  ';
                index += 1;
                continue;
            }
            result += char;
            continue;
        }

        if (state === 'line') {
            if (char === '\n') {
                state = 'code';
                result += char;
            } else {
                result += ' ';
            }
            continue;
        }

        // state === 'block'
        if (char === '*' && next === '/') {
            state = 'code';
            result += '  ';
            index += 1;
            continue;
        }
        result += char === '\n' ? char : ' ';
    }

    return result;
};

const coerceDefineValue = (value: string | number | boolean): string => {
    if (value === true) return '1';
    if (value === false) return '0';
    return String(value);
};

const parseDefine = (rest: string, name: string): ShaderMacro => {
    if (rest.charCodeAt(0) === 40 /* '(' */) {
        let depth = 0;
        let closed = false;
        const params: string[] = [];
        let current = '';

        for (let index = 0; index < rest.length; index += 1) {
            const char = rest[index];
            if (char === '(') {
                depth += 1;
                if (depth > 1) current += char;
                continue;
            }
            if (char === ')') {
                depth -= 1;
                if (depth === 0) {
                    closed = true;
                    if (current.trim() !== '') params.push(current.trim());
                    continue;
                }
                current += char;
                continue;
            }
            if (char === ',' && depth === 1) {
                params.push(current.trim());
                current = '';
                continue;
            }
            current += char;
        }

        if (!closed) {
            // malformed — treat as object-like
            return { name, params: undefined, body: `(${rest}` };
        }

        const body = rest.slice(rest.indexOf(')') + 1).trim();
        return { name, params: params.filter((param) => param !== ''), body };
    }

    return { name, params: undefined, body: rest.trim() };
};

const readCallArguments = (
    text: string,
    openParenIndex: number
): { readonly args: readonly string[]; readonly nextIndex: number } | undefined => {
    if (text[openParenIndex] !== '(') return undefined;

    const args: string[] = [];
    let current = '';
    let depth = 0;

    for (let index = openParenIndex; index < text.length; index += 1) {
        const char = text[index];
        if (char === '(') {
            depth += 1;
            if (depth > 1) current += char;
            continue;
        }
        if (char === ')') {
            depth -= 1;
            if (depth === 0) {
                if (current.trim() !== '' || args.length > 0) args.push(current.trim());
                return { args, nextIndex: index + 1 };
            }
            current += char;
            continue;
        }
        if (char === ',' && depth === 1) {
            args.push(current.trim());
            current = '';
            continue;
        }
        current += char;
    }

    return undefined;
};

const substituteParameters = (macro: ShaderMacro, args: readonly string[]): string => {
    if (!macro.params) return macro.body;
    const map = new Map<string, string>();
    macro.params.forEach((param, index) => {
        map.set(param, args[index] ?? '');
    });

    let out = '';
    let index = 0;
    while (index < macro.body.length) {
        const char = macro.body[index];
        if (isIdentStart(char)) {
            let end = index + 1;
            while (end < macro.body.length && isIdentPart(macro.body[end])) end += 1;
            const token = macro.body.slice(index, end);
            out += map.has(token) ? (map.get(token) as string) : token;
            index = end;
            continue;
        }
        out += char;
        index += 1;
    }
    return out;
};

const expandMacros = (
    text: string,
    macros: Map<string, ShaderMacro>,
    active: ReadonlySet<string> = new Set()
): string => {
    let out = '';
    let index = 0;

    while (index < text.length) {
        const char = text[index];
        if (!isIdentStart(char)) {
            out += char;
            index += 1;
            continue;
        }

        let end = index + 1;
        while (end < text.length && isIdentPart(text[end])) end += 1;
        const name = text.slice(index, end);
        const macro = macros.get(name);

        if (!macro || active.has(name)) {
            out += name;
            index = end;
            continue;
        }

        let consumed = end;
        if (macro.params) {
            let scan = end;
            while (scan < text.length && (text[scan] === ' ' || text[scan] === '\t')) scan += 1;
            const call = readCallArguments(text, scan);
            if (!call) {
                out += name;
                index = end;
                continue;
            }
            const expandedArgs = call.args.map((arg) => expandMacros(arg, macros));
            const substituted = substituteParameters(macro, expandedArgs);
            const nextActive = new Set(active);
            nextActive.add(name);
            out += expandMacros(substituted, macros, nextActive);
            consumed = call.nextIndex;
        } else {
            const nextActive = new Set(active);
            nextActive.add(name);
            out += expandMacros(macro.body, macros, nextActive);
        }

        index = consumed;
    }

    return out;
};

interface ExpressionToken {
    readonly kind: 'num' | 'op' | 'lparen' | 'rparen' | 'question' | 'colon';
    readonly value: number | string;
}

const tokenizeExpression = (expr: string): ExpressionToken[] => {
    const tokens: ExpressionToken[] = [];
    let index = 0;

    while (index < expr.length) {
        const char = expr[index];
        if (char === ' ' || char === '\t') {
            index += 1;
            continue;
        }
        if (/[0-9]/.test(char) || (char === '.' && /[0-9]/.test(expr[index + 1] ?? ''))) {
            let end = index;
            while (end < expr.length && /[0-9a-fA-FxXuUlLfF.]/.test(expr[end])) end += 1;
            const raw = expr.slice(index, end);
            index = end;
            let value: number;
            if (/^0[xX]/.test(raw)) value = parseInt(raw, 16);
            else if (raw.includes('.')) value = Math.trunc(Number.parseFloat(raw));
            else value = Number.parseInt(raw, 10);
            tokens.push({ kind: 'num', value: Number.isNaN(value) ? 0 : value });
            continue;
        }
        const two = expr.slice(index, index + 2);
        if (
            ['<<', '>>', '<=', '>=', '==', '!=', '&&', '||'].includes(two)
        ) {
            tokens.push({ kind: 'op', value: two });
            index += 2;
            continue;
        }
        if ('+-*/%<>&^|~!'.includes(char)) {
            tokens.push({ kind: 'op', value: char });
            index += 1;
            continue;
        }
        if (char === '(') {
            tokens.push({ kind: 'lparen', value: char });
            index += 1;
            continue;
        }
        if (char === ')') {
            tokens.push({ kind: 'rparen', value: char });
            index += 1;
            continue;
        }
        if (char === '?') {
            tokens.push({ kind: 'question', value: char });
            index += 1;
            continue;
        }
        if (char === ':') {
            tokens.push({ kind: 'colon', value: char });
            index += 1;
            continue;
        }
        if (isIdentStart(char)) {
            // Undefined identifier in a constant expression resolves to 0.
            let end = index + 1;
            while (end < expr.length && isIdentPart(expr[end])) end += 1;
            index = end;
            tokens.push({ kind: 'num', value: 0 });
            continue;
        }
        index += 1;
    }

    return tokens;
};

const BINARY_PRECEDENCE: ReadonlyArray<readonly string[]> = [
    ['||'],
    ['&&'],
    ['|'],
    ['^'],
    ['&'],
    ['==', '!='],
    ['<', '<=', '>', '>='],
    ['<<', '>>'],
    ['+', '-'],
    ['*', '/', '%'],
];

const precedenceOf = (operator: string): number => {
    for (let level = 0; level < BINARY_PRECEDENCE.length; level += 1) {
        if (BINARY_PRECEDENCE[level].includes(operator)) return level;
    }
    return -1;
};

const evaluateConstantExpression = (tokens: ExpressionToken[]): number => {
    let cursor = 0;

    const peek = (): ExpressionToken | undefined => tokens[cursor];
    const consume = (): ExpressionToken | undefined => tokens[cursor++];

    const parsePrimary = (): number => {
        const token = consume();
        if (!token) return 0;
        if (token.kind === 'num') return token.value as number;
        if (token.kind === 'lparen') {
            const value = parseTernary();
            if (peek()?.kind === 'rparen') consume();
            return value;
        }
        return 0;
    };

    const parseUnary = (): number => {
        const token = peek();
        if (token && token.kind === 'op') {
            const operator = token.value as string;
            if (['!', '~', '-', '+'].includes(operator)) {
                consume();
                const value = parseUnary();
                if (operator === '!') return value === 0 ? 1 : 0;
                if (operator === '~') return ~value;
                if (operator === '-') return -value;
                return value;
            }
        }
        return parsePrimary();
    };

    const parseBinary = (minPrecedence: number): number => {
        let left = parseUnary();
        while (true) {
            const token = peek();
            if (!token || token.kind !== 'op') break;
            const operator = token.value as string;
            const precedence = precedenceOf(operator);
            if (precedence < 0 || precedence < minPrecedence) break;
            consume();
            const right = parseBinary(precedence + 1);
            left = applyBinary(operator, left, right);
        }
        return left;
    };

    const parseTernary = (): number => {
        const condition = parseBinary(0);
        if (peek()?.kind === 'question') {
            consume();
            const thenValue = parseTernary();
            if (peek()?.kind === 'colon') consume();
            const elseValue = parseTernary();
            return condition !== 0 ? thenValue : elseValue;
        }
        return condition;
    };

    const applyBinary = (operator: string, left: number, right: number): number => {
        switch (operator) {
            case '*':
                return left * right;
            case '/':
                return right === 0 ? 0 : Math.trunc(left / right);
            case '%':
                return right === 0 ? 0 : left - Math.trunc(left / right) * right;
            case '+':
                return left + right;
            case '-':
                return left - right;
            case '<<':
                return left << right;
            case '>>':
                return left >> right;
            case '<':
                return left < right ? 1 : 0;
            case '<=':
                return left <= right ? 1 : 0;
            case '>':
                return left > right ? 1 : 0;
            case '>=':
                return left >= right ? 1 : 0;
            case '==':
                return left === right ? 1 : 0;
            case '!=':
                return left !== right ? 1 : 0;
            case '&':
                return left & right;
            case '^':
                return left ^ right;
            case '|':
                return left | right;
            case '&&':
                return left !== 0 && right !== 0 ? 1 : 0;
            case '||':
                return left !== 0 || right !== 0 ? 1 : 0;
            default:
                return 0;
        }
    };

    return parseTernary();
};

const resolveDefinedOperator = (
    text: string,
    macros: Map<string, ShaderMacro>
): string => {
    let out = '';
    let index = 0;

    while (index < text.length) {
        const char = text[index];
        if (!isIdentStart(char) || text.slice(index, index + 7) !== 'defined') {
            out += char;
            index += 1;
            continue;
        }
        // confirm it is a standalone token
        const after = index + 7;
        if (after < text.length && isIdentPart(text[after])) {
            out += char;
            index += 1;
            continue;
        }

        let scan = after;
        while (scan < text.length && (text[scan] === ' ' || text[scan] === '\t')) scan += 1;
        const parenthesised = text[scan] === '(';
        if (parenthesised) scan += 1;
        while (scan < text.length && (text[scan] === ' ' || text[scan] === '\t')) scan += 1;

        let name = '';
        while (scan < text.length && isIdentPart(text[scan])) {
            name += text[scan];
            scan += 1;
        }
        if (parenthesised) {
            while (scan < text.length && (text[scan] === ' ' || text[scan] === '\t')) scan += 1;
            if (text[scan] === ')') scan += 1;
        }

        out += macros.has(name) ? '1' : '0';
        index = scan;
    }

    return out;
};

const evaluateDirectiveCondition = (
    expression: string,
    macros: Map<string, ShaderMacro>
): number => {
    const withDefined = resolveDefinedOperator(expression, macros);
    const expanded = expandMacros(withDefined, macros);
    const tokens = tokenizeExpression(expanded);
    return evaluateConstantExpression(tokens);
};

interface ConditionalFrame {
    active: boolean;
    taken: boolean;
    seenElse: boolean;
    parentActive: boolean;
    line: number;
}

const parseDirectiveLine = (
    line: string
): { readonly name: string; readonly rest: string } | undefined => {
    const trimmed = line.trimStart();
    if (trimmed.charCodeAt(0) !== 35 /* '#' */) return undefined;
    const body = trimmed.slice(1).trimStart();
    const match = body.match(/^([A-Za-z_]\w*)/);
    if (!match) return { name: '', rest: body };
    const name = match[1];
    return { name, rest: body.slice(name.length).trim() };
};

export const preprocessGLSL = (
    source: string,
    options: ShaderPreprocessOptions = {}
): ShaderPreprocessResult => {
    const sink = options.sink ?? createDiagnosticSink();
    const sourceId = options.sourceId;
    const macros = new Map<string, ShaderMacro>();
    const loc = (line?: number) => (line !== undefined ? { line, sourceId } : {});

    for (const [name, value] of Object.entries(options.defines ?? {})) {
        macros.set(name, { name, params: undefined, body: coerceDefineValue(value) });
    }

    const withoutComments = stripComments(source);
    const physicalLines = withoutComments.split('\n');

    interface LogicalLine {
        readonly text: string;
        readonly line: number;
    }
    const logicalLines: LogicalLine[] = [];
    let logicalIndex = 0;
    while (logicalIndex < physicalLines.length) {
        let text = physicalLines[logicalIndex];
        const startLine = logicalIndex + 1;
        while (text.endsWith('\\') && logicalIndex + 1 < physicalLines.length) {
            text = `${text.slice(0, -1)}${physicalLines[logicalIndex + 1]}`;
            logicalIndex += 1;
        }
        logicalLines.push({ text, line: startLine });
        logicalIndex += 1;
    }

    const frames: ConditionalFrame[] = [];
    const isEmitting = () => frames.length === 0 || frames.every((frame) => frame.active);
    const parentActive = () =>
        frames.length <= 1 ? true : frames.slice(0, -1).every((frame) => frame.active);

    interface OutputLine {
        readonly text: string;
        readonly line: number;
        readonly directive: boolean;
    }
    const output: OutputLine[] = [];
    const passThrough = (text: string, line: number) => output.push({ text, line, directive: true });

    for (const { text, line } of logicalLines) {
        const directive = parseDirectiveLine(text);

        if (!directive) {
            if (isEmitting()) output.push({ text, line, directive: false });
            continue;
        }

        switch (directive.name) {
            case 'if': {
                const condition = isEmitting()
                    ? evaluateDirectiveCondition(directive.rest, macros) !== 0
                    : false;
                frames.push({
                    active: condition,
                    taken: condition,
                    seenElse: false,
                    parentActive: isEmitting(),
                    line,
                });
                break;
            }
            case 'ifdef': {
                const condition = isEmitting() && macros.has(directive.rest.trim());
                frames.push({
                    active: condition,
                    taken: condition,
                    seenElse: false,
                    parentActive: condition ? true : isEmitting(),
                    line,
                });
                break;
            }
            case 'ifndef': {
                const condition = isEmitting() && !macros.has(directive.rest.trim());
                frames.push({
                    active: condition,
                    taken: condition,
                    seenElse: false,
                    parentActive: isEmitting(),
                    line,
                });
                break;
            }
            case 'elif': {
                const frame = frames[frames.length - 1];
                if (!frame) {
                    sink.reportError('PP_UNEXPECTED_ELIF', '#elif without matching #if', loc(line));
                    break;
                }
                if (frame.seenElse) {
                    sink.reportError('PP_ELIF_AFTER_ELSE', '#elif after #else', loc(line));
                    break;
                }
                const evaluate = frame.parentActive && !frame.taken;
                const condition = evaluate
                    ? evaluateDirectiveCondition(directive.rest, macros) !== 0
                    : false;
                frame.active = condition;
                frame.taken = frame.taken || condition;
                break;
            }
            case 'else': {
                const frame = frames[frames.length - 1];
                if (!frame) {
                    sink.reportError('PP_UNEXPECTED_ELSE', '#else without matching #if', loc(line));
                    break;
                }
                if (frame.seenElse) {
                    sink.reportError('PP_DUPLICATE_ELSE', 'duplicate #else', loc(line));
                    break;
                }
                frame.active = frame.parentActive && !frame.taken;
                frame.taken = true;
                frame.seenElse = true;
                break;
            }
            case 'endif': {
                if (frames.length === 0) {
                    sink.reportError('PP_UNEXPECTED_ENDIF', '#endif without matching #if', loc(line));
                    break;
                }
                frames.pop();
                break;
            }
            case 'define': {
                if (isEmitting()) {
                    const match = directive.rest.match(/^([A-Za-z_]\w*)/);
                    if (match) {
                        const name = match[1];
                        macros.set(name, parseDefine(directive.rest.slice(name.length), name));
                    }
                }
                break;
            }
            case 'undef': {
                if (isEmitting()) macros.delete(directive.rest.trim());
                break;
            }
            case 'error': {
                if (isEmitting()) {
                    sink.reportError('PP_ERROR', directive.rest.trim() || '#error', loc(line));
                }
                break;
            }
            case 'version':
            case 'extension':
            case 'pragma':
            case 'line':
                if (isEmitting()) passThrough(text.trimStart(), line);
                break;
            default:
                if (isEmitting()) passThrough(text.trimStart(), line);
                break;
        }
    }

    if (frames.length > 0) {
        sink.reportError('PP_UNTERMINATED_IF', 'unterminated conditional directive', {
            line: frames[frames.length - 1]?.line ?? 1,
            sourceId,
        });
    }

    const fragments: string[] = [];
    let lastEmittedLine = 0;
    const preserveLineMarkers = options.preserveLineMarkers ?? false;

    for (const entry of output) {
        const expanded = entry.directive ? entry.text : expandMacros(entry.text, macros);
        if (
            preserveLineMarkers &&
            (lastEmittedLine === 0 || entry.line !== lastEmittedLine + 1)
        ) {
            fragments.push(`#line ${entry.line}${sourceId ? ` "${sourceId}"` : ''}`);
        }
        fragments.push(expanded);
        lastEmittedLine = entry.line;
    }

    const code = fragments.join('\n');

    if (sink.hasErrors) {
        throw new ShaderPreprocessError(
            `Shader preprocessing failed with ${sink.diagnostics.filter((d) => d.severity === 'error').length} error(s)`,
            sink.diagnostics
        );
    }

    return { code, diagnostics: sink.diagnostics };
};
