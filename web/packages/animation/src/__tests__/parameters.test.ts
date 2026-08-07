import { describe, expect, it } from 'vitest';
import { AnimationParameterStore } from '../parameters';
import { AnimationStateMachineError, AnimationValidationError } from '../errors';

describe('AnimationParameterStore constructor validation', () => {
    it('throws on empty parameter name', () => {
        expect(() => new AnimationParameterStore([{ name: '', kind: 'float' }])).toThrow(
            AnimationValidationError
        );
    });

    it('throws on duplicate parameter name', () => {
        expect(
            () =>
                new AnimationParameterStore([
                    { name: 'speed', kind: 'float' },
                    { name: 'speed', kind: 'int' },
                ])
        ).toThrow(AnimationValidationError);
    });

    it('throws on unsupported parameter kind', () => {
        expect(
            () => new AnimationParameterStore([{ name: 'x', kind: 'vector' as never }])
        ).toThrow(AnimationValidationError);
    });

    it('accepts an empty definition array', () => {
        const store = new AnimationParameterStore();
        expect(store.definitions).toHaveLength(0);
    });
});

describe('AnimationParameterStore default values', () => {
    it('defaults float and int to 0 when no defaultValue is provided', () => {
        const store = new AnimationParameterStore([
            { name: 'f', kind: 'float' },
            { name: 'i', kind: 'int' },
        ]);
        expect(store.get('f')).toBe(0);
        expect(store.get('i')).toBe(0);
    });

    it('defaults bool and trigger to false when no defaultValue is provided', () => {
        const store = new AnimationParameterStore([
            { name: 'b', kind: 'bool' },
            { name: 't', kind: 'trigger' },
        ]);
        expect(store.get('b')).toBe(false);
        expect(store.get('t')).toBe(false);
    });

    it('applies finite float defaultValue correctly', () => {
        const store = new AnimationParameterStore([{ name: 'speed', kind: 'float', defaultValue: 3.5 }]);
        expect(store.get('speed')).toBe(3.5);
    });

    it('applies finite int defaultValue correctly', () => {
        const store = new AnimationParameterStore([{ name: 'count', kind: 'int', defaultValue: 7 }]);
        expect(store.get('count')).toBe(7);
    });

    it('applies true defaultValue for bool', () => {
        const store = new AnimationParameterStore([{ name: 'active', kind: 'bool', defaultValue: true }]);
        expect(store.get('active')).toBe(true);
    });

    it('falls back to 0 for non-finite float defaultValue', () => {
        const store = new AnimationParameterStore([
            { name: 'nan', kind: 'float', defaultValue: NaN },
            { name: 'inf', kind: 'float', defaultValue: Infinity },
        ]);
        expect(store.get('nan')).toBe(0);
        expect(store.get('inf')).toBe(0);
    });
});

describe('AnimationParameterStore get/set for all kinds', () => {
    it('float get/set returns exact value', () => {
        const store = new AnimationParameterStore([{ name: 'f', kind: 'float' }]);
        store.set('f', 2.75);
        expect(store.get('f')).toBe(2.75);
    });

    it('int get/set truncates to integer', () => {
        const store = new AnimationParameterStore([{ name: 'i', kind: 'int' }]);
        store.set('i', 3.9);
        expect(store.get('i')).toBe(3);
    });

    it('int truncates negative values toward zero', () => {
        const store = new AnimationParameterStore([{ name: 'i', kind: 'int' }]);
        store.set('i', -2.7);
        expect(store.get('i')).toBe(-2);
    });

    it('bool get/set coerces truthy to true', () => {
        const store = new AnimationParameterStore([{ name: 'b', kind: 'bool' }]);
        store.set('b', true);
        expect(store.get('b')).toBe(true);
        store.set('b', false);
        expect(store.get('b')).toBe(false);
    });

    it('trigger get returns boolean state', () => {
        const store = new AnimationParameterStore([{ name: 't', kind: 'trigger' }]);
        expect(store.get('t')).toBe(false);
        store.set('t', true);
        expect(store.get('t')).toBe(true);
    });
});

describe('AnimationParameterStore NaN and non-finite handling', () => {
    it('set float NaN yields 0', () => {
        const store = new AnimationParameterStore([{ name: 'f', kind: 'float' }]);
        store.set('f', NaN);
        expect(store.get('f')).toBe(0);
    });

    it('set float Infinity yields 0', () => {
        const store = new AnimationParameterStore([{ name: 'f', kind: 'float' }]);
        store.set('f', Infinity);
        expect(store.get('f')).toBe(0);
    });

    it('set float -Infinity yields 0', () => {
        const store = new AnimationParameterStore([{ name: 'f', kind: 'float' }]);
        store.set('f', -Infinity);
        expect(store.get('f')).toBe(0);
    });

    it('set int NaN yields 0', () => {
        const store = new AnimationParameterStore([{ name: 'i', kind: 'int' }]);
        store.set('i', NaN);
        expect(store.get('i')).toBe(0);
    });

    it('set int Infinity yields 0', () => {
        const store = new AnimationParameterStore([{ name: 'i', kind: 'int' }]);
        store.set('i', -Infinity);
        expect(store.get('i')).toBe(0);
    });
});

