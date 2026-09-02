import type { UIRuntime } from '../runtime';
import type { UIInputEvent, UIImageSource, WidgetId, WidgetImageInput, WidgetStrokeData } from '../types';
import type { WidgetController, WidgetControllerContext } from '../widget';
import { animateWidgetColor, Easing, type UIAnimationHandle } from './animation';

/**
 * Declarative checkbox controller for `.ui.json` authored checkboxes.
 *
 * The authored widget acts as the interaction surface and its `props` name the
 * child widgets that visualise the checked state:
 *
 *   props: {
 *     isOn: boolean,
 *     indeterminate: boolean,
 *     boxKey,     // the box child widget
 *     markKey,    // the mark indicator inside the box
 *     labelKey,   // the text label child
 *     states: { normal, hover, checked, disabled },
 *     transition: 'color' | 'tint' | 'sprite',
 *     boxTints:   { normal: '#ffffffff', hover: '#ccccccccff', ... },
 *     markTints:  { normal: '#ffffffff', checked: '#ff0000ff', ... },
 *     boxSprites: { normal: { kind:'texture', resourceId:'box_off.png', ... }, ... },
 *     markSprites:{ normal: { kind:'texture', resourceId:'mark_off.png', ... }, ... },
 *     markStyle: 'check' | 'cross' | 'dot' | 'dash',
 *     markColor: string,
 *     markSize: number,
 *     markWeight: number,
 *     labelPosition: 'left' | 'right' | 'hidden',
 *     labelColor: string,      // omitted = keep the authored label color
 *     labelGap: number,
 *     boxSize: number,
 *   }
 *
 * Transition modes:
 *   'color'  — swaps `style.background` on the box child (default).
 *   'tint'   — patches `image.tint` on box and/or mark children (requires image).
 *   'sprite' — swaps `image.source` on box and/or mark children (requires image).
 *
 * Child widgets are resolved through the asset's binding table.
 */
export const CHECKBOX_TOGGLE_CONTROLLER_TYPE = 'checkbox-toggle';

export type CheckboxVisualState = 'normal' | 'hover' | 'checked' | 'disabled';
export type CheckboxMarkStyle = 'check' | 'cross' | 'dot' | 'dash';
export type CheckboxTransitionMode = 'color' | 'tint' | 'sprite';

/**
 * Inline image-source descriptor for per-state sprite swapping on checkbox
 * children. Mirrors `UIImageSource` without requiring consumers to import
 * the full widget type.
 */
export interface CheckboxImageSourceInput {
	readonly kind: 'texture' | 'material';
	readonly resourceId?: string;
	readonly materialId?: string;
	readonly textureBinding?: string;
	readonly width: number;
	readonly height: number;
}

export interface CheckboxControllerProps {
    readonly isOn?: boolean;
    readonly indeterminate?: boolean;
    readonly boxKey?: string;
    readonly markKey?: string;
    readonly labelKey?: string;
    readonly states?: Partial<Record<CheckboxVisualState, string>>;
    readonly transition?: CheckboxTransitionMode;
    readonly transitionDuration?: number;
    readonly zoomScale?: number;
    readonly boxTints?: Partial<Record<CheckboxVisualState, string>>;
    readonly markTints?: Partial<Record<CheckboxVisualState, string>>;
    readonly boxSprites?: Partial<Record<CheckboxVisualState, CheckboxImageSourceInput>>;
    readonly markSprites?: Partial<Record<CheckboxVisualState, CheckboxImageSourceInput>>;
    readonly markStyle?: CheckboxMarkStyle;
    readonly markColor?: string;
    readonly markSize?: number;
    readonly markWeight?: number;
    readonly labelPosition?: 'left' | 'right' | 'hidden';
    readonly labelColor?: string;
    readonly labelGap?: number;
    readonly boxSize?: number;
}

export interface CheckboxControllerState {
    checked: boolean;
    indeterminate: boolean;
    hovered: boolean;
    pressed: boolean;
    initialized: boolean;
    originalBoxSource: UIImageSource | null;
    originalMarkSource: UIImageSource | null;
    boxAnimation: UIAnimationHandle | null;
    previousBoxColor: string;
}

