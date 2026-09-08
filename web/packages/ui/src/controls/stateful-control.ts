import type { UIRuntime } from '../runtime';
import type { UIPointerEvent, UIKeyEvent } from '../types';
import { disposeWidget } from './internals';

/**
 * Common interactive state shared by all stateful controls.
 * Controls extend this with widget-specific fields (checked, value, etc.).
 */
export interface StatefulControlState {
    disabled: boolean;
    hovered: boolean;
    pressed: boolean;
    focused: boolean;
}

/**
 * Handler hooks that a control can supply to the factory.
 * All are optional — the factory wires only the ones provided.
 */
export interface StatefulHandlers {
    pointerEnter?: (event: Readonly<UIPointerEvent>) => boolean;
    pointerLeave?: (event: Readonly<UIPointerEvent>) => boolean;
    pointerDown?: (event: Readonly<UIPointerEvent>) => boolean;
    pointerUp?: (event: Readonly<UIPointerEvent>) => boolean;
    pointerMove?: (event: Readonly<UIPointerEvent>) => boolean;
    keyDown?: (event: Readonly<UIKeyEvent>) => boolean;
    focus?: () => void;
    blur?: () => void;
}

/**
 * Configuration supplied by each control to the factory.
 */
export interface StatefulControlConfig<S extends StatefulControlState> {
    /** The control's mutable state object (mutated in place). */
    state: S;
    /** Called after every state change to push visuals. */
    apply: () => void;
    /** Optional interaction handlers. */
    handlers?: StatefulHandlers;
}

/**
 * Return value from the factory — utilities shared by all stateful controls.
 */
export interface StatefulControlUtils<S extends StatefulControlState> {
    /** The control's state (same reference as config.state). */
    readonly state: S;
    /** Syncs enabled/interactive/focusable on the root widget to !state.disabled. */
    syncDisabled: (root: number, focusOverride?: Record<string, unknown>) => void;
    /** Clears hovered + pressed, syncs disabled, calls apply. */
    setDisabled: (disabled: boolean, root: number, focusOverride?: Record<string, unknown>) => void;
    /** Disposes the root widget. */
    dispose: (root: number) => void;
    /** The resolved handlers (with defaults for focus/blur). */
    readonly handlers: StatefulHandlers;
}

/**
 * Factory that captures the shared lifecycle pattern for interactive controls:
 *   - Common state fields: disabled, hovered, pressed, focused
 *   - Standard focus/blur transitions
 *   - syncDisabled for pushing enabled/interactive/focusable to the root widget
 *   - setDisabled that clears hover/press and re-applies
 *   - dispose via disposeWidget
 *
 * Each control supplies its own initial state, apply function, and event handlers.
 */
export const createStatefulControl = <S extends StatefulControlState>(
    runtime: UIRuntime<unknown>,
    config: StatefulControlConfig<S>,
): StatefulControlUtils<S> => {
    const { state, apply } = config;

    const defaultFocus = (): void => {
        state.focused = true;
        apply();
    };

    const defaultBlur = (): void => {
        state.focused = false;
        state.pressed = false;
        apply();
    };

    const handlers: StatefulHandlers = {
        ...config.handlers,
        focus: config.handlers?.focus ?? defaultFocus,
        blur: config.handlers?.blur ?? defaultBlur,
    };

    const syncDisabled = (root: number, focusOverride?: Record<string, unknown>): void => {
        runtime.updateWidget(root, {
            enabled: !state.disabled,
            interactive: !state.disabled,
            focus: {
                focusable: !state.disabled,
                ...(focusOverride ?? {}),
            },
        });
    };

    const setDisabled = (disabled: boolean, root: number, focusOverride?: Record<string, unknown>): void => {
        state.disabled = disabled;
        state.pressed = false;
        state.hovered = false;
        syncDisabled(root, focusOverride);
        apply();
    };

    const dispose = (root: number): void => {
        disposeWidget(runtime, root);
    };

    return {
        state,
        syncDisabled,
        setDisabled,
        dispose,
        handlers,
    };
};
