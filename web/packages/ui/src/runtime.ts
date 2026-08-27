import {
    DisposedUIError,
    InvalidUIAssetError,
    UIError,
    UIErrorCode,
    WidgetNotFoundError,
    WidgetTreeIntegrityError,
} from './errors';
import { FontRegistry, ensureDefaultUIFont } from './font';
import { UILayoutEngine, compileLayoutInput } from './layout';
import {
    type CanvasScaleResult,
    resolveCanvasScale,
    canvasScaleToTransform,
    mapViewportPointToCanvas,
} from './layout/canvas-scaler';
import { NodeFlag } from './runtime/node-flags';
import {
    type StoredWidgetRecord,
    compileWidgetFocus,
    compileWidgetImage,
    compileWidgetStyle,
    compileWidgetText,
    normalizeWidgetRecord,
} from './runtime/records';
import {
    type UIInputDispatchHost,
    dispatchKeyEvent,
    dispatchPointerEvent,
    dispatchTextEvent,
    moveFocusDirectional,
    moveFocusLinear,
} from './runtime/runtime-input';
import {
    buildCaretCommand,
    buildLineDecorationCommands,
    buildSelectionCommands,
    measureImageContent,
    resolveImageCommand,
} from './runtime/runtime-frame';
import {
    EMPTY_FOCUS_INPUT,
    EMPTY_LAYOUT_INPUT,
    EMPTY_RECORD_OBJECT,
    EMPTY_STYLE_INPUT,
    TRANSPARENT,
    cloneData,
    intersectRect,
    intersectsPoint,
    mergeFocusInput,
    mergeHandlers,
    mergeImageInput,
    mergeLayoutInput,
    mergeProps,
    mergeStyleInput,
    mergeTextInput,
} from './runtime/internals';
import { TextLayoutEngine } from './text';
import { WidgetRegistry } from './widget';
import type {
    ColorInput,
    CustomRenderCommand,
    FocusMoveDirection,
    FontRegistryOptions,
    ImageRenderCommand,
    LayoutBox,
    QuadRenderCommand,
    RenderCommand,
    ResolvedFocusPolicy,
    ResolvedWidgetImage,
    ResolvedLayout,
    ResolvedTextBlock,
    ResolvedWidgetStyle,
    RectLike,
    SizeLike,
    StrokeRenderCommand,
    TextLayoutResult,
    TextLayoutConstraint,
    TextRenderCommand,
    UIFrame,
    UIFrameMetrics,
    UIInputEvent,
    UIPointerEvent,
    UIKeyEvent,
    UITextInputEvent,
    WidgetConfig,
    WidgetEventContext,
    WidgetEventHandlers,
    WidgetFocusChangeEvent,
    WidgetImageInput,
    UIAsset,
    UICanvasConfig,
    WidgetId,
    WidgetKey,
    WidgetLayoutInput,
    WidgetPatch,
    WidgetSerializableKey,
    WidgetSnapshot,
    WidgetStyleInput,
    UIRuntimeSnapshot,
} from './types';

export interface UIRuntimeOptions<TPayload = unknown> {
    readonly width?: number;
    readonly height?: number;
    readonly locale?: string;
    readonly fonts?: FontRegistry;
    readonly textEngine?: TextLayoutEngine;
    readonly registry?: WidgetRegistry<UIRuntime<TPayload>, TPayload>;
    readonly fontOptions?: FontRegistryOptions;
    readonly textCacheSize?: number;
}

export class UIRuntime<TPayload = unknown> implements Disposable {
    readonly fonts: FontRegistry;
    readonly textEngine: TextLayoutEngine;
    readonly registry: WidgetRegistry<UIRuntime<TPayload>, TPayload>;

    private readonly layoutEngine = new UILayoutEngine<WidgetId>();
    private records: Array<StoredWidgetRecord<UIRuntime<TPayload>> | null> = [];
    private layouts: Array<ResolvedLayout | null> = [];
    private styles: Array<ResolvedWidgetStyle | null> = [];
    private texts: Array<ResolvedTextBlock | null> = [];
    private images: Array<ResolvedWidgetImage | null> = [];
    private focuses: Array<ResolvedFocusPolicy | null> = [];
    private states: unknown[] = [];
    private textLayouts: Array<TextLayoutResult | null> = [];
    private textLayoutWidths: number[] = [];
    private flags = new Uint32Array(16);
    private parent = new Int32Array(16);
    private firstChild = new Int32Array(16);
    private lastChild = new Int32Array(16);
    private previousSibling = new Int32Array(16);
    private nextSibling = new Int32Array(16);
    private sequence = new Uint32Array(16);
    private depth = new Uint16Array(16);
    private boxX = new Float32Array(16);
    private boxY = new Float32Array(16);
    private boxWidth = new Float32Array(16);
    private boxHeight = new Float32Array(16);
    private contentX = new Float32Array(16);
    private contentY = new Float32Array(16);
    private contentWidth = new Float32Array(16);
    private contentHeight = new Float32Array(16);
    private freeList: number[] = [];
    private focusOrder: WidgetId[] = [];
    private rootId: WidgetId;
    private nextId = 1;
    private nextSequence = 1;
    private liveCount = 0;
    private hovered: WidgetId | null = null;
    private focused: WidgetId | null = null;
    private pressed: WidgetId | null = null;
    private focusDirty = true;
    private layoutDirty = true;
    private disposed = false;
    private viewportWidth: number;
    private viewportHeight: number;
    private locale: string;
    private lastLayoutPasses = 0;
    private canvasConfig: UICanvasConfig | null = null;
    private lastViewport: { readonly width: number; readonly height: number } | null = null;
    private readonly bindingTable = new Map<string, WidgetId>();
    private readonly controllerListeners = new Map<number, Map<string, Set<(data: unknown) => void>>>();
    private readonly autoSizeCache = new Map<string, TextLayoutResult>();
    private readonly autoSizeCacheMaxSize = 64;

    private readonly inputHost: UIInputDispatchHost = {
        getPressed: () => this.pressed,
        setPressed: (widget) => {
            this.pressed = widget;
        },
        getFocused: () => this.focused,
        hitTest: (x, y) => this.hitTest(x, y),
        updateHover: (target, event) => this.updateHover(target, event),
        bubbleEvent: (index, event) => this.bubbleEvent(index, event),
        isFocusable: (index) => this.isFocusable(index),
        setFocus: (widget, reason) => this.setFocus(widget, reason),
        moveFocus: (direction) => this.moveFocus(direction),
    };

    constructor(options: UIRuntimeOptions<TPayload> = {}) {
        this.locale = options.locale ?? 'en';
        this.viewportWidth = Math.max(0, options.width ?? 0);
        this.viewportHeight = Math.max(0, options.height ?? 0);
        this.fonts = options.fonts ?? new FontRegistry(options.fontOptions);
        if (this.fonts.getDefaultFamily() === null) {
            try {
                ensureDefaultUIFont(this.fonts);
            } catch (error) {
                if (
                    !(error instanceof UIError) ||
                    error.code !== UIErrorCode.FontLoadFailed ||
                    !error.message.includes('No 2D canvas implementation is available')
                ) {
                    throw error;
                }
            }
        }
        this.textEngine =
            options.textEngine ??
            new TextLayoutEngine(this.fonts, { cacheSize: options.textCacheSize, locale: this.locale });
        this.registry = options.registry ?? new WidgetRegistry<UIRuntime<TPayload>, TPayload>();
        const rootId = this.allocate();
        this.rootId = rootId as WidgetId;
        this.records[rootId] = normalizeWidgetRecord({
            role: 'root',
            layout: {
                display: 'overlay',
                width: '100%',
                height: '100%',
            },
            style: {
                visible: true,
            },
            enabled: true,
            interactive: false,
        });
        this.applyRecord(rootId, null, null, true);
    }

