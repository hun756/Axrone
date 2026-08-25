import type { IGLStateCache } from '@axrone/render-webgl2';
import type { SceneRenderStateApplier } from '../render-state-applier';

/**
 * Shared self-isolation contract for direct-GL render add-ons.
 *
 * Any runtime that issues raw WebGL state mutations outside of
 * {@link SceneRenderStateApplier.apply} (sprite/particle batch runtimes,
 * future gizmo or debug overlays) desyncs the applier's state cache: the
 * applier only re-issues GL calls when its cached value changes, so a raw
 * mutation silently leaks into the next mesh draw and corrupts the scene.
 *
 * The guard enforces the engine-wide rule for such add-ons:
 *  1. the pass body runs inside try/catch — an add-on must never be able to
 *     halt the scene loop;
 *  2. after the first failure the add-on is disabled (one-shot flag) and the
 *     error is surfaced exactly once through the `onDisabled` hook;
 *  3. `finally` always restores the GL baseline (including SCISSOR_TEST,
 *     which the applier does not track) and invalidates the applier cache via
 *     {@link SceneRenderStateApplier.reset}.
 */
export interface SceneDirectGlPassGuardOptions {
    readonly gl: WebGL2RenderingContext;
    readonly renderStateApplier: Pick<SceneRenderStateApplier, 'reset'>;
    /** Diagnostic label used when surfacing the one-shot failure. */
    readonly label: string;
    /** Optional hook invoked exactly once when the pass gets disabled. */
    readonly onDisabled?: (error: unknown) => void;
    /** Optional GL state cache — reset in `finally` so raw-GL passes cannot desync dedup. */
    readonly stateCache?: IGLStateCache;
}

export class SceneDirectGlPassGuard {
    private _failed = false;

    constructor(private readonly _options: SceneDirectGlPassGuardOptions) {}

    /** True once a pass body has thrown; subsequent runs are skipped. */
    get isDisabled(): boolean {
        return this._failed;
    }

    /**
     * Executes a direct-GL pass body with self-isolation. Returns the body
     * result, or `fallback` when the guard is disabled or the body throws.
     */
    run<TResult>(fallback: TResult, body: () => TResult): TResult {
        if (this._failed) {
            return fallback;
        }

        const gl = this._options.gl;

        try {
            return body();
        } catch (error) {
            // A rendering add-on must never be able to halt the scene loop.
            // Disable further attempts and surface the failure once.
            this._failed = true;
            if (this._options.onDisabled) {
                this._options.onDisabled(error);
            } else {
                // eslint-disable-next-line no-console
                console.error(
                    `[${this._options.label}] disabled after render failure:`,
                    error
                );
            }
            return fallback;
        } finally {
            // Return the GL context to a known-good baseline and invalidate
            // the shared render-state-applier cache. SCISSOR_TEST is restored
            // explicitly because the applier does not track it.
            gl.depthMask?.(true);
            gl.disable?.(gl.BLEND);
            gl.enable?.(gl.CULL_FACE);
            gl.disable?.(gl.SCISSOR_TEST);
            gl.bindVertexArray?.(null);
            gl.bindBuffer?.(gl.ARRAY_BUFFER, null);
            this._options.renderStateApplier.reset();
            this._options.stateCache?.reset();
        }
    }
}
