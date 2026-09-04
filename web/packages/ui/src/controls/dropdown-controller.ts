import type { UIRuntime } from '../runtime';
import type { UIInputEvent, WidgetId } from '../types';
import type { WidgetController, WidgetControllerContext } from '../widget';
import { clamp } from '@axrone/numeric';

/**
 * Declarative dropdown-select controller for `.ui.json` authored dropdowns.
 *
 * The authored widget acts as the interaction surface and its `props` name the
 * child widgets that visualise the selection and popup list:
 *
 *   props: {
 *     options,           // display labels for each option
 *     selectedIndex,     // currently selected index (0-based)
 *     triggerKey,        // named binding -> text widget showing current selection
 *     popupKey,          // named binding -> popup panel widget (toggled visible/hidden)
 *     itemContainerKey,  // named binding -> container holding item widgets
 *     chevronKey,        // named binding -> chevron indicator (rotates when open)
 *     placeholder,       // text when no selection
 *   }
 *
 * Child widgets are resolved through the asset's binding table, so the authored
 * keys are the contract; the controller never assumes a tree shape.
 */
export const DROPDOWN_SELECT_CONTROLLER_TYPE = 'dropdown-select';

export interface DropdownControllerProps {
    readonly options?: readonly string[];
    readonly selectedIndex?: number;
    readonly triggerKey?: string;
    readonly popupKey?: string;
    readonly itemContainerKey?: string;
    readonly chevronKey?: string;
    readonly placeholder?: string;
}

export interface DropdownControllerState {
    selectedIndex: number;
    isOpen: boolean;
    hoveredIndex: number;
    /** Cached container widget resolved from itemContainerKey. */
    cachedContainer: WidgetId | null;
    /** Cached direct-child item widgets inside the container. */
    cachedItems: WidgetId[];
}

type DropdownContext = WidgetControllerContext<
    Record<string, unknown>,
    DropdownControllerState,
    UIRuntime
>;

const asNumber = (value: unknown, fallback: number): number =>
    typeof value === 'number' && Number.isFinite(value) ? value : fallback;

const asString = (value: unknown): string =>
    typeof value === 'string' ? value.trim() : '';

const asArray = (value: unknown): readonly string[] =>
    Array.isArray(value) ? value.filter((v): v is string => typeof v === 'string') : [];

/**
 * Pushes the currently selected option label onto the trigger text widget.
 * Falls back to the placeholder when no valid selection exists.
 */
const applySelection = (context: DropdownContext): void => {
    const props = context.props as DropdownControllerProps;
    const runtime = context.runtime;
    const state = context.state;

    const triggerKey = asString(props.triggerKey);
    if (!triggerKey) return;

    const trigger = runtime.getBoundWidget(triggerKey);
    if (trigger === null) return;

    const options = asArray(props.options);
    const index = state.selectedIndex;
    const hasValidSelection = index >= 0 && index < options.length;
    const text = hasValidSelection
        ? options[index]
        : (asString(props.placeholder) || '');

    runtime.updateWidget(trigger, {
        text: { value: text },
    });
};

/** Shows or hides the popup panel widget based on the current open state. */
const applyPopupVisibility = (context: DropdownContext): void => {
    const props = context.props as DropdownControllerProps;
    const runtime = context.runtime;
    const state = context.state;

    const popupKey = asString(props.popupKey);
    if (!popupKey) return;

    const popup = runtime.getBoundWidget(popupKey);
    if (popup === null) return;

    runtime.updateWidget(popup, { enabled: state.isOpen });
};

/**
 * Resolves and caches the item widgets inside the item container.
 * Uses collectSubtreeWidgetIds and filters to direct children by checking
 * which widgets have a layout box that fits within the container bounds
 * and are not the container itself. Results are cached in state to avoid
 * per-frame allocations.
 */
