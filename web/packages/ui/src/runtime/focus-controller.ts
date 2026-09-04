import type { WidgetId, FocusMoveDirection, WidgetFocusChangeEvent, LayoutBox } from '../types';
import type { ResolvedFocusPolicy } from '../types';
import { NodeFlag } from './node-flags';
import {
    moveFocusDirectional,
    moveFocusLinear,
} from './runtime-input';

/**
 * Adapter interface providing the FocusController access to UIRuntime's
 * SoA arrays and helper methods without circular imports.
 */
export interface FocusControllerHost {
    readonly flags: Uint32Array;
    readonly parent: Int32Array;
    readonly sequence: Uint32Array;
    readonly focuses: Array<ResolvedFocusPolicy | null>;
    readonly rootId: WidgetId;
    readonly nextId: number;
    isFocusable(index: number): boolean;
    isAncestor(ancestor: number, candidate: number): boolean;
    readBox(index: number): LayoutBox;
}

/**
 * Manages focus state and traversal for the UI runtime.
 *
 * Responsibilities:
 * - Track the currently focused widget
 * - Maintain a cached focus order (tab order) that is recomputed when dirty
 * - Provide focus traversal (linear forward/backward, directional)
 * - Scope focus navigation to focus-scope roots
 * - Compute focus-ring geometry for rendering
 */
export class FocusController {
    private focused: WidgetId | null = null;
    private focusDirty = true;
    private focusOrder: WidgetId[] = [];

    /**
     * Returns the currently focused widget, or null if nothing is focused.
     */
    getFocused(): WidgetId | null {
        return this.focused;
    }

    /**
     * Sets focus to the given widget. Returns true if focus changed.
     * The host's emitFocusChange is called for focus/blur notifications.
     */
    setFocus(
        widget: WidgetId | null,
        host: FocusControllerHost,
        options: {
            reason?: WidgetFocusChangeEvent['reason'];
            direction?: FocusMoveDirection;
            onFocusedChange?: (widget: WidgetId | null, previous: WidgetId | null) => void;
        } = {}
    ): boolean {
        const { reason = 'api', direction, onFocusedChange } = options;
        if (widget !== null) {
            if (!host.isFocusable(widget)) {
                return false;
            }
        }
        if (this.focused === widget) {
            return true;
        }
        const previous = this.focused;
        this.focused = widget;
        onFocusedChange?.(widget, previous);
        return true;
    }

    /**
     * Clears focus unconditionally.
     */
    clearFocus(): void {
        this.focused = null;
    }

    /**
     * Marks the focus order as dirty, forcing recomputation on next access.
     */
    markDirty(): void {
        this.focusDirty = true;
    }

    /**
     * Moves focus in the given direction. Returns the newly focused widget,
     * or null if no movement occurred.
     */
    moveFocus(
        direction: FocusMoveDirection,
        host: FocusControllerHost,
        setFocusFn: (widget: WidgetId | null, reason: WidgetFocusChangeEvent['reason'], direction?: FocusMoveDirection) => boolean
    ): WidgetId | null {
        const candidates = this.getFocusableCandidates(host);
        if (candidates.length === 0) {
            return null;
        }
        const current = this.focused ? (this.focused as number) : 0;
        const scopeRoot = current === 0 ? host.rootId : this.findScopeRoot(current, host);
        const scoped = scopeRoot === host.rootId
            ? candidates
            : candidates.filter((candidate) => host.isAncestor(scopeRoot as number, candidate as number));
        const targetList = scoped.length > 0 ? scoped : candidates;
        let next: WidgetId | null = null;
        if (direction === 'forward' || direction === 'backward') {
            const cycle = host.focuses[scopeRoot as number]?.cycle ?? false;
            next = moveFocusLinear(targetList, direction, this.focused, cycle);
        } else {
            next = moveFocusDirectional(targetList, direction, this.focused, (index) => host.readBox(index));
        }
        if (next) {
            setFocusFn(next, 'navigation', direction);
        }
        return next;
    }

    /**
     * Returns the list of focusable widgets in tab order, recomputing if dirty.
     */
    getFocusableCandidates(host: FocusControllerHost): WidgetId[] {
        if (this.focusDirty) {
            const candidates: WidgetId[] = [];
            for (let index = 1; index < host.nextId; index += 1) {
                if ((host.flags[index] & NodeFlag.Allocated) === 0 || !host.isFocusable(index)) {
                    continue;
                }
                candidates.push(index as WidgetId);
            }
            candidates.sort((left, right) => {
                const leftIndex = left as number;
                const rightIndex = right as number;
                const leftFocus = host.focuses[leftIndex]!;
                const rightFocus = host.focuses[rightIndex]!;
                if (leftFocus.tabIndex !== rightFocus.tabIndex) {
                    return leftFocus.tabIndex - rightFocus.tabIndex;
                }
                if (leftFocus.order !== rightFocus.order) {
                    return leftFocus.order - rightFocus.order;
                }
                return host.sequence[leftIndex] - host.sequence[rightIndex];
            });
            this.focusOrder = candidates;
            this.focusDirty = false;
        }
        return this.focusOrder;
    }

    /**
     * Finds the nearest ancestor (or self) that is a focus scope root.
     */
    findScopeRoot(index: number, host: FocusControllerHost): WidgetId {
        for (let current = index; current !== 0; current = host.parent[current]) {
            if (host.focuses[current]?.scope) {
                return current as WidgetId;
            }
        }
        return host.rootId;
    }
}
