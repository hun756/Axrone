import type { ContextSource, IGLContext } from './context';
import { resolveContext } from './context';
import { GLContextError } from './context/errors';
import type { IBuffer } from './buffer';

export interface TransformFeedbackOptions {
    readonly program: WebGLProgram;
    readonly varyings: readonly string[];
    readonly bufferMode?: number;
}

export class GLTransformFeedback implements Disposable {
    private readonly ctx: IGLContext;
    private readonly gl: WebGL2RenderingContext;
    private readonly tf: WebGLTransformFeedback;
    private readonly program: WebGLProgram;
    private disposed = false;
    private active = false;

    constructor(source: ContextSource, options: TransformFeedbackOptions) {
        const ctx = resolveContext(source);
        this.ctx = ctx;
        this.gl = ctx.gl;
        this.program = options.program;
        const mode = options.bufferMode ?? this.gl.SEPARATE_ATTRIBS;
        this.gl.transformFeedbackVaryings(this.program, [...options.varyings], mode);
        this.gl.linkProgram(this.program);
        const tf = this.gl.createTransformFeedback();
        if (!tf) throw new GLContextError('INVALID_OPERATION', 'en', { reason: 'Failed to create WebGLTransformFeedback' });
        this.tf = tf;
    }

    bindBuffer(index: number, buffer: IBuffer): void {
        this.gl.bindTransformFeedback(this.gl.TRANSFORM_FEEDBACK, this.tf);
        this.gl.bindBufferBase(this.gl.TRANSFORM_FEEDBACK_BUFFER, index, buffer.id as unknown as WebGLBuffer);
        this.gl.bindTransformFeedback(this.gl.TRANSFORM_FEEDBACK, null);
    }

    begin(primitive: number): void {
        if (this.active) return;
        this.gl.bindTransformFeedback(this.gl.TRANSFORM_FEEDBACK, this.tf);
        this.gl.beginTransformFeedback(primitive);
        this.active = true;
    }

    pause(): void {
        this.gl.pauseTransformFeedback();
    }

    resume(): void {
        this.gl.resumeTransformFeedback();
    }

    end(): void {
        if (!this.active) return;
        this.gl.endTransformFeedback();
        this.gl.bindTransformFeedback(this.gl.TRANSFORM_FEEDBACK, null);
        this.active = false;
    }

    dispose(): void {
        if (this.disposed) return;
        try {
            this.gl.deleteTransformFeedback(this.tf);
        } catch {
            // best-effort
        }
        this.disposed = true;
    }

    [Symbol.dispose](): void {
        this.dispose();
    }
}
