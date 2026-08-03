import { describe, expect, it, vi } from 'vitest';
import { dispatchWorldPointerToUIRuntime } from '../world-input';
import type { UIWorldHit, UIWorldPointerEvent } from '../world-input';

const makeHit = (u: number, v: number, distance = 1): UIWorldHit => ({ u, v, distance });

const makeRuntime = () => ({
	dispatchInput: vi.fn(),
});

describe('dispatchWorldPointerToUIRuntime', () => {
	it('returns false when runtime is null', () => {
		const hit = makeHit(0.5, 0.5);
		const event: UIWorldPointerEvent = { phase: 'down' };
		expect(dispatchWorldPointerToUIRuntime(null, hit, event, 800, 600)).toBe(false);
	});

	it('dispatches mapped canvas coordinates on hit', () => {
		const runtime = makeRuntime();
		const hit = makeHit(0.5, 0.25);
		const event: UIWorldPointerEvent = { phase: 'down', pointerId: 2, button: 0 };

		const result = dispatchWorldPointerToUIRuntime(runtime as never, hit, event, 800, 600);

		expect(result).toBe(true);
		expect(runtime.dispatchInput).toHaveBeenCalledTimes(1);
		const dispatched = runtime.dispatchInput.mock.calls[0][0];
		expect(dispatched.type).toBe('pointer');
		expect(dispatched.phase).toBe('down');
		expect(dispatched.x).toBeCloseTo(400); // 0.5 * 800
		expect(dispatched.y).toBeCloseTo(150); // 0.25 * 600
		expect(dispatched.pointerId).toBe(2);
	});

	it('dispatches a move to (-1, -1) on miss to clear hover', () => {
		const runtime = makeRuntime();
		const event: UIWorldPointerEvent = { phase: 'leave' };

		const result = dispatchWorldPointerToUIRuntime(runtime as never, null, event, 800, 600);

		expect(result).toBe(false);
		expect(runtime.dispatchInput).toHaveBeenCalledTimes(1);
		const dispatched = runtime.dispatchInput.mock.calls[0][0];
		// A miss must become a 'move' (not 'leave') at a point outside the canvas.
		expect(dispatched.phase).toBe('move');
		expect(dispatched.x).toBe(-1);
		expect(dispatched.y).toBe(-1);
	});

	it('uses the event phase directly when there is a hit', () => {
		const runtime = makeRuntime();
		const hit = makeHit(0.1, 0.9);

		dispatchWorldPointerToUIRuntime(runtime as never, hit, { phase: 'up' }, 100, 100);

		const dispatched = runtime.dispatchInput.mock.calls[0][0];
		expect(dispatched.phase).toBe('up');
		expect(dispatched.x).toBeCloseTo(10); // 0.1 * 100
		expect(dispatched.y).toBeCloseTo(90);  // 0.9 * 100
	});

	it('defaults pointerId to 1 and button to 0', () => {
		const runtime = makeRuntime();
		const hit = makeHit(0.5, 0.5);

		dispatchWorldPointerToUIRuntime(runtime as never, hit, { phase: 'move' }, 200, 200);

		const dispatched = runtime.dispatchInput.mock.calls[0][0];
		expect(dispatched.pointerId).toBe(1);
		expect(dispatched.button).toBe(0);
	});

	it('sets buttons=1 on down phase when no buttons are specified', () => {
		const runtime = makeRuntime();
		const hit = makeHit(0.5, 0.5);

		dispatchWorldPointerToUIRuntime(runtime as never, hit, { phase: 'down' }, 200, 200);

		const dispatched = runtime.dispatchInput.mock.calls[0][0];
		expect(dispatched.buttons).toBe(1);
	});

	it('sets buttons=0 on non-down phase when no buttons are specified', () => {
		const runtime = makeRuntime();
		const hit = makeHit(0.5, 0.5);

		dispatchWorldPointerToUIRuntime(runtime as never, hit, { phase: 'up' }, 200, 200);

		const dispatched = runtime.dispatchInput.mock.calls[0][0];
		expect(dispatched.buttons).toBe(0);
	});
});
