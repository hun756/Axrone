import type { UIAsset } from '@axrone/ui/types';
import { UIRuntime, deserializeUIAsset } from '@axrone/ui/runtime';
import { UIHost } from '@axrone/scene-runtime/scene-facade';
import { attachUIOverlayToScene } from './scene';
import type { SceneUIOverlayHandle, SceneUIOverlayTarget } from './types';

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
        disconnectInput?.();
        overlay.dispose();
        runtime.dispose();
    };

    return {
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
}

/** Options for binding multiple UIHost components in one call. */
export interface UIHostsBindingOptions<TPayload = unknown>
    extends Omit<UIHostBindingOptions<TPayload>, 'host'> {
    /**
     * The hosts to bind. Defaults to every live UIHost instance
     * (`UIHost.getAllInstances()`) that is enabled and uses screen-overlay mode.
     */
    readonly hosts?: readonly UIHost[];
}

/**
 * Binds every eligible UIHost component to the scene in one call.
 * Hosts with an empty assetId or an unresolvable asset are skipped silently,
 * mirroring the null behavior of `bindUIHostToScene`.
 */
export function bindUIHostsToScene<TPayload = unknown>(
    options: UIHostsBindingOptions<TPayload>
): UIHostSceneBindings<TPayload> {
    const hosts =
        options.hosts ??
        UIHost.getAllInstances().filter(
            (host) => host.enabled && host.renderMode === 'screen-overlay'
        );

    const handles: UIHostBindingHandle<TPayload>[] = [];
    for (const host of hosts) {
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
