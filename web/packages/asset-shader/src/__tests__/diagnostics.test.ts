import { describe, expect, it } from 'vitest';
import { createDiagnosticSink, formatShaderDiagnostic } from '../diagnostics';

describe('createDiagnosticSink', () => {
    it('starts empty with no errors', () => {
        const sink = createDiagnosticSink();
        expect(sink.diagnostics).toHaveLength(0);
        expect(sink.hasErrors).toBe(false);
    });

    it('report() appends a raw diagnostic', () => {
        const sink = createDiagnosticSink();
        sink.report({ code: 'TEST', severity: 'info', message: 'hello' });
        expect(sink.diagnostics).toHaveLength(1);
        expect(sink.diagnostics[0]).toMatchObject({
            code: 'TEST',
            severity: 'info',
            message: 'hello',
        });
    });

    it('reportError() pushes an error and flips hasErrors', () => {
        const sink = createDiagnosticSink();
        sink.reportError('E001', 'something broke', { line: 10, sourceId: 'a.glsl' });
        expect(sink.hasErrors).toBe(true);
        expect(sink.diagnostics[0]).toMatchObject({
            code: 'E001',
            severity: 'error',
            message: 'something broke',
            line: 10,
            sourceId: 'a.glsl',
        });
    });

    it('reportWarning() pushes a warning without affecting hasErrors', () => {
        const sink = createDiagnosticSink();
        sink.reportWarning('W001', 'risky stuff');
        expect(sink.hasErrors).toBe(false);
        expect(sink.diagnostics[0]?.severity).toBe('warning');
    });

    it('reportInfo() pushes an info diagnostic', () => {
        const sink = createDiagnosticSink();
        sink.reportInfo('I001', 'fyi', { column: 5 });
        expect(sink.diagnostics[0]?.severity).toBe('info');
        expect(sink.diagnostics[0]?.column).toBe(5);
    });

    it('hasErrors is true only when at least one error-severity diagnostic exists', () => {
        const sink = createDiagnosticSink();
        sink.reportWarning('W', 'w');
        sink.reportInfo('I', 'i');
        expect(sink.hasErrors).toBe(false);
        sink.reportError('E', 'e');
        expect(sink.hasErrors).toBe(true);
    });

    it('clear() empties the diagnostics array', () => {
        const sink = createDiagnosticSink();
        sink.reportError('E1', 'one');
        sink.reportWarning('W1', 'two');
        expect(sink.diagnostics).toHaveLength(2);
        sink.clear();
        expect(sink.diagnostics).toHaveLength(0);
        expect(sink.hasErrors).toBe(false);
    });

    it('location fields are spread into the diagnostic', () => {
        const sink = createDiagnosticSink();
        sink.reportError('X', 'msg', { line: 3, column: 7, sourceId: 's.glsl' });
        expect(sink.diagnostics[0]).toMatchObject({
            line: 3,
            column: 7,
            sourceId: 's.glsl',
        });
    });

    it('reportError without location omits location fields', () => {
        const sink = createDiagnosticSink();
        sink.reportError('X', 'msg');
        const d = sink.diagnostics[0];
        expect(d?.line).toBeUndefined();
        expect(d?.column).toBeUndefined();
        expect(d?.sourceId).toBeUndefined();
    });
});

describe('formatShaderDiagnostic', () => {
    it('formats sourceId + line + column', () => {
        const result = formatShaderDiagnostic({
            code: 'ERR',
            severity: 'error',
            message: 'bad thing',
            sourceId: 'shader.glsl',
            line: 10,
            column: 5,
        });
        expect(result).toBe('shader.glsl:10:5: error [ERR]: bad thing');
    });

    it('formats sourceId + line without column', () => {
        const result = formatShaderDiagnostic({
            code: 'ERR',
            severity: 'error',
            message: 'bad thing',
            sourceId: 'shader.glsl',
            line: 10,
        });
        expect(result).toBe('shader.glsl:10: error [ERR]: bad thing');
    });

    it('formats line only (no sourceId)', () => {
        const result = formatShaderDiagnostic({
            code: 'WARN',
            severity: 'warning',
            message: 'heads up',
            line: 42,
        });
        expect(result).toBe('42: warning [WARN]: heads up');
    });

    it('formats without any location', () => {
        const result = formatShaderDiagnostic({
            code: 'INFO',
            severity: 'info',
            message: 'all good',
        });
        expect(result).toBe('info [INFO]: all good');
    });
});