type CheckboxContext = WidgetControllerContext<
    Record<string, unknown>,
    CheckboxControllerState,
    UIRuntime
>;

const asBoolean = (value: unknown, fallback: boolean): boolean =>
    typeof value === 'boolean' ? value : fallback;

const asNumber = (value: unknown, fallback: number): number =>
    typeof value === 'number' && Number.isFinite(value) ? value : fallback;

const asString = (value: unknown): string =>
    typeof value === 'string' ? value.trim() : '';

const asRecord = (value: unknown): Record<string, unknown> =>
    value && typeof value === 'object' && !Array.isArray(value)
        ? (value as Record<string, unknown>)
        : {};

/**
 * Resolves the visual state from interaction state alone.
 * The runtime guards input dispatch — disabled widgets never receive pointer
 * or key events, so the controller only needs to track hover/press/checked.
 */
const resolveVisualState = (
    state: CheckboxControllerState,
): CheckboxVisualState => {
    if (state.checked || state.indeterminate) return 'checked';
    if (state.hovered || state.pressed) return 'hover';
    return 'normal';
};

const DEFAULT_STATES: Readonly<Record<CheckboxVisualState, string>> = Object.freeze({
    normal: '#334155ff',
    hover: '#475569ff',
    checked: '#0a74daff',
    disabled: '#1e293bff',
});

const TRANSITION_MODES: readonly CheckboxTransitionMode[] = Object.freeze(['color', 'tint', 'sprite']);

/**
 * Resolves the authored transition mode. Anything outside the supported set
 * (legacy documents carried `"animation"`) falls back to `'color'` explicitly
 * instead of relying on an `else` branch to absorb the typo silently.
 */
const resolveTransitionMode = (value: unknown): CheckboxTransitionMode => {
    const mode = asString(value);
    return (TRANSITION_MODES as readonly string[]).includes(mode)
        ? (mode as CheckboxTransitionMode)
        : 'color';
};

const isValidImageSource = (source: unknown): source is UIImageSource => {
    if (!source || typeof source !== 'object') return false;
    const src = source as Record<string, unknown>;
    if (src.kind === 'texture') return typeof src.resourceId === 'string' && !!src.resourceId;
    if (src.kind === 'material') return typeof src.materialId === 'string' && !!src.materialId;
    return false;
};

const toImageSource = (input: CheckboxImageSourceInput): UIImageSource | null => {
    if (input.kind === 'texture' && input.resourceId) {
        return { kind: 'texture', resourceId: input.resourceId, width: input.width, height: input.height };
    }
    if (input.kind === 'material' && input.materialId) {
        return {
            kind: 'material',
            materialId: input.materialId,
            textureBinding: input.textureBinding,
            width: input.width,
            height: input.height,
        };
    }
    return null;
};

const extractSourceInput = (record: Record<string, unknown>): CheckboxImageSourceInput | null => {
    const kind = record.kind;
    if (kind !== 'texture' && kind !== 'material') return null;
    const width = Number(record.width);
    const height = Number(record.height);
    if (!Number.isFinite(width) || !Number.isFinite(height)) return null;
    const result: Record<string, unknown> = { kind, width, height };
    if (typeof record.resourceId === 'string') result.resourceId = record.resourceId;
    if (typeof record.materialId === 'string') result.materialId = record.materialId;
    if (typeof record.textureBinding === 'string') result.textureBinding = record.textureBinding;
    return result as unknown as CheckboxImageSourceInput;
};

const captureOriginalSource = (runtime: UIRuntime, key: string): UIImageSource | null => {
    const widgetId = runtime.getBoundWidget(key);
    if (widgetId === null) return null;
    const imageInput = runtime.getWidgetImageInput(widgetId);
    return imageInput?.source ?? null;
};

/**
 * Applies tint-mode visuals to a single child widget.
 * Patches `image.tint` from the state-specific tint map.
 * No-op when the child has no image configured.
 */
