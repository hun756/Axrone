import { Vec3, IVec3Like, EPSILON } from '@axrone/numeric';
import type { IRaycastHit3D, LayerMask, RaycastFlags } from '../types/raycast-types';
import type { RaycastSystem3D } from './raycast-system';
import type { BodyId } from '../types/primitives';

export interface ITimeOfImpact {
    readonly hit: boolean;
    readonly time: number;
    readonly fraction: number;
    readonly normal: Readonly<IVec3Like>;
    readonly witness1: Readonly<IVec3Like>;
    readonly witness2: Readonly<IVec3Like>;
}

export interface ISweepTestQuery {
    readonly startPosition: Readonly<IVec3Like>;
    readonly endPosition: Readonly<IVec3Like>;
    readonly layerMask: LayerMask;
    readonly maxIterations: number;
    readonly tolerance: number;
}

export class ContinuousRaycast3D {
    private readonly _raycastSystem: RaycastSystem3D;
    private readonly _epsilon: number = 1e-6;

    constructor(raycastSystem: RaycastSystem3D) {
        this._raycastSystem = raycastSystem;
    }

    /**
     * Conservative advancement sweep (TOI).
     *
     * Scans the swept segment in fixed sub-intervals [i/iter, (i+1)/iter], casting
     * forward from the *start* of each sub-interval by exactly one step. This
     * guarantees the entire path is covered with no gaps and no overshoot beyond
     * the endpoint, so tunnelling cannot occur as long as the step is smaller than
     * the smallest object diameter. The first contact is then tightened with a
     * bisection between the sub-interval bounds.
     */
    public sweepTest(query: ISweepTestQuery): ITimeOfImpact {
        const displacement = Vec3.subtract(query.endPosition, query.startPosition);
        const distance = Vec3.len(displacement);

        if (distance < this._epsilon) return this._createNoHit();

        const direction = Vec3.multiplyScalar(displacement, 1 / distance);
        const iterations = Math.max(1, Math.floor(query.maxIterations));
        const stepSize = distance / iterations;

        let prevT = 0;
        let prevPos = query.startPosition;

        for (let i = 0; i < iterations; i++) {
            const hit = this._raycastSystem.raycast(
                prevPos, direction, stepSize + this._epsilon, query.layerMask
            );

            if (hit && hit.distance <= stepSize + this._epsilon) {
                const hitFrac = hit.distance / distance;
                let lo = prevT;
                let hi = prevT + hitFrac;

                for (let b = 0; b < 12 && hi - lo > query.tolerance; b++) {
                    const mid = (lo + hi) * 0.5;
                    const midPos = Vec3.create(
                        query.startPosition.x + displacement.x * mid,
                        query.startPosition.y + displacement.y * mid,
                        query.startPosition.z + displacement.z * mid
                    );
                    const midHit = this._raycastSystem.raycast(
                        midPos, direction, distance * (1 - mid) + this._epsilon, query.layerMask
                    );
                    if (midHit) hi = mid;
                    else lo = mid;
                }

                const impactPos = Vec3.create(
                    query.startPosition.x + displacement.x * hi,
                    query.startPosition.y + displacement.y * hi,
                    query.startPosition.z + displacement.z * hi
                );
                const finalHit = this._raycastSystem.raycast(
                    impactPos, direction, this._epsilon * 2 + stepSize, query.layerMask
                );

                return {
                    hit: true,
                    time: hi,
                    fraction: hi,
                    normal: finalHit ? finalHit.normal : hit.normal,
                    witness1: impactPos,
                    witness2: finalHit ? finalHit.point : hit.point,
                };
            }

            prevT = (i + 1) / iterations;
            prevPos = Vec3.create(
                query.startPosition.x + displacement.x * prevT,
                query.startPosition.y + displacement.y * prevT,
                query.startPosition.z + displacement.z * prevT
            );
        }

        return this._createNoHit();
    }

    public continuousCast(
        startOrigin: Readonly<IVec3Like>,
        endOrigin: Readonly<IVec3Like>,
        direction: Readonly<IVec3Like>,
        maxDistance: number,
        layerMask: LayerMask,
        samples: number = 10
    ): IRaycastHit3D | null {
        let closestHit: IRaycastHit3D | null = null;
        let closestFraction = 1;

        for (let i = 0; i <= samples; i++) {
            const t = i / samples;
            const origin = Vec3.create(
                startOrigin.x * (1 - t) + endOrigin.x * t,
                startOrigin.y * (1 - t) + endOrigin.y * t,
                startOrigin.z * (1 - t) + endOrigin.z * t
            );

            const hit = this._raycastSystem.raycast(origin, direction, maxDistance, layerMask);

            if (hit) {
                const totalFraction = t + (1 - t) * hit.fraction;
                if (totalFraction < closestFraction) {
                    closestHit = hit;
                    closestFraction = totalFraction;
                }
            }
        }

        return closestHit;
    }

