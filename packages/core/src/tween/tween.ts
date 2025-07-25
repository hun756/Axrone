import { DeepPartial, TypedArray } from '@axrone/utility';
import { IMat3Like } from '@axrone/numeric/src/mat3';
import { IMat4Like } from '@axrone/numeric/src/mat4';
import { IVec2Like } from '@axrone/numeric/src/vec2';
import { IVec3Like } from '@axrone/numeric/src/vec3';
import { IVec4Like } from '@axrone/numeric/src/vec4';
import { ReadonlyRecord } from '../types';
import { IMat2Like } from '@axrone/numeric/src/mat2';
import { EasingFunction } from './easing-functions';

export type TweenableNumber = number;
export type TweenableArray = readonly number[];
export type TweenableVec2 = IVec2Like;
export type TweenableVec3 = IVec3Like;
export type TweenableVec4 = IVec4Like;
export type TweenableMatrix2 = IMat2Like;
export type TweenableMatrix3 = IMat3Like;
export type TweenableMatrix4 = IMat4Like;
export type TweenableTypedArray = TypedArray;
export type TweenableRecord = ReadonlyRecord<string, number>;

export type TweenableValue =
    | TweenableNumber
    | TweenableArray
    | TweenableVec2
    | TweenableVec3
    | TweenableVec4
    | TweenableMatrix2
    | TweenableMatrix3
    | TweenableMatrix4
    | TweenableTypedArray
    | TweenableRecord
    | TweenableObject;

export type TweenableObject = ReadonlyRecord<
    string,
    | TweenableNumber
    | TweenableArray
    | TweenableVec2
    | TweenableVec3
    | TweenableVec4
    | TweenableMatrix2
    | TweenableMatrix3
    | TweenableMatrix4
    | TweenableTypedArray
    | TweenableRecord
>;

export type TweenableValueType<T> = T extends number
    ? 'number'
    : T extends readonly number[]
      ? 'array'
      : T extends TypedArray
        ? 'typedarray'
        : T extends object
          ? 'object'
          : never;

export type TweenEventType =
    | 'start'
    | 'update'
    | 'complete'
    | 'stop'
    | 'repeat'
    | 'pause'
    | 'resume';

export type TweenEventCallback<T> = (tween: TweenCore<T>, elapsed?: number) => void;
export type VoidCallback = () => void;
export type UpdateCallback<T> = (value: T) => void;

export type TweenStatus = 'idle' | 'running' | 'paused' | 'completed';

export type TweenConfig<T> = {
    readonly from?: DeepPartial<T>;
    readonly to?: DeepPartial<T>;
    readonly duration?: number;
    readonly delay?: number;
    readonly easing?: EasingFunction;
    readonly repeat?: number;
    readonly yoyo?: boolean;
    readonly interpolation?: (v: ArrayLike<number>, k: number) => number;
    readonly autoStart?: boolean;
};

export interface ITween<T> {
    id: number;
    isPlaying(): boolean;
    getStatus(): TweenStatus;
    getDuration(): number;
    from(properties: DeepPartial<T>): this;
    to(properties: DeepPartial<T>, duration?: number): this;
    duration(ms: number): this;
    start(time?: number): this;
    stop(): this;
    end(): this;
    pause(): this;
    resume(): this;
    delay(ms: number): this;
    repeat(times: number): this;
    repeatDelay(ms: number): this;
    yoyo(enable: boolean): this;
    easing(fn: EasingFunction): this;
    interpolation(fn: (v: ArrayLike<number>, k: number) => number): this;
    chain(...tweens: ITween<any>[]): this;
    on(event: TweenEventType, callback: TweenEventCallback<T>): this;
    off(event: TweenEventType, callback?: TweenEventCallback<T>): this;
    update(time?: number): this;
}

export interface IGroupable {
    id: number;
    isPlaying(): boolean;
    start(time?: number): this;
    stop(): this;
    pause(): this;
    resume(): this;
    update(time?: number): this;
}

export interface ITimeline extends IGroupable {
    add(tween: IGroupable, options?: TimelineOptions): this;
    getDuration(): number;
    setTimeScale(scale: number): this;
    onComplete(callback: VoidCallback): this;
    onUpdate(callback: (time: number) => void): this;
}

export type TimelineOptions = {
    readonly offset?: number;
    readonly position?: number;
};

const EventPool = new Map<string, Set<Function>>();

abstract class TweenCore<T> implements ITween<T> {
    id: number;
    isPlaying(): boolean {
        throw new Error('Method not implemented.');
    }
    getStatus(): TweenStatus {
        throw new Error('Method not implemented.');
    }
    getDuration(): number {
        throw new Error('Method not implemented.');
    }
    from(properties: DeepPartial<T>): this {
        throw new Error('Method not implemented.');
    }
    to(properties: DeepPartial<T>, duration?: number): this {
        throw new Error('Method not implemented.');
    }
    duration(ms: number): this {
        throw new Error('Method not implemented.');
    }
    start(time?: number): this {
        throw new Error('Method not implemented.');
    }
    stop(): this {
        throw new Error('Method not implemented.');
    }
    end(): this {
        throw new Error('Method not implemented.');
    }
    pause(): this {
        throw new Error('Method not implemented.');
    }
    resume(): this {
        throw new Error('Method not implemented.');
    }
    delay(ms: number): this {
        throw new Error('Method not implemented.');
    }
    repeat(times: number): this {
        throw new Error('Method not implemented.');
    }
    repeatDelay(ms: number): this {
        throw new Error('Method not implemented.');
    }
    yoyo(enable: boolean): this {
        throw new Error('Method not implemented.');
    }
    easing(fn: EasingFunction): this {
        throw new Error('Method not implemented.');
    }
    interpolation(fn: (v: ArrayLike<number>, k: number) => number): this {
        throw new Error('Method not implemented.');
    }
    chain(...tweens: ITween<any>[]): this {
        throw new Error('Method not implemented.');
    }
    on(event: TweenEventType, callback: TweenEventCallback<T>): this {
        throw new Error('Method not implemented.');
    }
    off(event: TweenEventType, callback?: TweenEventCallback<T> | undefined): this {
        throw new Error('Method not implemented.');
    }
    update(time?: number): this {
        throw new Error('Method not implemented.');
    }
}