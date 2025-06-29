export class EventScheduler {
    #concurrencyLimit: number;
    #activeCount = 0;
    #pendingPromises = new Set<Promise<void>>();
    #queue: Array<() => void> = [];

    constructor(concurrencyLimit: number = Infinity) {
        this.#concurrencyLimit = concurrencyLimit;
    }

    get activeCount(): number {
        return this.#activeCount;
    }

    get pendingCount(): number {
        return this.#queue.length;
    }

    schedule<T>(fn: () => Promise<T>): Promise<T> {
        if (this.#concurrencyLimit === Infinity) {
            const promise = fn();
            this.#pendingPromises.add(promise as Promise<any>);
            promise.finally(() => {
                this.#pendingPromises.delete(promise as Promise<any>);
            });
            return promise;
        }

        return new Promise<T>((resolve, reject) => {
            const execute = async (): Promise<void> => {
                this.#activeCount++;
                try {
                    const result = await fn();
                    resolve(result);
                } catch (error) {
                    reject(error);
                } finally {
                    this.#activeCount--;
                    this.#processQueue();
                }
            };

            if (this.#activeCount < this.#concurrencyLimit) {
                execute();
            } else {
                this.#queue.push(execute);
            }
        });
    }

    #processQueue(): void {
        if (this.#queue.length > 0 && this.#activeCount < this.#concurrencyLimit) {
            const nextTask = this.#queue.shift();
            if (nextTask) {
                nextTask();
            }
        }
    }

    async drain(): Promise<void> {
        while (this.#activeCount > 0 || this.#queue.length > 0) {
            await new Promise((resolve) => setTimeout(resolve, 1));
        }

        if (this.#concurrencyLimit === Infinity && this.#pendingPromises.size > 0) {
            await Promise.allSettled(Array.from(this.#pendingPromises));
        }
    }
}