    public predictiveRaycast(
        origin: Readonly<IVec3Like>,
        velocity: Readonly<IVec3Like>,
        targetPosition: Readonly<IVec3Like>,
        targetVelocity: Readonly<IVec3Like>,
        layerMask: LayerMask,
        maxTime: number = 10,
        timeStep: number = 0.016
    ): IRaycastHit3D | null {
        const steps = Math.ceil(maxTime / timeStep);

        for (let i = 0; i < steps; i++) {
            const t = i * timeStep;

            const currentOrigin = Vec3.create(
                origin.x + velocity.x * t,
                origin.y + velocity.y * t,
                origin.z + velocity.z * t
            );

            const currentTarget = Vec3.create(
                targetPosition.x + targetVelocity.x * t,
                targetPosition.y + targetVelocity.y * t,
                targetPosition.z + targetVelocity.z * t
            );

            const direction = Vec3.subtract(currentTarget, currentOrigin);
            const distance = Vec3.len(direction);

            if (distance < EPSILON) continue;

            Vec3.normalize(direction, direction);

            const hit = this._raycastSystem.raycast(currentOrigin, direction, distance, layerMask);

            if (hit) {
                return hit;
            }
        }

        return null;
    }

    public linearSweep(
        start: Readonly<IVec3Like>,
        end: Readonly<IVec3Like>,
        layerMask: LayerMask,
        resolution: number = 0.1
    ): IRaycastHit3D | null {
        const displacement = Vec3.subtract(end, start);
        const distance = Vec3.len(displacement);

        if (distance < this._epsilon) {
            return null;
        }

        const direction = Vec3.multiplyScalar(displacement, 1 / distance);
        const steps = Math.max(1, Math.ceil(distance / resolution));
        const stepSize = distance / steps;

        for (let i = 0; i <= steps; i++) {
            const t = (i * stepSize) / distance;
            const position = Vec3.create(
                start.x + displacement.x * t,
                start.y + displacement.y * t,
                start.z + displacement.z * t
            );

            const remainingDistance = distance * (1 - t);
            const hit = this._raycastSystem.raycast(
                position,
                direction,
                remainingDistance + this._epsilon,
                layerMask
            );

            if (hit) {
                return hit;
            }
        }

        return null;
    }

    private _createNoHit(): ITimeOfImpact {
        return {
            hit: false,
            time: 0,
            fraction: 0,
            normal: Vec3.ZERO,
            witness1: Vec3.ZERO,
            witness2: Vec3.ZERO,
        };
    }
}

export class AdaptiveRaycaster3D {
    private readonly _raycastSystem: RaycastSystem3D;
    private readonly _performanceThreshold: number = 16.67;
    // Ring buffer for O(1) push/evict instead of array.shift() O(n)
    private readonly _historySize: number = 60;
    private readonly _history: Float64Array;
    private _historyHead: number = 0;
    private _historyCount: number = 0;
    private _currentQuality: number = 1.0;
    private _minQuality: number = 0.25;
    private _maxQuality: number = 1.0;

    constructor(raycastSystem: RaycastSystem3D) {
        this._raycastSystem = raycastSystem;
        this._history = new Float64Array(this._historySize);
    }

    public adaptiveRaycast(
        origin: Readonly<IVec3Like>,
        direction: Readonly<IVec3Like>,
        maxDistance: number,
        layerMask: LayerMask,
        performanceTime: number
    ): IRaycastHit3D | null {
        this._updatePerformanceHistory(performanceTime);
        this._adjustQuality();

        return this._raycastSystem.raycast(origin, direction, maxDistance, layerMask);
    }

    public adaptiveBatchRaycast(
        origins: readonly IVec3Like[],
        directions: readonly IVec3Like[],
        maxDistances: readonly number[],
        layerMask: LayerMask,
        performanceTime: number
    ): (IRaycastHit3D | null)[] {
        this._updatePerformanceHistory(performanceTime);
        this._adjustQuality();

        const count = Math.min(origins.length, directions.length, maxDistances.length);
        const qualityCount = Math.max(1, Math.floor(count * this._currentQuality));
        const stride = Math.max(1, Math.floor(count / qualityCount));

        const results: (IRaycastHit3D | null)[] = new Array(count).fill(null);

        for (let i = 0; i < count; i += stride) {
            const hit = this._raycastSystem.raycast(
                origins[i],
                directions[i],
                maxDistances[i],
                layerMask
            );
            results[i] = hit;
        }

        return results;
    }

    public get currentQuality(): number {
        return this._currentQuality;
    }

    public set minQuality(value: number) {
        this._minQuality = Math.max(0.1, Math.min(1.0, value));
    }

