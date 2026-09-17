import { describe, expect, it } from 'vitest';
import { to } from '../utils';
import { Timeline } from '../timeline';
import { TweenChain } from '../chain';
import { waitFor } from '../utils';

describe('hybrid regression coverage', () => {
    it('timeline fires item completion exactly once across frames', () => {
        const obj = { x: 0 };
        const t1 = to(obj, { x: 100 }, 50);
        let completions = 0;
        t1.on('complete', () => {
            completions++;
        });
        const tl = new Timeline().add(t1);
        tl.start(0);
        for (let time = 0; time <= 300; time += 10) {
            tl.update(time);
        }
        expect(obj.x).toBe(100);
        expect(completions).toBe(1);
        expect(tl.getStatus()).toBe('completed');
    });

    it('end() on a repeating tween completes with the final value', () => {
        const obj = { x: 0 };
        const tw = to(obj, { x: 100 }, 100).repeat(3);
        tw.start(0);
        tw.update(50);
        tw.end();
        expect(obj.x).toBe(100);
        expect(tw.getStatus()).toBe('completed');
        tw.update(100000);
        expect(tw.getStatus()).toBe('completed');
    });

    it('chain occupies non-overlapping windows on a master clock', () => {
        const obj = { x: 0 };
        const t1 = to(obj, { x: 10 }, 10);
        const t2 = to(obj, { x: 20 }, 10);
        const ch = new TweenChain().add(t1).add(t2);
        ch.start(0);
        for (let time = 0; time <= 10; time += 1) {
            ch.update(time);
        }
        expect(obj.x).toBeCloseTo(10, 5);
        for (let time = 11; time <= 30; time += 1) {
            ch.update(time);
        }
        expect(obj.x).toBeCloseTo(20, 5);
        expect(ch.getStatus()).toBe('completed');
    });

    it('total duration accounts for repeat delay', () => {
        const obj = { x: 0 };
        const tw = to(obj, { x: 100 }, 100).repeat(2).repeatDelay(50);
        expect(tw.getTotalDuration()).toBe(400);
    });

    it('waitFor settles on stop instead of hanging', async () => {
        const obj = { x: 0 };
        const tw = to(obj, { x: 100 }, 10000);
        tw.start(0);
        const pending = waitFor(tw);
        tw.stop();
        await expect(pending).resolves.toBeUndefined();
    });

    it('manual pause and resume with explicit time does not jump', () => {
        const obj = { x: 0 };
        const tw = to(obj, { x: 100 }, 100);
        tw.start(0);
        tw.update(40);
        tw.pause();
        tw.resume(140);
        tw.update(150);
        expect(obj.x).toBeCloseTo(50, 5);
    });
});
