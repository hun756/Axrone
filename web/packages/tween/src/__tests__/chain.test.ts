import { describe, expect, it } from 'vitest';
import { TweenChain } from '../chain';
import { to } from '../utils';
import { Timeline } from '../timeline';

describe('TweenChain', () => {
    describe('Empty chain', () => {
        it('start() is no-op, status stays idle', () => {
            const ch = new TweenChain();
            ch.start(0);
            expect(ch.getStatus()).toBe('idle');
            expect(ch.isPlaying()).toBe(false);
        });
    });

    describe('Sequential playback', () => {
        it('3 tweens play in order', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 10 }, 10);
            const t2 = to(obj, { x: 20 }, 10);
            const t3 = to(obj, { x: 30 }, 10);
            const ch = new TweenChain().add(t1).add(t2).add(t3);

            ch.start(0);
            expect(ch.isPlaying()).toBe(true);
            expect(ch.getStatus()).toBe('running');

            t1.update(10);
            expect(obj.x).toBe(10);

            t1.update(11);
            t2.update(20);
            expect(obj.x).toBe(20);

            t2.update(21);
            t3.update(30);
            expect(obj.x).toBe(30);
            expect(ch.getStatus()).toBe('completed');
        });
    });

    describe('pause / resume', () => {
        it('pauses current tween', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const ch = new TweenChain().add(t1);
            ch.start(0);
            ch.pause();
            expect(ch.getStatus()).toBe('paused');
            expect(t1.getStatus()).toBe('paused');
        });

        it('resumes current tween', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const ch = new TweenChain().add(t1);
            ch.start(0);
            ch.pause();
            ch.resume();
            expect(ch.getStatus()).toBe('running');
            expect(t1.getStatus()).toBe('running');
        });

        it('pause is no-op when not playing', () => {
            const ch = new TweenChain();
            ch.pause();
            expect(ch.getStatus()).toBe('idle');
        });

        it('resume is no-op when not paused', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const ch = new TweenChain().add(t1);
            ch.start(0);
            ch.resume();
            expect(ch.getStatus()).toBe('running');
        });
    });

    describe('stop()', () => {
        it('stops current tween, resets index, emits stop', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const ch = new TweenChain().add(t1);
            let stopFired = false;
            ch.on('stop', () => {
                stopFired = true;
            });
            ch.start(0);
            ch.stop();
            expect(ch.isPlaying()).toBe(false);
            expect(ch.getStatus()).toBe('idle');
            expect(t1.isPlaying()).toBe(false);
            expect(stopFired).toBe(true);
        });

        it('no-op when not playing', () => {
            const ch = new TweenChain();
            let stopFired = false;
            ch.on('stop', () => {
                stopFired = true;
            });
            ch.stop();
            expect(stopFired).toBe(false);
        });
    });

    describe('update()', () => {
        it('no-op when not playing', () => {
            const ch = new TweenChain();
            expect(() => ch.update(0)).not.toThrow();
        });

        it('no-op when paused', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const ch = new TweenChain().add(t1);
            ch.start(0);
            ch.pause();
            ch.update(50);
            expect(obj.x).toBe(0);
        });
    });

    describe('getTotalDuration()', () => {
        it('sum of all child durations', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 10 }, 50);
            const t2 = to(obj, { x: 20 }, 100);
            const t3 = to(obj, { x: 30 }, 75);
            const ch = new TweenChain().add(t1).add(t2).add(t3);
            expect(ch.getTotalDuration()).toBe(225);
        });

        it('empty chain returns 0', () => {
            const ch = new TweenChain();
            expect(ch.getTotalDuration()).toBe(0);
        });
    });

    describe('onComplete()', () => {
        it('fires after last tween completes', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 10 }, 10);
            const t2 = to(obj, { x: 20 }, 10);
            const ch = new TweenChain().add(t1).add(t2);
            let completed = false;
            ch.onComplete(() => {
                completed = true;
            });
            ch.start(0);
            t1.update(10);
            expect(completed).toBe(false);
            t1.update(11);
            t2.update(20);
            expect(completed).toBe(true);
        });
    });

    describe('getStatus()', () => {
        it('tracks idle -> running -> paused -> completed', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 10 }, 10);
            const ch = new TweenChain().add(t1);

            expect(ch.getStatus()).toBe('idle');
            ch.start(0);
            expect(ch.getStatus()).toBe('running');
            ch.pause();
            expect(ch.getStatus()).toBe('paused');
            ch.resume();
            expect(ch.getStatus()).toBe('running');
            t1.update(10);
            expect(ch.getStatus()).toBe('completed');
        });
    });

    describe('start() guards', () => {
        it('no-op when already playing', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 100 }, 100);
            const ch = new TweenChain().add(t1);
            ch.start(0);
            const statusBefore = ch.getStatus();
            ch.start(50);
            expect(ch.getStatus()).toBe(statusBefore);
        });
    });

    describe('Mixed tween types', () => {
        it('TweenCore + Timeline in same chain', () => {
            const obj = { x: 0 };
            const t1 = to(obj, { x: 50 }, 50);
            const t2 = to(obj, { x: 100 }, 50);
            const tl = new Timeline().add(t2);
            const ch = new TweenChain().add(t1).add(tl as any);

            ch.start(0);
            expect(ch.isPlaying()).toBe(true);
            t1.update(50);
            expect(obj.x).toBe(50);
        });
    });
});