const applyTintToChild = (
    runtime: UIRuntime,
    childId: WidgetId,
    visualState: CheckboxVisualState,
    tints: Record<string, unknown>,
): void => {
    if (!runtime.getWidgetImageInput(childId)) return;
    const tintValue = asString(tints[visualState]);
    runtime.updateWidget(childId, {
        image: { tint: (tintValue || '#ffffffff') } as Partial<WidgetImageInput>,
    });
};

/**
 * Applies sprite-mode visuals to a single child widget.
 * Swaps `image.source` from the state-specific sprite map, falling back to
 * the original source captured at mount time.
 * No-op when the child has no image configured and no original source.
 */
const applySpriteToChild = (
    runtime: UIRuntime,
    childId: WidgetId,
    visualState: CheckboxVisualState,
    sprites: Record<string, unknown>,
    originalSource: UIImageSource | null,
): void => {
    const stateEntry = sprites[visualState] as Record<string, unknown> | undefined;
    const sourceInput = stateEntry ? extractSourceInput(stateEntry) : null;
    const source = sourceInput ? toImageSource(sourceInput) : originalSource;
    if (isValidImageSource(source)) {
        runtime.updateWidget(childId, {
            image: { source } as Partial<WidgetImageInput>,
        });
    }
};

/**
 * Normalized stroke coordinates for checkbox mark styles in 0–1 space.
 * Derived from Editor SVG previews (14-unit viewBox):
 *   check = polyline M2.8 7.6 L5.7 10.4 L11.2 3.4 → normalized to 14-unit space
 *   cross = two diagonals
 *   dash  = horizontal line
 * Dot uses a filled circle (background + radius), not strokes.
 */
const CHECK_STROKE_POINTS: readonly (readonly [number, number])[] = Object.freeze([
    [0.2, 0.543],
    [0.407, 0.743],
    [0.8, 0.243],
]);

const CROSS_STROKE_POINTS: readonly (readonly [number, number])[] = Object.freeze([
    [0.25, 0.25],
    [0.75, 0.75],
]);

const CROSS_STROKE_POINTS_2: readonly (readonly [number, number])[] = Object.freeze([
    [0.75, 0.25],
    [0.25, 0.75],
]);

const DASH_STROKE_POINTS: readonly (readonly [number, number])[] = Object.freeze([
    [0.25, 0.5],
    [0.75, 0.5],
]);

/**
 * Resolves the stroke data for a given mark style. Returns null for 'dot' style
 * which uses background fill instead of strokes.
 */
const resolveMarkStrokes = (
    markStyle: CheckboxMarkStyle,
    markColor: string,
    markWeight: number,
): readonly WidgetStrokeData[] | null => {
    switch (markStyle) {
        case 'check':
            return [{ points: CHECK_STROKE_POINTS, color: markColor, weight: markWeight }];
        case 'cross':
            return [
                { points: CROSS_STROKE_POINTS, color: markColor, weight: markWeight },
                { points: CROSS_STROKE_POINTS_2, color: markColor, weight: markWeight },
            ];
        case 'dash':
            return [{ points: DASH_STROKE_POINTS, color: markColor, weight: markWeight }];
        case 'dot':
            // Dot uses background fill, not strokes.
            return null;
        default:
            return null;
    }
};

/**
 * Pushes the visual state onto the box, mark, and label child widgets.
 * Returns true once at least the box widget was reached.
 */
