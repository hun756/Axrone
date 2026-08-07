import type { UIRuntime } from '../runtime';
import type { UIInputEvent, WidgetId } from '../types';
import type { WidgetController } from '../widget';

/**
 * Declarative button-feedback controller for `.ui.json` authored buttons.
 *
 * Applies per-state visual feedback (background colour or opacity) when the
 * user interacts with a button widget. The authored `props` contract:
 *
 *   props: {
 *     states: { normal: '#0a74daff', hover: '#1b85ebff', pressed: '...', disabled: '...' },
 *     transition: 'color' | 'opacity' | 'none'
 *   }
 *
 * Missing state entries fall back to the widget's authored style, so a button
 * with no props still gets a sensible hover/press response via opacity dim.
 */
export const BUTTON_FEEDBACK_CONTROLLER_TYPE = 'button-feedback';

export type ButtonVisualState = 'normal' | 'hover' | 'pressed' | 'disabled';

export type ButtonTransitionMode = 'color' | 'opacity' | 'none';

export interface ButtonFeedbackProps {
	readonly states?: Partial<Record<ButtonVisualState, string>>;
	readonly transition?: ButtonTransitionMode;
}

interface ButtonFeedbackState {
	pressed: boolean;
	hovered: boolean;
}

/** Opacity applied per state when the transition mode is `opacity`. */
export const BUTTON_STATE_OPACITY: Readonly<Record<ButtonVisualState, number>> = Object.freeze({
	normal: 1,
	hover: 0.85,
	pressed: 0.6,
	disabled: 0.45,
});

const asRecord = (value: unknown): Record<string, unknown> =>
	value && typeof value === 'object' && !Array.isArray(value)
		? (value as Record<string, unknown>)
		: {};

const asString = (value: unknown): string | null =>
	typeof value === 'string' && value.trim() !== '' ? value : null;

const resolveVisualState = (state: ButtonFeedbackState): ButtonVisualState =>
	state.pressed ? 'pressed' : state.hovered ? 'hover' : 'normal';

/**
 * Visual feedback for interactive buttons. Applies the authored per-state
 * background colour when `props.states` provides one, and otherwise falls back
 * to an opacity dim so buttons always respond to pointer input.
 */
export const buttonFeedbackController: WidgetController<
	typeof BUTTON_FEEDBACK_CONTROLLER_TYPE,
	Record<string, unknown>,
	ButtonFeedbackState,
	UIRuntime,
	unknown
> = {
	type: BUTTON_FEEDBACK_CONTROLLER_TYPE,
	createState: () => ({ pressed: false, hovered: false }),
	input: (event, context) => {
		if (event.type !== 'pointer') {
			return false;
		}
		const state = context.state;
		if (!state) {
			return false;
		}
		switch (event.phase) {
			case 'down':
				state.pressed = true;
				break;
			case 'up':
				state.pressed = false;
				state.hovered = true;
				break;
			case 'enter':
				state.hovered = true;
				break;
			case 'leave':
				state.pressed = false;
				state.hovered = false;
				break;
			default:
				return false;
		}

		const props = context.props as ButtonFeedbackProps;
		const states = asRecord(props.states);
		const visualState = resolveVisualState(state);
		const transition: ButtonTransitionMode = props.transition ?? 'color';
		const handled = event.phase === 'down' || event.phase === 'up';
		if (transition === 'none') {
			return handled;
		}

		const stylePatch: Record<string, unknown> = {};
		if (transition === 'opacity') {
			const normalColor = asString(states.normal);
			if (normalColor) {
				stylePatch.background = normalColor;
			}
			stylePatch.opacity = BUTTON_STATE_OPACITY[visualState];
		} else {
			const stateColor = asString(states[visualState]);
			if (stateColor) {
				stylePatch.background = stateColor;
				stylePatch.opacity = 1;
			} else {
				stylePatch.opacity = BUTTON_STATE_OPACITY[visualState];
			}
		}
		context.runtime.updateWidget(context.widget as WidgetId, { style: stylePatch });
		return handled;
	},
};
