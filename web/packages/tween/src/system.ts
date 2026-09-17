import { IGroupable } from './types';

export class TweenSystem {
    private _active: IGroupable[] = [];
    private _count = 0;
    private _autoUpdate = false;
    private _lastTime = 0;
    private _lastUpdateTime?: number;
    private _maxDelta?: number;
    private _animFrameId?: number;

    setAutoUpdate(enabled: boolean): void {
        this._autoUpdate = enabled;

        if (!enabled && this._animFrameId !== undefined) {
            cancelAnimationFrame(this._animFrameId);
            this._animFrameId = undefined;
        }
    }

    getAutoUpdate(): boolean {
        return this._autoUpdate;
    }

    /**
     * Cap the timestamp jump applied in a single `update`, in the same units
     * as the driven clock. After a background-tab stall the excess is
     * discarded instead of fast-forwarding every tween to its end state.
     * `undefined` (default) preserves the legacy pass-through behavior.
     */
    setMaxDelta(maxDelta?: number): void {
        this._maxDelta = maxDelta === undefined ? undefined : Math.max(0, maxDelta);
    }

    getMaxDelta(): number | undefined {
        return this._maxDelta;
    }

    add(tween: IGroupable): void {
        if (this._active.indexOf(tween) >= 0) {
            return;
        }
        this._active[this._count] = tween;
        this._count += 1;

        if (this._autoUpdate && !this._isInternalLoopRunning() && this._count > 0) {
            this._startInternalLoop();
        }
    }

    remove(tween: IGroupable): void {
        const index = this._active.indexOf(tween);
        if (index < 0 || index >= this._count) {
            return;
        }
        this._swapAndPop(index);
    }

    update(time?: number): boolean {
        if (this._count === 0) {
            return false;
        }

        let now = time !== undefined ? time : performance.now();

        if (
            this._maxDelta !== undefined &&
            this._lastUpdateTime !== undefined &&
            now - this._lastUpdateTime > this._maxDelta
        ) {
            now = this._lastUpdateTime + this._maxDelta;
        }
        this._lastUpdateTime = now;

        for (let index = this._count - 1; index >= 0; index -= 1) {
            const tween = this._active[index]!;
            tween.update(now);

            if (tween.getStatus() === 'completed') {
                this._swapAndPop(index);
            }
        }

        return this._count > 0;
    }

    getActiveTweenCount(): number {
        return this._count;
    }

    clear(): void {
        for (let index = 0; index < this._count; index += 1) {
            this._active[index]!.stop();
            this._active[index] = undefined as unknown as IGroupable;
        }
        this._count = 0;
        this._lastUpdateTime = undefined;

        if (this._animFrameId !== undefined) {
            cancelAnimationFrame(this._animFrameId);
            this._animFrameId = undefined;
        }
    }

    private _swapAndPop(index: number): void {
        const last = this._count - 1;
        this._active[index] = this._active[last]!;
        this._active[last] = undefined as unknown as IGroupable;
        this._count = last;
    }

    private _isInternalLoopRunning(): boolean {
        return this._animFrameId !== undefined;
    }

    private _startInternalLoop(): void {
        if (this._isInternalLoopRunning()) return;

        this._lastTime = performance.now();
        this._tick();
    }

    private _tick = (): void => {
        if (!this._autoUpdate) return;

        this._animFrameId = requestAnimationFrame(this._tick);

        const now = performance.now();
        const hasActiveTweens = this.update(now);

        if (!hasActiveTweens) {
            cancelAnimationFrame(this._animFrameId!);
            this._animFrameId = undefined;
        }
    };
}
