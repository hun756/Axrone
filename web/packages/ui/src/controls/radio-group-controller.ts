import type { UIRuntime } from '../runtime';
import type { UIInputEvent, WidgetId } from '../types';
import type { WidgetController, WidgetControllerContext } from '../widget';
import { isPointInside } from './internals';

/**
 * Declarative radio-group controller for `.ui.json` authored radio groups.
 *
 * The authored widget acts as the interaction surface and its `props` name the
 * child widgets that visualise selection state:
 *
 *   props: {
 *     selectedIndex: number,
 *     itemCount: number,
 *     dotPrefix: string,      // e.g. 'radio-' resolves 'radio-0-dot', 'radio-1-dot', ...
 *     circlePrefix: string,   // e.g. 'radio-' resolves 'radio-0-circle', 'radio-1-circle', ...
 *     selectedColor: string,
 *     unselectedColor: string,
 *   }
 *
 * For each index i, the controller resolves:
 *   - dot widget:    `${dotPrefix}${i}-dot`
 *   - circle widget: `${circlePrefix}${i}-circle`
 *
 * Child widgets are resolved through the asset's binding table.
 */
export const RADIO_GROUP_CONTROLLER_TYPE = 'radio-group';

export interface RadioGroupControllerProps {
    readonly selectedIndex?: number;
    readonly itemCount?: number;
    readonly dotPrefix?: string;
    readonly circlePrefix?: string;
    readonly selectedColor?: string;
    readonly unselectedColor?: string;
}

export interface RadioGroupControllerState {
    selectedIndex: number;
    hoveredIndex: number;
}

type RadioGroupContext = WidgetControllerContext<
    Record<string, unknown>,
    RadioGroupControllerState,
    UIRuntime
>;

const asNumber = (value: unknown, fallback: number): number =>
    typeof value === 'number' && Number.isFinite(value) ? value : fallback;

const asString = (value: unknown): string =>
    typeof value === 'string' ? value.trim() : '';

const DEFAULT_SELECTED_COLOR = '#0a74daff';
const DEFAULT_UNSELECTED_COLOR = '#475569ff';

/** Resolves the dot widget key for a given index. */
const resolveDotKey = (props: RadioGroupControllerProps, index: number): string =>
    `${asString(props.dotPrefix)}${index}-dot`;

/** Resolves the circle widget key for a given index. */
const resolveCircleKey = (props: RadioGroupControllerProps, index: number): string =>
    `${asString(props.circlePrefix)}${index}-circle`;

/**
 * Pushes the visual state onto all radio item widgets.
 * For each item: shows/hides the dot and sets the circle border color.
 * Returns true once at least one visual was applied.
 */
const applyVisuals = (context: RadioGroupContext): boolean => {
    const props = context.props as RadioGroupControllerProps;
    const runtime = context.runtime;
    const state = context.state;

    const count = Math.max(0, asNumber(props.itemCount, 0) | 0);
    const selectedColor = (asString(props.selectedColor) || DEFAULT_SELECTED_COLOR) as `#${string}`;
    const unselectedColor = (asString(props.unselectedColor) || DEFAULT_UNSELECTED_COLOR) as `#${string}`;

    let applied = false;

    for (let i = 0; i < count; i++) {
        const isSelected = i === state.selectedIndex;

        // --- dot (inner indicator) ---
        const dotKey = resolveDotKey(props, i);
        const dot = runtime.getBoundWidget(dotKey);
        if (dot !== null) {
            runtime.updateWidget(dot, { enabled: isSelected });
            applied = true;
        }

        // --- circle (border ring) ---
        const circleKey = resolveCircleKey(props, i);
        const circle = runtime.getBoundWidget(circleKey);
        if (circle !== null) {
            runtime.updateWidget(circle, {
                style: { background: isSelected ? selectedColor : unselectedColor },
            });
            applied = true;
        }
    }

    return applied || count === 0;
};

/**
 * Determines which radio item the pointer is over by hit-testing each item's
 * circle widget layout box. Returns -1 when no item contains the point.
 */