const applyVisuals = (context: CheckboxContext): boolean => {
    const props = context.props as CheckboxControllerProps;
    const runtime = context.runtime;
    const state = context.state;

    const visualState = resolveVisualState(state);
    const transition = resolveTransitionMode(props.transition);

    let applied = false;

    // ─── box ──────────────────────────────────────────────────────────────
    const boxKey = asString(props.boxKey);
    if (boxKey) {
        const box = runtime.getBoundWidget(boxKey);
        if (box !== null) {
            if (transition === 'tint') {
                const boxTints = asRecord(props.boxTints);
                applyTintToChild(runtime, box, visualState, boxTints);
            } else if (transition === 'sprite') {
                const boxSprites = asRecord(props.boxSprites);
                applySpriteToChild(runtime, box, visualState, boxSprites, state.originalBoxSource);
            } else {
                const states = asRecord(props.states);
                const isActive = state.checked || state.indeterminate;
                const bg = isActive
                    ? (asString(states.checked) || DEFAULT_STATES.checked)
                    : (visualState === 'hover'
                        ? (asString(states.hover) || DEFAULT_STATES.hover)
                        : (asString(states.normal) || DEFAULT_STATES.normal));

                const duration = asNumber(props.transitionDuration, 0);
                if (state.previousBoxColor === '') {
                    // First run: sentinel — apply directly without animation
                    // and record the initial color to avoid a mount flash.
                    if (state.boxAnimation) {
                        state.boxAnimation.cancel();
                        state.boxAnimation = null;
                    }
                    runtime.updateWidget(box, {
                        style: { background: bg as `#${string}` },
                    });
                    state.previousBoxColor = bg;
                } else if (duration > 0 && state.previousBoxColor !== bg) {
                    if (state.boxAnimation) {
                        state.boxAnimation.cancel();
                    }
                    // Read the widget's actual current color so that a
                    // cancelled mid-flight animation starts from where the
                    // widget truly is, not from a stale target.
                    const currentStyle = runtime.getWidgetStyleInput(box);
                    const fromColor = (asString(currentStyle?.background) || state.previousBoxColor) as `#${string}`;
                    state.boxAnimation = animateWidgetColor(
                        runtime,
                        box,
                        'style.background',
                        fromColor,
                        bg as `#${string}`,
                        duration,
                        Easing.easeOutQuad
                    );
                    state.previousBoxColor = bg;
                } else {
                    if (state.boxAnimation) {
                        state.boxAnimation.cancel();
                        state.boxAnimation = null;
                    }
                    runtime.updateWidget(box, {
                        style: { background: bg as `#${string}` },
                    });
                    state.previousBoxColor = bg;
                }
            }

            // Apply boxSize to box child layout dimensions.
            const boxSize = asNumber(props.boxSize, NaN);
            if (Number.isFinite(boxSize)) {
                runtime.updateWidget(box, {
                    layout: { width: boxSize, height: boxSize },
                });
            }

            applied = true;
        }
    }

    // ─── mark ─────────────────────────────────────────────────────────────
    const markKey = asString(props.markKey);
    if (markKey) {
        const mark = runtime.getBoundWidget(markKey);
        if (mark !== null) {
            const markVisible = state.checked || state.indeterminate;
            const markStyle = (asString(props.markStyle) || 'check') as CheckboxMarkStyle;
            const markColor = (asString(props.markColor) || '#ffffffff') as `#${string}`;
            const markWeight = asNumber(props.markWeight, 2);
            const markSize = asNumber(props.markSize, NaN);

            if (transition === 'tint') {
                const markTints = asRecord(props.markTints);
                if (markVisible) {
                    applyTintToChild(runtime, mark, visualState, markTints);
                    runtime.updateWidget(mark, { enabled: true });
                } else {
                    runtime.updateWidget(mark, { enabled: false });
                }
            } else if (transition === 'sprite') {
                const markSprites = asRecord(props.markSprites);
                if (markVisible) {
                    applySpriteToChild(runtime, mark, visualState, markSprites, state.originalMarkSource);
                    runtime.updateWidget(mark, { enabled: true });
                } else {
                    runtime.updateWidget(mark, { enabled: false });
                }
            } else {
                // Color transition mode: use strokes for mark styles (check/cross/dash)
                // or background fill for dot style. A base image authored on the mark
                // child replaces the default tick outright, so the procedural mark is
                // suppressed instead of painted on top of the user's artwork.
                const hasMarkImage = isValidImageSource(runtime.getWidgetImageInput(mark)?.source);
                const proceduralMark = markVisible && !hasMarkImage;
                const strokes = proceduralMark ? resolveMarkStrokes(markStyle, markColor, markWeight) : null;
                if (markStyle === 'dot') {
                    // Dot uses background fill with radius for circular shape.
                    runtime.updateWidget(mark, {
                        style: {
                            background: proceduralMark ? markColor : '#00000000',
                            radius: Number.isFinite(markSize) ? markSize * 0.5 : 7,
                            strokes: [],
                        },
                        enabled: markVisible,
                    });
                } else {
                    // Stroke-based marks: transparent background, strokes render the shape.
                    runtime.updateWidget(mark, {
                        style: {
                            background: '#00000000',
                            strokes: strokes ?? [],
                        },
                        enabled: markVisible,
                    });
                }
            }

            // Apply markSize to mark child layout dimensions. The mark is centered
            // inside the box by contract: boxSize/markSize are authoritative, so the
            // insets are dropped rather than zeroed — a zero inset still counts as
            // "present" and would make layoutAbsoluteChild resolve from the inset and
            // skip the anchor branch, pinning the mark to the box's top-left corner.
            // Older assets carry stale baked pixel insets and sometimes no anchor.
            if (Number.isFinite(markSize)) {
                runtime.updateWidget(mark, {
                    layout: {
                        width: markSize,
                        height: markSize,
                        position: 'absolute',
                        anchor: { x: 0.5, y: 0.5, maxX: 0.5, maxY: 0.5, pivotX: 0.5, pivotY: 0.5 },
                        inset: { left: undefined, top: undefined, right: undefined, bottom: undefined },
                    },
                });
            }

            applied = true;
        }
    }

    // ─── label ────────────────────────────────────────────────────────────
    const labelKey = asString(props.labelKey);
    if (labelKey) {
        const label = runtime.getBoundWidget(labelKey);
        if (label !== null) {
            const labelPosition = asString(props.labelPosition) || 'right';
            const isHidden = labelPosition === 'hidden';
            // Left/right ordering is authored in the document (child order), not
            // here; the runtime only owns visibility and the optional color override.
            const labelColor = asString(props.labelColor);
            runtime.updateWidget(label, {
                style: labelColor
                    ? { color: labelColor as `#${string}` }
                    : {},
                enabled: !isHidden,
            });
            applied = true;
        }
    }

    // ─── root layout: gap ───────────────────────────────────────────────────
    const labelGap = asNumber(props.labelGap, NaN);
    const rootUpdate: Record<string, unknown> = {};
    if (Number.isFinite(labelGap)) {
        rootUpdate.layout = { gap: labelGap };
    }
    if (Object.keys(rootUpdate).length > 0) {
        runtime.updateWidget(context.widget, rootUpdate);
    }

    return applied || (!boxKey && !markKey && !labelKey);
};

