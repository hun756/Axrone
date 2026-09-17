import { DeepPartial } from '@axrone/utility';
import {
    SpringConfig,
    TweenableValue,
    UpdateCallback,
    VoidCallback,
} from './types';
import { collectTweenLeafPaths, deepCloneTweenValue } from './runtime-utils';
import {
    getOrCreateTweenPropertyAccessor,
    TweenPropertyAccessor,
} from './property-accessor';
import { UnsubscribeFn } from './dispatcher';

export interface SpringStep {
    position: number;
    velocity: number;
    atRest: boolean;
}

/** Upper bound for a single integration step; prevents teleporting after stalls. */
export const MAX_SPRING_DT = 0.064;

export class SpringSimulation {
    private _mass: number;
    private _stiffness: number;
    private _damping: number;
    private _precision: number;

    constructor(config: SpringConfig = {}) {
        this._mass = config.mass ?? 1;
        this._stiffness = config.stiffness ?? 100;
        this._damping = config.damping ?? 10;
        this._precision = config.precision ?? 0.001;
    }

    update(
        position: number,
        velocity: number,
        target: number,
        dt: number
    ): [number, number, boolean] {
        const out: SpringStep = { position: 0, velocity: 0, atRest: false };
        this.stepInto(position, velocity, target, dt, out);
        return [out.position, out.velocity, out.atRest];
    }

    /**
     * Allocation-free integration into a caller-owned scratch record.
     * The hot path reuses one scratch per `Spring` instead of boxing a
     * tuple per property per frame.
     */
    stepInto(
        position: number,
        velocity: number,
        target: number,
        dt: number,
        out: SpringStep
    ): void {
        const displacement = position - target;
        const springForce = -this._stiffness * displacement;
        const dampingForce = -this._damping * velocity;
        const force = springForce + dampingForce;

        const acceleration = force / this._mass;

        const newVelocity = velocity + acceleration * dt;
        const newPosition = position + newVelocity * dt;

        out.position = newPosition;
        out.velocity = newVelocity;
        out.atRest =
            Math.abs(newPosition - target) < this._precision &&
            Math.abs(newVelocity) < this._precision;
    }
}

export type SpringEventType = 'start' | 'stop' | 'update' | 'complete';

export class Spring<T extends TweenableValue> {
    private _target: T;
    private _current: T;
    private _velocity: Record<string, number> = Object.create(null);
    private _simulation: SpringSimulation;
    private _isRunning = false;
    private _animFrameId?: number;
    private _lastTime?: number;
    private _props = new Set<string>();
    private _autoUpdate = false;
    private _propertyAccessors = new Map<string, TweenPropertyAccessor>();
    private _listeners = new Map<SpringEventType, Array<(...args: never[]) => void>>();
    private _stepScratch: SpringStep = { position: 0, velocity: 0, atRest: false };

    constructor(initial: T, config: SpringConfig = {}) {
        this._current = this._deepClone(initial);
        this._target = this._deepClone(initial);
        this._simulation = new SpringSimulation(config);

        const initialVelocity = config.velocity ?? 0;

        if (typeof initial === 'number') {
            this._current = { value: initial } as any;
            this._target = { value: initial } as any;
            this._velocity['value'] = initialVelocity;
            this._props.add('value');
            this._getAccessor('value');
        } else {
            this._collectProps(initial, this._props);

            for (const prop of this._props) {
                this._velocity[prop] = initialVelocity;
            }
        }
    }

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

    private _collectProps(obj: any, props: Set<string>): void {
        for (const path of collectTweenLeafPaths(obj, true)) {
            props.add(path);
            this._getAccessor(path);
        }
    }

    setTarget(target: DeepPartial<T>): this {
        if (typeof target === 'number') {
            this._target = { value: target } as any;
        } else {
            this._updateTarget(this._target, target);
        }

        this._collectProps(target, this._props);

        for (const prop of this._props) {
            if (!(prop in this._velocity)) {
                this._velocity[prop] = 0;
            }
        }

        if (!this._isRunning && this._autoUpdate) {
            this.start();
        }

        return this;
    }

    private _updateTarget(current: any, target: any): void {
        if (!target || typeof target !== 'object') return;

        for (const key of Object.keys(target)) {
            const value = target[key];

            if (
                value !== null &&
                typeof value === 'object' &&
                !Array.isArray(value) &&
                !ArrayBuffer.isView(value)
            ) {
                if (!Object.prototype.hasOwnProperty.call(current, key)) {
                    current[key] = Array.isArray(value) ? [] : {};
                }
                this._updateTarget(current[key], value);
            } else {
                current[key] = value;
            }
        }
    }

