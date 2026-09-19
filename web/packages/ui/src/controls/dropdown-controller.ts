import type { UIRuntime } from '../runtime';
import type { UIInputEvent, WidgetId } from '../types';
import type { WidgetController, WidgetControllerContext } from '../widget';
import { clamp } from '@axrone/numeric';
import { asString, asNumber, setWidgetVisible } from './internals';

/**
 * Declarative dropdown-select controller for `.ui.json` authored dropdowns.
 *
 * The authored widget acts as the interaction surface and its `props` name the
 * child widgets that visualise the selection and popup list:
 *
 *   props: {
 *     options,              // display labels for each option
 *     selectedIndex,        // currently selected index (0-based)
 *     triggerContainerKey,  // named binding -> trigger box (field height/radius/border, state tint)
 *     triggerKey,           // named binding -> text widget showing current selection
 *     popupKey,             // named binding -> popup panel widget (toggled visible/hidden)
 *     itemContainerKey,     // named binding -> container holding item widgets
 *     chevronKey,           // named binding -> chevron indicator (tinted when open)
 *     placeholder,          // text when no selection
 *     fieldHeight,          // trigger box height in px
 *     cornerRadius,         // trigger box corner radius in px
 *     borderWidth,          // trigger box border width in px
 *     arrowSize,            // chevron glyph size in px
 *     arrowColor,           // chevron color when closed
 *     itemHeight,           // option row height in px
 *     hoverColor,           // option row highlight while hovered
 *     selectedColor,        // option row highlight for the current selection
 *     panelRadius,          // popup panel corner radius in px
 *     states,               // { normal, open } trigger background tints
 *   }
 *
 * Child widgets are resolved through the asset's binding table, so the authored
 * keys are the contract; the controller never assumes a tree shape.
 *
 * Visibility contract: the popup is hidden with *both* `enabled: false` and
 * `style.visible: false`. `enabled` alone does not remove a widget from the
 * render frame, layout measurement, or hit-testing (`isVisible` only tracks
 * `style.visible`), so pushing only `enabled` leaves the popup permanently
 * painted on screen.
 */
export const DROPDOWN_SELECT_CONTROLLER_TYPE = 'dropdown-select';

export type DropdownVisualState = 'normal' | 'hover' | 'open' | 'disabled';

export interface DropdownControllerProps {
    readonly options?: readonly string[];
    readonly selectedIndex?: number;
    readonly triggerContainerKey?: string;
    readonly triggerKey?: string;
    readonly popupKey?: string;
    readonly itemContainerKey?: string;
    readonly chevronKey?: string;
    readonly placeholder?: string;
    readonly fieldHeight?: number;
    readonly cornerRadius?: number;
    readonly borderWidth?: number;
    readonly arrowSize?: number;
    readonly arrowColor?: string;
    readonly itemHeight?: number;
    readonly hoverColor?: string;
    readonly selectedColor?: string;
    readonly panelRadius?: number;
    readonly states?: Partial<Record<DropdownVisualState, string>>;
}

export interface DropdownControllerState {
    selectedIndex: number;
    isOpen: boolean;
    hoveredIndex: number;
    /** Cached container widget resolved from itemContainerKey (or popupKey fallback). */
    cachedContainer: WidgetId | null;
    /** Cached top-level item widgets inside the container. */
    cachedItems: WidgetId[];
    /** Subtree size the cache was built from; guards against option add/remove. */
    cachedSubtreeSize: number;
}

type DropdownContext = WidgetControllerContext<
    Record<string, unknown>,
    DropdownControllerState,
    UIRuntime
>;

const asArray = (value: unknown): readonly string[] =>
    Array.isArray(value) ? value.filter((v): v is string => typeof v === 'string') : [];

/** Finite number or null when the prop is absent/invalid (absent = keep authored). */
const asFiniteNumber = (value: unknown): number | null =>
    typeof value === 'number' && Number.isFinite(value) ? value : null;

/** Non-empty color string or null when the prop is absent (absent = keep authored). */
const asColorString = (value: unknown): string | null =>
    typeof value === 'string' && value.trim() !== '' ? value : null;

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

    // Both flags are required: `enabled` gates input dispatch while only
    // `style.visible` removes the popup from the render frame, layout
    // measurement, and hit-testing. Pushing `enabled` alone leaves the popup
    // permanently painted on screen (the "always open" preview bug).
    setWidgetVisible(runtime, popup, state.isOpen);
};

/**
 * Resolves the widget that owns the option rows: the `itemContainerKey`
 * binding when it resolves, otherwise the popup panel itself (legacy assets
 * authored before the items container existed).
 */
