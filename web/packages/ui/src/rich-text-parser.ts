import type { RichTextSpan } from './types/widget';
import type { ColorInput } from './types/layout';
import type { FontStyle, FontWeight } from './types/foundation';
import { normalizeWeight } from './runtime/internals';

interface ActiveStyle {
    readonly tag: string;
    readonly patch: Partial<RichTextSpan>;
}

interface ParsedTag {
    readonly tagName: string;
    readonly closing: boolean;
    readonly patch: Partial<RichTextSpan>;
}

const parseTagContent = (content: string): ParsedTag | null => {
    if (content.startsWith('/')) {
        const tagName = content.slice(1).trim().toLowerCase();
        if (tagName.length === 0) {
            return null;
        }
        return { tagName, closing: true, patch: {} };
    }
    const eqIndex = content.indexOf('=');
    if (eqIndex !== -1) {
        const tagName = content.slice(0, eqIndex).trim().toLowerCase();
        const rawValue = content.slice(eqIndex + 1).trim();
        switch (tagName) {
            case 'color': {
                const value = rawValue.replace(/^["']|["']$/g, '');
                if (value.length === 0) {
                    return null;
                }
                return { tagName, closing: false, patch: { color: value as ColorInput } };
            }
            case 'size': {
                const parsed = Number(rawValue);
                if (!Number.isFinite(parsed) || parsed <= 0) {
                    return null;
                }
                return { tagName, closing: false, patch: { size: parsed } };
            }
            case 'family': {
                const value = rawValue.replace(/^["']|["']$/g, '');
                if (value.length === 0) {
                    return null;
                }
                return { tagName, closing: false, patch: { family: value } };
            }
            case 'weight': {
                const value = rawValue.replace(/^["']|["']$/g, '').toLowerCase();
                // Try to parse as a number first (e.g., '400')
                const numericWeight = Number(value);
                let weightValue: FontWeight;
                if (Number.isFinite(numericWeight)) {
                    weightValue = numericWeight as FontWeight;
                } else {
                    weightValue = value as FontWeight;
                }
                // Validate by checking if normalizeWeight returns a valid weight (100-900)
                const normalized = normalizeWeight(weightValue);
                if (normalized < 100 || normalized > 900) {
                    return null;
                }
                return { tagName, closing: false, patch: { weight: weightValue } };
            }
            default:
                return null;
        }
    }
    const tagName = content.trim().toLowerCase();
    switch (tagName) {
        case 'b':
        case 'bold':
            return { tagName: 'b', closing: false, patch: { weight: 'bold' as FontWeight } };
        case 'i':
        case 'italic':
            return { tagName: 'i', closing: false, patch: { style: 'italic' as FontStyle } };
        case 'u':
        case 'underline':
            return { tagName: 'u', closing: false, patch: { underline: true } };
        case 's':
        case 'strike':
        case 'strikethrough':
            return { tagName: 's', closing: false, patch: { strikeThrough: true } };
        default:
            return null;
    }
};

const mergeStyleStack = (stack: readonly ActiveStyle[]): Partial<RichTextSpan> => {
    const result: Record<string, unknown> = {};
    for (const entry of stack) {
        for (const [key, value] of Object.entries(entry.patch)) {
            if (value !== undefined) {
                result[key] = value;
            }
        }
    }
    return result as Partial<RichTextSpan>;
};

/**
 * Parses a BBCode-like markup string into an array of RichTextSpan.
 *
 * Supported tags:
 * - [b] / [bold] ... [/b] — bold weight
 * - [i] / [italic] ... [/i] — italic style
 * - [u] / [underline] ... [/u] — underline
 * - [s] / [strike] ... [/s] — strikethrough
 * - [color=#rrggbb] ... [/color] — text color (hex string)
 * - [size=N] ... [/size] — font size in pixels
 * - [family=name] ... [/family] — font family name
 * - [weight=bold] ... [/weight] — explicit font weight
 *
 * Tags can be nested. Unknown tags are treated as literal text.
 * Unclosed tags are auto-closed at the end of the markup.
 * Closing a tag also auto-closes any tags nested inside it.
 */
export const parseRichTextMarkup = (markup: string): RichTextSpan[] => {
    if (markup.length === 0) {
        return [];
    }
    const spans: RichTextSpan[] = [];
    const stack: ActiveStyle[] = [];
    let cursor = 0;
    let textStart = 0;

    const flushText = (end: number): void => {
        if (end > textStart) {
            const text = markup.slice(textStart, end);
            const currentStyle = mergeStyleStack(stack);
            spans.push({ text, ...currentStyle });
        }
    };

    while (cursor < markup.length) {
        if (markup[cursor] !== '[') {
            cursor += 1;
            continue;
        }
        const closeBracket = markup.indexOf(']', cursor + 1);
        if (closeBracket === -1) {
            break;
        }
        const tagContent = markup.slice(cursor + 1, closeBracket);
        const parsed = parseTagContent(tagContent);
        if (parsed === null) {
            cursor += 1;
            continue;
        }
        flushText(cursor);
        if (parsed.closing) {
            for (let index = stack.length - 1; index >= 0; index -= 1) {
                if (stack[index].tag === parsed.tagName) {
                    stack.splice(index);
                    break;
                }
            }
        } else {
            stack.push({ tag: parsed.tagName, patch: parsed.patch });
        }
        textStart = closeBracket + 1;
        cursor = closeBracket + 1;
    }
    flushText(markup.length);

    // Merge consecutive spans with identical styles to keep the output compact.
    return compactSpans(spans);
};

const compactSpans = (spans: readonly RichTextSpan[]): RichTextSpan[] => {
    if (spans.length <= 1) {
        return [...spans];
    }
    const result: RichTextSpan[] = [spans[0]];
    for (let index = 1; index < spans.length; index += 1) {
        const current = spans[index];
        const previous = result[result.length - 1];
        if (
            current.color === previous.color &&
            current.size === previous.size &&
            current.weight === previous.weight &&
            current.style === previous.style &&
            current.family === previous.family &&
            current.underline === previous.underline &&
            current.strikeThrough === previous.strikeThrough
        ) {
            result[result.length - 1] = { ...previous, text: previous.text + current.text };
        } else {
            result.push(current);
        }
    }
    return result;
};
