import { describe, expect, it } from 'vitest';
import { PostProcessStack, createPostProcessStack } from '../post-process';

describe('PostProcessStack', () => {
    describe('add / upsertBuiltin', () => {
        it('adds a builtin effect with default settings', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom');
            expect(stack.size).toBe(1);
        });

        it('merges partial settings with defaults', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom', { threshold: 0.5 });
            const resolved = stack.resolve();
            expect(resolved).toHaveLength(1);
            expect(resolved[0]!.settings).toMatchObject({ threshold: 0.5 });
            expect(resolved[0]!.settings).toMatchObject({ intensity: 0.65 });
        });

        it('uses default phase for builtin effects', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom');
            expect(stack.hasPhase('before-tonemap')).toBe(true);

            const stack2 = new PostProcessStack();
            stack2.upsertBuiltin('fxaa');
            expect(stack2.hasPhase('after-tonemap')).toBe(true);
        });

        it('throws for invalid builtin effect name', () => {
            const stack = new PostProcessStack();
            expect(() => stack.upsertBuiltin('nonexistent' as any)).toThrow(/INVALID_EFFECT/);
        });

        it('allows overriding phase and quality', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom', undefined, { phase: 'after-tonemap', quality: 'low' });
            const resolved = stack.resolve();
            expect(resolved[0]!.phase).toBe('after-tonemap');
            expect(resolved[0]!.quality).toBe('low');
        });
    });

    describe('upsertCustom', () => {
        it('adds a custom effect', () => {
            const stack = new PostProcessStack();
            stack.upsertCustom('my-effect', { strength: 0.5 });
            expect(stack.size).toBe(1);
        });

        it('throws for empty name', () => {
            const stack = new PostProcessStack();
            expect(() => stack.upsertCustom('', {})).toThrow(/INVALID_EFFECT/);
        });

        it('throws for whitespace-only name', () => {
            const stack = new PostProcessStack();
            expect(() => stack.upsertCustom('   ', {})).toThrow(/INVALID_EFFECT/);
        });

        it('clones settings (not reference)', () => {
            const stack = new PostProcessStack();
            const settings = { strength: 0.5 };
            stack.upsertCustom('fx', settings);
            settings.strength = 999;
            const resolved = stack.resolve();
            expect((resolved[0]!.settings as any).strength).toBe(0.5);
        });
    });

    describe('enable / disable', () => {
        it('disable removes effect from resolve output', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom');
            stack.disable('bloom');
            expect(stack.resolve()).toHaveLength(0);
            expect(stack.hasPhase('before-tonemap')).toBe(false);
        });

        it('enable re-adds effect to resolve output', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom');
            stack.disable('bloom');
            stack.enable('bloom');
            expect(stack.resolve()).toHaveLength(1);
        });

        it('enable/disable on nonexistent effect is a no-op', () => {
            const stack = new PostProcessStack();
            stack.enable('nope');
            stack.disable('nope');
            expect(stack.size).toBe(0);
        });
    });

    describe('move / remove / clear', () => {
        it('move changes effect order', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom', undefined, { order: 0 });
            stack.upsertBuiltin('ssao', undefined, { order: 1 });
            stack.move('ssao', -1);
            const resolved = stack.resolve();
            expect(resolved[0]!.name).toBe('ssao');
        });

        it('remove returns true for existing and false for missing', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom');
            expect(stack.remove('bloom')).toBe(true);
            expect(stack.remove('bloom')).toBe(false);
            expect(stack.size).toBe(0);
        });

        it('clear removes all effects', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom');
            stack.upsertBuiltin('fxaa');
            stack.clear();
            expect(stack.size).toBe(0);
        });

        it('clear on empty stack is a no-op', () => {
            const stack = new PostProcessStack();
            stack.clear();
            expect(stack.size).toBe(0);
        });
    });

    describe('resolve', () => {
        it('orders before-tonemap effects before after-tonemap', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('fxaa');
            stack.upsertBuiltin('bloom');
            const resolved = stack.resolve();
            expect(resolved[0]!.name).toBe('bloom');
            expect(resolved[1]!.name).toBe('fxaa');
        });

        it('respects maxPasses limit', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom');
            stack.upsertBuiltin('ssao');
            stack.upsertBuiltin('fxaa');
            const resolved = stack.resolve(2);
            expect(resolved).toHaveLength(2);
        });

        it('caches result when not dirty', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom');
            const r1 = stack.resolve();
            const r2 = stack.resolve();
            expect(r1).toBe(r2);
        });

        it('invalidates cache on mutation', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom');
            const r1 = stack.resolve();
            stack.upsertBuiltin('fxaa');
            const r2 = stack.resolve();
            expect(r1).not.toBe(r2);
            expect(r2).toHaveLength(2);
        });

        it('returns frozen settings', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom');
            const resolved = stack.resolve();
            expect(Object.isFrozen(resolved[0]!.settings)).toBe(true);
        });

        it('skips disabled effects', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom');
            stack.upsertBuiltin('fxaa');
            stack.disable('bloom');
            const resolved = stack.resolve();
            expect(resolved).toHaveLength(1);
            expect(resolved[0]!.name).toBe('fxaa');
        });
    });

    describe('serialize', () => {
        it('round-trips builtin effects', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom', { threshold: 0.8 });
            stack.upsertBuiltin('fxaa');
            const serialized = stack.serialize();
            expect(serialized).toHaveLength(2);
            const bloom = serialized.find((e) => e.category === 'builtin' && e.name === 'bloom');
            expect(bloom).toBeDefined();
            expect((bloom as any).settings.threshold).toBe(0.8);
        });

        it('round-trips custom effects', () => {
            const stack = new PostProcessStack();
            stack.upsertCustom('my-fx', { power: 5 });
            const serialized = stack.serialize();
            expect(serialized).toHaveLength(1);
            expect(serialized[0]!.category).toBe('custom');
            expect((serialized[0] as any).settings.power).toBe(5);
        });

        it('returns frozen array', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom');
            expect(Object.isFrozen(stack.serialize())).toBe(true);
        });
    });

    describe('hasPhase', () => {
        it('returns false when no effects in that phase', () => {
            const stack = new PostProcessStack();
            expect(stack.hasPhase('before-tonemap')).toBe(false);
            expect(stack.hasPhase('after-tonemap')).toBe(false);
        });

        it('only counts enabled effects', () => {
            const stack = new PostProcessStack();
            stack.upsertBuiltin('bloom');
            stack.disable('bloom');
            expect(stack.hasPhase('before-tonemap')).toBe(false);
        });
    });

    describe('constructor with initial effects', () => {
        it('accepts initial builtin effects', () => {
            const stack = new PostProcessStack([
                { category: 'builtin', name: 'bloom' },
            ]);
            expect(stack.size).toBe(1);
        });
    });
});

describe('createPostProcessStack', () => {
    it('returns a PostProcessStack instance', () => {
        const stack = createPostProcessStack();
        expect(stack).toBeInstanceOf(PostProcessStack);
    });

    it('accepts initial effects', () => {
        const stack = createPostProcessStack([
            { category: 'builtin', name: 'fxaa' },
        ]);
        expect(stack.size).toBe(1);
    });
});
