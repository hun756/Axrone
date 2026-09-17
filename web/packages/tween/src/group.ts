import { TweenCore } from './core';
import { IGroupable } from './types';

export class TweenGroup {
    private _tweens = new Set<IGroupable>();
    private _pausedTweens = new Set<IGroupable>();
    private _tweensToAdd = new Set<IGroupable>();
    private _tweensToRemove = new Set<IGroupable>();
    private _isUpdating = false;

    add(tween: IGroupable): this {
        if (this._isUpdating) {
            this._tweensToRemove.delete(tween);
            this._tweensToAdd.add(tween);
        } else {
            this._tweens.add(tween);
        }
        return this;
    }

    remove(tween: IGroupable): this {
        if (this._isUpdating) {
            this._tweensToAdd.delete(tween);
            this._tweensToRemove.add(tween);
        } else {
            this._tweens.delete(tween);
        }
        return this;
    }

    start(time?: number): this {
        this._pausedTweens.clear();
        for (const tween of this._tweens) {
            tween.start(time);
        }
        return this;
    }

    stop(): this {
        for (const tween of this._tweens) {
            tween.stop();
        }
        this._pausedTweens.clear();
        return this;
    }

    pause(): this {
        this._pausedTweens.clear();
        for (const tween of this._tweens) {
            if (tween.isPlaying()) {
                this._pausedTweens.add(tween);
                tween.pause();
            }
        }
        return this;
    }

    resume(): this {
        for (const tween of this._pausedTweens) {
            tween.resume();
        }
        this._pausedTweens.clear();
        return this;
    }

    update(time?: number): this {
        this._isUpdating = true;
        try {
            for (const tween of this._tweens) {
                if (this._tweensToRemove.has(tween)) {
                    continue;
                }
                tween.update(time);
            }
        } finally {
            this._isUpdating = false;
        }

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
        return this;
    }

    dispose(): void {
        this.stop();
        this._tweens.clear();
        this._pausedTweens.clear();
        this._tweensToAdd.clear();
        this._tweensToRemove.clear();
        this._isUpdating = false;
    }
}
