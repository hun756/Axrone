import type { UIRuntime } from '../runtime';
import type { UICheckboxHandle, UICheckboxOptions } from './types';
import { attachToParent, createTextBlock, disposeWidget, isPointInside } from './internals';
import { resolveTheme, resolveVariantPalette } from './theme';

export const createUICheckbox = <TRuntime>(
    runtime: UIRuntime<TRuntime>,
    options: UICheckboxOptions = {}
): UICheckboxHandle => {
    const theme = resolveTheme(options.theme);
    const state = {
        checked: options.checked ?? false,
        indeterminate: options.indeterminate ?? false,
        disabled: options.disabled ?? false,
        hovered: false,
        pressed: false,
        focused: false,
        label: options.label ?? 'Checkbox',
        onChange: options.onChange,
        variant: options.variant ?? 'primary',
        markStyle: options.markStyle ?? 'check',
    };
    const labelPosition = options.labelPosition ?? 'right';
    const boxSize = Math.max(18, Math.round(theme.controlHeight * 0.6));
    const markSize = Math.max(12, Math.round(boxSize * 0.65));

    let handle: UICheckboxHandle;

    const root = runtime.createWidget({
        role: 'custom:checkbox',
        key: options.key,
        enabled: !state.disabled,
        interactive: !state.disabled,
        focus: {
            focusable: !state.disabled,
            ...(options.focus ?? {}),
        },
        layout: {
            display: 'stack',
            direction: 'row',
            alignItems: 'center',
            gap: Math.max(8, Math.round(theme.controlHeight * 0.24)),
            width: 'content',
            height: 'content',
            ...(options.layout ?? {}),
        },
        style: {
            background: options.style?.background ?? '#00000000',
            ...(options.style ?? {}),
        },
        handlers: {
            pointerEnter: () => {
                if (state.disabled) return false;
                state.hovered = true;
                apply();
                return true;
            },
            pointerLeave: () => {
                if (state.disabled) return false;
                state.hovered = false;
                apply();
                return true;
            },
            pointerDown: () => {
                if (state.disabled) return false;
                state.pressed = true;
                runtime.setFocus(root, 'pointer');
                apply();
                return true;
            },
            pointerUp: (event) => {
                if (state.disabled) return false;
                const shouldToggle = state.pressed && isPointInside(runtime, root, event.x, event.y);
                state.pressed = false;
                if (shouldToggle) {
                    if (state.indeterminate) {
                        state.indeterminate = false;
                        state.checked = true;
                    } else {
                        state.checked = !state.checked;
                    }
                    state.onChange?.(state.checked, handle);
                }
                apply();
                return true;
            },
            keyDown: (event) => {
                if (state.disabled) return false;
                if ((event.key === 'Enter' || event.key === ' ') && !event.repeat) {
                    if (state.indeterminate) {
                        state.indeterminate = false;
                        state.checked = true;
                    } else {
                        state.checked = !state.checked;
                    }
                    state.onChange?.(state.checked, handle);
                    apply();
                    return true;
                }
                return false;
            },
            focus: () => {
                state.focused = true;
                apply();
            },
            blur: () => {
                state.focused = false;
                state.pressed = false;
                apply();
            },
        },
    });

    const box = runtime.createWidget({
        role: 'custom:checkbox-box',
        layout: {
            width: boxSize,
            height: boxSize,
            display: 'overlay',
            shrink: 0,
        },
    });

    const mark = runtime.createWidget({
        role: 'custom:checkbox-mark',
        layout: {
            position: 'absolute',
            width: markSize,
            height: markSize,
            anchor: { x: 0.5, y: 0.5, maxX: 0.5, maxY: 0.5, pivotX: 0.5, pivotY: 0.5 },
        },
    });

    const label = runtime.createWidget({
        role: 'text',
        layout: {
            width: 'content',
            height: 'content',
        },
    });

    attachToParent(runtime, options.parent, root);
    runtime.appendChild(root, box);
    runtime.appendChild(box, mark);
    if (labelPosition === 'left') {
        runtime.appendChild(root, label);
    } else if (labelPosition === 'right') {
        runtime.appendChild(root, label);
    }

    const apply = (): void => {
        const palette = resolveVariantPalette(theme, state.variant);
        const activeColor = state.checked || state.indeterminate ? palette.idle : theme.surfaceColor;
        const hoverColor = state.checked ? palette.hover : theme.surfaceHoverColor;
        const currentColor = state.disabled
            ? theme.surfaceDisabledColor
            : state.hovered || state.pressed
              ? hoverColor
              : activeColor;

        runtime.updateWidget(root, {
            enabled: !state.disabled,
            interactive: !state.disabled,
            focus: {
                focusable: !state.disabled,
                ...(options.focus ?? {}),
            },
        });

        runtime.updateWidget(box, {
            style: {
                background: currentColor,
                borderColor: state.focused ? theme.focusColor : state.checked || state.indeterminate ? palette.border : theme.borderColor,
                borderWidth: state.focused ? theme.borderWidth + 1 : theme.borderWidth,
                radius: theme.controlRadius,
            },
        });

        const markVisible = state.checked || state.indeterminate;
        runtime.updateWidget(mark, {
            style: {
                background: markVisible ? theme.thumbColor : '#00000000',
                radius: state.markStyle === 'dot' ? 999 : 2,
            },
            layout: {
                position: 'absolute',
                width: markSize,
                height: markSize,
                anchor: { x: 0.5, y: 0.5, maxX: 0.5, maxY: 0.5, pivotX: 0.5, pivotY: 0.5 },
            },
        });

        runtime.updateWidget(label, {
            text: labelPosition === 'hidden'
                ? null
                : createTextBlock(runtime, state.label, theme, { wrap: 'none' }, state.disabled ? theme.textMutedColor : theme.textColor),
            style: {
                color: state.disabled ? theme.textMutedColor : theme.textColor,
            },
        });
    };

    handle = {
        root,
        isChecked() {
            return state.checked;
        },
        setChecked(checked) {
            state.checked = checked;
            if (checked) state.indeterminate = false;
            apply();
        },
        isIndeterminate() {
            return state.indeterminate;
        },
        setIndeterminate(value) {
            state.indeterminate = value;
            apply();
        },
        toggle() {
            if (!state.disabled) {
                if (state.indeterminate) {
                    state.indeterminate = false;
                    state.checked = true;
                } else {
                    state.checked = !state.checked;
                }
                state.onChange?.(state.checked, handle);
                apply();
            }
        },
        setDisabled(disabled) {
            state.disabled = disabled;
            state.pressed = false;
            state.hovered = false;
            apply();
        },
        dispose() {
            disposeWidget(runtime, root);
        },
    };

    apply();
    return handle;
};
