import { describe, expect, it, vi } from 'vitest';
import { animate, Easing } from '../controls/animation';
import { UIError, UIErrorCode, WidgetNotFoundError } from '../errors';
import type { UIRuntime } from '../runtime';
import type { WidgetId } from '../types';

const createMockRuntime = (overrides: Partial<UIRuntime> = {}): UIRuntime => ({
	updateWidget: vi.fn(),
	getWidgetStyleInput: vi.fn(() => undefined),
	...overrides,
} as unknown as UIRuntime);

describe('animation tick error handling', () => {
	it('cancels silently when widget is disposed mid-animation (WidgetNotFoundError)', () => {
		const updateWidget = vi.fn().mockImplementationOnce(() => {
			throw new WidgetNotFoundError(42);
		});
		const runtime = createMockRuntime({ updateWidget });

		const handle = animate(runtime, {
			targets: [{ widget: 42 as WidgetId, property: 'style.opacity', from: 1, to: 0 }],
			duration: 0.1,
			easing: Easing.linear,
		});

		// Wait for the animation frame to fire.
		return new Promise<void>((resolve) => {
			requestAnimationFrame(() => {
				// Animation should have completed silently (no throw).
				expect(handle.isComplete).toBe(true);
				resolve();
			});
		});
	});

	it('cancels silently when widget is disposed mid-animation (UIError with Disposed code)', () => {
		const updateWidget = vi.fn().mockImplementationOnce(() => {
			throw new UIError(UIErrorCode.Disposed, 'Widget disposed');
		});
		const runtime = createMockRuntime({ updateWidget });

		const handle = animate(runtime, {
			targets: [{ widget: 42 as WidgetId, property: 'style.opacity', from: 1, to: 0 }],
			duration: 0.1,
			easing: Easing.linear,
		});

		return new Promise<void>((resolve) => {
			requestAnimationFrame(() => {
				expect(handle.isComplete).toBe(true);
				resolve();
			});
		});
	});

	it('rethrows unexpected errors from the interpolation path', () => {
		const unexpectedError = new Error('Unexpected interpolation failure');
		const updateWidget = vi.fn().mockImplementationOnce(() => {
			throw unexpectedError;
		});
		const runtime = createMockRuntime({ updateWidget });

		// Stub requestAnimationFrame to invoke callback synchronously
		vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
			callback(0);
			return 0;
		});

		// The error should propagate synchronously
		expect(() => {
			animate(runtime, {
				targets: [{ widget: 42 as WidgetId, property: 'style.opacity', from: 1, to: 0 }],
				duration: 0.1,
				easing: Easing.linear,
			});
		}).toThrow('Unexpected interpolation failure');

		vi.unstubAllGlobals();
	});
});
