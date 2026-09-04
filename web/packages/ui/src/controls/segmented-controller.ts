import type { UIRuntime } from '../runtime';
import type { UIInputEvent, WidgetId } from '../types';
import type { WidgetController, WidgetControllerContext } from '../widget';
import { isPointInside, asString, asNumber } from './internals';

/**
 * Declarative segmented-control controller for `.ui.json` authored tab selectors.
 *
 * The authored widget acts as the interaction surface and its `props` name the
 * child widgets that visualise segment selection:
 *
 *   props: {
 *     selectedIndex: number,
 *     segmentCount: number,
 *     segmentPrefix: string,       // e.g. 'seg-' resolves 'seg-0', 'seg-1', ...
 *     selectedBackground: string,
 *     unselectedBackground: string,
 *   }
 *
 * For each index i, the controller resolves:
 *   - segment widget: `${segmentPrefix}${i}`
 *
 * Child widgets are resolved through the asset's binding table.
 */
export const SEGMENTED_CONTROL_CONTROLLER_TYPE = 'segmented-control';

export interface SegmentedControllerProps {
    readonly selectedIndex?: number;
    readonly segmentCount?: number;
    readonly segmentPrefix?: string;
    readonly selectedBackground?: string;
    readonly unselectedBackground?: string;
}

export interface SegmentedControllerState {
    selectedIndex: number;
    hoveredIndex: number;
}

type SegmentedContext = WidgetControllerContext<
    Record<string, unknown>,
    SegmentedControllerState,
    UIRuntime
>;

const DEFAULT_SELECTED_BACKGROUND = '#334155ff';
const DEFAULT_UNSELECTED_BACKGROUND = '#00000000';

/** Resolves the segment widget key for a given index. */
const resolveSegmentKey = (props: SegmentedControllerProps, index: number): string =>
    `${asString(props.segmentPrefix)}${index}`;

/**
 * Pushes the visual state onto all segment widgets.
 * Selected segment gets the selected background; others get the unselected background.
 * Returns true once at least one visual was applied.
 */
const applyVisuals = (context: SegmentedContext): boolean => {
    const props = context.props as SegmentedControllerProps;
    const runtime = context.runtime;
    const state = context.state;

    const count = Math.max(0, asNumber(props.segmentCount, 0) | 0);
    const selectedBg = (asString(props.selectedBackground) || DEFAULT_SELECTED_BACKGROUND) as `#${string}`;
    const unselectedBg = (asString(props.unselectedBackground) || DEFAULT_UNSELECTED_BACKGROUND) as `#${string}`;

    let applied = false;

    for (let i = 0; i < count; i++) {
        const segmentKey = resolveSegmentKey(props, i);
        const segment = runtime.getBoundWidget(segmentKey);
        if (segment !== null) {
            const color = i === state.selectedIndex ? selectedBg : unselectedBg;
            runtime.updateWidget(segment, {
                style: { background: color },
            });
            applied = true;
        }
    }

    return applied || count === 0;
};

/**
 * Determines which segment the pointer is over by hit-testing each segment
 * widget's layout box. Returns -1 when no segment contains the point.
 */
const hitTestSegment = (context: SegmentedContext, x: number, y: number): number => {
    const props = context.props as SegmentedControllerProps;
    const runtime = context.runtime;
    const count = Math.max(0, asNumber(props.segmentCount, 0) | 0);

    for (let i = 0; i < count; i++) {
        const segmentKey = resolveSegmentKey(props, i);
        const segment = runtime.getBoundWidget(segmentKey);
        if (segment !== null && isPointInside(runtime, segment, x, y)) {
            return i;
        }
    }
    return -1;
};

export const segmentedController: WidgetController<
    typeof SEGMENTED_CONTROL_CONTROLLER_TYPE,
    Record<string, unknown>,
    SegmentedControllerState,
    UIRuntime,
    unknown
> = {
    type: SEGMENTED_CONTROL_CONTROLLER_TYPE,
    createState: (props) => {
        const segProps = props as SegmentedControllerProps;
        const count = Math.max(0, asNumber(segProps.segmentCount, 0) | 0);
        const rawIndex = asNumber(segProps.selectedIndex, 0);
        const clampedIndex = count > 0 ? Math.min(Math.max(rawIndex, 0), count - 1) : 0;
        return {
            selectedIndex: clampedIndex,
            hoveredIndex: -1,
        };
    },
    mount: (context) => {
        const typed = context as SegmentedContext;
        applyVisuals(typed);
    },
    update: (context, previousProps) => {
        const typed = context as SegmentedContext;
        const props = typed.props as SegmentedControllerProps;
        const previous = previousProps as SegmentedControllerProps;

        if (
            props.selectedIndex !== previous.selectedIndex ||
            props.segmentCount !== previous.segmentCount ||
            props.segmentPrefix !== previous.segmentPrefix ||
            props.selectedBackground !== previous.selectedBackground ||
            props.unselectedBackground !== previous.unselectedBackground
        ) {
            const count = Math.max(0, asNumber(props.segmentCount, 0) | 0);
            const authored = asNumber(props.selectedIndex, typed.state.selectedIndex);
            typed.state.selectedIndex = count > 0 ? Math.min(Math.max(authored, 0), count - 1) : 0;
            applyVisuals(typed);
        }
    },
    input: (event: Readonly<UIInputEvent>, context) => {
        const typed = context as SegmentedContext;
        const state = typed.state;
        if (!state) return false;

        const props = typed.props as SegmentedControllerProps;
        const count = Math.max(0, asNumber(props.segmentCount, 0) | 0);
        if (count === 0) return false;

        if (event.type === 'pointer') {
            switch (event.phase) {
                case 'down': {
                    const hit = hitTestSegment(typed, event.x, event.y);
                    if (hit >= 0) {
                        state.selectedIndex = hit;
                        applyVisuals(typed);
                        return true;
                    }
                    return false;
                }
                case 'move': {
                    const hit = hitTestSegment(typed, event.x, event.y);
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
                case 'ArrowRight': {
                    const next = state.selectedIndex + 1;
                    state.selectedIndex = next < count ? next : 0;
                    applyVisuals(typed);
                    return true;
                }
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
 * Reads the live selected index of a segmented control driven by `segmented-control`.
 * Returns null when the widget has no segmented-control state.
 */
export const getSegmentedSelectedIndex = (
    runtime: UIRuntime,
    widget: WidgetId
): number | null => {
    const state = runtime.getWidgetState(widget) as SegmentedControllerState | null;
    return state && typeof state.selectedIndex === 'number' ? state.selectedIndex : null;
};