    get root(): WidgetId {
        return this.rootId;
    }

    get width(): number {
        return this.viewportWidth;
    }

    get height(): number {
        return this.viewportHeight;
    }

    /** Returns the currently loaded canvas configuration, or null if no UIAsset is loaded. */
    getCanvasConfig(): UICanvasConfig | null {
        return this.canvasConfig;
    }

    /**
     * Loads a UIAsset into this runtime.
     * Sets the viewport to the asset's reference resolution, restores the widget tree,
     * stores the canvas configuration for later use during commit(), and resolves the
     * asset's named bindings against restored widget keys.
     */
    loadFromAsset(asset: UIAsset): this {
        this.ensureActive();
        this.canvasConfig = asset.canvas;
        this.lastViewport = null;
        this.setViewport(asset.canvas.referenceWidth, asset.canvas.referenceHeight);
        this.restore({
            viewportWidth: asset.canvas.referenceWidth,
            viewportHeight: asset.canvas.referenceHeight,
            locale: this.locale,
            root: asset.root,
        });
        this.rebuildBindingTable(asset.bindings);
        this.remountControllers();
        return this;
    }

    /**
     * Re-runs controller `mount` hooks after the binding table exists.
     *
     * Widgets are created before bindings are resolved, so a controller that
     * reaches sibling widgets through `getBoundWidget` (a slider syncing its
     * fill and handle, for example) cannot do so during the initial mount.
     * Replaying `mount` once the table is ready gives controllers a chance to
     * push their authored state into the tree before the first layout pass, so
     * `mount` implementations must be idempotent.
     */
    private remountControllers(): void {
        for (let index = 0; index < this.records.length; index += 1) {
            const record = this.records[index];
            if (!record?.controller) {
                continue;
            }
            const controller = this.registry.resolve(record.controller);
            controller?.mount?.(this.createControllerContext(index));
        }
    }

    /**
     * Resolves a named binding (declared in the loaded UIAsset) to the widget it targets.
     * Returns null when no asset is loaded or the binding name is unknown.
     */
    getBoundWidget(name: string): WidgetId | null {
        this.ensureActive();
        return this.bindingTable.get(name) ?? null;
    }

    /** Returns the full binding-name to widget table resolved by the last loadFromAsset(). */
    getBindingTable(): ReadonlyMap<string, WidgetId> {
        this.ensureActive();
        return this.bindingTable;
    }

    /**
     * Registers a listener for a named controller event on a widget.
     * Controllers emit semantic events (e.g. 'onDragStart') at meaningful
     * interaction points. Listeners are called synchronously during emit.
     */
    onControllerEvent(widget: WidgetId, eventName: string, callback: (data: unknown) => void): void {
        this.ensureActive();
        this.requireWidget(widget);
        let widgetListeners = this.controllerListeners.get(widget);
        if (!widgetListeners) {
            widgetListeners = new Map();
            this.controllerListeners.set(widget, widgetListeners);
        }
        let eventListeners = widgetListeners.get(eventName);
        if (!eventListeners) {
            eventListeners = new Set();
            widgetListeners.set(eventName, eventListeners);
        }
        eventListeners.add(callback);
    }

    /**
     * Removes a previously registered controller event listener.
     */
    offControllerEvent(widget: WidgetId, eventName: string, callback: (data: unknown) => void): void {
        this.ensureActive();
        const widgetListeners = this.controllerListeners.get(widget);
        if (!widgetListeners) return;
        const eventListeners = widgetListeners.get(eventName);
        if (!eventListeners) return;
        eventListeners.delete(callback);
        if (eventListeners.size === 0) {
            widgetListeners.delete(eventName);
        }
        if (widgetListeners.size === 0) {
            this.controllerListeners.delete(widget);
        }
    }

    /**
     * Emits a named controller event for a widget. Called by controllers at
     * meaningful interaction points (drag start, drop, etc.). Returns true
     * when at least one listener was invoked.
     */
    emitControllerEvent(widget: WidgetId, eventName: string, data?: unknown): boolean {
        this.ensureActive();
        const widgetListeners = this.controllerListeners.get(widget);
        if (!widgetListeners) return false;
        const eventListeners = widgetListeners.get(eventName);
        if (!eventListeners || eventListeners.size === 0) return false;
        for (const listener of [...eventListeners]) {
            listener(data);
        }
        return true;
    }

    /**
     * Returns the controller state of a widget, or null when it has no
     * controller. Lets callers read live control values (a slider's value, a
     * toggle's checked flag) without reaching into runtime internals.
     */
    getWidgetState<TState = unknown>(widget: WidgetId): TState | null {
        this.ensureActive();
        const index = this.requireWidget(widget);
        return (this.states[index] as TState | undefined) ?? null;
    }

    /**
     * Returns a clone of the widget's current image input, or null when the
     * widget has no image. Lets controllers read the authored image source
     * (e.g. to restore it after a per-state sprite swap).
     */
    getWidgetImageInput(widget: WidgetId): WidgetImageInput | null {
        this.ensureActive();
        const index = this.requireWidget(widget);
        const record = this.records[index]!;
        return cloneData(record.imageInput);
    }

    /**
     * Returns a clone of the widget's current style input, or null when the
     * widget has no style. Lets controllers read the authored visual state
     * (e.g. current background color for animation start values).
     */
    getWidgetStyleInput(widget: WidgetId): WidgetStyleInput | null {
        this.ensureActive();
        const index = this.requireWidget(widget);
        const record = this.records[index]!;
        return cloneData(record.styleInput);
    }

    setViewport(width: number, height: number): this {
        this.ensureActive();
        if (width !== this.viewportWidth || height !== this.viewportHeight) {
            this.viewportWidth = Math.max(0, width);
            this.viewportHeight = Math.max(0, height);
            this.layoutDirty = true;
        }
        return this;
    }

    createWidget<TProps extends Record<string, unknown> = Record<string, never>>(
        config: WidgetConfig<TProps, UIRuntime<TPayload>> = {}
    ): WidgetId {
        this.ensureActive();
        const id = this.allocate();
        this.records[id] = normalizeWidgetRecord(config as WidgetConfig<Record<string, unknown>, UIRuntime<TPayload>>);
        this.applyRecord(id, null, null, true);
        return id as WidgetId;
    }

    appendChild(parent: WidgetId, child: WidgetId): this {
        return this.insertChildBefore(parent, child, null);
    }

    insertChildBefore(parent: WidgetId, child: WidgetId, before: WidgetId | null): this {
        this.ensureActive();
        const parentIndex = this.requireWidget(parent);
        const childIndex = this.requireWidget(child);
        if (childIndex === this.rootId) {
            throw new WidgetTreeIntegrityError('The root widget cannot be re-parented.');
        }
        if (parentIndex === childIndex || this.isAncestor(childIndex, parentIndex)) {
            throw new WidgetTreeIntegrityError('Re-parenting would create a cycle.', {
                parent,
                child,
                before,
            });
        }
        if (before !== null) {
            const beforeIndex = this.requireWidget(before);
            if (this.parent[beforeIndex] !== parentIndex) {
                throw new WidgetTreeIntegrityError('The insertion reference must already belong to the parent.', {
                    parent,
                    child,
                    before,
                });
            }
        }
        this.detachNode(childIndex);
        this.parent[childIndex] = parentIndex;
        if (before === null) {
            const last = this.lastChild[parentIndex];
            if (last === 0) {
                this.firstChild[parentIndex] = childIndex;
                this.lastChild[parentIndex] = childIndex;
            } else {
                this.nextSibling[last] = childIndex;
                this.previousSibling[childIndex] = last;
                this.lastChild[parentIndex] = childIndex;
            }
        } else {
            const beforeIndex = before as number;
            const previous = this.previousSibling[beforeIndex];
            this.nextSibling[childIndex] = beforeIndex;
            this.previousSibling[beforeIndex] = childIndex;
            if (previous !== 0) {
                this.nextSibling[previous] = childIndex;
                this.previousSibling[childIndex] = previous;
            } else {
                this.firstChild[parentIndex] = childIndex;
            }
        }
        this.refreshDepths(childIndex, this.depth[parentIndex] + 1);
        this.markTreeChanged(childIndex);
        return this;
    }