const resolveItemsContainer = (context: DropdownContext): WidgetId | null => {
    const props = context.props as DropdownControllerProps;
    const runtime = context.runtime;

    const containerKey = asString(props.itemContainerKey);
    if (containerKey) {
        const container = runtime.getBoundWidget(containerKey);
        if (container !== null) return container;
    }
    const popupKey = asString(props.popupKey);
    if (!popupKey) return null;
    return runtime.getBoundWidget(popupKey);
};

/**
 * Resolves and caches the top-level item widgets inside the items container.
 *
 * Items are the outermost widgets of the container subtree: any candidate
 * strictly nested inside another candidate (e.g. a label text inside its
 * option row) is excluded. The old box-equality heuristic leaked those
 * nested children into the list and broke hover-to-option mapping.
 * Results are cached in state; the cache is rebuilt when the container
 * changes or the subtree grows/shrinks (options added or removed).
 */
const resolveItems = (context: DropdownContext): readonly WidgetId[] => {
    const runtime = context.runtime;
    const state = context.state;

    const container = resolveItemsContainer(context);
    if (container === null) return [];

    const subtree = runtime.collectSubtreeWidgetIds(container);
    // Return the cached list when nothing structural changed. The membership
    // probe covers same-size swaps (an item removed and another added): the
    // length matches but the cached ids are stale (possibly destroyed).
    if (
        state.cachedContainer === container &&
        state.cachedSubtreeSize === subtree.length &&
        state.cachedItems.length > 0
    ) {
        const members = new Set<WidgetId>(subtree);
        let intact = true;
        for (let i = 0; i < state.cachedItems.length; i++) {
            if (!members.has(state.cachedItems[i]!)) {
                intact = false;
                break;
            }
        }
        if (intact) {
            return state.cachedItems;
        }
    }

    const containerBox = runtime.getLayoutBox(container);
    if (containerBox.width <= 0 || containerBox.height <= 0) {
        // Layout has not run yet (all boxes are zero); resolving now would
        // cache garbage, so report empty and let the next pass rebuild.
        return [];
    }

    type Box = { x: number; y: number; width: number; height: number };
    const candidates: { widget: WidgetId; box: Box }[] = [];
    for (let i = 0; i < subtree.length; i++) {
        const candidate = subtree[i];
        if (candidate === container) continue;
        const box = runtime.getLayoutBox(candidate);
        if (box.width <= 0 || box.height <= 0) continue;
        // Skip widgets that fill the container exactly (overlay roots).
        if (
            box.x === containerBox.x && box.y === containerBox.y &&
            box.width === containerBox.width && box.height === containerBox.height
        ) {
            continue;
        }
        candidates.push({ widget: candidate, box });
    }

    const isStrictlyInside = (inner: Box, outer: Box): boolean =>
        inner.x >= outer.x &&
        inner.y >= outer.y &&
        inner.x + inner.width <= outer.x + outer.width &&
        inner.y + inner.height <= outer.y + outer.height &&
        (inner.width < outer.width || inner.height < outer.height);

    const items: WidgetId[] = [];
    for (let i = 0; i < candidates.length; i++) {
        let nested = false;
        for (let j = 0; j < candidates.length; j++) {
            if (i === j) continue;
            if (isStrictlyInside(candidates[i]!.box, candidates[j]!.box)) {
                nested = true;
                break;
            }
        }
        if (!nested) items.push(candidates[i]!.widget);
    }

    state.cachedContainer = container;
    state.cachedItems = items;
    state.cachedSubtreeSize = subtree.length;
    return items;
};

/**
 * Updates item background colours to reflect hover and selection.
 * The hovered row uses `hoverColor`; the current selection uses
 * `selectedColor` when it is not hovered; all other rows are transparent.
 */
const applyHoverHighlight = (context: DropdownContext): void => {
    const props = context.props as DropdownControllerProps;
    const runtime = context.runtime;
    const state = context.state;
    const items = resolveItems(context);

    const hoverColor = asColorString(props.hoverColor) ?? '#334155ff';
    const selectedColor = asColorString(props.selectedColor);

    for (let i = 0; i < items.length; i++) {
        const background =
            i === state.hoveredIndex
                ? hoverColor
                : i === state.selectedIndex && selectedColor !== null
                  ? selectedColor
                  : '#00000000';
        runtime.updateWidget(items[i], { style: { background } });
    }
};

/** Updates the chevron indicator to reflect open/closed state and arrow props. */
const applyChevron = (context: DropdownContext): void => {
    const props = context.props as DropdownControllerProps;
    const runtime = context.runtime;
    const state = context.state;

    const chevronKey = asString(props.chevronKey);
    if (!chevronKey) return;

    const chevron = runtime.getBoundWidget(chevronKey);
    if (chevron === null) return;

    const closedColor = asColorString(props.arrowColor) ?? '#94a3b8ff';
    const patch: { style?: Record<string, unknown>; text?: Record<string, unknown> } = {
        style: { color: state.isOpen ? '#e2e8f0ff' : closedColor },
    };
    const arrowSize = asFiniteNumber(props.arrowSize);
    if (arrowSize !== null && arrowSize > 0) {
        patch.text = { size: arrowSize };
    }
    runtime.updateWidget(chevron, patch);
};

