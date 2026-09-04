import type { UIRuntime } from '../runtime';
import type { UIInputEvent, UIImageSource, WidgetId, WidgetImageInput } from '../types';
import type { WidgetController } from '../widget';
import { asStringOrNull, asRecord, isValidImageSource, toImageSource, extractSourceInput, type ImageSourceInput } from './internals';

/**
 * Declarative button-feedback controller for `.ui.json` authored buttons.
 *
 * Applies per-state visual feedback when the user interacts with a button.
 * Five transition modes are supported:
 *
 *   'color'   — swaps `style.background` per state (default, backward-compatible).
 *   'opacity' — adjusts `style.opacity` per state.
 *   'tint'    — patches `image.tint` per state (requires widget.image).
 *   'sprite'  — swaps `image.source` per state (requires widget.image).
 *   'none'    — no visual feedback.
 *
 * Props contract:
 *
 *   props: {
 *     states:     { normal: '#0a74daff', hover: '#1b85ebff', ... },
 *     transition: 'color' | 'opacity' | 'tint' | 'sprite' | 'none',
 *     tints:      { normal: '#ffffffff', hover: '#cccccccc', ... },
 *     sprites:    { normal: { kind:'texture', resourceId:'btn_normal.png', ... }, ... }
 *   }
 *
 * `tints` is read only when transition is `'tint'`.
 * `sprites` is read only when transition is `'sprite'`.
 * Both fall back gracefully when the widget has no image configured.
 */
export const BUTTON_FEEDBACK_CONTROLLER_TYPE = 'button-feedback';

export type ButtonVisualState = 'normal' | 'hover' | 'pressed' | 'disabled';

export type ButtonTransitionMode = 'color' | 'opacity' | 'tint' | 'sprite' | 'none';

/**
 * Inline image-source descriptor for per-state sprite swapping.
 * Mirrors `UIImageSource` without requiring an import-time dependency on the
 * full widget type — keeps the props contract self-contained in JSON.
 * @deprecated Use ImageSourceInput from internals instead.
 */
export type ButtonImageSourceInput = ImageSourceInput;

export interface ButtonFeedbackProps {
	readonly states?: Partial<Record<ButtonVisualState, string>>;
	readonly transition?: ButtonTransitionMode;
	readonly tints?: Partial<Record<ButtonVisualState, string>>;
	readonly sprites?: Partial<Record<ButtonVisualState, ButtonImageSourceInput>>;
}

interface ButtonFeedbackState {
	pressed: boolean;
	hovered: boolean;
	originalSource: UIImageSource | null;
}

/** Opacity applied per state when the transition mode is `opacity`. */
export const BUTTON_STATE_OPACITY: Readonly<Record<ButtonVisualState, number>> = Object.freeze({
	normal: 1,
	hover: 0.85,
	pressed: 0.6,
	disabled: 0.45,
});

const resolveVisualState = (state: ButtonFeedbackState): ButtonVisualState =>
	state.pressed ? 'pressed' : state.hovered ? 'hover' : 'normal';

/**
 * Visual feedback for interactive buttons. Supports five transition modes:
 *
 * - `color` (default): per-state background colour with opacity fallback.
 * - `opacity`: per-state opacity dimming.
 * - `tint`: per-state image tint (widget must have an image configured).
 * - `sprite`: per-state image source swap (widget must have an image configured).
 * - `none`: no visual feedback.
 *
 * `tint` and `sprite` modes fall back to `color` mode when the widget has no
 * image, so buttons always respond to pointer input regardless of configuration.
 */
export const buttonFeedbackController: WidgetController<
	typeof BUTTON_FEEDBACK_CONTROLLER_TYPE,
	Record<string, unknown>,
	ButtonFeedbackState,
	UIRuntime,
	unknown
> = {
	type: BUTTON_FEEDBACK_CONTROLLER_TYPE,
	createState: () => ({ pressed: false, hovered: false, originalSource: null }),
	mount: (context) => {
		const imageInput = context.runtime.getWidgetImageInput(context.widget);
		context.state.originalSource = imageInput?.source ?? null;
	},
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
		const visualState = resolveVisualState(state);
		const transition: ButtonTransitionMode = props.transition ?? 'color';
		const handled = event.phase === 'down' || event.phase === 'up';

		if (transition === 'none') {
			return handled;
		}

		if (transition === 'tint') {
			const tints = asRecord(props.tints);
			const tintValue = asStringOrNull(tints[visualState]);
			if (tintValue) {
				context.runtime.updateWidget(context.widget as WidgetId, {
					image: { tint: tintValue } as Partial<WidgetImageInput>,
				});
			} else {
				context.runtime.updateWidget(context.widget as WidgetId, {
					image: { tint: '#ffffffff' } as Partial<WidgetImageInput>,
				});
			}
			return handled;
		}

		if (transition === 'sprite') {
			const sprites = asRecord(props.sprites);
			const stateEntry = sprites[visualState] as Record<string, unknown> | undefined;
			const sourceInput = stateEntry ? extractSourceInput(stateEntry) : null;
			const source = sourceInput ? toImageSource(sourceInput) : state.originalSource;
			if (isValidImageSource(source)) {
				context.runtime.updateWidget(context.widget as WidgetId, {
					image: { source } as Partial<WidgetImageInput>,
				});
			}
			return handled;
		}

		const states = asRecord(props.states);
		const stylePatch: Record<string, unknown> = {};
		if (transition === 'opacity') {
			const normalColor = asStringOrNull(states.normal);
			if (normalColor) {
				stylePatch.background = normalColor;
			}
			stylePatch.opacity = BUTTON_STATE_OPACITY[visualState];
		} else {
			const stateColor = asStringOrNull(states[visualState]);
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