describe('AnimationParameterStore typed setters', () => {
    it('setFloat delegates to set', () => {
        const store = new AnimationParameterStore([{ name: 'f', kind: 'float' }]);
        store.setFloat('f', 1.5);
        expect(store.get('f')).toBe(1.5);
    });

    it('setInt delegates to set with truncation', () => {
        const store = new AnimationParameterStore([{ name: 'i', kind: 'int' }]);
        store.setInt('i', 4.8);
        expect(store.get('i')).toBe(4);
    });

    it('setBool delegates to set', () => {
        const store = new AnimationParameterStore([{ name: 'b', kind: 'bool' }]);
        store.setBool('b', true);
        expect(store.get('b')).toBe(true);
    });

    it('setTrigger sets trigger to true', () => {
        const store = new AnimationParameterStore([{ name: 't', kind: 'trigger' }]);
        store.setTrigger('t');
        expect(store.get('t')).toBe(true);
    });

    it('resetTrigger sets trigger to false', () => {
        const store = new AnimationParameterStore([{ name: 't', kind: 'trigger' }]);
        store.setTrigger('t');
        store.resetTrigger('t');
        expect(store.get('t')).toBe(false);
    });

    it('resetTrigger on non-trigger throws AnimationStateMachineError', () => {
        const store = new AnimationParameterStore([{ name: 'f', kind: 'float' }]);
        expect(() => store.resetTrigger('f')).toThrow(AnimationStateMachineError);
    });
});

describe('AnimationParameterStore consumeTrigger', () => {
    it('returns true if trigger was set and resets it', () => {
        const store = new AnimationParameterStore([{ name: 't', kind: 'trigger' }]);
        store.setTrigger('t');
        expect(store.consumeTrigger('t')).toBe(true);
        expect(store.get('t')).toBe(false);
    });

    it('returns false if trigger was not set', () => {
        const store = new AnimationParameterStore([{ name: 't', kind: 'trigger' }]);
        expect(store.consumeTrigger('t')).toBe(false);
    });

    it('throws on non-trigger parameter', () => {
        const store = new AnimationParameterStore([{ name: 'f', kind: 'float' }]);
        expect(() => store.consumeTrigger('f')).toThrow(AnimationStateMachineError);
    });
});

describe('AnimationParameterStore clearTriggers', () => {
    it('resets all trigger parameters and leaves float/bool untouched', () => {
        const store = new AnimationParameterStore([
            { name: 'f', kind: 'float', defaultValue: 5 },
            { name: 'b', kind: 'bool', defaultValue: true },
            { name: 't1', kind: 'trigger' },
            { name: 't2', kind: 'trigger' },
        ]);
        store.setTrigger('t1');
        store.setTrigger('t2');

        store.clearTriggers();

        expect(store.get('t1')).toBe(false);
        expect(store.get('t2')).toBe(false);
        expect(store.get('f')).toBe(5);
        expect(store.get('b')).toBe(true);
    });
});

describe('AnimationParameterStore copyFrom', () => {
    it('copies all values from another store', () => {
        const source = new AnimationParameterStore([
            { name: 'f', kind: 'float', defaultValue: 3 },
            { name: 'b', kind: 'bool', defaultValue: true },
        ]);
        const target = new AnimationParameterStore([
            { name: 'f', kind: 'float' },
            { name: 'b', kind: 'bool' },
        ]);

        target.copyFrom(source);
        expect(target.get('f')).toBe(3);
        expect(target.get('b')).toBe(true);
    });

    it('throws on layout length mismatch', () => {
        const source = new AnimationParameterStore([{ name: 'a', kind: 'float' }]);
        const target = new AnimationParameterStore([
            { name: 'a', kind: 'float' },
            { name: 'b', kind: 'float' },
        ]);
        expect(() => target.copyFrom(source)).toThrow(AnimationStateMachineError);
    });
});

describe('AnimationParameterStore snapshot', () => {
    it('returns correct key-value map for all kinds', () => {
        const store = new AnimationParameterStore([
            { name: 'speed', kind: 'float', defaultValue: 2.5 },
            { name: 'count', kind: 'int', defaultValue: 10 },
            { name: 'active', kind: 'bool', defaultValue: true },
            { name: 'fire', kind: 'trigger' },
        ]);
        store.setTrigger('fire');

        const snap = store.snapshot();
        expect(snap).toEqual({
            speed: 2.5,
            count: 10,
            active: true,
            fire: true,
        });
    });
});

describe('AnimationParameterStore has and getKind', () => {
    it('has returns true for existing parameters', () => {
        const store = new AnimationParameterStore([{ name: 'x', kind: 'float' }]);
        expect(store.has('x')).toBe(true);
        expect(store.has('y')).toBe(false);
    });

    it('getKind returns correct kind string', () => {
        const store = new AnimationParameterStore([
            { name: 'f', kind: 'float' },
            { name: 'i', kind: 'int' },
            { name: 'b', kind: 'bool' },
            { name: 't', kind: 'trigger' },
        ]);
        expect(store.getKind('f')).toBe('float');
        expect(store.getKind('i')).toBe('int');
        expect(store.getKind('b')).toBe('bool');
        expect(store.getKind('t')).toBe('trigger');
    });
});

describe('AnimationParameterStore unknown parameter errors', () => {
    it('get with unknown name throws AnimationStateMachineError', () => {
        const store = new AnimationParameterStore([]);
        expect(() => store.get('missing')).toThrow(AnimationStateMachineError);
    });

    it('set with unknown name throws AnimationStateMachineError', () => {
        const store = new AnimationParameterStore([]);
        expect(() => store.set('missing', 1)).toThrow(AnimationStateMachineError);
    });

    it('getKind with unknown name throws AnimationStateMachineError', () => {
        const store = new AnimationParameterStore([]);
        expect(() => store.getKind('missing')).toThrow(AnimationStateMachineError);
    });
});
