import { describe, expect, it, vi } from 'vitest';
import {
	moveFocusLinear,
	moveFocusDirectional,
	dispatchPointerEvent,
	dispatchKeyEvent,
	dispatchTextEvent,
} from '../runtime/runtime-input';
import type { UIInputDispatchHost } from '../runtime/runtime-input';
import type { FocusMoveDirection, LayoutBox, UIInputEvent, UIPointerEvent, WidgetId } from '../types';

const box = (x: number, y: number, w: number, h: number): LayoutBox => ({
	x, y, width: w, height: h,
	contentX: x, contentY: y, contentWidth: w, contentHeight: h,
});

const makeHost = (overrides: Partial<UIInputDispatchHost> = {}): UIInputDispatchHost => ({
	getPressed: vi.fn(() => null),
	setPressed: vi.fn(),
	getFocused: vi.fn(() => null),
	hitTest: vi.fn(() => null),
	updateHover: vi.fn(),
	bubbleEvent: vi.fn(() => false),
	isFocusable: vi.fn(() => true),
	setFocus: vi.fn(() => true),
	moveFocus: vi.fn(() => null),
	...overrides,
});

describe('@axrone/ui runtime-input', () => {
	describe('moveFocusLinear', () => {
		const ids = [1, 2, 3] as WidgetId[];

		it('returns null for empty candidates', () => {
			expect(moveFocusLinear([], 'forward', null, false)).toBeNull();
		});

		it('returns the first candidate when no widget is focused (forward)', () => {
			expect(moveFocusLinear(ids, 'forward', null, false)).toBe(1);
		});

		it('returns the last candidate when no widget is focused (backward)', () => {
			expect(moveFocusLinear(ids, 'backward', null, false)).toBe(3);
		});

		it('moves forward to the next candidate', () => {
			expect(moveFocusLinear(ids, 'forward', 1 as WidgetId, false)).toBe(2);
		});

		it('moves backward to the previous candidate', () => {
			expect(moveFocusLinear(ids, 'backward', 2 as WidgetId, false)).toBe(1);
		});

		it('returns null at the end without cycle', () => {
			expect(moveFocusLinear(ids, 'forward', 3 as WidgetId, false)).toBeNull();
		});

		it('wraps to the start with cycle enabled', () => {
			expect(moveFocusLinear(ids, 'forward', 3 as WidgetId, true)).toBe(1);
		});

		it('wraps to the end with cycle enabled (backward)', () => {
			expect(moveFocusLinear(ids, 'backward', 1 as WidgetId, true)).toBe(3);
		});

		it('falls back to first when focused is not in the list', () => {
			expect(moveFocusLinear(ids, 'forward', 99 as WidgetId, false)).toBe(1);
		});
	});

	describe('moveFocusDirectional', () => {
		const readBox = (id: number): LayoutBox => {
			const boxes: Record<number, LayoutBox> = {
				1: box(0, 0, 50, 50),
				2: box(100, 0, 50, 50),
				3: box(0, 100, 50, 50),
				4: box(100, 100, 50, 50),
			};
			return boxes[id] ?? box(0, 0, 0, 0);
		};
		const ids = [1, 2, 3, 4] as WidgetId[];

		it('returns null for empty candidates', () => {
			expect(moveFocusDirectional([], 'right', null, readBox)).toBeNull();
		});

		it('moves right to the nearest widget on the right', () => {
			expect(moveFocusDirectional(ids, 'right', 1 as WidgetId, readBox)).toBe(2);
		});

		it('moves left to the nearest widget on the left', () => {
			expect(moveFocusDirectional(ids, 'left', 2 as WidgetId, readBox)).toBe(1);
		});

		it('moves down to the nearest widget below', () => {
			expect(moveFocusDirectional(ids, 'down', 1 as WidgetId, readBox)).toBe(3);
		});

		it('moves up to the nearest widget above', () => {
			expect(moveFocusDirectional(ids, 'up', 3 as WidgetId, readBox)).toBe(1);
		});

		it('returns null when no widget is in the direction', () => {
			expect(moveFocusDirectional(ids, 'right', 2 as WidgetId, readBox)).toBeNull();
		});
	});

	describe('dispatchPointerEvent', () => {
		it('sets pressed and focuses on pointer down', () => {
			const host = makeHost({
				hitTest: vi.fn(() => 10),
				isFocusable: vi.fn(() => true),
			});
			const event: UIPointerEvent = { type: 'pointer', phase: 'down', x: 50, y: 50 } as UIPointerEvent;

			const result = dispatchPointerEvent(host, event);

			expect(host.setPressed).toHaveBeenCalledWith(10);
			expect(host.setFocus).toHaveBeenCalledWith(10, 'pointer');
			expect(host.bubbleEvent).toHaveBeenCalledWith(10, event);
			expect(result).toBe(false);
		});

		it('clears pressed on pointer up', () => {
			const host = makeHost({
				getPressed: vi.fn(() => 10),
				hitTest: vi.fn(() => 10),
			});
			const event: UIPointerEvent = { type: 'pointer', phase: 'up', x: 50, y: 50 } as UIPointerEvent;

			dispatchPointerEvent(host, event);

			expect(host.setPressed).toHaveBeenCalledWith(null);
		});

		it('updates hover on pointer move', () => {
			const host = makeHost({
				hitTest: vi.fn(() => 10),
			});
			const event: UIPointerEvent = { type: 'pointer', phase: 'move', x: 50, y: 50 } as UIPointerEvent;

			dispatchPointerEvent(host, event);

			expect(host.updateHover).toHaveBeenCalledWith(10, event);
		});

		it('bubbles wheel event to the hit target', () => {
			const host = makeHost({
				hitTest: vi.fn(() => 10),
				bubbleEvent: vi.fn(() => true),
			});
			const event = { type: 'pointer', phase: 'wheel', x: 50, y: 50, deltaX: 0, deltaY: 10 } as never;

			const result = dispatchPointerEvent(host, event);

			expect(host.bubbleEvent).toHaveBeenCalledWith(10, event);
			expect(result).toBe(true);
		});

		it('returns false when no target is hit on down', () => {
			const host = makeHost({ hitTest: vi.fn(() => null) });
			const event: UIPointerEvent = { type: 'pointer', phase: 'down', x: 50, y: 50 } as UIPointerEvent;

			const result = dispatchPointerEvent(host, event);

			expect(result).toBe(false);
		});
	});

	describe('dispatchKeyEvent', () => {
		it('routes key events to the focused widget', () => {
			const host = makeHost({
				getFocused: vi.fn(() => 10),
				bubbleEvent: vi.fn(() => true),
			});
			const event = { type: 'key', phase: 'down', key: 'a' } as never;

			const result = dispatchKeyEvent(host, event);

			expect(host.bubbleEvent).toHaveBeenCalledWith(10, event);
			expect(result).toBe(true);
		});

		it('moves focus forward on Tab', () => {
			const host = makeHost({
				getFocused: vi.fn(() => null),
				moveFocus: vi.fn(() => 20),
			});
			const event = { type: 'key', phase: 'down', key: 'Tab', shiftKey: false } as never;

			const result = dispatchKeyEvent(host, event);

			expect(host.moveFocus).toHaveBeenCalledWith('forward');
			expect(result).toBe(true);
		});

		it('moves focus backward on Shift+Tab', () => {
			const host = makeHost({
				getFocused: vi.fn(() => null),
				moveFocus: vi.fn(() => 20),
			});
			const event = { type: 'key', phase: 'down', key: 'Tab', shiftKey: true } as never;

			dispatchKeyEvent(host, event);

			expect(host.moveFocus).toHaveBeenCalledWith('backward');
		});

		it('moves focus directionally on arrow keys', () => {
			const host = makeHost({
				getFocused: vi.fn(() => null),
				moveFocus: vi.fn(() => 20),
			});

			dispatchKeyEvent(host, { type: 'key', phase: 'down', key: 'ArrowRight' } as never);
			expect(host.moveFocus).toHaveBeenCalledWith('right');

			dispatchKeyEvent(host, { type: 'key', phase: 'down', key: 'ArrowLeft' } as never);
			expect(host.moveFocus).toHaveBeenCalledWith('left');

			dispatchKeyEvent(host, { type: 'key', phase: 'down', key: 'ArrowUp' } as never);
			expect(host.moveFocus).toHaveBeenCalledWith('up');

			dispatchKeyEvent(host, { type: 'key', phase: 'down', key: 'ArrowDown' } as never);
			expect(host.moveFocus).toHaveBeenCalledWith('down');
		});

		it('returns false when no focused widget handles the event and no navigation key', () => {
			const host = makeHost({
				getFocused: vi.fn(() => null),
				bubbleEvent: vi.fn(() => false),
			});
			const event = { type: 'key', phase: 'down', key: 'a' } as never;

			expect(dispatchKeyEvent(host, event)).toBe(false);
		});
	});

	describe('dispatchTextEvent', () => {
		it('routes text input to the focused widget', () => {
			const host = makeHost({
				getFocused: vi.fn(() => 10),
				bubbleEvent: vi.fn(() => true),
			});
			const event = { type: 'text', text: 'A' } as never;

			expect(dispatchTextEvent(host, event)).toBe(true);
			expect(host.bubbleEvent).toHaveBeenCalledWith(10, event);
		});

		it('returns false when no widget is focused', () => {
			const host = makeHost({ getFocused: vi.fn(() => null) });
			const event = { type: 'text', text: 'A' } as never;

			expect(dispatchTextEvent(host, event)).toBe(false);
		});
	});
});
