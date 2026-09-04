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
import type { LayoutTreeAdapter } from './layout';
import {
    type CanvasScaleResult,
    resolveCanvasScale,
    canvasScaleToTransform,
    mapViewportPointToCanvas,
} from './layout/canvas-scaler';
import { NodeFlag } from './runtime/node-flags';
import { ControllerEventBus, type ControllerEventHandler } from './runtime/controller-event-bus';
import { FocusController, type FocusControllerHost } from './runtime/focus-controller';
import { AutoSizeService } from './runtime/autosize-service';
import { RenderCommandBuilder, type RenderCommandBuilderHost } from './runtime/render-command-builder';
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
} from './runtime/runtime-input';
import { measureImageContent } from './runtime/runtime-frame';
import {
    EMPTY_FOCUS_INPUT,
    EMPTY_LAYOUT_INPUT,
    EMPTY_RECORD_OBJECT,
    EMPTY_STYLE_INPUT,
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
import { WidgetRegistry, type WidgetController } from './widget';
import type {
    ColorInput,
    FocusMoveDirection,
    FontRegistryOptions,
    LayoutBox,
    RenderCommand,
    ResolvedFocusPolicy,
    ResolvedWidgetImage,
    ResolvedLayout,
    ResolvedTextBlock,
    ResolvedWidgetStyle,
    RectLike,
    SizeLike,
    TextLayoutResult,
    TextLayoutConstraint,
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
    /** Per-widget translation offset X (absolute, set by translateWidgetBox). Uses number[] for full precision. */
    private translateX: number[] = new Array(16).fill(0);
    /** Per-widget translation offset Y (absolute, set by translateWidgetBox). Uses number[] for full precision. */
    private translateY: number[] = new Array(16).fill(0);
    private freeList: number[] = [];
    private rootId: WidgetId;
    private nextId = 1;
    private nextSequence = 1;
    private liveCount = 0;
    private hovered: WidgetId | null = null;
    private pressed: WidgetId | null = null;
    private focusController = new FocusController();
    private layoutDirty = true;
    private disposed = false;
    private viewportWidth: number;
    private viewportHeight: number;
    private locale: string;
    private lastLayoutPasses = 0;
    private canvasConfig: UICanvasConfig | null = null;
    private lastViewport: { readonly width: number; readonly height: number } | null = null;
    private readonly bindingTable = new Map<string, WidgetId>();
    private readonly controllerEventBus = new ControllerEventBus();
    private readonly controllerResolveCache = new Map<string, WidgetController<any, any, any> | null>();
    private readonly autoSizeService = new AutoSizeService();
    private readonly renderCommandBuilder = new RenderCommandBuilder<TPayload>();
    private readonly dirtyNodes = new Set<number>();
    private structuralDirty = false;

    private readonly inputHost: UIInputDispatchHost = {
        getPressed: () => this.pressed,
        setPressed: (widget) => {
            this.pressed = widget;
        },
        getFocused: () => this.focusController.getFocused(),
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
        this.controllerEventBus.on(widget, eventName, callback as ControllerEventHandler);
    }

    /**
     * Removes a previously registered controller event listener.
     */
    offControllerEvent(widget: WidgetId, eventName: string, callback: (data: unknown) => void): void {
        this.ensureActive();
        this.controllerEventBus.off(widget, eventName, callback as ControllerEventHandler);
    }

    /**
     * Emits a named controller event for a widget. Called by controllers at
     * meaningful interaction points (drag start, drop, etc.). Returns true
     * when at least one listener was invoked.
     */
    emitControllerEvent(widget: WidgetId, eventName: string, data?: unknown): boolean {
        this.ensureActive();
        return this.controllerEventBus.emit(widget, eventName, data);
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
        const styleOnly = !patch.layout && !patch.text && !patch.image && !patch.focus && !patch.controller && !patch.role && patch.enabled === undefined && patch.interactive === undefined;
        this.applyRecord(index, previousProps, previousController, false, styleOnly);
        return this;
    }

    /**
     * Updates a widget's scroll content offset without triggering a full
     * relayout pass.
     *
     * Scrolling is a pure translation: children keep their measured sizes and
     * relative arrangement, so the widget's resolved layout offset is updated
     * in place and the subtree boxes are translated by the delta. The widget's
     * layoutInput is kept in sync (when it already authors content offsets) so
     * a later full relayout produces the same geometry. Per-frame scroll
     * updates must use this instead of updateWidget, which marks the whole
     * tree dirty and re-measures everything.
     */
    setContentOffset(widget: WidgetId, offsetX: number, offsetY: number): this {
        this.ensureActive();
        const index = this.requireWidget(widget);
        const layout = this.layouts[index];
        if (!layout) {
            return this;
        }
        const deltaX = offsetX - layout.contentOffsetX;
        const deltaY = offsetY - layout.contentOffsetY;
        if (deltaX === 0 && deltaY === 0) {
            return this;
        }
        this.layouts[index] = { ...layout, contentOffsetX: offsetX, contentOffsetY: offsetY };
        const record = this.records[index];
        if (
            record &&
            (record.layoutInput.contentOffsetX !== undefined || record.layoutInput.contentOffsetY !== undefined)
        ) {
            this.records[index] = {
                ...record,
                layoutInput: { ...record.layoutInput, contentOffsetX: offsetX, contentOffsetY: offsetY },
            };
        }
        // The content origin moves opposite to the offset; children follow it.
        const contentShiftX = -deltaX;
        const contentShiftY = -deltaY;
        this.contentX[index] += contentShiftX;
        this.contentY[index] += contentShiftY;
        for (let child = this.firstChild[index]; child !== 0; child = this.nextSibling[child]) {
            this.translateSubtreeBoxes(child, contentShiftX, contentShiftY);
        }
        return this;
    }

    /**
     * Translates an already-laid-out widget subtree by a pixel delta without
     * invalidating layout. Used for per-frame scroll-driven repositioning
     * (scrollbar thumbs). The translation is stored as an absolute offset
     * (not by mutating insets) to prevent float drift across repeated calls.
     * A later full relayout will clear the translation offset.
     */
    translateWidgetBox(widget: WidgetId, dx: number, dy: number): this {
        this.ensureActive();
        const index = this.requireWidget(widget);
        if (dx === 0 && dy === 0) {
            return this;
        }
        // Store translation as absolute offset (accumulated)
        this.translateSubtreeOffsets(index, dx, dy);
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
        this.focusController.markDirty();
        this.structuralDirty = true;
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
            const adapter = this.createLayoutAdapter();
            const viewportSize = { width: this.viewportWidth, height: this.viewportHeight };
            if (this.canScopedRelayout()) {
                this.scopedRelayout(adapter, viewportSize);
            } else {
                this.layoutEngine.compute(adapter, viewportSize);
                // Clear translation offsets after full layout (they're baked into the new boxes)
                this.clearTranslationOffsets();
            }
            this.layoutDirty = false;
            this.dirtyNodes.clear();
            this.structuralDirty = false;
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
            const adapter = this.createLayoutAdapter();
            const viewportSize = { width: this.viewportWidth, height: this.viewportHeight };
            if (this.canScopedRelayout()) {
                this.scopedRelayout(adapter, viewportSize);
            } else {
                this.layoutEngine.compute(adapter, viewportSize);
            }
            this.layoutDirty = false;
            this.dirtyNodes.clear();
            this.structuralDirty = false;
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
        // RenderCommand declares clip/transform readonly, but the frame and its
        // commands are owned by this call — mutate in place to avoid rebuilding
        // every command array on each commitToViewport().
        for (const command of frame.commands) {
            const mutable = command as { clip: RectLike | null; transform?: unknown };
            mutable.clip = command.clip ? scaleClipRect(command.clip, scaleResult) : null;
            if (command.kind === 'quad' || command.kind === 'text' || command.kind === 'image' || command.kind === 'stroke') {
                mutable.transform = transform;
            }
        }
        return {
            viewportWidth: actualWidth,
            viewportHeight: actualHeight,
            commands: frame.commands,
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
                if (!event.focused && this.focusController.getFocused()) {
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
        const previous = this.focusController.getFocused();
        const changed = this.focusController.setFocus(widget, this.getFocusHost(), {
            reason,
            direction,
            onFocusedChange: (next, prev) => {
                if (prev !== null) {
                    this.emitFocusChange(prev as number, false, reason, direction);
                }
                if (next !== null) {
                    this.emitFocusChange(next as number, true, reason, direction);
                }
            },
        });
        return changed;
    }

    /** Returns the currently focused widget, or null if nothing is focused. */
    getFocused(): WidgetId | null {
        return this.focusController.getFocused();
    }

    moveFocus(direction: FocusMoveDirection): WidgetId | null {
        this.ensureActive();
        return this.focusController.moveFocus(direction, this.getFocusHost(), (w, r, d) => this.setFocus(w, r, d));
    }

    private getFocusHost(): FocusControllerHost {
        return {
            flags: this.flags,
            parent: this.parent,
            sequence: this.sequence,
            focuses: this.focuses,
            rootId: this.rootId,
            nextId: this.nextId,
            isFocusable: (index) => this.isFocusable(index),
            isAncestor: (ancestor, candidate) => this.isAncestor(ancestor, candidate),
            readBox: (index) => this.readBox(index),
        };
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

    /**
     * Disposes the runtime, releasing all resources.
     *
     * NOTE: Controller re-registration after dispose is unsupported. The
     * controller resolve cache is cleared here; any controllers registered
     * before dispose become unreachable.
     */
    dispose(): void {
        if (!this.disposed) {
            this.clear();
            this.fonts.dispose();
            this.textEngine.dispose();
            this.registry.clear();
            this.controllerResolveCache.clear();
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
        initial: boolean,
        styleOnly = false
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
        if (!styleOnly) {
            this.layoutDirty = true;
            this.dirtyNodes.add(index);
        }
        this.focusController.markDirty();
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
        this.translateX.length = nextCapacity;
        this.translateY.length = nextCapacity;
        this.translateX.fill(0, this.records.length);
        this.translateY.fill(0, this.records.length);
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
        this.dirtyNodes.add(index);
        this.layoutDirty = true;
        this.focusController.markDirty();
        this.structuralDirty = true;
    }

    private createLayoutAdapter(): LayoutTreeAdapter<number> {
        return {
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
        };
    }

    /**
     * Returns true when scoped relayout is safe:
     * - dirtyNodes is non-empty
     * - no structural changes (insert/remove/reparent) since last commit
     * - root is not among the dirty nodes
     * - dirtyNodes.size <= 50% of live node count
     */
    private canScopedRelayout(): boolean {
        if (this.dirtyNodes.size === 0 || this.structuralDirty) {
            return false;
        }
        if (this.dirtyNodes.has(this.rootId)) {
            return false;
        }
        return this.dirtyNodes.size <= this.liveCount * 0.5;
    }

    private scopedRelayout(adapter: LayoutTreeAdapter<number>, viewport: Readonly<SizeLike>): void {
        // Collect unique parents of dirty nodes. Re-laying out the parent
        // (rather than the dirty node itself) ensures that siblings are
        // re-positioned when a dirty node's measured size changes.
        const parents = new Set<number>();
        for (const nodeId of this.dirtyNodes) {
            const parent = this.parent[nodeId];
            if (parent !== 0) {
                parents.add(parent);
            }
        }
        // Sort parents by depth ascending so grandparents are processed before
        // their descendants. This prevents stale availWidth propagation when
        // a dirty grandparent is processed after its descendant.
        const sortedParents = Array.from(parents).sort((left, right) => this.depth[left] - this.depth[right]);
        for (const parentId of sortedParents) {
            const box = this.readBox(parentId);
            const grandparent = this.parent[parentId];
            const gpBox = grandparent !== 0 ? this.readBox(grandparent) : null;
            const availWidth = gpBox ? gpBox.contentWidth : viewport.width;
            const availHeight = gpBox ? gpBox.contentHeight : viewport.height;
            this.layoutEngine.computeSubtree(adapter, viewport, parentId, box.x, box.y, availWidth, availHeight);
        }
    }

    /** Translates the stored boxes of a subtree by a pixel delta (no relayout). */
    private translateSubtreeBoxes(index: number, dx: number, dy: number): void {
        const stack = [index];
        while (stack.length > 0) {
            const current = stack.pop()!;
            this.boxX[current] += dx;
            this.boxY[current] += dy;
            this.contentX[current] += dx;
            this.contentY[current] += dy;
            for (let child = this.firstChild[current]; child !== 0; child = this.nextSibling[child]) {
                stack.push(child);
            }
        }
    }

    /** Accumulates translation offsets for a subtree (prevents float drift). */
    private translateSubtreeOffsets(index: number, dx: number, dy: number): void {
        const stack = [index];
        while (stack.length > 0) {
            const current = stack.pop()!;
            this.translateX[current] += dx;
            this.translateY[current] += dy;
            for (let child = this.firstChild[current]; child !== 0; child = this.nextSibling[child]) {
                stack.push(child);
            }
        }
    }

    /** Clears all translation offsets (called after full layout). */
    private clearTranslationOffsets(): void {
        for (let i = 0; i < this.translateX.length; i++) {
            this.translateX[i] = 0;
            this.translateY[i] = 0;
        }
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
            x: this.boxX[index] + this.translateX[index],
            y: this.boxY[index] + this.translateY[index],
            width: this.boxWidth[index],
            height: this.boxHeight[index],
            contentX: this.contentX[index] + this.translateX[index],
            contentY: this.contentY[index] + this.translateY[index],
            contentWidth: this.contentWidth[index],
            contentHeight: this.contentHeight[index],
        };
    }

    private resolveControllerCached(controllerName: string | null): WidgetController<any, any, any> | null {
        if (!controllerName) {
            return null;
        }
        const cached = this.controllerResolveCache.get(controllerName);
        if (cached !== undefined) {
            return cached;
        }
        const resolved = this.registry.resolve(controllerName);
        this.controllerResolveCache.set(controllerName, resolved);
        return resolved;
    }

    private renderFrame(): UIFrame<TPayload> {
        return this.renderCommandBuilder.build(this.getRenderHost());
    }

    private getRenderHost(): RenderCommandBuilderHost<TPayload> {
        return {
            flags: this.flags,
            parent: this.parent,
            firstChild: this.firstChild,
            nextSibling: this.nextSibling,
            sequence: this.sequence,
            styles: this.styles,
            layouts: this.layouts,
            images: this.images,
            texts: this.texts,
            focuses: this.focuses,
            records: this.records as Array<{ controller: string | null; props: Record<string, unknown> } | null>,
            states: this.states,
            rootId: this.rootId,
            viewportWidth: this.viewportWidth,
            viewportHeight: this.viewportHeight,
            lastLayoutPasses: this.lastLayoutPasses,
            isVisible: (index) => this.isVisible(index),
            readBox: (index) => this.readBox(index),
            resolveTextLayoutForRender: (index) => this.resolveTextLayoutForRender(index),
            resolveControllerCached: (name) => this.resolveControllerCached(name),
            getFocused: () => this.focusController.getFocused(),
            getWidgetCount: () => this.getWidgetCount(),
            getRuntime: () => this,
        };
    }

    private measureTextWithAutoSize(
        text: ResolvedTextBlock,
        constraints: TextLayoutConstraint
    ): TextLayoutResult {
        return this.autoSizeService.measure(this, text, constraints);
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
        const focusedWidget = this.focusController.getFocused();
        if (focusedWidget && this.isAncestor(index, focusedWidget as number)) {
            this.focusController.clearFocus();
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
        this.controllerEventBus.clear(index as WidgetId);
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
