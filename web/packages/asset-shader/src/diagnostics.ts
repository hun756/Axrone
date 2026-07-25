/**
 * Structured diagnostics for the shader toolkit.
 *
 * Every toolkit pass (preprocessor, library resolver, variant builder, …)
 * reports issues into a {@link ShaderDiagnosticSink} instead of throwing for
 * recoverable problems. Fatal problems (e.g. a hard `#error` or an unbalanced
 * `#endif`) still throw, but they carry the collected diagnostics so callers
 * can surface a full report — exactly how Unity / Cocos surface shader errors.
 */

export type ShaderDiagnosticSeverity = 'error' | 'warning' | 'info';

export interface ShaderDiagnosticLocation {
    readonly line?: number;
    readonly column?: number;
    readonly sourceId?: string;
}

export interface ShaderDiagnostic {
    readonly code: string;
    readonly severity: ShaderDiagnosticSeverity;
    readonly message: string;
    readonly line?: number;
    readonly column?: number;
    readonly sourceId?: string;
}

export interface ShaderDiagnosticSink {
    readonly diagnostics: readonly ShaderDiagnostic[];
    readonly hasErrors: boolean;
    report(diagnostic: ShaderDiagnostic): void;
    reportError(code: string, message: string, location?: ShaderDiagnosticLocation): void;
    reportWarning(code: string, message: string, location?: ShaderDiagnosticLocation): void;
    reportInfo(code: string, message: string, location?: ShaderDiagnosticLocation): void;
    clear(): void;
}

export const createDiagnosticSink = (): ShaderDiagnosticSink => {
    const diagnostics: ShaderDiagnostic[] = [];

    return {
        get diagnostics() {
            return diagnostics;
        },
        get hasErrors() {
            return diagnostics.some((diagnostic) => diagnostic.severity === 'error');
        },
        report(diagnostic: ShaderDiagnostic): void {
            diagnostics.push(diagnostic);
        },
        reportError(code, message, location) {
            diagnostics.push({ code, severity: 'error', message, ...location });
        },
        reportWarning(code, message, location) {
            diagnostics.push({ code, severity: 'warning', message, ...location });
        },
        reportInfo(code, message, location) {
            diagnostics.push({ code, severity: 'info', message, ...location });
        },
        clear(): void {
            diagnostics.length = 0;
        },
    };
};

export const formatShaderDiagnostic = (diagnostic: ShaderDiagnostic): string => {
    const where =
        diagnostic.sourceId || diagnostic.line !== undefined
            ? `${diagnostic.sourceId ? `${diagnostic.sourceId}` : ''}${
                  diagnostic.line !== undefined
                      ? `${diagnostic.sourceId ? ':' : ''}${diagnostic.line}${
                            diagnostic.column !== undefined ? `:${diagnostic.column}`
                            : ''
                        }`
                      : ''
              }`
            : '';
    const prefix = where ? `${where}: ` : '';
    return `${prefix}${diagnostic.severity} [${diagnostic.code}]: ${diagnostic.message}`;
};
