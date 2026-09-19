import type { UIRuntime } from '../runtime';
import type { UIInputEvent, UIImageSource, WidgetId, WidgetImageInput } from '../types';
import type { WidgetController, WidgetControllerContext } from '../widget';
import { asString, asStringOrNull, asRecord, isPointInside, isValidImageSource, toImageSource, extractSourceInput, type ImageSourceInput } from './internals';

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
 *     states:     { normal: '<theme.accentColor>', hover: '<theme.accentHoverColor>', ... },
 *     transition: 'color' | 'opacity' | 'tint' | 'sprite' | 'none',
 *     tints:      { normal: '#ffffffff', hover: '#cccccccc', ... },
 *     sprites:    { normal: { kind:'texture', resourceId:'btn_normal.png', ... }, ... },
 *     onPress:    'myButtonPressed',
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
	readonly onPress?: string;
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

type ButtonContext = WidgetControllerContext<
	Record<string, unknown>,
	ButtonFeedbackState,
	UIRuntime
>;

const resolveVisualState = (state: ButtonFeedbackState, disabled: boolean): ButtonVisualState => {
	if (disabled) return 'disabled';
	return state.pressed ? 'pressed' : state.hovered ? 'hover' : 'normal';
};

const emitPress = (context: ButtonContext, pointerX: number, pointerY: number): void => {
	const props = context.props as ButtonFeedbackProps;
	const name = asString(props.onPress);
	if (!name) return;
	const widget = context.widget as WidgetId;
	const box = context.runtime.getLayoutBox(widget);
	context.runtime.emitControllerEvent(widget, name, {
		x: box.x,
		y: box.y,
		pointerX,
		pointerY,
	});
};

const applyFeedback = (context: ButtonContext): void => {
	const props = context.props as ButtonFeedbackProps;
	const state = context.state;
	const widget = context.widget as WidgetId;
	const disabled = !context.runtime.isWidgetEnabled(widget);
	const visualState = resolveVisualState(state, disabled);
	const transition: ButtonTransitionMode = props.transition ?? 'color';

	if (transition === 'none') {
		return;
	}

	if (transition === 'tint') {
		const tints = asRecord(props.tints);
		const tintValue = asStringOrNull(tints[visualState]);
		context.runtime.updateWidget(widget, {
			image: { tint: tintValue ?? '#ffffffff' } as Partial<WidgetImageInput>,
		});
		if (disabled) {
			context.runtime.updateWidget(widget, {
				style: { opacity: BUTTON_STATE_OPACITY.disabled },
			});
		}
		return;
	}

	if (transition === 'sprite') {
		const sprites = asRecord(props.sprites);
		const stateEntry = sprites[visualState] as Record<string, unknown> | undefined;
		const sourceInput = stateEntry ? extractSourceInput(stateEntry) : null;
		const source = sourceInput ? toImageSource(sourceInput) : state.originalSource;
		if (isValidImageSource(source)) {
			context.runtime.updateWidget(widget, {
				image: { source } as Partial<WidgetImageInput>,
			});
		}
		if (disabled) {
			context.runtime.updateWidget(widget, {
				style: { opacity: BUTTON_STATE_OPACITY.disabled },
			});
		}
		return;
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
	context.runtime.updateWidget(widget, { style: stylePatch });
};

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
		const typed = context as ButtonContext;
		const imageInput = typed.runtime.getWidgetImageInput(typed.widget as WidgetId);
		typed.state.originalSource = imageInput?.source ?? null;
		applyFeedback(typed);
	},
	update: (context, previousProps) => {
		const typed = context as ButtonContext;
		const props = typed.props as ButtonFeedbackProps;
		const previous = previousProps as ButtonFeedbackProps;

		if (
			props.states !== previous.states ||
			props.transition !== previous.transition ||
			props.tints !== previous.tints ||
			props.sprites !== previous.sprites ||
			props.onPress !== previous.onPress
		) {
			applyFeedback(typed);
		}
	},
	input: (event, context) => {
		const typed = context as ButtonContext;
		const state = typed.state;
		if (!state) {
			return false;
		}

		if (event.type === 'key') {
			if (event.phase === 'down' && (event.key === 'Enter' || event.key === ' ') && !event.repeat) {
				const box = typed.runtime.getLayoutBox(typed.widget as WidgetId);
				emitPress(typed, box.x + box.width / 2, box.y + box.height / 2);
				return true;
			}
			return false;
		}

		if (event.type !== 'pointer') {
			return false;
		}
		switch (event.phase) {
			case 'down':
				state.pressed = true;
				break;
			case 'up': {
				const wasPressed = state.pressed;
				state.pressed = false;
				state.hovered = true;
				applyFeedback(typed);
				if (
					wasPressed &&
					isPointInside(typed.runtime, typed.widget as WidgetId, event.x, event.y)
				) {
					emitPress(typed, event.x, event.y);
				}
				return true;
			}
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

		applyFeedback(typed);
		return event.phase === 'down' || event.phase === 'up';
	},
};