    public set maxQuality(value: number) {
        this._maxQuality = Math.max(0.1, Math.min(1.0, value));
    }

    private _updatePerformanceHistory(performanceTime: number): void {
        this._history[this._historyHead] = performanceTime;
        this._historyHead = (this._historyHead + 1) % this._historySize;
        if (this._historyCount < this._historySize) this._historyCount++;
    }

    private _adjustQuality(): void {
        if (this._historyCount < 10) return;

        // Compute average over last 10 samples from ring buffer
        let sum = 0;
        const count = Math.min(10, this._historyCount);
        for (let i = 0; i < count; i++) {
            const idx = (this._historyHead - 1 - i + this._historySize) % this._historySize;
            sum += this._history[idx];
        }
        const avgPerformance = sum / count;

        if (avgPerformance > this._performanceThreshold * 1.2) {
            this._currentQuality = Math.max(this._minQuality, this._currentQuality * 0.9);
        } else if (avgPerformance < this._performanceThreshold * 0.8) {
            this._currentQuality = Math.min(this._maxQuality, this._currentQuality * 1.1);
        }

        this._currentQuality = Math.max(
            this._minQuality,
            Math.min(this._maxQuality, this._currentQuality)
        );
    }
}

export class PriorityRaycaster3D {
    private readonly _raycastSystem: RaycastSystem3D;
    // Min-heap: O(log N) enqueue vs O(N log N) full sort
    private readonly _heap: Array<{
        priority: number;
        origin: IVec3Like;
        direction: IVec3Like;
        maxDistance: number;
        layerMask: LayerMask;
        callback: (hit: IRaycastHit3D | null) => void;
    }> = [];
    private _maxBudget: number = 100;
    private _currentBudget: number = 100;

    constructor(raycastSystem: RaycastSystem3D) {
        this._raycastSystem = raycastSystem;
    }

    public enqueue(
        priority: number,
        origin: Readonly<IVec3Like>,
        direction: Readonly<IVec3Like>,
        maxDistance: number,
        layerMask: LayerMask,
        callback: (hit: IRaycastHit3D | null) => void
    ): void {
        this._heap.push({ priority, origin, direction, maxDistance, layerMask, callback });
        this._heapifyUp(this._heap.length - 1);
    }

    public processBatch(budget?: number): number {
        const effectiveBudget = budget ?? this._currentBudget;
        let processed = 0;

        while (this._heap.length > 0 && processed < effectiveBudget) {
            const item = this._heapPop()!;
            const hit = this._raycastSystem.raycast(
                item.origin, item.direction, item.maxDistance, item.layerMask
            );
            item.callback(hit);
            processed++;
        }

        this._currentBudget = Math.max(0, effectiveBudget - processed);
        return processed;
    }

    public resetBudget(): void {
        this._currentBudget = this._maxBudget;
    }

    public set maxBudget(value: number) {
        this._maxBudget = Math.max(1, value);
    }

    public get queueSize(): number {
        return this._heap.length;
    }

    public clear(): void {
        this._heap.length = 0;
    }

    // Max-heap by priority (higher priority = processed first)
    private _heapifyUp(i: number): void {
        while (i > 0) {
            const parent = (i - 1) >> 1;
            if (this._heap[parent].priority >= this._heap[i].priority) break;
            const tmp = this._heap[parent];
            this._heap[parent] = this._heap[i];
            this._heap[i] = tmp;
            i = parent;
        }
    }

    private _heapifyDown(i: number): void {
        const n = this._heap.length;
        while (true) {
            let largest = i;
            const l = 2 * i + 1;
            const r = 2 * i + 2;
            if (l < n && this._heap[l].priority > this._heap[largest].priority) largest = l;
            if (r < n && this._heap[r].priority > this._heap[largest].priority) largest = r;
            if (largest === i) break;
            const tmp = this._heap[largest];
            this._heap[largest] = this._heap[i];
            this._heap[i] = tmp;
            i = largest;
        }
    }

    private _heapPop() {
        if (this._heap.length === 0) return null;
        const top = this._heap[0];
        const last = this._heap.pop()!;
        if (this._heap.length > 0) {
            this._heap[0] = last;
            this._heapifyDown(0);
        }
        return top;
    }
}

export function createContinuousRaycast3D(raycastSystem: RaycastSystem3D): ContinuousRaycast3D {
    return new ContinuousRaycast3D(raycastSystem);
}

export function createAdaptiveRaycaster3D(raycastSystem: RaycastSystem3D): AdaptiveRaycaster3D {
    return new AdaptiveRaycaster3D(raycastSystem);
}

export function createPriorityRaycaster3D(raycastSystem: RaycastSystem3D): PriorityRaycaster3D {
    return new PriorityRaycaster3D(raycastSystem);
}
