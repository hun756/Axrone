import type { UIAsset, WidgetId } from '@axrone/ui/types';
import { UIRuntime, deserializeUIAsset } from '@axrone/ui/runtime';
import { UIHost, setSceneUIWidgetRefResolver } from '@axrone/scene-runtime/scene-facade';
// Re-export UIHost so the module namespace (imported via __AXRONE_RUNTIME__.modules)
// exposes the class for the boot-factory fallback discovery path.
export { UIHost };
import { attachUIOverlayToScene } from './scene';
import { WebGL2UIRenderer } from './renderer';
import { createUIWorldSurface } from './world-surface';
import { createUIWorldQuadRenderer, orientQuadTowardCamera } from './world-quad';
import type { SceneUIOverlayHandle, SceneUIOverlayTarget } from './types';

let nextWorldHostSystemId = 1;

/**
 * Structural DOM-like event target used for UIHost input wiring.
 * Matches HTMLCanvasElement without depending on the full DOM lib surface.
 */
export interface UIHostInputTarget {
    addEventListener(type: string, listener: (event: never) => void): void;
    removeEventListener(type: string, listener: (event: never) => void): void;
    getBoundingClientRect(): { left: number; top: number; width: number; height: number };
}

interface UIHostPointerEventLike {
    readonly clientX: number;
    readonly clientY: number;
    readonly pointerId?: number;
    readonly button?: number;
    readonly buttons?: number;
    readonly deltaX?: number;
    readonly deltaY?: number;
    readonly altKey?: boolean;
    readonly ctrlKey?: boolean;
    readonly shiftKey?: boolean;
    readonly metaKey?: boolean;
}

interface UIHostKeyEventLike {
    readonly key: string;
    readonly code?: string;
    readonly repeat?: boolean;
    readonly altKey?: boolean;
    readonly ctrlKey?: boolean;
    readonly shiftKey?: boolean;
    readonly metaKey?: boolean;
}

/** Input wiring options for a UIHost binding. */
export interface UIHostInputOptions {
    /** The DOM element (typically the scene canvas) that emits pointer events. */
    readonly target: UIHostInputTarget;
    /** Also forward keydown/keyup/blur events. Defaults to false. */
    readonly keyboard?: boolean;
}

/**
 * Options for binding a UIHost component to a scene's rendering pipeline.
 * At least one of `resolveAsset` / `resolveAssetJson` must be provided;
 * `resolveAsset` is preferred when both are present.
 */
export interface UIHostBindingOptions<TPayload = unknown> {
    /** The scene target that provides canvas, gl, and game loop. */
    readonly scene: SceneUIOverlayTarget;
    /** The UIHost component instance to bind. */
    readonly host: UIHost;
    /**
     * Resolves a UI asset ID (from UIHost.assetId) to a deserialized UIAsset.
     * Preferred integration seam; see `createUIAssetResolver` in @axrone/asset-ui.
     */
    readonly resolveAsset?: (assetId: string) => UIAsset | null;
    /**
     * Resolves a UI asset ID (from UIHost.assetId) to the raw JSON string.
     * Kept for callers that load `.ui.json` text directly.
     */
    readonly resolveAssetJson?: (assetId: string) => string | null;
    /** Optional priority for the overlay game loop system. */
    readonly priority?: number;
    /**
     * When provided and the host has `receiveInput: true`, pointer (and optionally
     * keyboard) events from the target are forwarded to the runtime with
     * viewport-to-canvas coordinate mapping applied.
     */
    readonly input?: UIHostInputOptions;
    /** Optional payload type parameter. */
    readonly _payload?: TPayload;
}

/**
 * Handle for a bound UIHost. Provides render and dispose methods,
 * plus access to the underlying UIRuntime for input dispatch.
 */
export interface UIHostBindingHandle<TPayload = unknown> extends Disposable {
    readonly runtime: UIRuntime<TPayload>;
    readonly asset: UIAsset;
    render(actualWidth: number, actualHeight: number): void;
    dispose(): void;
}

/** Aggregate handle returned by `bindUIHostsToScene`. */
export interface UIHostSceneBindings<TPayload = unknown> extends Disposable {
    readonly handles: readonly UIHostBindingHandle<TPayload>[];
    dispose(): void;
}

// ─── UIHost runtime registry ────────────────────────────────────────────────