    getCurrent(): T {
        if (
            typeof (this._current as any).value === 'number' &&
            Object.keys(this._current as any).length === 1
        ) {
            return (this._current as any).value;
        }
        return this._deepClone(this._current);
    }

    start(): this {
        if (this._isRunning) {
            return this;
        }

        this._isRunning = true;
        this._lastTime = performance.now();

        if (this._autoUpdate) {
            this._startInternalLoop();
        }

        this.emit('start');

        return this;
    }

    updateManual(deltaTime: number): boolean {
        if (!this._isRunning) return false;

        const dt = Math.min(deltaTime / 1000, MAX_SPRING_DT);
        return this._simulateStep(dt);
    }

    stop(): this {
        if (!this._isRunning) {
            return this;
        }

        this._isRunning = false;

        if (this._animFrameId !== undefined) {
            cancelAnimationFrame(this._animFrameId);
            this._animFrameId = undefined;
        }

        this.emit('stop');

        return this;
    }

    on(event: SpringEventType, callback: (...args: never[]) => void): UnsubscribeFn {
        let list = this._listeners.get(event);
        if (list === undefined) {
            list = [];
            this._listeners.set(event, list);
        }
        list.push(callback);
        return () => this.off(event, callback);
    }

    off(event: SpringEventType, callback?: (...args: never[]) => void): boolean {
        const list = this._listeners.get(event);
        if (list === undefined) {
            return false;
        }
        if (callback === undefined) {
            const removed = list.length > 0;
            this._listeners.delete(event);
            return removed;
        }
        const index = list.indexOf(callback);
        if (index < 0) {
            return false;
        }
        list.splice(index, 1);
        if (list.length === 0) {
            this._listeners.delete(event);
        }
        return true;
    }

    has(event: SpringEventType): boolean {
        return (this._listeners.get(event)?.length ?? 0) > 0;
    }

    private emit(event: SpringEventType, value?: T): void {
        const list = this._listeners.get(event);
        if (list === undefined) {
            return;
        }
        for (let index = 0; index < list.length; index += 1) {
            (list[index] as (value?: T) => void)(value);
        }
    }

    onUpdate(callback: UpdateCallback<T>): this {
        this.on('update', callback as (...args: never[]) => void);
        return this;
    }

    onComplete(callback: VoidCallback): this {
        this.on('complete', callback as (...args: never[]) => void);
        return this;
    }

    onStart(callback: VoidCallback): this {
        this.on('start', callback as (...args: never[]) => void);
        return this;
    }

    dispose(): void {
        this.stop();

        if (this._animFrameId !== undefined) {
            cancelAnimationFrame(this._animFrameId);
            this._animFrameId = undefined;
        }

        this._lastTime = undefined;
        this._listeners.clear();
        this._props.clear();
        this._velocity = Object.create(null);
        this._propertyAccessors.clear();
        this._isRunning = false;
        this._autoUpdate = false;
    }

    private _startInternalLoop(): void {
        if (this._animFrameId !== undefined) return;
        this._tick();
    }

    private _simulateStep(dt: number): boolean {
        let allAtRest = true;

        for (const prop of this._props) {
            const accessor = this._propertyAccessors.get(prop) ?? this._getAccessor(prop);
            const position = accessor.get(this._current) ?? 0;
            const target = accessor.get(this._target) ?? 0;

            if (typeof position === 'number' && typeof target === 'number') {
                const step = this._stepScratch;
                this._simulation.stepInto(
                    position,
                    this._velocity[prop] ?? 0,
                    target,
                    dt,
                    step
                );

                accessor.set(this._current, step.position);
                this._velocity[prop] = step.velocity;

                if (!step.atRest) {
                    allAtRest = false;
                }
            }
        }

        this.emit('update', this._current);

        if (allAtRest) {
            this._current = this._deepClone(this._target);

            for (const prop in this._velocity) {
                this._velocity[prop] = 0;
            }

            this._isRunning = false;
            this.emit('update', this._current);
            this.emit('complete');
            return false;
        }

        return true;
    }

    private _tick = (): void => {
        if (!this._isRunning || this._lastTime === undefined || !this._autoUpdate) {
            return;
        }

        const now = performance.now();
        const dt = Math.min((now - this._lastTime) / 1000, MAX_SPRING_DT);
        this._lastTime = now;

        const isStillRunning = this._simulateStep(dt);

        if (isStillRunning) {
            this._animFrameId = requestAnimationFrame(this._tick);
        } else {
            this._animFrameId = undefined;
        }
    };

    private _deepClone<U>(source: U): U {
        return deepCloneTweenValue(source);
    }

    private _getAccessor(path: string): TweenPropertyAccessor {
        return getOrCreateTweenPropertyAccessor(this._propertyAccessors, path);
    }
}
