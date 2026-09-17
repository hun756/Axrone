import type { ITween, TweenEventCallback, TweenEventType } from './types';

/**
 * Allocation-free event fan-out for tween lifecycles.
 *
 * Unlike the former per-tween heavyweight `EventEmitter`, this dispatcher
 * owns no buckets, indexes, meters, schedulers or timers. `update` listeners
 * are invoked positionally — no `{ tween, elapsed }` boxing — and skipped
 * through `hasUpdate` when nobody listens, so listener-less frames cost a
 * single length check.
 */
export class TweenDispatcher<T> {
    private _update: Array<TweenEventCallback<T>> = [];
    private _lifecycle = new Map<TweenEventType, Array<TweenEventCallback<T>>>();

    public onUpdate(listener: TweenEventCallback<T>): void {
        this._update.push(listener);
    }

    public offUpdate(listener?: TweenEventCallback<T>): void {
        if (listener === undefined) {
            this._update = [];
            return;
        }
        const index = this._update.indexOf(listener);
        if (index >= 0) {
            this._update.splice(index, 1);
        }
    }

    public hasUpdate(): boolean {
        return this._update.length > 0;
    }

    public emitUpdate(tween: ITween<T>, elapsed: number): void {
        const listeners = this._update;
        for (let index = 0; index < listeners.length; index += 1) {
            listeners[index]!(tween, elapsed);
        }
    }

    public on(event: TweenEventType, listener: TweenEventCallback<T>): void {
        let list = this._lifecycle.get(event);
        if (list === undefined) {
            list = [];
            this._lifecycle.set(event, list);
        }
        list.push(listener);
    }

    public off(event: TweenEventType, listener?: TweenEventCallback<T>): void {
        if (listener === undefined) {
            this._lifecycle.delete(event);
            return;
        }
        const list = this._lifecycle.get(event);
        if (list === undefined) {
            return;
        }
        const index = list.indexOf(listener);
        if (index >= 0) {
            list.splice(index, 1);
        }
        if (list.length === 0) {
            this._lifecycle.delete(event);
        }
    }

    public has(event: TweenEventType): boolean {
        return (this._lifecycle.get(event)?.length ?? 0) > 0;
    }

    public emit(event: TweenEventType, tween: ITween<T>, elapsed?: number): void {
        if (event === 'update') {
            if (this._update.length === 0) {
                return;
            }
            this.emitUpdate(tween, elapsed ?? 0);
            return;
        }
        const list = this._lifecycle.get(event);
        if (list === undefined) {
            return;
        }
        for (let index = 0; index < list.length; index += 1) {
            list[index]!(tween);
        }
    }

    public clear(): void {
        this._update = [];
        this._lifecycle.clear();
    }
}
