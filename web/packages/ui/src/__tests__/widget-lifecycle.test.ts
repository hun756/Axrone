import { describe, expect, it, vi } from 'vitest';
import { WidgetLifecycleManager } from '../runtime/lifecycle/WidgetLifecycleManager';
import type { WidgetLifecycleHost } from '../runtime/lifecycle/WidgetLifecycleManager';

const makeMockHost = (): WidgetLifecycleHost => ({
	allocate: vi.fn(() => 1),
	requireWidget: vi.fn(() => 0),
	isAncestor: vi.fn(() => false),
	detachNode: vi.fn(),
	refreshDepths: vi.fn(),
	markTreeChanged: vi.fn(),
	destroyNode: vi.fn(),
	normalizeRecord: vi.fn(() => ({} as never)),
	applyRecord: vi.fn(),
	updateFlags: vi.fn(),
	compileStyle: vi.fn(() => ({} as never)),
	compileText: vi.fn(() => null),
	compileImage: vi.fn(() => null),
	compileFocus: vi.fn(() => ({} as never)),
	createControllerContext: vi.fn(() => ({})),
	measureContent: vi.fn(() => ({ width: 0, height: 0 })),
	measureImageContent: vi.fn(() => ({ width: 0, height: 0 })),
	writeBox: vi.fn(),
	readBox: vi.fn(() => ({
		x: 0, y: 0, width: 0, height: 0,
		contentX: 0, contentY: 0, contentWidth: 0, contentHeight: 0,
	})),
	renderFrame: vi.fn(() => ({ commands: [], metrics: { widgetCount: 0, fps: 0, layoutPasses: 0 } })),
	resolveImageCommand: vi.fn(() => null),
});

describe('@axrone/ui WidgetLifecycleManager', () => {
	it('delegates allocate to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		manager.allocate();
		expect(host.allocate).toHaveBeenCalledTimes(1);
	});

	it('delegates requireWidget to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		manager.requireWidget(42);
		expect(host.requireWidget).toHaveBeenCalledWith(42);
	});

	it('delegates isAncestor to the host', () => {
		const host = makeMockHost();
		host.isAncestor.mockReturnValue(true);
		const manager = new WidgetLifecycleManager(host);
		expect(manager.isAncestor(1, 2)).toBe(true);
		expect(host.isAncestor).toHaveBeenCalledWith(1, 2);
	});

	it('delegates detachNode to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		manager.detachNode(5);
		expect(host.detachNode).toHaveBeenCalledWith(5);
	});

	it('delegates refreshDepths to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		manager.refreshDepths(5, 3);
		expect(host.refreshDepths).toHaveBeenCalledWith(5, 3);
	});

	it('delegates markTreeChanged to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		manager.markTreeChanged(7);
		expect(host.markTreeChanged).toHaveBeenCalledWith(7);
	});

	it('delegates destroyNode to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		manager.destroyNode(3);
		expect(host.destroyNode).toHaveBeenCalledWith(3);
	});

	it('delegates normalizeRecord to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		const config = { type: 'badge' } as never;
		manager.normalizeRecord(config);
		expect(host.normalizeRecord).toHaveBeenCalledWith(config);
	});

	it('delegates applyRecord to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		manager.applyRecord(1, null, null, true);
		expect(host.applyRecord).toHaveBeenCalledWith(1, null, null, true);
	});

	it('delegates updateFlags to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		manager.updateFlags(2);
		expect(host.updateFlags).toHaveBeenCalledWith(2);
	});

	it('delegates compileStyle to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		const input = {} as never;
		manager.compileStyle(input);
		expect(host.compileStyle).toHaveBeenCalledWith(input);
	});

	it('delegates compileText to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		const input = {} as never;
		const color = { r: 1, g: 1, b: 1, a: 1 };
		manager.compileText(input, color);
		expect(host.compileText).toHaveBeenCalledWith(input, color);
	});

	it('delegates compileImage to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		const input = {} as never;
		manager.compileImage(input);
		expect(host.compileImage).toHaveBeenCalledWith(input);
	});

	it('delegates compileFocus to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		const input = {} as never;
		manager.compileFocus(input, true);
		expect(host.compileFocus).toHaveBeenCalledWith(input, true);
	});

	it('delegates createControllerContext to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		manager.createControllerContext(5);
		expect(host.createControllerContext).toHaveBeenCalledWith(5);
	});

	it('delegates measureContent to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		const constraints = { width: 100, height: 50 };
		manager.measureContent(3, constraints);
		expect(host.measureContent).toHaveBeenCalledWith(3, constraints);
	});

	it('delegates measureImageContent to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		const image = {} as never;
		const constraints = { width: 100, height: 50 };
		manager.measureImageContent(image, constraints);
		expect(host.measureImageContent).toHaveBeenCalledWith(image, constraints);
	});

	it('delegates writeBox and readBox to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		const box = { x: 1, y: 2, width: 3, height: 4, contentX: 1, contentY: 2, contentWidth: 3, contentHeight: 4 };
		manager.writeBox(1, box);
		expect(host.writeBox).toHaveBeenCalledWith(1, box);
		manager.readBox(1);
		expect(host.readBox).toHaveBeenCalledWith(1);
	});

	it('delegates renderFrame to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		manager.renderFrame();
		expect(host.renderFrame).toHaveBeenCalledTimes(1);
	});

	it('delegates resolveImageCommand to the host', () => {
		const host = makeMockHost();
		const manager = new WidgetLifecycleManager(host);
		const box = { x: 0, y: 0, width: 10, height: 10, contentX: 0, contentY: 0, contentWidth: 10, contentHeight: 10 };
		const image = {} as never;
		const style = {} as never;
		manager.resolveImageCommand(1, box, image, style, null, 0);
		expect(host.resolveImageCommand).toHaveBeenCalledWith(1, box, image, style, null, 0);
	});
});
