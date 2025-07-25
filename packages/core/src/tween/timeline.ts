import { EventEmitter } from '../event/event-emitter';
import { TweenCore } from './core';
import { ITimeline, IGroupable, TimelineOptions, TimelineEventMap, VoidCallback } from './types';

let _nextId = 0;

export class Timeline extends EventEmitter<TimelineEventMap> implements ITimeline {
    readonly id: number = _nextId++;

    private _timelineItems: Array<{
        target: IGroupable;
        start: number;
        end: number;
        originalDuration: number;
    }> = [];
    private _duration = 0;
    private _currentTime = 0;
    private _isPlaying = false;
    private _isPaused = false;
    private _timeScale = 1;
    private _lastUpdateTime = 0;
    private _animFrameId?: number;

    constructor() {
        super();
    }

    isPlaying(): boolean {
        return this._isPlaying;
    }

    add(tween: IGroupable, options: TimelineOptions = {}): this {
        const { offset = 0, position } = options;

        const startPosition = position !== undefined ? position : this._duration + offset;
        const duration =
            tween instanceof TweenCore ? tween.getDuration() * ((tween as any)._repeat + 1) : 0;
        const endPosition = startPosition + duration;

        this._timelineItems.push({
            target: tween,
            start: startPosition,
            end: endPosition,
            originalDuration: duration,
        });

        this._duration = Math.max(this._duration, endPosition);

        this._timelineItems.sort((a, b) => a.start - b.start);

        return this;
    }

    start(time?: number): this {
        if (this._isPlaying) {
            return this;
        }

        this._isPlaying = true;
        this._isPaused = false;
        this._lastUpdateTime = time ?? performance.now();

        for (const item of this._timelineItems) {
            item.target.stop();
        }

        this._currentTime = 0;

        this.emitSync('start', undefined);
        this._update();

        return this;
    }

    stop(): this {
        if (!this._isPlaying) {
            return this;
        }

        this._isPlaying = false;
        this._isPaused = false;

        if (this._animFrameId) {
            cancelAnimationFrame(this._animFrameId);
            this._animFrameId = undefined;
        }

        for (const item of this._timelineItems) {
            item.target.stop();
        }

        this.emitSync('stop', undefined);

        return this;
    }

    pause(): this {
        if (!this._isPlaying || this._isPaused) {
            return this;
        }

        this._isPaused = true;

        if (this._animFrameId) {
            cancelAnimationFrame(this._animFrameId);
            this._animFrameId = undefined;
        }

        for (const item of this._timelineItems) {
            if (item.target.isPlaying()) {
                item.target.pause();
            }
        }

        this.emitSync('pause', undefined);

        return this;
    }

    resume(): this {
        if (!this._isPaused) {
            return this;
        }

        this._isPaused = false;
        this._lastUpdateTime = performance.now();

        for (const item of this._timelineItems) {
            if ((item.target as any)._status === 'paused') {
                item.target.resume();
            }
        }

        this._animFrameId = requestAnimationFrame(() => this._update());

        this.emitSync('resume', undefined);

        return this;
    }

    update(time?: number): this {
        if (!this._isPlaying || this._isPaused) return this;

        const now = time ?? performance.now();
        
        if (time !== undefined) {
            this._currentTime = now;
        } else {
            const delta = (now - this._lastUpdateTime) * this._timeScale;
            this._currentTime += delta;
        }
        
        this._lastUpdateTime = now;

        this.emitSync('update', this._currentTime);

        this._updateItems(now);

        if (this._currentTime >= this._duration) {
            this._isPlaying = false;
            this.emitSync('complete', undefined);
            return this;
        }

        return this;
    }

    getDuration(): number {
        return this._duration;
    }

    setTimeScale(scale: number): this {
        this._timeScale = scale;
        return this;
    }

    onComplete(callback: VoidCallback): this {
        this.on('complete', callback);
        return this;
    }

    onUpdate(callback: (time: number) => void): this {
        this.on('update', callback);
        return this;
    }

    private _update(): void {
        if (!this._isPlaying || this._isPaused) return;

        this._animFrameId = requestAnimationFrame(() => this._update());

        const now = performance.now();
        this.update(now);
    }

    private _updateItems(now: number): void {
        for (const item of this._timelineItems) {
            const { target, start, end } = item;

            if (this._currentTime >= start && this._currentTime <= end) {
                if (!target.isPlaying()) {
                    const localTime = this._currentTime - start;
                    const adjustedStartTime = now - localTime;
                    target.start(adjustedStartTime);
                }
                target.update(now);
            } else if (this._currentTime > end) {
                if (target.isPlaying()) {
                    target.update(Infinity);
                    target.stop();
                }
            } else if (this._currentTime < start) {
                if (target.isPlaying()) {
                    target.stop();
                }
            }
        }
    }
}
