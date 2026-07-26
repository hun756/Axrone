import type { PoolableObject } from '../pool-support';
import type { WaitQueueEntry } from './pool-types';

export class WaitQueue<T extends PoolableObject> {
    readonly #entries: WaitQueueEntry<T>[] = [];

    get size(): number {
        return this.#entries.length;
    }

    push(resolve: (obj: T | null) => void, reject: (err: Error) => void): WaitQueueEntry<T> {
        const entry: WaitQueueEntry<T> = { resolve, reject, timer: null };
        this.#entries.push(entry);
        return entry;
    }

    pop(): WaitQueueEntry<T> | undefined {
        return this.#entries.shift();
    }

    remove(predicate: (entry: WaitQueueEntry<T>) => boolean): WaitQueueEntry<T> | null {
        const idx = this.#entries.findIndex(predicate);
        if (idx === -1) return null;
        const [removed] = this.#entries.splice(idx, 1);
        if (removed.timer !== null) clearTimeout(removed.timer);
        return removed;
    }

    rejectAll(reason: Error): void {
        while (this.#entries.length > 0) {
            const entry = this.#entries.shift()!;
            if (entry.timer !== null) clearTimeout(entry.timer);
            try {
                entry.reject(reason);
            } catch (e) {
                console.error('Error rejecting wait queue entry:', e);
            }
        }
    }

    clear(): void {
        this.#entries.length = 0;
    }
}