    updateWidget<TProps extends Record<string, unknown> = Record<string, never>>(
        widget: WidgetId,
        patch: WidgetPatch<TProps, UIRuntime<TPayload>>
    ): this {
        this.ensureActive();
        const index = this.requireWidget(widget);
        const current = this.records[index];
        if (!current) {
            throw new WidgetNotFoundError(index);
        }
        const previousController = current.controller;
        const previousProps = current.props;
        const merged: StoredWidgetRecord<UIRuntime<TPayload>> = {
            role: patch.role ?? current.role,
            controller: patch.controller ?? current.controller,
            key: patch.key ?? current.key,
            props: mergeProps(current.props, patch.props as Readonly<Record<string, unknown>> | undefined),
            enabled: patch.enabled ?? current.enabled,
            interactive: patch.interactive ?? current.interactive,
            layoutInput: mergeLayoutInput(current.layoutInput, patch.layout),
            styleInput: mergeStyleInput(current.styleInput, patch.style),
            textInput: mergeTextInput(current.textInput, patch.text),
            imageInput: mergeImageInput(current.imageInput, patch.image),
            focusInput: mergeFocusInput(current.focusInput, patch.focus),
            handlers: mergeHandlers(
                current.handlers,
                patch.handlers as WidgetEventHandlers<Record<string, unknown>, UIRuntime<TPayload>> | undefined
            ),
        };
        this.records[index] = merged;
        this.applyRecord(index, previousProps, previousController, false);
        return this;
    }

    removeWidget(widget: WidgetId): this {
        this.ensureActive();
        const index = this.requireWidget(widget);
        if (index === this.rootId) {
            throw new WidgetTreeIntegrityError('The root widget cannot be removed.');
        }
        const traversal: number[] = [];
        const stack = [index];
        while (stack.length > 0) {
            const current = stack.pop()!;
            traversal.push(current);
            for (let child = this.firstChild[current]; child !== 0; child = this.nextSibling[child]) {
                stack.push(child);
            }
        }
        this.detachNode(index);
        for (let offset = traversal.length - 1; offset >= 0; offset -= 1) {
            this.destroyNode(traversal[offset]);
        }
        this.layoutDirty = true;
        this.focusDirty = true;
        return this;
    }

    clear(): this {
        this.ensureActive();
        const children: WidgetId[] = [];
        for (let child = this.firstChild[this.rootId]; child !== 0; child = this.nextSibling[child]) {
            children.push(child as WidgetId);
        }
        for (const child of children) {
            this.removeWidget(child);
        }
        this.bindingTable.clear();
        this.lastViewport = null;
        return this;
    }

    getLayoutBox(widget: WidgetId): LayoutBox {
        const index = this.requireWidget(widget);
        return this.readBox(index);
    }

    getTextLayout(widget: WidgetId): TextLayoutResult | null {
        const index = this.requireWidget(widget);
        return this.textLayouts[index] ?? null;
    }

    getWidgetCount(): number {
        return Math.max(0, this.liveCount - 1);
    }

    collectSubtreeWidgetIds(widget: WidgetId): WidgetId[] {
        const index = this.requireWidget(widget);
        const widgets: WidgetId[] = [];
        const stack = [index];

        while (stack.length > 0) {
            const current = stack.pop()!;
            widgets.push(current as WidgetId);
            for (let child = this.lastChild[current]; child !== 0; child = this.previousSibling[child]) {
                stack.push(child);
            }
        }

        return widgets;
    }

    commit(viewport?: Partial<SizeLike>): UIFrame<TPayload> {
        this.ensureActive();
        if (viewport) {
            this.setViewport(viewport.width ?? this.viewportWidth, viewport.height ?? this.viewportHeight);
        }
        if (this.layoutDirty) {
            this.layoutEngine.compute(
                {
                    root: this.rootId,
                    getLayout: (node) => this.layouts[node as number]!,
                    getFirstChild: (node) => {
                        const child = this.firstChild[node as number];
                        return child === 0 ? null : (child as WidgetId);
                    },
                    getNextSibling: (node) => {
                        const sibling = this.nextSibling[node as number];
                        return sibling === 0 ? null : (sibling as WidgetId);
                    },
                    measureContent: (node, constraints) => this.measureContent(node as number, constraints),
                    setBox: (node, box) => this.writeBox(node as number, box),
                    isVisible: (node) => this.isVisible(node as number),
                },
                { width: this.viewportWidth, height: this.viewportHeight }
            );
            this.layoutDirty = false;
            this.lastLayoutPasses = this.layoutEngine.getLayoutPassCount();
        }
        return this.renderFrame();
    }

    /**
     * Commits the UI frame targeting a specific actual viewport size.
     * When a canvas config is loaded (via loadFromAsset), layout is computed at the
     * reference resolution and then a scale transform is applied to all render commands
     * to map them into the actual viewport.
     *
     * When no canvas config is loaded, this behaves identically to commit(viewport).
     */
    commitToViewport(actualWidth: number, actualHeight: number): UIFrame<TPayload> {
        this.ensureActive();
        this.lastViewport = { width: actualWidth, height: actualHeight };
        if (!this.canvasConfig) {
            return this.commit({ width: actualWidth, height: actualHeight });
        }
        // Ensure layout is computed at reference resolution
        if (this.layoutDirty) {
            this.layoutEngine.compute(
                {
                    root: this.rootId,
                    getLayout: (node) => this.layouts[node as number]!,
                    getFirstChild: (node) => {
                        const child = this.firstChild[node as number];
                        return child === 0 ? null : (child as WidgetId);
                    },
                    getNextSibling: (node) => {
                        const sibling = this.nextSibling[node as number];
                        return sibling === 0 ? null : (sibling as WidgetId);
                    },
                    measureContent: (node, constraints) => this.measureContent(node as number, constraints),
                    setBox: (node, box) => this.writeBox(node as number, box),
                    isVisible: (node) => this.isVisible(node as number),
                },
                { width: this.viewportWidth, height: this.viewportHeight }
            );
            this.layoutDirty = false;
            this.lastLayoutPasses = this.layoutEngine.getLayoutPassCount();
        }
        // Render frame at reference resolution
        const frame = this.renderFrame();
        // Compute canvas scale from reference to actual viewport
        const scaleResult = resolveCanvasScale(this.canvasConfig, actualWidth, actualHeight);
        const transform = canvasScaleToTransform(scaleResult);
        // Commands keep their reference-resolution geometry; the canvas scale is
        // carried by `transform` and applied once by the renderer. Pre-scaling the
        // geometry here as well would double-apply the scale. Clip rects are the
        // exception: they feed the scissor test, which operates in viewport space.
        const scaledCommands = frame.commands.map((command) => {
            switch (command.kind) {
                case 'quad':
                case 'text':
                case 'image':
                    return {
                        ...command,
                        clip: command.clip ? scaleClipRect(command.clip, scaleResult) : null,
                        transform,
                    };
                case 'custom':
                    return {
                        ...command,
                        clip: command.clip ? scaleClipRect(command.clip, scaleResult) : null,
                    };
                default:
                    return command;
            }
        });
        return {
            viewportWidth: actualWidth,
            viewportHeight: actualHeight,
            commands: scaledCommands,
            metrics: frame.metrics,
        };
    }