const uiHostHandles = new WeakMap<UIHost, UIHostBindingHandle<unknown>>();

/** Returns the live binding handle for a bound UIHost, or null when unbound/disposed. */
export function getUIHostBinding(host: UIHost): UIHostBindingHandle | null {
    return (uiHostHandles.get(host) as UIHostBindingHandle | undefined) ?? null;
}

/** Returns the live UIRuntime driving a bound UIHost, or null when unbound/disposed. */
export function getUIHostRuntime(host: UIHost): UIRuntime | null {
    return getUIHostBinding(host)?.runtime ?? null;
}

// ─── UIWidgetRef — script-facing widget handle ──────────────────────────────

/** Serialized shape of a `@property({ type: 'ui-widget' })` value. */
export interface UIWidgetRefValue {
    readonly hostEntityId: string;
    readonly widgetKey: string;
}

/**
 * Lightweight handle a script uses to talk to a single widget inside a bound
 * UIHost, resolved through the asset's key-based bindings. All mutations go
 * through `UIRuntime.updateWidget`; when the host is unbound or disposed the
 * calls become no-ops and `isValid()` reports false. The ref re-resolves
 * lazily, so it survives a host being re-bound.
 */
export class UIWidgetRef {
    constructor(
        private readonly host: UIHost,
        readonly widgetKey: string
    ) {}

    get runtime(): UIRuntime | null {
        return getUIHostRuntime(this.host);
    }

    get widgetId(): WidgetId | null {
        return this.runtime?.getBoundWidget(this.widgetKey) ?? null;
    }

    isValid(): boolean {
        return this.widgetId !== null;
    }

    setText(value: string): boolean {
        return this.update({ text: { value } });
    }

    setStyle(patch: Record<string, unknown>): boolean {
        return this.update({ style: patch });
    }

    setLayout(patch: Record<string, unknown>): boolean {
        return this.update({ layout: patch });
    }

    setHandlers(handlers: Record<string, unknown>): boolean {
        return this.update({ handlers });
    }

    setEnabled(enabled: boolean): boolean {
        return this.update({ enabled });
    }

    private update(patch: Record<string, unknown>): boolean {
        const runtime = this.runtime;
        const widgetId = runtime?.getBoundWidget(this.widgetKey) ?? null;
        if (!runtime || widgetId === null) {
            return false;
        }
        runtime.updateWidget(widgetId, patch as never);
        return true;
    }
}

/**
 * Resolves a widget key against a bound UIHost. Returns null when the host is
 * not bound yet or the key is missing from the asset's binding table.
 */
export function resolveUIWidgetRef(host: UIHost, widgetKey: string): UIWidgetRef | null {
    const runtime = getUIHostRuntime(host);
    if (!runtime || runtime.getBoundWidget(widgetKey) === null) {
        return null;
    }
    return new UIWidgetRef(host, widgetKey);
}

/**
 * Fulfils scene-runtime's structural resolver seam so `@property('ui-widget')`
 * script values hydrate into live UIWidgetRefs without scene-runtime ever
 * importing UI packages. Installed idempotently by the bind entry points.
 */
const uiWidgetRefResolver = (host: unknown, widgetKey: string): UIWidgetRef | null =>
    host instanceof UIHost ? resolveUIWidgetRef(host, widgetKey) : null;

const installUIWidgetRefResolver = (): void => {
    setSceneUIWidgetRefResolver(uiWidgetRefResolver);
};

const resolveFramebufferSize = (scene: SceneUIOverlayTarget): { width: number; height: number } => ({
    width: scene.canvas.width || scene.gl.drawingBufferWidth,
    height: scene.canvas.height || scene.gl.drawingBufferHeight,
});

