import { tween, to, from, fromTo, TWEEN, chain, group, timeline } from '../../tween';

describe('Tween', () => {
    beforeEach(() => {
        // Reset TWEEN system before each test
        if ((TWEEN as any)._animFrameId) {
            cancelAnimationFrame((TWEEN as any)._animFrameId);
            (TWEEN as any)._animFrameId = undefined;
        }
        (TWEEN as any)._running = false;
        (TWEEN as any)._tweens.clear?.();
        (TWEEN as any)._tweensToAdd.clear?.();
        (TWEEN as any)._tweensToRemove.clear?.();
    });

    afterEach(() => {
        if ((TWEEN as any)._animFrameId) {
            cancelAnimationFrame((TWEEN as any)._animFrameId);
            (TWEEN as any)._animFrameId = undefined;
        }
        (TWEEN as any)._running = false;
        (TWEEN as any)._tweens.clear?.();
        (TWEEN as any)._tweensToAdd.clear?.();
        (TWEEN as any)._tweensToRemove.clear?.();
    });

    test('basic number tween', () => {
        let obj = { x: 0 };
        const tw = tween(obj, { to: { x: 10 }, duration: 100 });
        tw.start(0);
        tw.update(0);
        expect(obj.x).toBe(0);
        tw.update(50);
        expect(obj.x).toBeCloseTo(5, 1);
        tw.update(100);
        expect(obj.x).toBe(10);
    });

    test('fromTo tween', () => {
        let obj = { y: 0 };
        const tw = fromTo(obj, { y: 5 }, { y: 15 }, 100);
        tw.start(0);
        tw.update(0);
        expect(obj.y).toBe(5);
        tw.update(50);
        expect(obj.y).toBeCloseTo(10, 1);
        tw.update(100);
        expect(obj.y).toBe(15);
    });

    test('chain tweens', () => {
        let obj = { z: 0 };
        const tw1 = to(obj, { z: 5 }, 50);
        const tw2 = to(obj, { z: 10 }, 50);
        const ch = chain().add(tw1).add(tw2);
        ch.start(0);
        tw1.update(0);
        expect(obj.z).toBe(0);
        tw1.update(50);
        expect(obj.z).toBe(5);
        // Simulate completion of first tween
        tw1.update(51);
        tw2.update(51);
        tw2.update(100);
        expect(obj.z).toBe(10);
    });

    test('group tweens', () => {
        let obj1 = { a: 0 };
        let obj2 = { b: 0 };
        const tw1 = to(obj1, { a: 1 }, 100);
        const tw2 = to(obj2, { b: 2 }, 100);
        const grp = group().add(tw1).add(tw2);
        grp.start(0);
        tw1.update(50);
        tw2.update(50);
        expect(obj1.a).toBeCloseTo(0.5, 1);
        expect(obj2.b).toBeCloseTo(1, 1);
        tw1.update(100);
        tw2.update(100);
        expect(obj1.a).toBe(1);
        expect(obj2.b).toBe(2);
    });

    test('timeline tweens', () => {
        let obj1 = { a: 0 };
        let obj2 = { b: 0 };
        const tw1 = to(obj1, { a: 1 }, 50);
        const tw2 = to(obj2, { b: 1 }, 50);
        const tl = timeline().add(tw1).add(tw2, { position: 25 });
        
        tl.start(0);
        tl.update(25);
        expect(obj1.a).toBeCloseTo(0.5, 1);
        expect(obj2.b).toBeCloseTo(0, 1);
        
        tl.update(50);
        expect(obj1.a).toBe(1);
        expect(obj2.b).toBeCloseTo(0.5, 1);
        
        tl.update(75);
        expect(obj2.b).toBe(1);
    });
});