    /**
     * Dispatches an input event whose pointer coordinates are expressed in actual
     * viewport (screen) pixels. When a canvas config is loaded and a commitToViewport()
     * has been performed, pointer coordinates are mapped back into the reference
     * canvas space before hit-testing. Non-pointer events pass through unchanged.
     */
    dispatchViewportInput(event: Readonly<UIInputEvent>): boolean {
        this.ensureActive();
        if (event.type !== 'pointer' || !this.canvasConfig || !this.lastViewport) {
            return this.dispatchInput(event);
        }
        const scale = resolveCanvasScale(
            this.canvasConfig,
            this.lastViewport.width,
            this.lastViewport.height
        );
        const mapped = mapViewportPointToCanvas(scale, event.x, event.y);
        return this.dispatchInput({ ...event, x: mapped.x, y: mapped.y });
    }

    dispatchInput(event: Readonly<UIInputEvent>): boolean {
        this.ensureActive();
        switch (event.type) {
            case 'pointer':
                return dispatchPointerEvent(this.inputHost, event);
            case 'key':
                return dispatchKeyEvent(this.inputHost, event);
            case 'text':
                return dispatchTextEvent(this.inputHost, event);
            case 'focus':
                if (!event.focused && this.focused) {
                    this.setFocus(null, 'window');
                }
                return false;
            default:
                return false;
        }
    }

    setFocus(widget: WidgetId | null, reason: WidgetFocusChangeEvent['reason'] = 'api', direction?: FocusMoveDirection): boolean {
        this.ensureActive();
        if (widget !== null) {
            const target = this.requireWidget(widget);
            if (!this.isFocusable(target)) {
                return false;
            }
            widget = target as WidgetId;
        }
        if (this.focused === widget) {
            return true;
        }
        const previous = this.focused;
        this.focused = widget;
        if (previous !== null) {
            this.emitFocusChange(previous as number, false, reason, direction);
        }
        if (widget !== null) {
            this.emitFocusChange(widget as number, true, reason, direction);
        }
        return true;
    }

    /** Returns the currently focused widget, or null if nothing is focused. */
    getFocused(): WidgetId | null {
        return this.focused;
    }

    moveFocus(direction: FocusMoveDirection): WidgetId | null {
        this.ensureActive();
        const candidates = this.getFocusableCandidates();
        if (candidates.length === 0) {
            return null;
        }
        const current = this.focused ? (this.focused as number) : 0;
        const scopeRoot = current === 0 ? this.rootId : this.findScopeRoot(current);
        const scoped = scopeRoot === this.rootId
            ? candidates
            : candidates.filter((candidate) => this.isAncestor(scopeRoot as number, candidate as number));
        const targetList = scoped.length > 0 ? scoped : candidates;
        let next: WidgetId | null = null;
        if (direction === 'forward' || direction === 'backward') {
            const cycle = this.focuses[scopeRoot as number]?.cycle ?? false;
            next = moveFocusLinear(targetList, direction, this.focused, cycle);
        } else {
            next = moveFocusDirectional(targetList, direction, this.focused, (index) => this.readBox(index));
        }
        if (next) {
            this.setFocus(next, 'navigation', direction);
        }
        return next;
    }

    snapshot(): UIRuntimeSnapshot {
        this.ensureActive();
        return {
            viewportWidth: this.viewportWidth,
            viewportHeight: this.viewportHeight,
            locale: this.locale,
            root: this.snapshotNode(this.rootId),
        };
    }

    restore(snapshot: UIRuntimeSnapshot): this {
        this.ensureActive();
        if (!snapshot || typeof snapshot !== 'object' || !snapshot.root) {
            throw new UIError(UIErrorCode.InvalidSnapshot, 'Runtime snapshot is invalid.', { snapshot });
        }
        this.setViewport(snapshot.viewportWidth, snapshot.viewportHeight);
        this.locale = snapshot.locale;
        this.clear();
        const rootSnapshot = snapshot.root;
        this.records[this.rootId] = normalizeWidgetRecord({
            role: rootSnapshot.role,
            controller: rootSnapshot.controller,
            key: rootSnapshot.key ?? undefined,
            props: cloneData(rootSnapshot.props ?? EMPTY_RECORD_OBJECT),
            enabled: rootSnapshot.enabled,
            interactive: rootSnapshot.interactive,
            layout: cloneData(rootSnapshot.layout ?? EMPTY_LAYOUT_INPUT),
            style: cloneData(rootSnapshot.style ?? EMPTY_STYLE_INPUT),
            text: cloneData(rootSnapshot.text ?? null),
            image: cloneData(rootSnapshot.image ?? null),
            focus: cloneData(rootSnapshot.focus ?? EMPTY_FOCUS_INPUT),
        });
        this.applyRecord(this.rootId, null, null, true);
        for (const child of rootSnapshot.children) {
            this.restoreChildSnapshot(this.rootId, child);
        }
        return this;
    }

    dispose(): void {
        if (!this.disposed) {
            this.clear();
            this.fonts.dispose();
            this.textEngine.dispose();
            this.registry.clear();
            this.disposed = true;
        }
    }

    [Symbol.dispose](): void {
        this.dispose();
    }

    private ensureActive(): void {
        if (this.disposed) {
            throw new DisposedUIError('UIRuntime');
        }
    }

    /**
     * Resolves asset bindings (binding name -> widget key) against the restored
     * widget tree. Ambiguous keys (shared by multiple widgets) and missing keys
     * are treated as malformed asset data.
     */
    private rebuildBindingTable(bindings: UIAsset['bindings']): void {
        this.bindingTable.clear();
        if (!bindings) {
            return;
        }
        const widgetsByKey = new Map<WidgetKey, WidgetId>();
        const duplicateKeys = new Set<WidgetKey>();
        for (let index = 0; index < this.records.length; index += 1) {
            const record = this.records[index];
            if (!record || record.key === undefined || record.key === null) {
                continue;
            }
            if (widgetsByKey.has(record.key)) {
                duplicateKeys.add(record.key);
            } else {
                widgetsByKey.set(record.key, index as WidgetId);
            }
        }
        for (const [name, key] of Object.entries(bindings)) {
            if (key === null) {
                continue;
            }
            if (duplicateKeys.has(key)) {
                throw new InvalidUIAssetError(
                    `Binding "${name}" is ambiguous: multiple widgets share the key "${String(key)}".`,
                    { name, key }
                );
            }
            const widget = widgetsByKey.get(key);
            if (widget === undefined) {
                throw new InvalidUIAssetError(
                    `Binding "${name}" refers to a widget key "${String(key)}" that does not exist in the asset tree.`,
                    { name, key }
                );
            }
            this.bindingTable.set(name, widget);
        }
    }

    private applyRecord(
        index: number,
        previousProps: Readonly<Record<string, unknown>> | null,
        previousController: string | null,
        initial: boolean
    ): void {
        const record = this.records[index];
        if (!record) {
            throw new WidgetNotFoundError(index);
        }
        const previousResolvedController = previousController ? this.registry.resolve(previousController) : null;
        const nextResolvedController = record.controller ? this.registry.resolve(record.controller) : null;
        if (!initial && previousResolvedController && previousResolvedController !== nextResolvedController) {
            previousResolvedController.disposeState?.(this.states[index], this, index as WidgetId);
            this.states[index] = undefined;
        }
        this.layouts[index] = compileLayoutInput(record.layoutInput);
        this.styles[index] = compileWidgetStyle(record.styleInput);
        this.texts[index] = compileWidgetText(record.textInput, {
            defaultFamily: this.fonts.getDefaultFamily(),
            locale: this.locale,
            fallbackColor: this.styles[index]!.color,
        });
        this.images[index] = compileWidgetImage(record.imageInput);
        this.focuses[index] = compileWidgetFocus(record.focusInput, record.interactive);
        this.textLayouts[index] = null;
        this.textLayoutWidths[index] = Number.NaN;
        this.updateFlags(index);
        if (!initial && previousResolvedController === nextResolvedController && nextResolvedController && previousProps) {
            nextResolvedController.update?.(this.createControllerContext(index), previousProps);
        } else if (nextResolvedController) {
            this.states[index] = nextResolvedController.createState?.(record.props, this, index as WidgetId);
            nextResolvedController.mount?.(this.createControllerContext(index));
        }
        this.layoutDirty = true;
        this.focusDirty = true;
    }