const connectUIHostInput = <TPayload>(
    runtime: UIRuntime<TPayload>,
    scene: SceneUIOverlayTarget,
    input: UIHostInputOptions
): (() => void) => {
    const target = input.target;

    const toFramebufferPoint = (event: UIHostPointerEventLike): { x: number; y: number } => {
        const rect = target.getBoundingClientRect();
        const framebuffer = resolveFramebufferSize(scene);
        const scaleX = rect.width > 0 ? framebuffer.width / rect.width : 1;
        const scaleY = rect.height > 0 ? framebuffer.height / rect.height : 1;
        return {
            x: (event.clientX - rect.left) * scaleX,
            y: (event.clientY - rect.top) * scaleY,
        };
    };

    const dispatchPointer = (
        phase: 'move' | 'down' | 'up' | 'leave' | 'wheel',
        event: UIHostPointerEventLike
    ): void => {
        const point = toFramebufferPoint(event);
        runtime.dispatchViewportInput({
            type: 'pointer',
            phase,
            x: point.x,
            y: point.y,
            pointerId: event.pointerId,
            button: event.button,
            buttons: event.buttons,
            deltaX: event.deltaX,
            deltaY: event.deltaY,
            altKey: event.altKey,
            ctrlKey: event.ctrlKey,
            shiftKey: event.shiftKey,
            metaKey: event.metaKey,
        });
    };

    const onPointerDown = (event: UIHostPointerEventLike): void => dispatchPointer('down', event);
    const onPointerMove = (event: UIHostPointerEventLike): void => dispatchPointer('move', event);
    const onPointerUp = (event: UIHostPointerEventLike): void => dispatchPointer('up', event);
    const onPointerLeave = (event: UIHostPointerEventLike): void => dispatchPointer('leave', event);
    const onWheel = (event: UIHostPointerEventLike): void => dispatchPointer('wheel', event);

    const dispatchKey = (phase: 'down' | 'up', event: UIHostKeyEventLike): void => {
        runtime.dispatchViewportInput({
            type: 'key',
            phase,
            key: event.key,
            code: event.code,
            repeat: event.repeat,
            altKey: event.altKey,
            ctrlKey: event.ctrlKey,
            shiftKey: event.shiftKey,
            metaKey: event.metaKey,
        });
    };

    const onKeyDown = (event: UIHostKeyEventLike): void => dispatchKey('down', event);
    const onKeyUp = (event: UIHostKeyEventLike): void => dispatchKey('up', event);
    const onBlur = (): void => {
        runtime.dispatchViewportInput({ type: 'focus', focused: false });
    };

    target.addEventListener('pointerdown', onPointerDown);
    target.addEventListener('pointermove', onPointerMove);
    target.addEventListener('pointerup', onPointerUp);
    target.addEventListener('pointerleave', onPointerLeave);
    target.addEventListener('wheel', onWheel);
    if (input.keyboard) {
        target.addEventListener('keydown', onKeyDown);
        target.addEventListener('keyup', onKeyUp);
        target.addEventListener('blur', onBlur);
    }

    return () => {
        target.removeEventListener('pointerdown', onPointerDown);
        target.removeEventListener('pointermove', onPointerMove);
        target.removeEventListener('pointerup', onPointerUp);
        target.removeEventListener('pointerleave', onPointerLeave);
        target.removeEventListener('wheel', onWheel);
        if (input.keyboard) {
            target.removeEventListener('keydown', onKeyDown);
            target.removeEventListener('keyup', onKeyUp);
            target.removeEventListener('blur', onBlur);
        }
    };
};

const resolveHostAsset = <TPayload>(
    options: UIHostBindingOptions<TPayload>,
    assetId: string
): UIAsset | null => {
    if (options.resolveAsset) {
        return options.resolveAsset(assetId);
    }
    if (options.resolveAssetJson) {
        const json = options.resolveAssetJson(assetId);
        return json ? deserializeUIAsset(json) : null;
    }
    throw new Error(
        'bindUIHostToScene requires either "resolveAsset" or "resolveAssetJson" to be provided.'
    );
};

/**
 * Binds a UIHost component to the scene's rendering pipeline.
 *
 * This function:
 * 1. Resolves the UI asset via `resolveAsset` (or `resolveAssetJson`)
 * 2. Creates a UIRuntime and loads the asset
 * 3. Attaches a WebGL2 overlay to the scene
 * 4. When the host has `receiveInput: true` and `input` is provided, wires
 *    pointer/keyboard events with viewport-to-canvas coordinate mapping
 *
 * Returns null when the host has no assetId or the asset cannot be resolved.
 */