const resolveItems = (context: DropdownContext): readonly WidgetId[] => {
    const props = context.props as DropdownControllerProps;
    const runtime = context.runtime;
    const state = context.state;

    const itemContainerKey = asString(props.itemContainerKey);
    if (!itemContainerKey) return state.cachedItems;

    const container = runtime.getBoundWidget(itemContainerKey);
    if (container === null) return state.cachedItems;

    // Return cached list when the container hasn't changed.
    if (state.cachedContainer === container && state.cachedItems.length > 0) {
        return state.cachedItems;
    }

    const containerBox = runtime.getLayoutBox(container);
    const subtree = runtime.collectSubtreeWidgetIds(container);

    // Direct children are subtree members (excluding the container itself)
    // whose layout box is not identical to the container's own box. This
    // filters out the container root and any deeply nested sub-containers.
    const items: WidgetId[] = [];
    for (let i = 0; i < subtree.length; i++) {
        const candidate = subtree[i];
        if (candidate === container) continue;
        const box = runtime.getLayoutBox(candidate);
        // Skip widgets whose box matches the container exactly (the container
        // itself or overlay children that fill the full area).
        if (box.x === containerBox.x && box.y === containerBox.y &&
            box.width === containerBox.width && box.height === containerBox.height) {
            continue;
        }
        items.push(candidate);
    }

    state.cachedContainer = container;
    state.cachedItems = items;
    return items;
};

/**
 * Updates item background colours to reflect the currently hovered index.
 * The hovered item receives a highlight; all others are reset to transparent.
 */
const applyHoverHighlight = (context: DropdownContext): void => {
    const runtime = context.runtime;
    const state = context.state;
    const items = resolveItems(context);

    for (let i = 0; i < items.length; i++) {
        const isHovered = i === state.hoveredIndex;
        runtime.updateWidget(items[i], {
            style: { background: isHovered ? '#334155ff' : '#00000000' },
        });
    }
};

/** Updates the chevron indicator colour to reflect open/closed state. */
const applyChevron = (context: DropdownContext): void => {
    const props = context.props as DropdownControllerProps;
    const runtime = context.runtime;
    const state = context.state;

    const chevronKey = asString(props.chevronKey);
    if (!chevronKey) return;

    const chevron = runtime.getBoundWidget(chevronKey);
    if (chevron === null) return;

    runtime.updateWidget(chevron, {
        style: { color: state.isOpen ? '#e2e8f0ff' : '#94a3b8ff' },
    });
};

/** Sets the open state and pushes all dependent visuals in one pass. */
const setOpen = (context: DropdownContext, isOpen: boolean): void => {
    const state = context.state;
    if (state.isOpen === isOpen) return;
    state.isOpen = isOpen;
    if (!isOpen) {
        state.hoveredIndex = -1;
    }
    applyPopupVisibility(context);
    applyChevron(context);
    if (isOpen) {
        applyHoverHighlight(context);
    }
};

/**
 * Selects the option at the given index, updates the trigger text, and closes
 * the popup. The index is clamped to the valid range before applying.
 */
const selectIndex = (context: DropdownContext, index: number): void => {
    const props = context.props as DropdownControllerProps;
    const options = asArray(props.options);
    if (options.length === 0) return;

    const clamped = clamp(index, 0, options.length - 1);
    context.state.selectedIndex = clamped;
    applySelection(context);
    setOpen(context, false);
};

export const dropdownController: WidgetController<
    typeof DROPDOWN_SELECT_CONTROLLER_TYPE,
    Record<string, unknown>,
    DropdownControllerState,
    UIRuntime,
    unknown
