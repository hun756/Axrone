import type { UIRuntime } from '../runtime';
import type { UIInputEvent, WidgetId } from '../types';
import type { WidgetController, WidgetControllerContext } from '../widget';
import { asString, asNumber, asBoolean, asRecord } from './internals';

/**
 * Declarative toggle-switch controller for `.ui.json` authored toggles.
 *
 * The authored widget acts as the interaction surface and its `props` name the
 * child widgets that visualise the on/off state:
 *
 *   props: {
 *     isOn: boolean,
 *     trackKey,   // the track background widget
 *     thumbKey,   // the thumb circle widget
 *     states: { on: '#0a74daff', off: '#334155ff' },
 *     thumbOnAnchor: 0.85,   // thumb X anchor when on (0-1)
 *     thumbOffAnchor: 0.15,  // thumb X anchor when off (0-1)
 *   }
 *
 * Child widgets are resolved through the asset's binding table.
 */
export const TOGGLE_SWITCH_CONTROLLER_TYPE = 'toggle-switch';

export interface ToggleControllerProps {
    readonly isOn?: boolean;
    readonly trackKey?: string;
    readonly thumbKey?: string;
    readonly states?: {
        readonly on?: string;
        readonly off?: string;
    };
    readonly thumbOnAnchor?: number;
    readonly thumbOffAnchor?: number;
}

export interface ToggleControllerState {
    on: boolean;
    hovered: boolean;
    pressed: boolean;
}

type ToggleContext = WidgetControllerContext<
    Record<string, unknown>,
    ToggleControllerState,
    UIRuntime
>;

const DEFAULT_ON_COLOR = '#0a74daff';
const DEFAULT_OFF_COLOR = '#334155ff';

/**
 * Pushes the visual state onto the track and thumb child widgets.
 * Track background color reflects on/off state.
 * Thumb position is set via anchor based on on/off state.
 * Returns true once at least one visual was applied.
 */
const applyVisuals = (context: ToggleContext): boolean => {
    const props = context.props as ToggleControllerProps;
    const runtime = context.runtime;
    const state = context.state;

    const states = asRecord(props.states);
    const onColor = (asString(states.on) || DEFAULT_ON_COLOR) as `#${string}`;
    const offColor = (asString(states.off) || DEFAULT_OFF_COLOR) as `#${string}`;
    const bgColor = state.on ? onColor : offColor;

    const thumbOnAnchor = asNumber(props.thumbOnAnchor, 0.85);
    const thumbOffAnchor = asNumber(props.thumbOffAnchor, 0.15);
    const anchor = state.on ? thumbOnAnchor : thumbOffAnchor;

    let applied = false;

    // --- track ---
    const trackKey = asString(props.trackKey);
    if (trackKey) {
        const track = runtime.getBoundWidget(trackKey);
        if (track !== null) {
            runtime.updateWidget(track, {
                style: { background: bgColor },
            });
            applied = true;
        }
    }

    // --- thumb ---
    const thumbKey = asString(props.thumbKey);
    if (thumbKey) {
        const thumb = runtime.getBoundWidget(thumbKey);
        if (thumb !== null) {
            runtime.updateWidget(thumb, {
                layout: {
                    position: 'absolute',
                    anchor: { x: anchor, y: 0.5, maxX: anchor, maxY: 0.5, pivotX: 0.5, pivotY: 0.5 },
                },
            });
            applied = true;
        }
    }

    return applied || (!trackKey && !thumbKey);
};

export const toggleSwitchController: WidgetController<
    typeof TOGGLE_SWITCH_CONTROLLER_TYPE,
    Record<string, unknown>,
    ToggleControllerState,
    UIRuntime,
    unknown
> = {
    type: TOGGLE_SWITCH_CONTROLLER_TYPE,
    createState: (props) => {
        const toggleProps = props as ToggleControllerProps;
        return {
            on: asBoolean(toggleProps.isOn, false),
            hovered: false,
            pressed: false,
        };
    },
    mount: (context) => {
        const typed = context as ToggleContext;
        applyVisuals(typed);
    },
    update: (context, previousProps) => {
        const typed = context as ToggleContext;
        const props = typed.props as ToggleControllerProps;
        const previous = previousProps as ToggleControllerProps;

        if (
            props.isOn !== previous.isOn ||
            props.states !== previous.states ||
            props.trackKey !== previous.trackKey ||
            props.thumbKey !== previous.thumbKey ||
            props.thumbOnAnchor !== previous.thumbOnAnchor ||
            props.thumbOffAnchor !== previous.thumbOffAnchor
        ) {
            typed.state.on = asBoolean(props.isOn, typed.state.on);
            applyVisuals(typed);
        }
    },
    input: (event: Readonly<UIInputEvent>, context) => {
        const typed = context as ToggleContext;
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
                        state.on = !state.on;
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
                state.on = !state.on;
                applyVisuals(typed);
                return true;
            }
        }

        return false;
    },
};

/**
 * Reads the live on/off state of a toggle widget driven by `toggle-switch`.
 * Returns null when the widget has no toggle state.
 */
export const getToggleIsOn = (
    runtime: UIRuntime,
    widget: WidgetId
): boolean | null => {
    const state = runtime.getWidgetState(widget) as ToggleControllerState | null;
    return state && typeof state.on === 'boolean' ? state.on : null;
};