    private updateFlags(index: number): void {
        const style = this.styles[index]!;
        const focus = this.focuses[index]!;
        const record = this.records[index]!;
        let flags = NodeFlag.Allocated;
        if (style.visible) {
            flags |= NodeFlag.Visible;
        }
        if (record.enabled) {
            flags |= NodeFlag.Enabled;
        }
        if (record.interactive) {
            flags |= NodeFlag.Interactive;
        }
        if (focus.focusable) {
            flags |= NodeFlag.Focusable;
        }
        flags |= NodeFlag.TextDirty;
        this.flags[index] = flags;
    }

    private allocate(): number {
        const id = this.freeList.pop() ?? this.nextId++;
        this.ensureCapacity(id + 1);
        this.flags[id] = NodeFlag.Allocated;
        this.sequence[id] = this.nextSequence++;
        this.liveCount += 1;
        return id;
    }

    private ensureCapacity(minimum: number): void {
        if (minimum < this.parent.length) {
            return;
        }
        let nextCapacity = this.parent.length;
        while (nextCapacity <= minimum) {
            nextCapacity *= 2;
        }
        this.parent = this.growTypedArray(this.parent, nextCapacity);
        this.firstChild = this.growTypedArray(this.firstChild, nextCapacity);
        this.lastChild = this.growTypedArray(this.lastChild, nextCapacity);
        this.previousSibling = this.growTypedArray(this.previousSibling, nextCapacity);
        this.nextSibling = this.growTypedArray(this.nextSibling, nextCapacity);
        this.sequence = this.growTypedArray(this.sequence, nextCapacity);
        this.depth = this.growTypedArray(this.depth, nextCapacity);
        this.flags = this.growTypedArray(this.flags, nextCapacity);
        this.boxX = this.growTypedArray(this.boxX, nextCapacity);
        this.boxY = this.growTypedArray(this.boxY, nextCapacity);
        this.boxWidth = this.growTypedArray(this.boxWidth, nextCapacity);
        this.boxHeight = this.growTypedArray(this.boxHeight, nextCapacity);
        this.contentX = this.growTypedArray(this.contentX, nextCapacity);
        this.contentY = this.growTypedArray(this.contentY, nextCapacity);
        this.contentWidth = this.growTypedArray(this.contentWidth, nextCapacity);
        this.contentHeight = this.growTypedArray(this.contentHeight, nextCapacity);
        this.records.length = nextCapacity;
        this.layouts.length = nextCapacity;
        this.styles.length = nextCapacity;
        this.texts.length = nextCapacity;
        this.images.length = nextCapacity;
        this.focuses.length = nextCapacity;
        this.states.length = nextCapacity;
        this.textLayouts.length = nextCapacity;
        this.textLayoutWidths.length = nextCapacity;
    }

    private growTypedArray<TArray extends Int32Array | Uint32Array | Uint16Array | Float32Array>(
        current: TArray,
        length: number
    ): TArray {
        const Ctor = current.constructor as new (size: number) => TArray;
        const next = new Ctor(length);
        next.set(current);
        return next;
    }

    private requireWidget(widget: WidgetId | null): number {
        if (widget === null) {
            throw new WidgetNotFoundError(-1);
        }
        const index = widget as number;
        if ((this.flags[index] & NodeFlag.Allocated) === 0) {
            throw new WidgetNotFoundError(index);
        }
        return index;
    }

    private isVisible(index: number): boolean {
        return (this.flags[index] & NodeFlag.Visible) !== 0;
    }

    private isFocusable(index: number): boolean {
        return (
            (this.flags[index] & NodeFlag.Focusable) !== 0 &&
            (this.flags[index] & NodeFlag.Enabled) !== 0 &&
            (this.flags[index] & NodeFlag.Visible) !== 0
        );
    }

    private isAncestor(ancestor: number, candidate: number): boolean {
        for (let current = candidate; current !== 0; current = this.parent[current]) {
            if (current === ancestor) {
                return true;
            }
        }
        return false;
    }

    private detachNode(index: number): void {
        const parent = this.parent[index];
        if (parent === 0) {
            return;
        }
        const previous = this.previousSibling[index];
        const next = this.nextSibling[index];
        if (previous !== 0) {
            this.nextSibling[previous] = next;
        } else {
            this.firstChild[parent] = next;
        }
        if (next !== 0) {
            this.previousSibling[next] = previous;
        } else {
            this.lastChild[parent] = previous;
        }
        this.parent[index] = 0;
        this.previousSibling[index] = 0;
        this.nextSibling[index] = 0;
    }

    private refreshDepths(index: number, depth: number): void {
        const queue = [index];
        this.depth[index] = depth;
        while (queue.length > 0) {
            const current = queue.shift()!;
            const currentDepth = this.depth[current];
            for (let child = this.firstChild[current]; child !== 0; child = this.nextSibling[child]) {
                this.depth[child] = currentDepth + 1;
                queue.push(child);
            }
        }
    }

    private markTreeChanged(index: number): void {
        void index;
        this.layoutDirty = true;
        this.focusDirty = true;
    }

    private createControllerContext(index: number): WidgetEventContext<Record<string, unknown>, UIRuntime<TPayload>> & {
        readonly state: unknown;
    } {
        const record = this.records[index]!;
        return {
            runtime: this,
            widget: index as WidgetId,
            props: record.props,
            state: this.states[index],
        };
    }

    private measureContent(index: number, constraints: Readonly<SizeLike>): SizeLike {
        const text = this.texts[index];
        const image = this.images[index];
        const controllerType = this.records[index]?.controller;
        if (controllerType) {
            const controller = this.registry.resolve(controllerType);
            const measured = controller?.measure?.({
                runtime: this,
                widget: index as WidgetId,
                props: this.records[index]!.props,
                state: this.states[index],
                availableWidth: constraints.width,
                availableHeight: constraints.height,
            });
            if (measured) {
                return measured;
            }
        }
        let measuredWidth = 0;
        let measuredHeight = 0;
        if (image) {
            const imageSize = measureImageContent(image, constraints);
            measuredWidth = Math.max(measuredWidth, imageSize.width);
            measuredHeight = Math.max(measuredHeight, imageSize.height);
        }
        if (text && text.value.length > 0) {
            const width = Number.isFinite(constraints.width)
                ? Math.max(0, constraints.width)
                : Number.POSITIVE_INFINITY;
            if (!this.textLayouts[index] || this.textLayoutWidths[index] !== width) {
                this.textLayouts[index] = this.measureTextWithAutoSize(text, {
                    width,
                    height: constraints.height,
                });
                this.textLayoutWidths[index] = width;
            }
            measuredWidth = Math.max(measuredWidth, this.textLayouts[index]!.width);
            measuredHeight = Math.max(measuredHeight, this.textLayouts[index]!.height);
        }
        return { width: measuredWidth, height: measuredHeight };
    }

