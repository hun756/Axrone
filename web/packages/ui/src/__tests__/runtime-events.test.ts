import { describe, expect, it, vi } from 'vitest';
import { UIRuntime } from '../runtime';
import { deserializeUIAsset } from '../runtime/ui-asset-io';

const createSimpleAssetJson = (): string =>
	JSON.stringify({
		id: 'ui.reentrancy-test',
		name: 'reentrancy-test',
		version: 1,
		canvas: { referenceWidth: 400, referenceHeight: 200, scaleMode: 'fixed', matchBias: 0.5 },
		bindings: { root: 'root', target: 'target' },
		root: {
			role: 'root',
			key: 'root',
			enabled: true,
			interactive: false,
			layout: { display: 'overlay', width: '100%', height: '100%' },
			children: [
				{
					role: 'container',
					key: 'target',
					enabled: true,
					interactive: true,
					layout: { width: 100, height: 50 },
					children: [],
				},
			],
		},
	});

const createRuntime = (): UIRuntime => {
	const runtime = new UIRuntime();
	runtime.loadFromAsset(deserializeUIAsset(createSimpleAssetJson()));
	runtime.commit();
	return runtime;
};

describe('@axrone/ui runtime emitControllerEvent re-entrancy', () => {
	it('handles listener removing itself during iteration', () => {
		const runtime = createRuntime();
		const widget = runtime.getBoundWidget('target')!;
		const calls: string[] = [];

		const listenerA = vi.fn(() => {
			calls.push('A');
		});
		const listenerB = vi.fn(() => {
			calls.push('B');
			// Remove listenerA during iteration — should not break iteration.
			runtime.offControllerEvent(widget, 'test', listenerA);
		});

		runtime.onControllerEvent(widget, 'test', listenerA);
		runtime.onControllerEvent(widget, 'test', listenerB);

		runtime.emitControllerEvent(widget, 'test', null);

		// Both listeners should have been called in this emission.
		expect(calls).toEqual(['A', 'B']);

		// After removal, listenerA should not be called again.
		calls.length = 0;
		runtime.emitControllerEvent(widget, 'test', null);
		expect(calls).toEqual(['B']);
	});

	it('handles listener adding a new listener during iteration', () => {
		const runtime = createRuntime();
		const widget = runtime.getBoundWidget('target')!;
		const calls: string[] = [];

		const listenerA = vi.fn(() => {
			calls.push('A');
		});
		const listenerB = vi.fn(() => {
			calls.push('B');
			// Add listenerA during iteration — should NOT be called in this emission
			// (snapshot was already taken), but should be called next time.
			runtime.onControllerEvent(widget, 'test', listenerA);
		});

		runtime.onControllerEvent(widget, 'test', listenerB);

		runtime.emitControllerEvent(widget, 'test', null);
		expect(calls).toEqual(['B']);

		// Next emission: both should be called.
		calls.length = 0;
		runtime.emitControllerEvent(widget, 'test', null);
		expect(calls).toEqual(['B', 'A']);
	});
});
