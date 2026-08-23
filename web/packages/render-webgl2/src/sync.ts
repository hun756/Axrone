import type { ContextSource, IGLContext } from './context';
import { resolveContext } from './context';

export class GLSync implements Disposable {
    private readonly ctx: IGLContext;
    private readonly gl: WebGL2RenderingContext;
    private sync: WebGLSync | null;
    private disposed = false;

    constructor(source: ContextSource, sync: WebGLSync) {
        const ctx = resolveContext(source);
        this.ctx = ctx;
        this.gl = ctx.gl;
        this.sync = sync;
    }

    static fence(source: ContextSource): GLSync | null {
        const ctx = resolveContext(source);
        const s = ctx.gl.fenceSync(ctx.gl.SYNC_GPU_COMMANDS_COMPLETE, 0);
        if (!s) return null;
        ctx.gl.flush();
        return new GLSync(ctx, s);
    }

    isSignaled(): boolean {
        if (!this.sync || this.disposed) return true;
        const status = this.gl.getSyncParameter(this.sync, this.gl.SYNC_STATUS) as number;
        return status === this.gl.SIGNALED;
    }

    clientWait(timeoutNs: number, flags = 0): number {
        if (!this.sync || this.disposed) return this.gl.CONDITION_SATISFIED;
        return this.gl.clientWaitSync(this.sync, flags, timeoutNs);
    }

    wait(): void {
        if (!this.sync || this.disposed) return;
        this.gl.waitSync(this.sync, 0, 0xffffffff);
    }

    dispose(): void {
        if (this.disposed || !this.sync) return;
        try {
            this.gl.deleteSync(this.sync);
        } catch {
            // best-effort
        }
        this.sync = null;
        this.disposed = true;
    }

    [Symbol.dispose](): void {
        this.dispose();
    }
}