    private writeBox(index: number, box: LayoutBox): void {
        this.boxX[index] = box.x;
        this.boxY[index] = box.y;
        this.boxWidth[index] = box.width;
        this.boxHeight[index] = box.height;
        this.contentX[index] = box.contentX;
        this.contentY[index] = box.contentY;
        this.contentWidth[index] = box.contentWidth;
        this.contentHeight[index] = box.contentHeight;
    }

    private readBox(index: number): LayoutBox {
        return {
            x: this.boxX[index],
            y: this.boxY[index],
            width: this.boxWidth[index],
            height: this.boxHeight[index],
            contentX: this.contentX[index],
            contentY: this.contentY[index],
            contentWidth: this.contentWidth[index],
            contentHeight: this.contentHeight[index],
        };
    }

    private renderFrame(): UIFrame<TPayload> {
        const commands: RenderCommand<TPayload>[] = [];
        let visibleWidgetCount = 0;
        let textCommandCount = 0;
        let imageCommandCount = 0;
        let strokeCommandCount = 0;
        let customCommandCount = 0;
        let glyphCount = 0;
        const visit = (index: number, clip: LayoutBox | null): void => {
            if (!this.isVisible(index)) {
                return;
            }
            visibleWidgetCount += 1;
            const style = this.styles[index]!;
            const box = this.readBox(index);
            const nextClip = style.clip ? intersectRect(clip, box) : clip;
            if (style.clip && nextClip === null) {
                return;
            }
            const zIndex = this.layouts[index]!.zIndex;
            if (style.background.a > 0 || (style.borderWidth > 0 && style.borderColor.a > 0)) {
                const quad: QuadRenderCommand = {
                    kind: 'quad',
                    widget: index as WidgetId,
                    x: box.x,
                    y: box.y,
                    width: box.width,
                    height: box.height,
                    zIndex,
                    color: style.background,
                    borderColor: style.borderColor,
                    borderWidth: style.borderWidth,
                    radius: style.radius,
                    opacity: style.opacity,
                    clip: nextClip,
                };
                commands.push(quad);
            }
            if (style.strokes.length > 0) {
                const stroke: StrokeRenderCommand = {
                    kind: 'stroke',
                    widget: index as WidgetId,
                    x: box.x,
                    y: box.y,
                    width: box.width,
                    height: box.height,
                    zIndex,
                    opacity: style.opacity,
                    clip: nextClip,
                    strokes: style.strokes,
                };
                strokeCommandCount += 1;
                commands.push(stroke);
            }
            const image = this.images[index];
            if (image) {
                const imageCommand = resolveImageCommand(index, box, image, style, nextClip, zIndex);
                if (imageCommand) {
                    imageCommandCount += 1;
                    commands.push(imageCommand);
                }
            }
            const textLayout = this.resolveTextLayoutForRender(index);
            if (textLayout) {
                const textStyle = this.texts[index]!;
                if (textStyle.selectionColor.a > 0) {
                    for (const quad of buildSelectionCommands(index, box, textStyle, textLayout, nextClip, zIndex, style.opacity)) {
                        commands.push(quad);
                    }
                }
                if (textLayout.glyphs.length > 0) {
                    if (textStyle.shadowColor.a > 0 && (textStyle.shadowOffsetX !== 0 || textStyle.shadowOffsetY !== 0)) {
                        const shadowCommand: TextRenderCommand = {
                            kind: 'text',
                            widget: index as WidgetId,
                            x: box.contentX + textStyle.shadowOffsetX,
                            y: box.contentY + textStyle.shadowOffsetY,
                            zIndex,
                            color: textStyle.shadowColor,
                            outlineColor: TRANSPARENT,
                            outlineWidth: 0,
                            edgeSoftness: textStyle.edgeSoftness,
                            opacity: style.opacity,
                            clip: nextClip,
                            layout: textLayout,
                        };
                        textCommandCount += 1;
                        glyphCount += textLayout.glyphs.length;
                        commands.push(shadowCommand);
                    }
                    for (const quad of buildLineDecorationCommands(index, box, textStyle, textLayout, nextClip, zIndex, style.opacity)) {
                        commands.push(quad);
                    }
                    const textCommand: TextRenderCommand = {
                        kind: 'text',
                        widget: index as WidgetId,
                        x: box.contentX,
                        y: box.contentY,
                        zIndex,
                        color: this.texts[index]!.color,
                        outlineColor: this.texts[index]!.outlineColor,
                        outlineWidth: this.texts[index]!.outlineWidth,
                        edgeSoftness: this.texts[index]!.edgeSoftness,
                        opacity: style.opacity,
                        clip: nextClip,
                        layout: textLayout,
                    };
                    textCommandCount += 1;
                    glyphCount += textLayout.glyphs.length;
                    commands.push(textCommand);
                }
                const caretCommand = buildCaretCommand(index, box, textStyle, textLayout, nextClip, zIndex, style.opacity);
                if (caretCommand) {
                    commands.push(caretCommand);
                }
            }
            const controller = this.records[index]!.controller
                ? this.registry.resolve(this.records[index]!.controller)
                : null;
            controller?.render?.({
                runtime: this,
                widget: index as WidgetId,
                props: this.records[index]!.props,
                state: this.states[index],
                push: (payload: TPayload) => {
                    const command: CustomRenderCommand<TPayload> = {
                        kind: 'custom',
                        widget: index as WidgetId,
                        zIndex,
                        clip: nextClip,
                        payload,
                    };
                    customCommandCount += 1;
                    commands.push(command);
                },
            });
            for (let child = this.firstChild[index]; child !== 0; child = this.nextSibling[child]) {
                visit(child, nextClip);
            }
        };
        visit(this.rootId, null);
        commands.sort((left, right) => {
            if (left.zIndex !== right.zIndex) {
                return left.zIndex - right.zIndex;
            }
            return this.sequence[left.widget as number] - this.sequence[right.widget as number];
        });

        // ── Focus ring overlay ──────────────────────────────────────────────
        if (this.focused !== null) {
            const focusIndex = this.focused as number;
            const focusPolicy = this.focuses[focusIndex];
            if (focusPolicy && (this.flags[focusIndex] & NodeFlag.Allocated) !== 0) {
                const focusBox = this.readBox(focusIndex);
                const ringOffset = focusPolicy.ringOffset;
                const ringWidth = focusPolicy.ringWidth;
                const ringColor = focusPolicy.ringColor;

                // Compute the parent clip by walking up the ancestor chain.
                // The focus ring should be clipped by ancestor clip rects, not the widget's own.
                let parentClip: LayoutBox | null = null;
                for (let ancestor = this.parent[focusIndex]; ancestor !== 0; ancestor = this.parent[ancestor]) {
                    if (!this.isVisible(ancestor)) {
                        continue;
                    }
                    const ancestorStyle = this.styles[ancestor];
                    if (ancestorStyle && ancestorStyle.clip) {
                        const ancestorBox = this.readBox(ancestor);
                        parentClip = parentClip === null ? ancestorBox : intersectRect(parentClip, ancestorBox);
                        if (parentClip === null) {
                            break;
                        }
                    }
                }

                const focusStyle = this.styles[focusIndex];
                const baseRadius = focusStyle?.radius ?? { topLeft: 0, topRight: 0, bottomRight: 0, bottomLeft: 0 };
                const ringExpansion = ringOffset + ringWidth;

                const focusRingQuad: QuadRenderCommand = {
                    kind: 'quad',
                    widget: focusIndex as WidgetId,
                    x: focusBox.x - ringExpansion,
                    y: focusBox.y - ringExpansion,
                    width: focusBox.width + ringExpansion * 2,
                    height: focusBox.height + ringExpansion * 2,
                    zIndex: Number.MAX_SAFE_INTEGER,
                    color: TRANSPARENT,
                    borderColor: ringColor,
                    borderWidth: ringWidth,
                    radius: {
                        topLeft: baseRadius.topLeft + ringExpansion,
                        topRight: baseRadius.topRight + ringExpansion,
                        bottomRight: baseRadius.bottomRight + ringExpansion,
                        bottomLeft: baseRadius.bottomLeft + ringExpansion,
                    },
                    opacity: 1,
                    clip: parentClip,
                };
                commands.push(focusRingQuad);
            }
        }

        const metrics: UIFrameMetrics = {
            widgetCount: this.getWidgetCount(),
            visibleWidgetCount,
            renderCount: commands.length,
            customCommandCount,
            imageCommandCount,
            textCommandCount,
            strokeCommandCount,
            glyphCount,
            layoutPasses: this.lastLayoutPasses,
        };
        return {
            viewportWidth: this.viewportWidth,
            viewportHeight: this.viewportHeight,
            commands,
            metrics,
        };
    }

