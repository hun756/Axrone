import type { UIRuntime } from '../runtime';
import type { UIInputEvent, WidgetId } from '../types';
import type { WidgetController, WidgetControllerContext } from '../widget';

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
 *     transition: 'color' | 'sprite' | 'scale' | 'animation',
 *     transitionDuration: number,
 *     zoomScale: number,
 *     markStyle: 'check' | 'cross' | 'dot' | 'dash',
 *     markColor: string,
 *     markSize: number,
 *     markWeight: number,
 *     labelPosition: 'left' | 'right' | 'hidden',
 *     labelGap: number,
 *   }
 *
 * Child widgets are resolved through the asset's binding table.
 */
export const CHECKBOX_TOGGLE_CONTROLLER_TYPE = 'checkbox-toggle';

export type CheckboxVisualState = 'normal' | 'hover' | 'checked' | 'disabled';
export type CheckboxMarkStyle = 'check' | 'cross' | 'dot' | 'dash';
export type CheckboxTransitionMode = 'color' | 'sprite' | 'scale' | 'animation';

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
    readonly markStyle?: CheckboxMarkStyle;
    readonly markColor?: string;
    readonly markSize?: number;
    readonly markWeight?: number;
    readonly labelPosition?: 'left' | 'right' | 'hidden';
    readonly labelGap?: number;
    readonly boxSize?: number;
}

export interface CheckboxControllerState {
    checked: boolean;
    indeterminate: boolean;
    hovered: boolean;
    pressed: boolean;
    initialized: boolean;
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

const resolveVisualState = (
    state: CheckboxControllerState,
    enabled: boolean
): CheckboxVisualState => {
    if (!enabled) return 'disabled';
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

/**
 * Pushes the visual state onto the box and mark child widgets.
 * Returns true once at least the box widget was reached.
 */
const applyVisuals = (context: CheckboxContext): boolean => {
    const props = context.props as CheckboxControllerProps;
    const runtime = context.runtime;
    const state = context.state;
    const enabled = context.widget != null;

    const visualState = resolveVisualState(state, enabled);
    const states = asRecord(props.states);
    const stateColor =
        asString(states[visualState]) ||
        DEFAULT_STATES[visualState];

    const transition: CheckboxTransitionMode =
        (props.transition as CheckboxTransitionMode) ?? 'color';
    const duration = asNumber(props.transitionDuration, 0.15);
    const zoom = asNumber(props.zoomScale, 0.9);

    let applied = false;

    const boxKey = asString(props.boxKey);
    if (boxKey) {
        const box = runtime.getBoundWidget(boxKey);
        if (box !== null) {
            const isActive = state.checked || state.indeterminate;
            const bg = transition === 'color'
                ? (isActive ? stateColor : (visualState === 'hover' ? asString(states.hover) || DEFAULT_STATES.hover : asString(states.normal) || DEFAULT_STATES.normal))
                : (isActive ? asString(states.checked) || DEFAULT_STATES.checked : asString(states.normal) || DEFAULT_STATES.normal);

            const scaleValue = (transition === 'scale' || transition === 'animation') && state.pressed
                ? zoom
                : 1;

            runtime.updateWidget(box, {
                style: {
                    background: bg,
                    opacity: visualState === 'disabled' ? 0.6 : 1,
                },
                layout: {
                    scale: scaleValue,
                },
            });
            applied = true;
        }
    }

    const markKey = asString(props.markKey);
    if (markKey) {
        const mark = runtime.getBoundWidget(markKey);
        if (mark !== null) {
            const markVisible = state.checked || state.indeterminate;
            const markColor = asString(props.markColor) || '#ffffffff';
            runtime.updateWidget(mark, {
                style: {
                    background: markVisible ? markColor : '#00000000',
                },
                enabled: markVisible,
            });
            applied = true;
        }
    }

    return applied || (!boxKey && !markKey);
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
        };
    },
    mount: (context) => {
        const typed = context as CheckboxContext;
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
            props.boxKey !== previous.boxKey ||
            props.markKey !== previous.markKey
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