> = {
    type: DROPDOWN_SELECT_CONTROLLER_TYPE,
    createState: (props) => {
        const dropdownProps = props as DropdownControllerProps;
        const options = asArray(dropdownProps.options);
        const authored = asNumber(dropdownProps.selectedIndex, -1);
        const selectedIndex = options.length > 0
            ? clamp(authored, 0, options.length - 1)
            : -1;
        return {
            selectedIndex,
            isOpen: false,
            hoveredIndex: -1,
            cachedContainer: null,
            cachedItems: [],
        };
    },
    mount: (context) => {
        const typed = context as DropdownContext;
        // Apply initial selection and hide the popup once bindings are ready.
        applySelection(typed);
        applyPopupVisibility(typed);
        applyChevron(typed);
    },
    update: (context, previousProps) => {
        const typed = context as DropdownContext;
        const props = typed.props as DropdownControllerProps;
        const previous = previousProps as DropdownControllerProps;

        if (
            props.options !== previous.options ||
            props.selectedIndex !== previous.selectedIndex ||
            props.triggerKey !== previous.triggerKey ||
            props.popupKey !== previous.popupKey ||
            props.itemContainerKey !== previous.itemContainerKey ||
            props.chevronKey !== previous.chevronKey ||
            props.placeholder !== previous.placeholder
        ) {
            const options = asArray(props.options);
            if (props.selectedIndex !== previous.selectedIndex) {
                const authored = asNumber(props.selectedIndex, typed.state.selectedIndex);
                typed.state.selectedIndex = options.length > 0
                    ? clamp(authored, 0, options.length - 1)
                    : -1;
            }
            // Guard against the options list shrinking below the current index.
            if (typed.state.selectedIndex >= options.length) {
                typed.state.selectedIndex = options.length > 0
                    ? options.length - 1
                    : -1;
            }
            applySelection(typed);
            applyPopupVisibility(typed);
            applyChevron(typed);
        }
    },
    input: (event: Readonly<UIInputEvent>, context) => {
        const typed = context as DropdownContext;
        const state = typed.state;
        if (!state) {
            return false;
        }

        const props = typed.props as DropdownControllerProps;
        const options = asArray(props.options);

        if (event.type === 'pointer') {
            switch (event.phase) {
                case 'down':
                    // Toggle the popup open/closed on pointer down on the root.
                    setOpen(typed, !state.isOpen);
                    return true;
                case 'move':
                    if (!state.isOpen || options.length === 0) {
                        return false;
                    }
                    // Determine which item the pointer is over via layout boxes.
                    const items = resolveItems(typed);
                    if (items.length === 0) return false;
                    let found = -1;
                    for (let i = 0; i < items.length; i++) {
                        const box = typed.runtime.getLayoutBox(items[i]);
                        if (
                            event.x >= box.x &&
                            event.y >= box.y &&
                            event.x <= box.x + box.width &&
                            event.y <= box.y + box.height
                        ) {
                            found = i;
                            break;
                        }
                    }
                    if (found !== state.hoveredIndex) {
                        state.hoveredIndex = found;
                        applyHoverHighlight(typed);
                    }
                    return true;
                case 'up':
                    if (!state.isOpen) return false;
                    if (state.hoveredIndex >= 0 && state.hoveredIndex < options.length) {
                        selectIndex(typed, state.hoveredIndex);
                    }
                    return true;
                case 'leave': {
                    const wasOpen = state.isOpen;
                    if (wasOpen) {
                        setOpen(typed, false);
                    }
                    return wasOpen;
                }
                default:
                    return false;
            }
        }

        if (event.type === 'key' && event.phase === 'down') {
            switch (event.key) {
                case 'Enter':
                case ' ':
                    setOpen(typed, !state.isOpen);
                    return true;
                case 'ArrowDown':
                    if (!state.isOpen) {
                        setOpen(typed, true);
                    } else if (options.length > 0) {
                        const next = state.hoveredIndex < 0 ? 0 : state.hoveredIndex + 1;
                        state.hoveredIndex = clamp(next, 0, options.length - 1);
                        applyHoverHighlight(typed);
                    }
                    return true;
                case 'ArrowUp':
                    if (!state.isOpen) {
                        setOpen(typed, true);
                    } else if (options.length > 0) {
                        const prev = state.hoveredIndex <= 0 ? 0 : state.hoveredIndex - 1;
                        state.hoveredIndex = clamp(prev, 0, options.length - 1);
                        applyHoverHighlight(typed);
                    }
                    return true;
                case 'Escape':
                    if (state.isOpen) {
                        setOpen(typed, false);
                    }
                    return true;
                case 'Home':
                    if (state.isOpen && options.length > 0) {
                        state.hoveredIndex = 0;
                        applyHoverHighlight(typed);
                    }
                    return true;
                case 'End':
                    if (state.isOpen && options.length > 0) {
                        state.hoveredIndex = options.length - 1;
                        applyHoverHighlight(typed);
                    }
                    return true;
                default:
                    return false;
            }
        }

        return false;
    },
};

/**
 * Reads the live selected index of a dropdown widget driven by `dropdown-select`.
 * Returns null when the widget has no dropdown state (wrong controller or key).
 */
export const getDropdownSelectedIndex = (
    runtime: UIRuntime,
    widget: WidgetId
): number | null => {
    const state = runtime.getWidgetState(widget) as DropdownControllerState | null;
    return state ? state.selectedIndex : null;
};
