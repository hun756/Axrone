import { ObjectPool } from '@axrone/memory';
import { AnimationFrame, type AnimationCurveLayout } from './pose-frame';
import { createAdditiveFrameScratch, type AdditiveFrameScratch } from './pose-blend';
import type { AnimationRig } from './rig';
import type { AnimationParameterStore } from './parameters';
import { MAX_BLEND_DEPTH } from './blend-types';

export class AnimationScratchPool {
    private readonly _framePool: ObjectPool<AnimationFrame>;
    private readonly _activeFrames: AnimationFrame[] = [];
    private readonly _frameArrayPool: ObjectPool<AnimationFrame[]>;
    private readonly _weightArrayPool: ObjectPool<number[]>;
    private readonly _activeFrameArrays: AnimationFrame[][] = [];
    private readonly _activeWeightArrays: number[][] = [];

    constructor(
        private readonly _rig: AnimationRig,
        private readonly _curveLayout: AnimationCurveLayout,
        private readonly _curveDefaults?: ArrayLike<number>
    ) {
        this._framePool = new ObjectPool<AnimationFrame>({
            initialCapacity: 8,
            maxCapacity: 256,
            minFree: 8,
            expansionStrategy: 'multiplicative',
            expansionFactor: 1.5,
            allocationStrategy: 'least-recently-used',
            evictionPolicy: 'lru',
            resetOnRecycle: true,
            preallocate: false,
            autoExpand: true,
            enableMetrics: false,
            name: 'AnimationScratchPool',
            factory: () => new AnimationFrame(this._rig, this._curveLayout),
            resetHandler: (frame) => {
                frame.reset(this._rig, this._curveDefaults);
            },
        });
        this._frameArrayPool = new ObjectPool<AnimationFrame[]>({
            initialCapacity: 4,
            maxCapacity: 32,
            minFree: 2,
            expansionStrategy: 'multiplicative',
            expansionFactor: 1.5,
            allocationStrategy: 'least-recently-used',
            evictionPolicy: 'lru',
            resetOnRecycle: true,
            preallocate: false,
            autoExpand: true,
            enableMetrics: false,
            name: 'AnimationFrameArrayPool',
            factory: () => [],
            resetHandler: (arr) => { arr.length = 0; },
        });
        this._weightArrayPool = new ObjectPool<number[]>({
            initialCapacity: 4,
            maxCapacity: 32,
            minFree: 2,
            expansionStrategy: 'multiplicative',
            expansionFactor: 1.5,
            allocationStrategy: 'least-recently-used',
            evictionPolicy: 'lru',
            resetOnRecycle: true,
            preallocate: false,
            autoExpand: true,
            enableMetrics: false,
            name: 'AnimationWeightArrayPool',
            factory: () => [],
            resetHandler: (arr) => { arr.length = 0; },
        });
    }

    reset(): void {
        for (let index = this._activeFrames.length - 1; index >= 0; index -= 1) {
            this._framePool.release(this._activeFrames[index]!);
        }
        this._activeFrames.length = 0;
        for (let index = this._activeFrameArrays.length - 1; index >= 0; index -= 1) {
            this._frameArrayPool.release(this._activeFrameArrays[index]!);
        }
        this._activeFrameArrays.length = 0;
        for (let index = this._activeWeightArrays.length - 1; index >= 0; index -= 1) {
            this._weightArrayPool.release(this._activeWeightArrays[index]!);
        }
        this._activeWeightArrays.length = 0;
    }

    acquire(): AnimationFrame {
        const frame = this._framePool.acquire();
        frame.reset(this._rig, this._curveDefaults);
        this._activeFrames.push(frame);
        return frame;
    }

    acquireFrameArray(length: number): AnimationFrame[] {
        const arr = this._frameArrayPool.acquire();
        arr.length = length;
        this._activeFrameArrays.push(arr);
        return arr;
    }

    acquireWeightArray(length: number): number[] {
        const arr = this._weightArrayPool.acquire();
        arr.length = length;
        this._activeWeightArrays.push(arr);
        return arr;
    }
}

interface RootDeltaScratchSlot {
    readonly translation: Float32Array;
    readonly rotation: Float32Array;
}

export class BlendScratchContext {
    private readonly _blend2DWeightSlots: number[][] = [];
    private readonly _rootDeltaSlots: RootDeltaScratchSlot[] = [];
    private readonly _referenceRotation = new Float32Array(4);
    private readonly _additiveScratch: AdditiveFrameScratch = createAdditiveFrameScratch();

    acquireBlend2DWeights(depth: number, count: number): number[] {
        const clampedDepth = Math.min(depth, MAX_BLEND_DEPTH - 1);
        let slot = this._blend2DWeightSlots[clampedDepth];
        if (!slot) {
            slot = new Array<number>(count);
            this._blend2DWeightSlots[clampedDepth] = slot;
        } else if (slot.length < count) {
            slot.length = count;
        }
        return slot;
    }

    acquireRootDeltaScratch(slotIndex: number): RootDeltaScratchSlot {
        const clampedIndex = Math.min(slotIndex, MAX_BLEND_DEPTH * 2 - 1);
        let slot = this._rootDeltaSlots[clampedIndex];
        if (!slot) {
            slot = {
                translation: new Float32Array(3),
                rotation: new Float32Array(4),
            };
            this._rootDeltaSlots[clampedIndex] = slot;
        }
        return slot;
    }

    get referenceRotation(): Float32Array {
        return this._referenceRotation;
    }

    get additiveScratch(): AdditiveFrameScratch {
        return this._additiveScratch;
    }
}

export interface AnimationMotionEvaluationContext {
    readonly rig: AnimationRig;
    readonly parameters: AnimationParameterStore;
    readonly restFrame: AnimationFrame;
    readonly scratch: AnimationScratchPool;
    readonly blendScratch: BlendScratchContext;
}
