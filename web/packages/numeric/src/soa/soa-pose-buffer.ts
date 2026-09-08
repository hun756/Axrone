import { SoaVec3Buffer, SoaVec3View } from './soa-vec3-buffer';
import { SoaQuatBuffer, SoaQuatView } from './soa-quat-buffer';
import type { SoaBufferOptions } from './soa-common';
import { IDisposable, SYMBOL_DISPOSE } from './soa-buffer';

export class SoaBoneHandle {
    constructor(
        public readonly pose: SoaPoseBuffer,
        public boneIndex: number,
    ) {}

    get translation(): SoaVec3View { return this.pose.translations.getView(this.boneIndex); }
    get rotation(): SoaQuatView { return this.pose.rotations.getView(this.boneIndex); }
    get scale(): SoaVec3View { return this.pose.scales.getView(this.boneIndex); }

    copyFrom(other: SoaBoneHandle): this {
        this.pose.translations.copyElement(other.boneIndex, this.pose.translations, this.boneIndex);
        this.pose.rotations.copyElement(other.boneIndex, this.pose.rotations, this.boneIndex);
        this.pose.scales.copyElement(other.boneIndex, this.pose.scales, this.boneIndex);
        return this;
    }
}

export class SoaPoseBuffer implements IDisposable {

    public readonly translations: SoaVec3Buffer;
    public readonly rotations: SoaQuatBuffer;
    public readonly scales: SoaVec3Buffer;
    public readonly boneCount: number;
    public readonly boneHandle: SoaBoneHandle;

    constructor(options: SoaBufferOptions & { boneCount: number }) {
        this.boneCount = options.boneCount | 0;
        const base: SoaBufferOptions = {
            capacity: this.boneCount,
            arrayType: options.arrayType,
        };
        this.translations = new SoaVec3Buffer(base);
        this.rotations = new SoaQuatBuffer(base);
        this.scales = new SoaVec3Buffer(base);
        this.translations.setCount(this.boneCount).zero();
        this.rotations.setCount(this.boneCount);
        this.scales.setCount(this.boneCount);
        for (let i = 0; i < this.boneCount; i++) {
            this.rotations.setElement(i, 0, 0, 0, 1);
            this.scales.setElement(i, 1, 1, 1);
        }
        this.boneHandle = new SoaBoneHandle(this, 0);
    }

    public bone(index: number): SoaBoneHandle {
        this.boneHandle.boneIndex = index | 0;
        return this.boneHandle;
    }

    public createBoneHandle(index: number): SoaBoneHandle {
        return new SoaBoneHandle(this, index | 0);
    }

    public lerpAll(a: SoaPoseBuffer, b: SoaPoseBuffer, t: number): this {
        this.translations.lerpAll(a.translations, b.translations, t);
        this.rotations.slerpAll(a.rotations, b.rotations, t);
        this.scales.lerpAll(a.scales, b.scales, t);
        return this;
    }

    public copyFrom(src: SoaPoseBuffer): this {
        this.translations.copyAllFrom(src.translations);
        this.rotations.copyAllFrom(src.rotations);
        this.scales.copyAllFrom(src.scales);
        return this;
    }

    public resetToRest(): this {
        this.translations.zero();
        for (let i = 0; i < this.boneCount; i++) {
            this.rotations.setElement(i, 0, 0, 0, 1);
            this.scales.setElement(i, 1, 1, 1);
        }
        return this;
    }

    public get byteLength(): number {
        return this.translations.byteLength + this.rotations.byteLength + this.scales.byteLength;
    }

    public dispose(): void {
        this.translations.dispose();
        this.rotations.dispose();
        this.scales.dispose();
    }

    public [SYMBOL_DISPOSE](): void { this.dispose(); }
}