/**
 * Applies the zoom-scale press effect to the root widget.
 * Uses opacity reduction as a visual proxy for scale since the UI
 * animation system supports opacity interpolation.
 */
const applyZoomScale = (context: CheckboxContext, pressed: boolean): void => {
    const props = context.props as CheckboxControllerProps;
    const zoomScale = asNumber(props.zoomScale, NaN);
    if (!Number.isFinite(zoomScale) || zoomScale >= 1) {
        return;
    }
    // Map zoomScale (e.g. 0.9) to opacity reduction (e.g. 0.9 opacity).
    const opacity = pressed ? zoomScale : 1;
    context.runtime.updateWidget(context.widget, {
        style: { opacity },
    });
};

export const checkboxToggleController: WidgetController<
    typeof CHECKBOX_TOGGLE_CONTROLLER_TYPE,
    Record<string, unknown>,
    CheckboxControllerState,
    UIRuntime,
    unknown
> = {
    type: CHECKBOX_TOGGLE_CONTROLLER_TYPE,
    createState: (props) => {
        const checkboxProps = props as CheckboxControllerProps;
        return {
            checked: asBoolean(checkboxProps.isOn, false),
            indeterminate: asBoolean(checkboxProps.indeterminate, false),
            hovered: false,
            pressed: false,
            initialized: false,
            originalBoxSource: null,
            originalMarkSource: null,
            boxAnimation: null,
            previousBoxColor: '',
        };
    },
    mount: (context) => {
        const typed = context as CheckboxContext;
        const props = typed.props as CheckboxControllerProps;
        const runtime = typed.runtime;

        const boxKey = asString(props.boxKey);
        if (boxKey) {
            typed.state.originalBoxSource = captureOriginalSource(runtime, boxKey);
        }
        const markKey = asString(props.markKey);
        if (markKey) {
            typed.state.originalMarkSource = captureOriginalSource(runtime, markKey);
        }

        typed.state.initialized = applyVisuals(typed);
    },
    update: (context, previousProps) => {
        const typed = context as CheckboxContext;
        const props = typed.props as CheckboxControllerProps;
        const previous = previousProps as CheckboxControllerProps;

        if (
            props.isOn !== previous.isOn ||
            props.indeterminate !== previous.indeterminate ||
            props.states !== previous.states ||
            props.transition !== previous.transition ||
            props.transitionDuration !== previous.transitionDuration ||
            props.zoomScale !== previous.zoomScale ||
            props.markColor !== previous.markColor ||
            props.markStyle !== previous.markStyle ||
            props.markSize !== previous.markSize ||
            props.markWeight !== previous.markWeight ||
            props.boxSize !== previous.boxSize ||
            props.boxKey !== previous.boxKey ||
            props.markKey !== previous.markKey ||
            props.labelKey !== previous.labelKey ||
            props.labelPosition !== previous.labelPosition ||
            props.labelColor !== previous.labelColor ||
            props.labelGap !== previous.labelGap ||
            props.boxTints !== previous.boxTints ||
            props.markTints !== previous.markTints ||
            props.boxSprites !== previous.boxSprites ||
            props.markSprites !== previous.markSprites
        ) {
            typed.state.checked = asBoolean(props.isOn, typed.state.checked);
            typed.state.indeterminate = asBoolean(props.indeterminate, typed.state.indeterminate);
            applyVisuals(typed);
        }
    },
    input: (event: Readonly<UIInputEvent>, context) => {
        const typed = context as CheckboxContext;
        const state = typed.state;
        if (!state) return false;

        if (event.type === 'pointer') {
            switch (event.phase) {
                case 'enter':
                    state.hovered = true;
                    applyVisuals(typed);
                    return false;
                case 'leave':
                    state.hovered = false;
                    state.pressed = false;
                    applyVisuals(typed);
                    return false;
                case 'down':
                    state.pressed = true;
                    applyVisuals(typed);
                    applyZoomScale(typed, true);
                    return true;
                case 'up': {
                    const wasPressed = state.pressed;
                    state.pressed = false;
                    if (wasPressed) {
                        if (state.indeterminate) {
                            state.indeterminate = false;
                            state.checked = true;
                        } else {
                            state.checked = !state.checked;
                        }
                    }
                    applyVisuals(typed);
                    applyZoomScale(typed, false);
                    return true;
                }
                default:
                    return false;
            }
        }

        if (event.type === 'key' && event.phase === 'down') {
            if (event.key === 'Enter' || event.key === ' ') {
                if (state.indeterminate) {
                    state.indeterminate = false;
                    state.checked = true;
                } else {
                    state.checked = !state.checked;
                }
                applyVisuals(typed);
                return true;
            }
        }

        return false;
    },
    disposeState: (state) => {
        if (state.boxAnimation) {
            state.boxAnimation.cancel();
        }
    },
};

/**
 * Reads the live checked state of a checkbox widget driven by `checkbox-toggle`.
 * Returns null when the widget has no checkbox state.
 */
export const getCheckboxChecked = (
    runtime: UIRuntime,
    widget: WidgetId
): boolean | null => {
    const state = runtime.getWidgetState(widget) as CheckboxControllerState | null;
    return state && typeof state.checked === 'boolean' ? state.checked : null;
};