export function bindUIHostToScene<TPayload = unknown>(
    options: UIHostBindingOptions<TPayload>
): UIHostBindingHandle<TPayload> | null {
    const { scene, host, priority } = options;
    installUIWidgetRefResolver();
    const assetId = host.assetId;
    if (!assetId) {
        return null;
    }

    const asset = resolveHostAsset(options, assetId);
    if (!asset) {
        return null;
    }

    const runtime = new UIRuntime<TPayload>();
    runtime.loadFromAsset(asset);

    const overlay: SceneUIOverlayHandle<TPayload> = attachUIOverlayToScene<TPayload>(scene, {
        ui: () => {
            const framebuffer = resolveFramebufferSize(scene);
            return runtime.commitToViewport(framebuffer.width, framebuffer.height);
        },
        priority: priority ?? -1000,
    });

    const disconnectInput =
        host.receiveInput && options.input
            ? connectUIHostInput(runtime, scene, options.input)
            : null;

    let disposed = false;

    const disposeBinding = (): void => {
        if (disposed) {
            return;
        }
        disposed = true;
        if (uiHostHandles.get(host) === handle) {
            uiHostHandles.delete(host);
        }
        disconnectInput?.();
        overlay.dispose();
        runtime.dispose();
    };

    const handle: UIHostBindingHandle<TPayload> = {
        runtime,
        asset,
        render(actualWidth: number, actualHeight: number) {
            if (disposed) {
                return;
            }
            runtime.commitToViewport(actualWidth, actualHeight);
        },
        dispose: disposeBinding,
        [Symbol.dispose]: disposeBinding,
    };

    uiHostHandles.set(host, handle as UIHostBindingHandle<unknown>);
    return handle;
}

/** Options for binding multiple UIHost components in one call. */
export interface UIHostsBindingOptions<TPayload = unknown>
    extends Omit<UIHostBindingOptions<TPayload>, 'host'> {
    /**
     * The hosts to bind. Defaults to every live, enabled UIHost instance
     * (`UIHost.getAllInstances()`); each host is routed by its `renderMode`.
     */
    readonly hosts?: readonly UIHost[];
    /**
     * Camera and transform sources required by `world-space` hosts. When omitted,
     * world-space hosts are skipped (screen-overlay hosts still bind).
     */
    readonly world?: UIHostWorldSources;
}

// ─── world-space binding ────────────────────────────────────────────────────

/**
 * Matrix sources world-space UI needs from the caller. Supplied as callbacks so
 * ui-webgl2 stays free of scene-3d/camera dependencies (see the ui boundary
 * architecture test).
 */
export interface UIHostWorldSources {
    /** Active camera view-projection matrix, 4x4 column-major. */
    readonly viewProjection: () => Float32Array;
    /** World matrix of the entity carrying the host, 4x4 column-major. */
    readonly entityWorldMatrix: (host: UIHost) => Float32Array;
    /** Camera world matrix; required only for `billboard: 'camera-facing'`. */
    readonly cameraWorldMatrix?: () => Float32Array;
}

export interface UIHostWorldBindingOptions<TPayload = unknown>
    extends UIHostBindingOptions<TPayload> {
    readonly world: UIHostWorldSources;
}

const MAX_WORLD_TEXTURE_SIZE = 2048;

const resolveWorldSurfaceSize = (host: UIHost): { width: number; height: number } => ({
    width: Math.min(MAX_WORLD_TEXTURE_SIZE, Math.max(1, Math.round(host.worldWidth * host.textureScale))),
    height: Math.min(MAX_WORLD_TEXTURE_SIZE, Math.max(1, Math.round(host.worldHeight * host.textureScale))),
});

/**
 * Binds a `world-space` UIHost: the asset is rendered into an offscreen texture
 * and displayed on a camera-projected quad inside the 3D scene (the Unity World
 * Space Canvas equivalent). Depth testing lets scene geometry occlude the UI.
 *
 * Returns null when the host has no assetId or the asset cannot be resolved.
 */
