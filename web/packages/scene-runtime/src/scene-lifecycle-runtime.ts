import type { GameLoop, GameLoopStatus } from '@axrone/game-loop';
import { SceneLifecycleError } from './errors';
import type { SceneLoopState } from './types';

export interface SceneLifecycleRuntimeOptions {
    readonly canvas: HTMLCanvasElement;
    readonly gl: WebGL2RenderingContext;
    readonly loop: GameLoop<SceneLoopState>;
    readonly autoCreatedCanvas: boolean;
    readonly pixelRatio: number;
    readonly defaultWidth: number;
    readonly defaultHeight: number;
    readonly render: (deltaTime: number) => void;
    readonly disposeAssets: () => void;
    readonly disposeWorld: () => void;
    /**
     * Invoked after `webglcontextlost` has been observed (and default-
     * prevented so the browser may restore the context). The loop is already
     * paused when this fires.
     */
    readonly onContextLost?: () => void;
    /**
     * Invoked on `webglcontextrestored`, before the loop is resumed. Wire
     * GPU cache invalidation (e.g. render runtime / backend) here so the
     * next frame rebuilds resources lazily.
     */
    readonly onContextRestored?: () => void;
}

export class SceneLifecycleRuntime {
    private readonly _canvas: HTMLCanvasElement;
    private readonly _gl: WebGL2RenderingContext;
    private readonly _loop: GameLoop<SceneLoopState>;
    private readonly _autoCreatedCanvas: boolean;
    private readonly _defaultWidth: number;
    private readonly _defaultHeight: number;
    private readonly _render: (deltaTime: number) => void;
    private readonly _disposeAssets: () => void;
    private readonly _disposeWorld: () => void;
    private readonly _onContextLost?: () => void;
    private readonly _onContextRestored?: () => void;
    private readonly _handleContextLost = (event: Event): void => {
        // preventDefault signals the browser that we intend to restore.
        event.preventDefault?.();
        this._contextLost = true;
        if (this._loop.status === 'running') {
            this._resumeAfterRestore = true;
            this._loop.pause();
        }
        this._onContextLost?.();
    };
    private readonly _handleContextRestored = (): void => {
        this._contextLost = false;
        this._onContextRestored?.();
        if (this._resumeAfterRestore && !this._disposed) {
            this._resumeAfterRestore = false;
            this._loop.resume();
        }
    };
    private _pixelRatio: number;
    private _contextLost = false;
    private _resumeAfterRestore = false;
    private _disposed = false;

    constructor(options: SceneLifecycleRuntimeOptions) {
        this._canvas = options.canvas;
        this._gl = options.gl;
        this._loop = options.loop;
        this._autoCreatedCanvas = options.autoCreatedCanvas;
        this._defaultWidth = options.defaultWidth;
        this._defaultHeight = options.defaultHeight;
        this._render = options.render;
        this._disposeAssets = options.disposeAssets;
        this._disposeWorld = options.disposeWorld;
        this._onContextLost = options.onContextLost;
        this._onContextRestored = options.onContextRestored;
        this._pixelRatio = options.pixelRatio > 0 ? options.pixelRatio : 1;
        this._canvas.addEventListener?.('webglcontextlost', this._handleContextLost);
        this._canvas.addEventListener?.('webglcontextrestored', this._handleContextRestored);
    }

    get status(): GameLoopStatus {
        return this._loop.status;
    }

    get isDisposed(): boolean {
        return this._disposed;
    }

    get isContextLost(): boolean {
        return this._contextLost;
    }

    start(now?: number): void {
        this.assertNotDisposed();
        this._loop.start(now);
    }

    pause(): void {
        this.assertNotDisposed();
        this._loop.pause();
    }

    resume(now?: number): void {
        this.assertNotDisposed();
        this._loop.resume(now);
    }

    stop(): void {
        this.assertNotDisposed();
        this._loop.stop();
    }

    renderNow(): void {
        this.assertNotDisposed();
        this._render(0);
    }

    resize(
        width: number = this._canvas.clientWidth || this._defaultWidth,
        height: number = this._canvas.clientHeight || this._defaultHeight,
        pixelRatio: number = this._pixelRatio
    ): void {
        this.assertNotDisposed();
        this._pixelRatio = pixelRatio > 0 ? pixelRatio : 1;

        const targetWidth = Math.max(1, Math.floor(width * this._pixelRatio));
        const targetHeight = Math.max(1, Math.floor(height * this._pixelRatio));
        this._canvas.width = targetWidth;
        this._canvas.height = targetHeight;
        this._canvas.style.width = `${width}px`;
        this._canvas.style.height = `${height}px`;
        this._gl.viewport(0, 0, targetWidth, targetHeight);
    }

    dispose(): void {
        if (this._disposed) {
            return;
        }

        this._canvas.removeEventListener?.('webglcontextlost', this._handleContextLost);
        this._canvas.removeEventListener?.('webglcontextrestored', this._handleContextRestored);

        let lifecycleError: SceneLifecycleError | null = null;

        try {
            this._loop.dispose();
        } catch (error) {
            lifecycleError = new SceneLifecycleError('Failed to dispose scene loop', error);
        }

        try {
            this._disposeAssets();
        } catch (error) {
            lifecycleError ??= new SceneLifecycleError('Failed to dispose scene assets', error);
        }

        try {
            this._disposeWorld();
        } catch (error) {
            lifecycleError ??= new SceneLifecycleError('Failed to dispose scene world', error);
        }

        try {
            if (
                this._autoCreatedCanvas &&
                this._canvas.parentNode &&
                typeof this._canvas.parentNode.removeChild === 'function'
            ) {
                this._canvas.parentNode.removeChild(this._canvas);
            }
        } catch (error) {
            lifecycleError ??= new SceneLifecycleError(
                'Failed to remove auto-created scene canvas',
                error
            );
        }

        this._disposed = true;

        if (lifecycleError) {
            throw lifecycleError;
        }
    }

    assertNotDisposed(): void {
        if (!this._disposed) {
            return;
        }

        throw new SceneLifecycleError('Scene has already been disposed');
    }
}