    private measureTextWithAutoSize(
        text: ResolvedTextBlock,
        constraints: TextLayoutConstraint
    ): TextLayoutResult {
        if (text.autoSize !== 'shrink-to-fit') {
            return this.textEngine.measure(text, constraints);
        }

        // Build cache key from text properties and constraints.
        const cacheKey = `${text.value}\0${text.family}\0${text.size}\0${text.weight}\0${text.style}\0${constraints.width}\0${constraints.height}`;
        const cached = this.autoSizeCache.get(cacheKey);
        if (cached) {
            return cached;
        }

        const maxWidth = constraints.width ?? Number.POSITIVE_INFINITY;
        const maxHeight = constraints.height ?? Number.POSITIVE_INFINITY;
        // When either constraint is unbounded, text already fits by definition — no-op.
        if (!Number.isFinite(maxWidth) && !Number.isFinite(maxHeight)) {
            return this.textEngine.measure(text, constraints);
        }
        const authoredSize = text.size;
        const maxSize = Math.min(authoredSize, text.maxAutoSize);
        const minSize = text.minAutoSize;
        if (minSize >= maxSize) {
            const clampedBlock: ResolvedTextBlock = { ...text, size: minSize, autoSize: 'none' };
            return this.textEngine.measure(clampedBlock, constraints);
        }
        // First check if the authored size already fits — if so, use it directly.
        const initialLayout = this.textEngine.measure(text, constraints);
        const fitsWidth = initialLayout.width <= (Number.isFinite(maxWidth) ? maxWidth : Number.POSITIVE_INFINITY);
        const fitsHeight = initialLayout.height <= (Number.isFinite(maxHeight) ? maxHeight : Number.POSITIVE_INFINITY);
        if (fitsWidth && fitsHeight) {
            return initialLayout;
        }
        // Binary search for the largest font size that fits within the constraints.
        let lo = minSize;
        let hi = maxSize;
        let bestSize = minSize;
        const maxIter = 8;
        for (let iter = 0; iter < maxIter && hi - lo > 0.5; iter += 1) {
            const mid = (lo + hi) * 0.5;
            const scaledBlock: ResolvedTextBlock = { ...text, size: mid, autoSize: 'none' };
            const scaledLayout = this.textEngine.measure(scaledBlock, constraints);
            const scaledFitsWidth = scaledLayout.width <= (Number.isFinite(maxWidth) ? maxWidth : Number.POSITIVE_INFINITY);
            const scaledFitsHeight = scaledLayout.height <= (Number.isFinite(maxHeight) ? maxHeight : Number.POSITIVE_INFINITY);
            if (scaledFitsWidth && scaledFitsHeight) {
                bestSize = mid;
                lo = mid;
            } else {
                hi = mid;
            }
        }
        if (bestSize >= maxSize - 0.5) {
            const clampedBlock = { ...text, size: maxSize };
            return this.textEngine.measure(clampedBlock, constraints);
        }
        const finalBlock: ResolvedTextBlock = { ...text, size: bestSize, autoSize: 'none' };
        const result = this.textEngine.measure(finalBlock, constraints);

        // Store result in cache, evicting oldest half if over capacity.
        if (this.autoSizeCache.size >= this.autoSizeCacheMaxSize) {
            const keysToDelete = Math.ceil(this.autoSizeCacheMaxSize / 2);
            const keys = this.autoSizeCache.keys();
            for (let i = 0; i < keysToDelete; i++) {
                const key = keys.next().value;
                if (key !== undefined) {
                    this.autoSizeCache.delete(key);
                }
            }
        }
        this.autoSizeCache.set(cacheKey, result);

        return result;
    }

    private resolveTextLayoutForRender(index: number): TextLayoutResult | null {
        const text = this.texts[index];
        if (!text) {
            return null;
        }
        const width = this.contentWidth[index];
        if (!this.textLayouts[index] || this.textLayoutWidths[index] !== width) {
            this.textLayouts[index] = this.measureTextWithAutoSize(text, {
                width,
                height: this.contentHeight[index],
            });
            this.textLayoutWidths[index] = width;
        }
        return this.textLayouts[index];
    }

    private hitTest(x: number, y: number): WidgetId | null {
        let bestId = 0;
        let bestZIndex = Number.NEGATIVE_INFINITY;
        let bestDepth = -1;
        let bestOrder = -1;
        const visit = (index: number, clip: LayoutBox | null): void => {
            if (!this.isVisible(index)) {
                return;
            }
            const box = this.readBox(index);
            const nextClip = this.styles[index]!.clip ? intersectRect(clip, box) : clip;
            if (this.styles[index]!.clip && nextClip === null) {
                return;
            }
            if (intersectsPoint(box, x, y) && (!nextClip || intersectsPoint(nextClip, x, y))) {
                if (
                    (this.flags[index] & NodeFlag.Interactive) !== 0 &&
                    (this.flags[index] & NodeFlag.Enabled) !== 0
                ) {
                    const candidateZIndex = this.layouts[index]!.zIndex;
                    const candidateDepth = this.depth[index];
                    const candidateOrder = this.sequence[index];
                    if (
                        bestId === 0 ||
                        candidateZIndex > bestZIndex ||
                        (candidateZIndex === bestZIndex && candidateDepth > bestDepth) ||
                        (candidateZIndex === bestZIndex &&
                            candidateDepth === bestDepth &&
                            candidateOrder > bestOrder)
                    ) {
                        bestId = index;
                        bestZIndex = candidateZIndex;
                        bestDepth = candidateDepth;
                        bestOrder = candidateOrder;
                    }
                }
                for (let child = this.firstChild[index]; child !== 0; child = this.nextSibling[child]) {
                    visit(child, nextClip);
                }
            }
        };
        visit(this.rootId, null);
        return bestId === 0 ? null : (bestId as WidgetId);
    }

    private updateHover(target: WidgetId | null, event: Readonly<UIPointerEvent>): void {
        if (this.hovered === target) {
            return;
        }
        const previous = this.hovered;
        this.hovered = target;
        if (previous) {
            this.invokeEvent(previous as number, { ...event, phase: 'leave' });
        }
        if (target) {
            this.invokeEvent(target as number, { ...event, phase: 'enter' });
        }
    }

    private bubbleEvent(index: number, event: Readonly<UIInputEvent>): boolean {
        for (let current = index; current !== 0; current = this.parent[current]) {
            if (this.invokeEvent(current, event)) {
                return true;
            }
        }
        return false;
    }

