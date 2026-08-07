import { describe, expect, it } from 'vitest';
import { AudioListenerRegistry } from '../internal/listener-registry';
import { AudioListenerError } from '../errors';
import { FakeAudioListener } from './helpers/fake-audio-context';

describe('AudioListenerRegistry', () => {
    describe('upsert', () => {
        it('auto-activates the first listener', () => {
            const registry = new AudioListenerRegistry();
            const state = registry.upsert({ id: 'main' });
            expect(state.active).toBe(true);
            expect(registry.activeListenerId).toBe('main');
        });

        it('does not auto-activate subsequent listeners', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({ id: 'first' });
            const second = registry.upsert({ id: 'second' });
            expect(second.active).toBe(false);
            expect(registry.activeListenerId).toBe('first');
        });

        it('activates a listener when active: true is explicitly set', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({ id: 'first' });
            const second = registry.upsert({ id: 'second', active: true });
            expect(second.active).toBe(true);
            expect(registry.activeListenerId).toBe('second');

            // First should now be deactivated
            const first = registry.get('first');
            expect(first!.active).toBe(false);
        });

        it('applies field-level patch updates', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({ id: 'main', position: { x: 0, y: 0, z: 0 }, globalVolume: 1 });
            const updated = registry.upsert({
                id: 'main',
                position: { x: 5, y: 10, z: -3 },
                globalVolume: 0.5,
                dopplerFactor: 2,
            });
            expect(updated.position).toEqual({ x: 5, y: 10, z: -3 });
            expect(updated.globalVolume).toBe(0.5);
            expect(updated.dopplerFactor).toBe(2);
        });

        it('clamps globalVolume to [0, 1]', () => {
            const registry = new AudioListenerRegistry();
            const over = registry.upsert({ id: 'main', globalVolume: 5 });
            expect(over.globalVolume).toBe(1);
            const under = registry.upsert({ id: 'main', globalVolume: -1 });
            expect(under.globalVolume).toBe(0);
        });

        it('clamps dopplerFactor to [0, 5]', () => {
            const registry = new AudioListenerRegistry();
            const state = registry.upsert({ id: 'main', dopplerFactor: 10 });
            expect(state.dopplerFactor).toBe(5);
        });

        it('uses default id "default" when no id is provided', () => {
            const registry = new AudioListenerRegistry();
            const state = registry.upsert({});
            expect(state.id).toBe('default');
        });

        it('normalizes sampleRate and dspBufferSize to allowed values', () => {
            const registry = new AudioListenerRegistry();
            const state = registry.upsert({ id: 'main', sampleRate: 33333, dspBufferSize: 999 });
            expect(state.sampleRate).toBe(48000);
            expect(state.dspBufferSize).toBe(1024);
        });

        it('accepts valid sampleRate values', () => {
            const registry = new AudioListenerRegistry();
            const state = registry.upsert({ id: 'main', sampleRate: 44100 });
            expect(state.sampleRate).toBe(44100);
        });

        it('clamps reverbLevel to [0, 1]', () => {
            const registry = new AudioListenerRegistry();
            const state = registry.upsert({ id: 'main', reverbLevel: 2 });
            expect(state.reverbLevel).toBe(1);
        });

        it('clones metadata to prevent external mutation', () => {
            const registry = new AudioListenerRegistry();
            const meta = { key: 'value' };
            registry.upsert({ id: 'main', metadata: meta });
            meta.key = 'changed';
            expect(registry.get('main')!.metadata.key).toBe('value');
        });
    });

    describe('remove', () => {
        it('returns false for unknown id', () => {
            const registry = new AudioListenerRegistry();
            expect(registry.remove('nonexistent')).toBe(false);
        });

        it('returns true and removes the listener', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({ id: 'main' });
            expect(registry.remove('main')).toBe(true);
            expect(registry.get('main')).toBeUndefined();
        });

        it('activates fallback listener when active listener is removed', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({ id: 'main', active: true });
            registry.upsert({ id: 'backup', enabled: true });

            registry.remove('main');

            expect(registry.activeListenerId).toBe('backup');
            const backup = registry.get('backup');
            expect(backup!.active).toBe(true);
        });

        it('sets activeListenerId to undefined when no fallback exists', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({ id: 'main' });
            registry.remove('main');
            expect(registry.activeListenerId).toBeUndefined();
        });
    });

    describe('setActive', () => {
        it('throws AudioListenerError for unknown id', () => {
            const registry = new AudioListenerRegistry();
            expect(() => registry.setActive('nonexistent')).toThrow(AudioListenerError);
        });

        it('deactivates all other listeners', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({ id: 'a' });
            registry.upsert({ id: 'b' });
            registry.upsert({ id: 'c' });

            registry.setActive('c');

            expect(registry.get('a')!.active).toBe(false);
            expect(registry.get('b')!.active).toBe(false);
            expect(registry.get('c')!.active).toBe(true);
            expect(registry.activeListenerId).toBe('c');
        });
    });

    describe('get / list', () => {
        it('get returns undefined for unknown id', () => {
            const registry = new AudioListenerRegistry();
            expect(registry.get('nonexistent')).toBeUndefined();
        });

        it('get returns a frozen snapshot', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({ id: 'main' });
            const state = registry.get('main');
            expect(Object.isFrozen(state)).toBe(true);
        });

        it('list returns a frozen array of frozen states', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({ id: 'a' });
            registry.upsert({ id: 'b' });
            const list = registry.list();
            expect(Object.isFrozen(list)).toBe(true);
            expect(list.length).toBe(2);
            for (const item of list) {
                expect(Object.isFrozen(item)).toBe(true);
            }
        });
    });

    describe('activeRuntime / audibleRuntime', () => {
        it('activeRuntime returns undefined when no active listener', () => {
            const registry = new AudioListenerRegistry();
            expect(registry.activeRuntime()).toBeUndefined();
        });

        it('activeRuntime returns the internal listener when active', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({ id: 'main' });
            const rt = registry.activeRuntime();
            expect(rt).toBeDefined();
            expect(rt!.id).toBe('main');
        });

        it('audibleRuntime returns undefined when active listener is disabled', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({ id: 'main', active: true, enabled: false });
            expect(registry.audibleRuntime()).toBeUndefined();
        });

        it('audibleRuntime returns the listener when active and enabled', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({ id: 'main', active: true, enabled: true });
            expect(registry.audibleRuntime()).toBeDefined();
        });
    });

    describe('syncToContext', () => {
        it('syncs audible listener position to the AudioContext listener', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({
                id: 'main',
                active: true,
                position: { x: 7, y: 8, z: 9 },
                forward: { x: 0, y: 0, z: -1 },
                up: { x: 0, y: 1, z: 0 },
            });

            const fakeListener = new FakeAudioListener();
            registry.syncToContext(fakeListener as unknown as AudioListener);

            expect(fakeListener.positionX.value).toBe(7);
            expect(fakeListener.positionY.value).toBe(8);
            expect(fakeListener.positionZ.value).toBe(9);
        });

        it('uses defaults when no audible runtime exists', () => {
            const registry = new AudioListenerRegistry();
            const fakeListener = new FakeAudioListener();
            registry.syncToContext(fakeListener as unknown as AudioListener);

            expect(fakeListener.positionX.value).toBe(0);
            expect(fakeListener.forwardZ.value).toBe(-1);
            expect(fakeListener.upY.value).toBe(1);
        });
    });

    describe('clear', () => {
        it('removes all listeners and resets active id', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({ id: 'a' });
            registry.upsert({ id: 'b' });
            registry.clear();

            expect(registry.list()).toHaveLength(0);
            expect(registry.activeListenerId).toBeUndefined();
            expect(registry.activeRuntime()).toBeUndefined();
        });
    });

    describe('require', () => {
        it('returns the internal listener for a valid id', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({ id: 'main' });
            const rt = registry.require('main');
            expect(rt.id).toBe('main');
        });

        it('throws AudioListenerError for unknown id', () => {
            const registry = new AudioListenerRegistry();
            expect(() => registry.require('nonexistent')).toThrow(AudioListenerError);
        });
    });

    describe('deactivation fallback', () => {
        it('selects first enabled listener as fallback when active is deactivated', () => {
            const registry = new AudioListenerRegistry();
            registry.upsert({ id: 'a', active: true, enabled: false });
            registry.upsert({ id: 'b', enabled: true });
            registry.upsert({ id: 'c', enabled: false });

            // Deactivate 'a' by setting active: false; 'a' is disabled so fallback skips it
            registry.upsert({ id: 'a', active: false });

            expect(registry.activeListenerId).toBe('b');
        });
    });
});