const hitTestItem = (context: RadioGroupContext, x: number, y: number): number => {
    const props = context.props as RadioGroupControllerProps;
    const runtime = context.runtime;
    const count = Math.max(0, asNumber(props.itemCount, 0) | 0);

    for (let i = 0; i < count; i++) {
        const circleKey = resolveCircleKey(props, i);
        const circle = runtime.getBoundWidget(circleKey);
        if (circle !== null && isPointInside(runtime, circle, x, y)) {
            return i;
        }
    }
    return -1;
};

export const radioGroupController: WidgetController<
    typeof RADIO_GROUP_CONTROLLER_TYPE,
    Record<string, unknown>,
    RadioGroupControllerState,
    UIRuntime,
    unknown
> = {
    type: RADIO_GROUP_CONTROLLER_TYPE,
    createState: (props) => {
        const radioProps = props as RadioGroupControllerProps;
        const count = Math.max(0, asNumber(radioProps.itemCount, 0) | 0);
        const rawIndex = asNumber(radioProps.selectedIndex, 0);
        const clampedIndex = count > 0 ? Math.min(Math.max(rawIndex, 0), count - 1) : 0;
        return {
            selectedIndex: clampedIndex,
            hoveredIndex: -1,
        };
    },
    mount: (context) => {
        const typed = context as RadioGroupContext;
        applyVisuals(typed);
    },
    update: (context, previousProps) => {
        const typed = context as RadioGroupContext;
        const props = typed.props as RadioGroupControllerProps;
        const previous = previousProps as RadioGroupControllerProps;

        if (
            props.selectedIndex !== previous.selectedIndex ||
            props.itemCount !== previous.itemCount ||
            props.dotPrefix !== previous.dotPrefix ||
            props.circlePrefix !== previous.circlePrefix ||
            props.selectedColor !== previous.selectedColor ||
            props.unselectedColor !== previous.unselectedColor
        ) {
            const count = Math.max(0, asNumber(props.itemCount, 0) | 0);
            const authored = asNumber(props.selectedIndex, typed.state.selectedIndex);
            typed.state.selectedIndex = count > 0 ? Math.min(Math.max(authored, 0), count - 1) : 0;
            applyVisuals(typed);
        }
    },
    input: (event: Readonly<UIInputEvent>, context) => {
        const typed = context as RadioGroupContext;
        const state = typed.state;
        if (!state) return false;

        const props = typed.props as RadioGroupControllerProps;
        const count = Math.max(0, asNumber(props.itemCount, 0) | 0);
        if (count === 0) return false;

        if (event.type === 'pointer') {
            switch (event.phase) {
                case 'down': {
                    const hit = hitTestItem(typed, event.x, event.y);
                    if (hit >= 0) {
                        state.selectedIndex = hit;
                        applyVisuals(typed);
                        return true;
                    }
                    return false;
                }
                case 'move': {
                    const hit = hitTestItem(typed, event.x, event.y);
                    if (hit !== state.hoveredIndex) {
                        state.hoveredIndex = hit;
                    }
                    return false;
                }
                case 'leave':
                    state.hoveredIndex = -1;
                    return false;
                default:
                    return false;
            }
        }

        if (event.type === 'key' && event.phase === 'down') {
            switch (event.key) {
                case 'ArrowDown':
                case 'ArrowRight': {
                    const next = state.selectedIndex + 1;
                    state.selectedIndex = next < count ? next : 0;
                    applyVisuals(typed);
                    return true;
                }
                case 'ArrowUp':
                case 'ArrowLeft': {
                    const prev = state.selectedIndex - 1;
                    state.selectedIndex = prev >= 0 ? prev : count - 1;
                    applyVisuals(typed);
                    return true;
                }
                default:
                    return false;
            }
        }

        return false;
    },
};

/**
 * Reads the live selected index of a radio group driven by `radio-group`.
 * Returns null when the widget has no radio-group state.
 */
export const getRadioGroupSelectedIndex = (
    runtime: UIRuntime,
    widget: WidgetId
): number | null => {
    const state = runtime.getWidgetState(widget) as RadioGroupControllerState | null;
    return state && typeof state.selectedIndex === 'number' ? state.selectedIndex : null;
};