/**
 * Pushes authored appearance props onto the trigger box and popup panel.
 * Every prop is optional: absent props leave the authored child styles
 * untouched, so legacy assets without appearance props render unchanged.
 */
const applyAppearance = (context: DropdownContext): void => {
    const props = context.props as DropdownControllerProps;
    const runtime = context.runtime;
    const state = context.state;

    const triggerContainerKey = asString(props.triggerContainerKey);
    if (triggerContainerKey) {
        const triggerBox = runtime.getBoundWidget(triggerContainerKey);
        if (triggerBox !== null) {
            const layoutPatch: Record<string, unknown> = {};
            const stylePatch: Record<string, unknown> = {};
            const fieldHeight = asFiniteNumber(props.fieldHeight);
            if (fieldHeight !== null && fieldHeight > 0) layoutPatch.height = fieldHeight;
            const cornerRadius = asFiniteNumber(props.cornerRadius);
            if (cornerRadius !== null && cornerRadius >= 0) stylePatch.radius = cornerRadius;
            const borderWidth = asFiniteNumber(props.borderWidth);
            if (borderWidth !== null && borderWidth >= 0) stylePatch.borderWidth = borderWidth;
            const states = (props.states && typeof props.states === 'object' && !Array.isArray(props.states)
                ? props.states
                : {}) as Partial<Record<DropdownVisualState, string>>;
            const openTint = asColorString(states.open);
            const normalTint = asColorString(states.normal);
            if (state.isOpen && openTint !== null) {
                stylePatch.background = openTint;
            } else if (!state.isOpen && normalTint !== null) {
                stylePatch.background = normalTint;
            }
            if (Object.keys(layoutPatch).length > 0 || Object.keys(stylePatch).length > 0) {
                runtime.updateWidget(
                    triggerBox,
                    {
                        ...(Object.keys(layoutPatch).length > 0 ? { layout: layoutPatch } : {}),
                        ...(Object.keys(stylePatch).length > 0 ? { style: stylePatch } : {}),
                    },
                );
            }
        }
    }

    const popupKey = asString(props.popupKey);
    if (popupKey) {
        const popup = runtime.getBoundWidget(popupKey);
        if (popup !== null) {
            const panelRadius = asFiniteNumber(props.panelRadius);
            if (panelRadius !== null && panelRadius >= 0) {
                runtime.updateWidget(popup, { style: { radius: panelRadius } });
            }
        }
    }

    const itemHeight = asFiniteNumber(props.itemHeight);
    if (itemHeight !== null && itemHeight > 0) {
        const items = resolveItems(context);
        for (let i = 0; i < items.length; i++) {
            runtime.updateWidget(items[i], { layout: { height: itemHeight } });
        }
    }
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
    applyAppearance(context);
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
            cachedSubtreeSize: 0,
        };
    },
    mount: (context) => {
        const typed = context as DropdownContext;
        // Apply initial selection and hide the popup once bindings are ready.
        applySelection(typed);
        applyPopupVisibility(typed);
        applyChevron(typed);
        applyAppearance(typed);
    },
    update: (context, previousProps) => {
        const typed = context as DropdownContext;
        const props = typed.props as DropdownControllerProps;
        const previous = previousProps as DropdownControllerProps;

        if (
            props.options !== previous.options ||
            props.selectedIndex !== previous.selectedIndex ||
            props.triggerContainerKey !== previous.triggerContainerKey ||
            props.triggerKey !== previous.triggerKey ||
            props.popupKey !== previous.popupKey ||
            props.itemContainerKey !== previous.itemContainerKey ||
            props.chevronKey !== previous.chevronKey ||
            props.placeholder !== previous.placeholder ||
            props.fieldHeight !== previous.fieldHeight ||
            props.cornerRadius !== previous.cornerRadius ||
            props.borderWidth !== previous.borderWidth ||
            props.arrowSize !== previous.arrowSize ||
            props.arrowColor !== previous.arrowColor ||
            props.itemHeight !== previous.itemHeight ||
            props.hoverColor !== previous.hoverColor ||
            props.selectedColor !== previous.selectedColor ||
            props.panelRadius !== previous.panelRadius ||
            props.states !== previous.states
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
            applyAppearance(typed);
            if (typed.state.isOpen) {
                applyHoverHighlight(typed);
            }
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
