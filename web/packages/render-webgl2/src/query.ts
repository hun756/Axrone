import type { ContextSource, IGLContext } from './context';
import { resolveContext } from './context';
import { GLContextError } from './context/errors';
import { createDisposedError } from './errors';

export type QueryKind = 'occlusion' | 'timer';

export interface QueryHandle {
    readonly query: WebGLQuery;
    readonly kind: QueryKind;
}

export class GLQuery implements Disposable {
    private readonly ctx: IGLContext;
    private readonly gl: WebGL2RenderingContext;
    private readonly handle: WebGLQuery;
    private disposed = false;
    private active = false;
    public readonly kind: QueryKind;

    constructor(source: ContextSource, kind: QueryKind) {
        const ctx = resolveContext(source);
        this.ctx = ctx;
        this.gl = ctx.gl;
        this.kind = kind;
        const q = this.gl.createQuery();
        if (!q) throw new GLContextError('INVALID_OPERATION', 'en', { reason: `Failed to create WebGLQuery (${kind})` });
        this.handle = q;
    }

    get query(): WebGLQuery {
        return this.handle;
    }

    isDisposed(): boolean {
        return this.disposed;
    }

    begin(target: number): void {
        if (this.disposed) throw createDisposedError('Query');
        this.gl.beginQuery(target, this.handle);
        this.active = true;
    }

    end(target: number): void {
        if (!this.active) return;
        this.gl.endQuery(target);
        this.active = false;
    }

    isResultAvailable(): boolean {
        return this.gl.getQueryParameter(this.handle, this.gl.QUERY_RESULT_AVAILABLE) as boolean;
    }

    getResult(): number {
        return this.gl.getQueryParameter(this.handle, this.gl.QUERY_RESULT) as number;
    }

    isTimerResultAvailable(ext: unknown): boolean {
        const e = ext as { QUERY_RESULT_AVAILABLE_EXT?: number } | null;
        if (!e) return this.isResultAvailable();
        return this.gl.getQueryParameter(this.handle, (e.QUERY_RESULT_AVAILABLE_EXT ?? this.gl.QUERY_RESULT_AVAILABLE) as number) as boolean;
    }

    dispose(): void {
        if (this.disposed) return;
        try {
            this.gl.deleteQuery(this.handle);
        } catch {
            // best-effort
        }
        this.disposed = true;
    }

    [Symbol.dispose](): void {
        this.dispose();
    }
}

export class GLOcclusionQuery extends GLQuery {
    constructor(source: ContextSource) {
        super(source, 'occlusion');
    }

    beginOcclusion(): void {
        this.begin(this.ctxAwareGL().ANY_SAMPLES_PASSED);
    }

    endOcclusion(): void {
        this.end(this.ctxAwareGL().ANY_SAMPLES_PASSED);
    }

    get passed(): boolean {
        return this.getResult() !== 0;
    }

    private ctxAwareGL(): WebGL2RenderingContext {
        return (this as unknown as { gl: WebGL2RenderingContext }).gl;
    }
}

export class GLTimerQuery extends GLQuery {
    private readonly ext: unknown;

    constructor(source: ContextSource) {
        const ctx = resolveContext(source);
        super(ctx, 'timer');
        this.ext = ctx.extensions.tryGet('EXT_disjoint_timer_query_webgl2');
    }

    beginTimer(): void {
        const gl = (this as unknown as { gl: WebGL2RenderingContext }).gl;
        const extVal = (this.ext as { TIME_ELAPSED_EXT?: number } | null)?.TIME_ELAPSED_EXT ?? 0x88bf;
        const target = (gl as unknown as { TIME_ELAPSED_EXT?: number }).TIME_ELAPSED_EXT ?? extVal;
        gl.beginQuery(target, this.query);
    }

    endTimer(): void {
        const gl = (this as unknown as { gl: WebGL2RenderingContext }).gl;
        const extVal = (this.ext as { TIME_ELAPSED_EXT?: number } | null)?.TIME_ELAPSED_EXT ?? 0x88bf;
        const target = (gl as unknown as { TIME_ELAPSED_EXT?: number }).TIME_ELAPSED_EXT ?? extVal;
        gl.endQuery(target);
    }

    get elapsedNs(): number {
        return this.getResult();
    }

    get disjoint(): boolean {
        const gl = (this as unknown as { gl: WebGL2RenderingContext }).gl;
        const key = (gl as unknown as { GPU_DISJOINT_EXT?: number }).GPU_DISJOINT_EXT ?? 0x8fbb;
        return (gl.getParameter(key) as boolean) ?? false;
    }
}

export type QueryFactory = {
    createOcclusionQuery(): GLOcclusionQuery;
    createTimerQuery(): GLTimerQuery | null;
    isTimerSupported(): boolean;
};

export const createQueryFactory = (source: ContextSource): QueryFactory => {
    const ctx = resolveContext(source);
    return {
        createOcclusionQuery: () => new GLOcclusionQuery(ctx),
        createTimerQuery: () => {
            if (!ctx.extensions.has('EXT_disjoint_timer_query_webgl2' as never)) return null;
            try {
                return new GLTimerQuery(ctx);
            } catch {
                return null;
            }
        },
        isTimerSupported: () => ctx.extensions.has('EXT_disjoint_timer_query_webgl2' as never),
    };
};
