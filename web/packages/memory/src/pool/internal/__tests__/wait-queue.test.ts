import { afterEach, describe, expect, it, vi } from 'vitest';
import { WaitQueue } from '../wait-queue';
import type { PoolableObject } from '../../pool-support';

describe('WaitQueue', () => {
    it('push and pop maintain FIFO order', () => {
        const queue = new WaitQueue<PoolableObject>();
        const resolve1 = () => {};
        const reject1 = () => {};
        const resolve2 = () => {};
        const reject2 = () => {};
        
        queue.push(resolve1, reject1);
        queue.push(resolve2, reject2);
        
        expect(queue.size).toBe(2);
        const entry1 = queue.pop();
        expect(entry1?.resolve).toBe(resolve1);
        const entry2 = queue.pop();
        expect(entry2?.resolve).toBe(resolve2);
        expect(queue.size).toBe(0);
    });

    it('pop returns undefined when empty', () => {
        const queue = new WaitQueue<PoolableObject>();
        expect(queue.pop()).toBeUndefined();
    });

    it('remove finds and removes entry', () => {
        const queue = new WaitQueue<PoolableObject>();
        const resolve1 = () => {};
        const reject1 = () => {};
        const resolve2 = () => {};
        const reject2 = () => {};
        
        queue.push(resolve1, reject1);
        const entry2 = queue.push(resolve2, reject2);
        
        const removed = queue.remove((e) => e === entry2);
        expect(removed).toBe(entry2);
        expect(queue.size).toBe(1);
    });

    it('remove returns null when not found', () => {
        const queue = new WaitQueue<PoolableObject>();
        const resolve = () => {};
        const reject = () => {};
        queue.push(resolve, reject);
        
        const removed = queue.remove(() => false);
        expect(removed).toBeNull();
        expect(queue.size).toBe(1);
    });

    it('rejectAll rejects all entries', () => {
        const queue = new WaitQueue<PoolableObject>();
        const errors: Error[] = [];
        
        queue.push(
            () => {},
            (err) => errors.push(err)
        );
        queue.push(
            () => {},
            (err) => errors.push(err)
        );
        
        const reason = new Error('test error');
        queue.rejectAll(reason);
        
        expect(errors).toHaveLength(2);
        expect(errors[0]).toBe(reason);
        expect(errors[1]).toBe(reason);
        expect(queue.size).toBe(0);
    });

    it('clear empties the queue', () => {
        const queue = new WaitQueue<PoolableObject>();
        queue.push(() => {}, () => {});
        queue.push(() => {}, () => {});
        queue.push(() => {}, () => {});
        
        queue.clear();
        expect(queue.size).toBe(0);
    });

    it('size reflects queue length', () => {
        const queue = new WaitQueue<PoolableObject>();
        expect(queue.size).toBe(0);

        queue.push(() => {}, () => {});
        expect(queue.size).toBe(1);

        queue.push(() => {}, () => {});
        expect(queue.size).toBe(2);

        queue.pop();
        expect(queue.size).toBe(1);
    });

    it('clears pending timers when an entry is popped', async () => {
        vi.useFakeTimers();
        const queue = new WaitQueue<PoolableObject>();
        let fired = false;

        const entry = queue.push(() => {}, () => {});
        entry.timer = setTimeout(() => {
            fired = true;
        }, 5);

        queue.pop();
        await vi.advanceTimersByTimeAsync(25);

        expect(fired).toBe(false);
        vi.useRealTimers();
    });
});
