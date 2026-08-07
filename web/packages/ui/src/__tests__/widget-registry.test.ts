import { describe, expect, it } from 'vitest';
import { UIError, UIErrorCode, WidgetRegistry, defineWidget, createWidgetFactory } from '../index';

describe('@axrone/ui WidgetRegistry', () => {
	const makeController = (type: string) => ({ type });

	it('registers and resolves a controller by type', () => {
		const registry = new WidgetRegistry();
		const controller = makeController('badge');
		registry.register(controller);

		expect(registry.has('badge')).toBe(true);
		expect(registry.resolve('badge')).toBe(controller);
	});

	it('returns null for unknown or empty types', () => {
		const registry = new WidgetRegistry();
		expect(registry.resolve('nope')).toBeNull();
		expect(registry.resolve(null)).toBeNull();
		expect(registry.resolve(undefined)).toBeNull();
		expect(registry.resolve('')).toBeNull();
	});

	it('throws UIError on duplicate registration', () => {
		const registry = new WidgetRegistry();
		registry.register(makeController('badge'));
		expect(() => registry.register(makeController('badge'))).toThrowError(UIError);
	});

	it('replaces an existing controller without throwing', () => {
		const registry = new WidgetRegistry();
		const first = makeController('badge');
		const second = makeController('badge');
		registry.register(first);
		registry.replace(second);

		expect(registry.resolve('badge')).toBe(second);
	});

	it('deletes a controller and returns the deletion status', () => {
		const registry = new WidgetRegistry();
		registry.register(makeController('badge'));
		expect(registry.delete('badge')).toBe(true);
		expect(registry.has('badge')).toBe(false);
		expect(registry.delete('badge')).toBe(false);
	});

	it('clears all controllers', () => {
		const registry = new WidgetRegistry();
		registry.register(makeController('a'));
		registry.register(makeController('b'));
		registry.clear();

		expect(registry.has('a')).toBe(false);
		expect(registry.has('b')).toBe(false);
	});

	it('iterates over registered controllers via values()', () => {
		const registry = new WidgetRegistry();
		const a = makeController('a');
		const b = makeController('b');
		registry.register(a);
		registry.register(b);

		const collected = [...registry.values()];
		expect(collected).toHaveLength(2);
		expect(collected).toContain(a);
		expect(collected).toContain(b);
	});

	it('returns this from register and replace for chaining', () => {
		const registry = new WidgetRegistry();
		const result = registry.register(makeController('a'));
		expect(result).toBe(registry);
		const replaced = registry.replace(makeController('a'));
		expect(replaced).toBe(registry);
	});
});

describe('defineWidget', () => {
	it('returns the same controller reference', () => {
		const controller = { type: 'test' as const };
		expect(defineWidget(controller)).toBe(controller);
	});
});

describe('createWidgetFactory', () => {
	it('creates a config with the given type', () => {
		const factory = createWidgetFactory('badge');
		const config = factory();
		expect(config.controller).toBe('badge');
		expect(config.props).toEqual({});
	});

	it('merges defaults with overrides', () => {
		const factory = createWidgetFactory('badge', { color: 'red' });
		const config = factory({ color: 'blue', size: 10 });
		expect(config.props).toEqual({ color: 'blue', size: 10 });
	});

	it('uses defaults when no overrides are provided', () => {
		const factory = createWidgetFactory('badge', { color: 'red' });
		const config = factory();
		expect(config.props).toEqual({ color: 'red' });
	});

	it('handles undefined defaults and undefined overrides', () => {
		const factory = createWidgetFactory('badge');
		const config = factory(undefined);
		expect(config.props).toEqual({});
	});
});