    private invokeEvent(index: number, event: Readonly<UIInputEvent>): boolean {
        const record = this.records[index];
        if (!record || !record.enabled) {
            return false;
        }
        const context: WidgetEventContext<Record<string, unknown>, UIRuntime<TPayload>> = {
            runtime: this,
            widget: index as WidgetId,
            props: record.props,
        };
        const handlers = record.handlers;
        let handled = false;
        switch (event.type) {
            case 'pointer':
                switch (event.phase) {
                    case 'move':
                        handled = Boolean(handlers?.pointerMove?.(event, context));
                        break;
                    case 'down':
                        handled = Boolean(handlers?.pointerDown?.(event, context));
                        break;
                    case 'up':
                        handled = Boolean(handlers?.pointerUp?.(event, context));
                        break;
                    case 'enter':
                        handled = Boolean(handlers?.pointerEnter?.(event, context));
                        break;
                    case 'leave':
                        handled = Boolean(handlers?.pointerLeave?.(event, context));
                        break;
                    case 'wheel':
                        handled = Boolean(handlers?.wheel?.(event, context));
                        break;
                    default:
                        break;
                }
                break;
            case 'key':
                handled = event.phase === 'down'
                    ? Boolean(handlers?.keyDown?.(event, context))
                    : Boolean(handlers?.keyUp?.(event, context));
                break;
            case 'text':
                handled = Boolean(handlers?.textInput?.(event, context));
                break;
            default:
                break;
        }
        const controller = record.controller ? this.registry.resolve(record.controller) : null;
        if (!handled && controller?.input) {
            handled = Boolean(
                controller.input(event, {
                    runtime: this,
                    widget: index as WidgetId,
                    props: record.props,
                    state: this.states[index],
                })
            );
        }
        return handled;
    }

    private emitFocusChange(
        index: number,
        focused: boolean,
        reason: WidgetFocusChangeEvent['reason'],
        direction?: FocusMoveDirection
    ): void {
        const record = this.records[index];
        if (!record) {
            return;
        }
        const event: WidgetFocusChangeEvent = {
            type: 'widget-focus',
            focused,
            reason,
        };
        const context: WidgetEventContext<Record<string, unknown>, UIRuntime<TPayload>> = {
            runtime: this,
            widget: index as WidgetId,
            props: record.props,
        };
        if (focused) {
            void record.handlers?.focus?.(event, context);
        } else {
            void record.handlers?.blur?.(event, context);
        }
        const controller = record.controller ? this.registry.resolve(record.controller) : null;
        if (controller) {
            const controllerContext = {
                runtime: this,
                widget: index as WidgetId,
                props: record.props,
                state: this.states[index],
                reason,
                direction,
            };
            if (focused) {
                controller.focus?.(controllerContext);
            } else {
                controller.blur?.(controllerContext);
            }
        }
    }

    private getFocusableCandidates(): WidgetId[] {
        if (this.focusDirty) {
            const candidates: WidgetId[] = [];
            for (let index = 1; index < this.nextId; index += 1) {
                if ((this.flags[index] & NodeFlag.Allocated) === 0 || !this.isFocusable(index)) {
                    continue;
                }
                candidates.push(index as WidgetId);
            }
            candidates.sort((left, right) => {
                const leftIndex = left as number;
                const rightIndex = right as number;
                const leftFocus = this.focuses[leftIndex]!;
                const rightFocus = this.focuses[rightIndex]!;
                if (leftFocus.tabIndex !== rightFocus.tabIndex) {
                    return leftFocus.tabIndex - rightFocus.tabIndex;
                }
                if (leftFocus.order !== rightFocus.order) {
                    return leftFocus.order - rightFocus.order;
                }
                return this.sequence[leftIndex] - this.sequence[rightIndex];
            });
            this.focusOrder = candidates;
            this.focusDirty = false;
        }
        return this.focusOrder;
    }

    private findScopeRoot(index: number): WidgetId {
        for (let current = index; current !== 0; current = this.parent[current]) {
            if (this.focuses[current]?.scope) {
                return current as WidgetId;
            }
        }
        return this.rootId;
    }

    private snapshotNode(index: number): WidgetSnapshot {
        const record = this.records[index]!;
        const children: WidgetSnapshot[] = [];
        for (let child = this.firstChild[index]; child !== 0; child = this.nextSibling[child]) {
            children.push(this.snapshotNode(child));
        }
        return {
            role: record.role,
            controller: record.controller ?? undefined,
            key: this.serializeKey(record.key),
            props: cloneData(record.props),
            enabled: record.enabled,
            interactive: record.interactive,
            layout: cloneData(record.layoutInput),
            style: cloneData(record.styleInput),
            text: cloneData(record.textInput),
            image: cloneData(record.imageInput),
            focus: cloneData(record.focusInput),
            children,
        };
    }

    private serializeKey(key: WidgetKey | undefined): WidgetSerializableKey | undefined {
        if (key === undefined) {
            return undefined;
        }
        if (typeof key === 'symbol') {
            return null;
        }
        return key;
    }

    private restoreChildSnapshot(parent: WidgetId, snapshot: WidgetSnapshot): WidgetId {
        const child = this.createWidget({
            role: snapshot.role,
            controller: snapshot.controller,
            key: snapshot.key ?? undefined,
            props: cloneData(snapshot.props ?? EMPTY_RECORD_OBJECT),
            enabled: snapshot.enabled,
            interactive: snapshot.interactive,
            layout: cloneData(snapshot.layout ?? EMPTY_LAYOUT_INPUT),
            style: cloneData(snapshot.style ?? EMPTY_STYLE_INPUT),
            text: cloneData(snapshot.text ?? null),
            image: cloneData(snapshot.image ?? null),
            focus: cloneData(snapshot.focus ?? EMPTY_FOCUS_INPUT),
        });
        this.appendChild(parent, child);
        for (const grandChild of snapshot.children) {
            this.restoreChildSnapshot(child, grandChild);
        }
        return child;
    }

    private destroyNode(index: number): void {
        if (this.focused && this.isAncestor(index, this.focused as number)) {
            this.focused = null;
        }
        if (this.hovered && this.isAncestor(index, this.hovered as number)) {
            this.hovered = null;
        }
        if (this.pressed && this.isAncestor(index, this.pressed as number)) {
            this.pressed = null;
        }
        const controller = this.records[index]?.controller
            ? this.registry.resolve(this.records[index]!.controller)
            : null;
        controller?.disposeState?.(this.states[index], this, index as WidgetId);
        this.controllerListeners.delete(index);
        this.records[index] = null;
        this.layouts[index] = null;
        this.styles[index] = null;
        this.texts[index] = null;
        this.images[index] = null;
        this.focuses[index] = null;
        this.states[index] = undefined;
        this.textLayouts[index] = null;
        this.textLayoutWidths[index] = Number.NaN;
        this.parent[index] = 0;
        this.firstChild[index] = 0;
        this.lastChild[index] = 0;
        this.previousSibling[index] = 0;
        this.nextSibling[index] = 0;
        this.flags[index] = 0;
        this.freeList.push(index);
        this.liveCount -= 1;
    }
}

// ─── Canvas-scale helpers (module-private) ──────────────────────────────────

function scaleClipRect(
    rect: RectLike,
    scale: CanvasScaleResult
): RectLike {
    return {
        x: rect.x * scale.scaleX + scale.offsetX,
        y: rect.y * scale.scaleY + scale.offsetY,
        width: rect.width * scale.scaleX,
        height: rect.height * scale.scaleY,
    };
}

export type {
    ColorInput,
    FocusMoveDirection,
    LayoutBox,
    RenderCommand,
    SizeLike,
    TextLayoutResult,
    UIFrame,
    UIFrameMetrics,
    UIInputEvent,
    WidgetConfig,
    WidgetEventHandlers,
    WidgetId,
    WidgetLayoutInput,
    WidgetPatch,
    WidgetSnapshot,
    WidgetStyleInput,
    UIRuntimeSnapshot,
};

export { serializeUIAsset, deserializeUIAsset, validateUIAsset } from './runtime/ui-asset-io';
export type { UIAsset, UICanvasConfig, UICanvasScaleMode, UISafeAreaInset } from './types/ui-asset';
