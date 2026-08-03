import { describe, expect, it, vi } from 'vitest';
import { resolveUIFrame, renderUIFrame, createRuntimeFrameSource } from '../render';
import type { UIFrame, UIFrameSource, SizeLike } from '../types';

const viewport: SizeLike = { width: 800, height: 600 };

const makeFrame = (commandCount = 1): UIFrame => ({
	commands: Array.from({ length: commandCount }, (_, i) => ({
		kind: 'quad' as const,
		widget: i,
		x: 0, y: 0, width: 10, height: 10,
		zIndex: 0,
		color: { r: 1, g: 1, b: 1, a: 1 },
		borderColor: { r: 0, g: 0, b: 0, a: 0 },
		borderWidth: 0,
		radius: { topLeft: 0, topRight: 0, bottomRight: 0, bottomLeft: 0 },
		opacity: 1,
		clip: null,
	})),
	metrics: { widgetCount: commandCount, fps: 60, layoutPasses: 1 },
});

describe('@axrone/ui render helpers', () => {
	describe('resolveUIFrame', () => {
		it('returns a direct frame object', () => {
			const frame = makeFrame(2);
			expect(resolveUIFrame(frame, viewport)).toBe(frame);
		});

		it('calls a function producer', () => {
			const frame = makeFrame();
			const producer = vi.fn(() => frame);
			const result = resolveUIFrame(producer, viewport);
			expect(result).toBe(frame);
			expect(producer).toHaveBeenCalledWith(viewport);
		});

		it('calls getFrame on a source producer', () => {
			const frame = makeFrame();
			const source: UIFrameSource = { getFrame: vi.fn(() => frame) };
			const result = resolveUIFrame(source, viewport);
			expect(result).toBe(frame);
			expect(source.getFrame).toHaveBeenCalledWith(viewport);
		});

		it('returns null when a function producer returns null', () => {
			const producer = () => null;
			expect(resolveUIFrame(producer, viewport)).toBeNull();
		});

		it('returns null when a source producer returns null', () => {
			const source: UIFrameSource = { getFrame: () => null };
			expect(resolveUIFrame(source, viewport)).toBeNull();
		});

		it('returns null for a non-frame object', () => {
			expect(resolveUIFrame({} as never, viewport)).toBeNull();
		});
	});

	describe('renderUIFrame', () => {
		it('calls sink.render with the resolved frame', () => {
			const frame = makeFrame();
			const sink = { render: vi.fn() };
			const result = renderUIFrame(sink, frame, viewport);
			expect(sink.render).toHaveBeenCalledWith(frame);
			expect(result).toBe(frame);
		});

		it('returns null and does not call render for unresolvable producer', () => {
			const sink = { render: vi.fn() };
			const producer = () => null;
			const result = renderUIFrame(sink, producer, viewport);
			expect(result).toBeNull();
			expect(sink.render).not.toHaveBeenCalled();
		});
	});

	describe('createRuntimeFrameSource', () => {
		it('delegates getFrame to runtime.commit', () => {
			const frame = makeFrame();
			const runtime = { commit: vi.fn(() => frame) };
			const source = createRuntimeFrameSource(runtime);
			expect(source.runtime).toBe(runtime);
			expect(source.getFrame(viewport)).toBe(frame);
			expect(runtime.commit).toHaveBeenCalledWith(viewport);
		});

		it('returns null when runtime.commit returns null', () => {
			const runtime = { commit: vi.fn(() => null) };
			const source = createRuntimeFrameSource(runtime);
			expect(source.getFrame(viewport)).toBeNull();
		});
	});
});