export function bindUIHostToWorld<TPayload = unknown>(
    options: UIHostWorldBindingOptions<TPayload>
): UIHostBindingHandle<TPayload> | null {
    const { scene, host, priority, world } = options;
    installUIWidgetRefResolver();
    const assetId = host.assetId;
    if (!assetId) {
        return null;
    }

    const asset = resolveHostAsset(options, assetId);
    if (!asset) {
        return null;
    }

    const runtime = new UIRuntime<TPayload>();
    runtime.loadFromAsset(asset);

    const initialSize = resolveWorldSurfaceSize(host);
    const surface = createUIWorldSurface(scene.gl, initialSize.width, initialSize.height);
    const quadRenderer = createUIWorldQuadRenderer(scene.gl);
    const uiRenderer = new WebGL2UIRenderer<TPayload>({ gl: scene.gl });

    const drawWorldFrame = (): void => {
        const size = resolveWorldSurfaceSize(host);
        surface.resize(size.width, size.height);
        // 1) UI into the offscreen texture at the surface resolution.
        const frame = runtime.commitToViewport(surface.width, surface.height);
        scene.gl.bindFramebuffer(scene.gl.FRAMEBUFFER, surface.framebuffer);
        scene.gl.viewport(0, 0, surface.width, surface.height);
        scene.gl.clearColor(0, 0, 0, 0);
        scene.gl.clear(scene.gl.COLOR_BUFFER_BIT);
        scene.gl.bindFramebuffer(scene.gl.FRAMEBUFFER, null);
        uiRenderer.render(frame, { framebuffer: surface.framebuffer });

        // 2) Textured quad into the scene, projected by the active camera.
        const baseModel = world.entityWorldMatrix(host);
        const cameraWorld =
            host.billboard === 'camera-facing' ? world.cameraWorldMatrix?.() : undefined;
        const model = cameraWorld ? orientQuadTowardCamera(baseModel, cameraWorld) : baseModel;
        quadRenderer.draw(surface.texture, {
            modelMatrix: model,
            viewProjection: world.viewProjection(),
            width: host.worldWidth,
            height: host.worldHeight,
            depthTest: true,
            depthWrite: false,
        });
    };

    const systemId = `axrone.ui.world-host:${nextWorldHostSystemId++}`;
    scene.loop.addSystem({
        id: systemId,
        priority: priority ?? -800,
        enabled: true,
        afterFrame: () => {
            if (!disposed) {
                drawWorldFrame();
            }
        },
    } as Parameters<SceneUIOverlayTarget['loop']['addSystem']>[0]);

    const disconnectInput =
        host.receiveInput && options.input
            ? connectUIHostInput(runtime, scene, options.input)
            : null;

    let disposed = false;

    const disposeBinding = (): void => {
        if (disposed) {
            return;
        }
        disposed = true;
        if (uiHostHandles.get(host) === handle) {
            uiHostHandles.delete(host);
        }
        disconnectInput?.();
        scene.loop.removeSystem(systemId);
        quadRenderer.dispose();
        surface.dispose();
        uiRenderer.dispose();
        runtime.dispose();
    };

    const handle: UIHostBindingHandle<TPayload> = {
        runtime,
        asset,
        render() {
            if (!disposed) {
                drawWorldFrame();
            }
        },
        dispose: disposeBinding,
        [Symbol.dispose]: disposeBinding,
    };

    uiHostHandles.set(host, handle as UIHostBindingHandle<unknown>);
    return handle;
}

/**
 * Binds every eligible UIHost component to the scene in one call, routing each
 * host by its `renderMode`. Hosts with an empty assetId or an unresolvable asset
 * are skipped silently, mirroring the null behavior of `bindUIHostToScene`.
 * World-space hosts additionally require `options.world`.
 */
export function bindUIHostsToScene<TPayload = unknown>(
    options: UIHostsBindingOptions<TPayload>
): UIHostSceneBindings<TPayload> {
    installUIWidgetRefResolver();
    const hosts = options.hosts ?? UIHost.getAllInstances().filter((host) => host.enabled);

    const handles: UIHostBindingHandle<TPayload>[] = [];
    for (const host of hosts) {
        if (host.renderMode === 'world-space') {
            if (!options.world) {
                continue;
            }
            const handle = bindUIHostToWorld<TPayload>({
                ...options,
                host,
                world: options.world,
            });
            if (handle) {
                handles.push(handle);
            }
            continue;
        }
        const handle = bindUIHostToScene<TPayload>({ ...options, host });
        if (handle) {
            handles.push(handle);
        }
    }

    let disposed = false;

    const disposeAll = (): void => {
        if (disposed) {
            return;
        }
        disposed = true;
        for (const handle of handles) {
            handle.dispose();
        }
    };

    return {
        handles,
        dispose: disposeAll,
        [Symbol.dispose]: disposeAll,
    };
}
