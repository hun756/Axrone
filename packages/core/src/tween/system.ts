import { IGroupable } from './types';

export class TweenSystem {
    private _tweens = new Set<IGroupable>();
    private _tweensToAdd = new Set<IGroupable>();
    private _tweensToRemove = new Set<IGroupable>();
    private _isUpdating = false;
    private _running = false;
    private _lastTime = 0;
    private _animFrameId?: number;

    add(tween: IGroupable): void {
        if (this._isUpdating) {
            this._tweensToAdd.add(tween);
        } else {
            this._tweens.add(tween);
        }

        if (!this._running && this._tweens.size > 0) {
            this._start();
        }
    }

    remove(tween: IGroupable): void {
        if (this._isUpdating) {
            this._tweensToRemove.add(tween);
        } else {
            this._tweens.delete(tween);
        }
    }

    update(time?: number): boolean {
        if (this._tweens.size === 0 && this._tweensToAdd.size === 0) {
            this._running = false;
            return false;
        }

        const now = time !== undefined ? time : performance.now();

        this._isUpdating = true;

        for (const tween of this._tweens) {
            tween.update(now);
        }

        this._isUpdating = false;

        if (this._tweensToRemove.size > 0) {
            for (const tween of this._tweensToRemove) {
                this._tweens.delete(tween);
            }
            this._tweensToRemove.clear();
        }

        if (this._tweensToAdd.size > 0) {
            for (const tween of this._tweensToAdd) {
                this._tweens.add(tween);
            }
            this._tweensToAdd.clear();
        }

        return this._tweens.size > 0;
    }

    private _start(): void {
        if (this._running) return;

        this._running = true;
        this._lastTime = performance.now();
        this._tick();
    }

    private _tick = (): void => {
        if (!this._running) return;

        this._animFrameId = requestAnimationFrame(this._tick);

        const now = performance.now();
        const hasActiveTweens = this.update(now);

        if (!hasActiveTweens) {
            this._running = false;
            cancelAnimationFrame(this._animFrameId!);
            this._animFrameId = undefined;
        }
    };
}
