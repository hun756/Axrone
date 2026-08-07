import { describe, expect, it, vi, afterEach } from 'vitest';
import { Timeline } from '../timeline';
import { to } from '../utils';

describe('Timeline', () => {
    afterEach(() => {
        vi.restoreAllMocks();
    });

    describe('add() with position', () => {
        it('explicit position overrides default sequencing', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 10 }, 50);
            const t2 = to(obj, { x: 20 }, 50);
            const tl = new Timeline().add(t1).add(t2, { position: 25 });

            expect(tl.getDuration()).toBe(75);
        });
    });

    describe('add() with offset', () => {
        it('offset from timeline end', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 10 }, 50);
            const t2 = to(obj, { x: 20 }, 50);
            const tl = new Timeline().add(t1).add(t2, { offset: 10 });

            expect(tl.getDuration()).toBe(110);
        });
    });

    describe('getDuration / getTotalDuration', () => {
        it('reflects max end position', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 10 }, 50);
            const t2 = to(obj, { x: 20 }, 100);
            const tl = new Timeline().add(t1).add(t2, { position: 0 });

            expect(tl.getDuration()).toBe(100);
            expect(tl.getTotalDuration()).toBe(100);
        });

        it('empty timeline returns 0', () => {
            const tl = new Timeline();
            expect(tl.getDuration()).toBe(0);
            expect(tl.getTotalDuration()).toBe(0);
        });
    });

    describe('setAutoUpdate / getAutoUpdate', () => {
        it('enables/disables auto update', () => {
            const tl = new Timeline();
            expect(tl.getAutoUpdate()).toBe(false);
            tl.setAutoUpdate(true);
            expect(tl.getAutoUpdate()).toBe(true);
            tl.setAutoUpdate(false);
            expect(tl.getAutoUpdate()).toBe(false);
        });

        it('cancels rAF on disable', () => {
            const cancelSpy = vi.spyOn(globalThis, 'cancelAnimationFrame').mockImplementation(() => undefined);
            const requestSpy = vi.spyOn(globalThis, 'requestAnimationFrame').mockImplementation(() => 42);
            const nowSpy = vi.spyOn(globalThis.performance, 'now').mockReturnValue(1000);

            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const tl = new Timeline().add(t1);
            tl.setAutoUpdate(true);
            tl.start();
            expect(tl.getAutoUpdate()).toBe(true);

            tl.setAutoUpdate(false);
            expect(cancelSpy).toHaveBeenCalled();
        });
    });

    describe('onUpdate callback', () => {
        it('receives current time', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const tl = new Timeline().add(t1);
            let receivedTime = -1;
            tl.onUpdate((time) => {
                receivedTime = time;
            });
            tl.start(0);
            tl.update(50);
            expect(receivedTime).toBe(50);
        });
    });

    describe('pause / resume', () => {
        it('pauses all playing children', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const tl = new Timeline().add(t1);
            tl.start(0);
            tl.update(20);
            tl.pause();
            expect(tl.getStatus()).toBe('paused');
        });

        it('resumes only paused children', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const tl = new Timeline().add(t1);
            tl.start(0);
            tl.update(20);
            tl.pause();
            tl.resume();
            expect(tl.getStatus()).toBe('running');
        });

        it('pause is no-op when not playing', () => {
            const tl = new Timeline();
            tl.pause();
            expect(tl.getStatus()).toBe('idle');
        });

        it('resume is no-op when not paused', () => {
            const tl = new Timeline();
            tl.resume();
            expect(tl.getStatus()).toBe('idle');
        });
    });

    describe('stop()', () => {
        it('stops all children', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const tl = new Timeline().add(t1);
            tl.start(0);
            tl.update(20);
            tl.stop();
            expect(tl.getStatus()).toBe('idle');
            expect(tl.isPlaying()).toBe(false);
        });

        it('no-op when not playing', () => {
            const tl = new Timeline();
            let stopFired = false;
            tl.on('stop', () => {
                stopFired = true;
            });
            tl.stop();
            expect(stopFired).toBe(false);
        });
    });

    describe('update() with time', () => {
        it('manual clock, applies timeScale', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const tl = new Timeline().add(t1).setTimeScale(2);
            tl.start(0);
            tl.update(25);
            expect(obj.x).toBeCloseTo(50, 0);
        });

        it('timeScale 0.5 = half speed', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const tl = new Timeline().add(t1).setTimeScale(0.5);
            tl.start(0);
            tl.update(100);
            expect(obj.x).toBeCloseTo(50, 0);
        });
    });

    describe('_updateItems logic', () => {
        it('items before start time are stopped if playing', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const tl = new Timeline().add(t1, { position: 50 });
            tl.start(0);
            tl.update(25);
            expect(t1.isPlaying()).toBe(false);
        });

        it('items in range are started and updated', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const tl = new Timeline().add(t1);
            tl.start(0);
            tl.update(50);
            expect(obj.x).toBeCloseTo(50, 0);
        });

        it('items past end are completed and stopped', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 50);
            const tl = new Timeline().add(t1);
            tl.start(0);
            tl.update(100);
            expect(obj.x).toBe(100);
        });
    });

    describe('Complete event', () => {
        it('fires when currentTime >= duration', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const tl = new Timeline().add(t1);
            let completed = false;
            tl.onComplete(() => {
                completed = true;
            });
            tl.start(0);
            tl.update(100);
            expect(completed).toBe(true);
            expect(tl.getStatus()).toBe('completed');
        });
    });

    describe('start() guards', () => {
        it('no-op when already playing', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const tl = new Timeline().add(t1);
            tl.start(0);
            const statusBefore = tl.getStatus();
            tl.start(50);
            expect(tl.getStatus()).toBe(statusBefore);
        });
    });

    describe('isPlaying / getStatus', () => {
        it('tracks lifecycle correctly', () => {
            const tl = new Timeline();
            expect(tl.isPlaying()).toBe(false);
            expect(tl.getStatus()).toBe('idle');

            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            tl.add(t1);
            tl.start(0);
            expect(tl.isPlaying()).toBe(true);
            expect(tl.getStatus()).toBe('running');

            tl.pause();
            expect(tl.getStatus()).toBe('paused');

            tl.resume();
            expect(tl.isPlaying()).toBe(true);
            expect(tl.getStatus()).toBe('running');

            tl.update(100);
            expect(tl.isPlaying()).toBe(false);
            expect(tl.getStatus()).toBe('completed');
        });
    });
});
