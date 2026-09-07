import type { IVec3Like } from '../vec3';
import type { IQuatLike } from '../quat';

export class SoaScratchVec3 implements IVec3Like {
    constructor(public readonly d: Float32Array, public readonly o: number) {}
    get x(): number { return this.d[this.o]; }
    set x(v: number) { this.d[this.o] = v; }
    get y(): number { return this.d[this.o + 1]; }
    set y(v: number) { this.d[this.o + 1] = v; }
    get z(): number { return this.d[this.o + 2]; }
    set z(v: number) { this.d[this.o + 2] = v; }
    set(x: number, y: number, z: number): this {
        this.d[this.o] = x;
        this.d[this.o + 1] = y;
        this.d[this.o + 2] = z;
        return this;
    }
}

export class SoaScratchQuat implements IQuatLike {
    constructor(public readonly d: Float32Array, public readonly o: number) {}
    get x(): number { return this.d[this.o]; }
    set x(v: number) { this.d[this.o] = v; }
    get y(): number { return this.d[this.o + 1]; }
    set y(v: number) { this.d[this.o + 1] = v; }
    get z(): number { return this.d[this.o + 2]; }
    set z(v: number) { this.d[this.o + 2] = v; }
    get w(): number { return this.d[this.o + 3]; }
    set w(v: number) { this.d[this.o + 3] = v; }
    identity(): this { this.x = 0; this.y = 0; this.z = 0; this.w = 1; return this; }
}

export class SoaScratchMat4 {
    constructor(public readonly d: Float32Array, public readonly o: number) {}
    get data(): Float32Array { return this.d; }
    get offset(): number { return this.o; }
    identity(): this {
        const o = this.o;
        this.d.fill(0, o, o + 16);
        this.d[o] = 1;
        this.d[o + 5] = 1;
        this.d[o + 10] = 1;
        this.d[o + 15] = 1;
        return this;
    }
}

export class SoaScratchPool {

    public static readonly V3_COUNT = 8;
    public static readonly Q4_COUNT = 4;
    public static readonly M4_COUNT = 2;
    public static readonly V3_OFFSET = 0;
    public static readonly Q4_OFFSET = SoaScratchPool.V3_COUNT * 3;
    public static readonly M4_OFFSET = SoaScratchPool.Q4_OFFSET + SoaScratchPool.Q4_COUNT * 4;
    public static readonly TOTAL = SoaScratchPool.M4_OFFSET + SoaScratchPool.M4_COUNT * 16;
    public static readonly V3_MASK = SoaScratchPool.V3_COUNT - 1;
    public static readonly Q4_MASK = SoaScratchPool.Q4_COUNT - 1;
    public static readonly M4_MASK = SoaScratchPool.M4_COUNT - 1;

    public readonly data: Float32Array;
    public readonly v3Views: SoaScratchVec3[];
    public readonly q4Views: SoaScratchQuat[];
    public readonly m4Views: SoaScratchMat4[];

    constructor() {
        this.data = new Float32Array(SoaScratchPool.TOTAL);
        this.v3Views = new Array(SoaScratchPool.V3_COUNT);
        this.q4Views = new Array(SoaScratchPool.Q4_COUNT);
        this.m4Views = new Array(SoaScratchPool.M4_COUNT);
        for (let i = 0; i < SoaScratchPool.V3_COUNT; i++) {
            this.v3Views[i] = new SoaScratchVec3(this.data, SoaScratchPool.V3_OFFSET + i * 3);
        }
        for (let i = 0; i < SoaScratchPool.Q4_COUNT; i++) {
            this.q4Views[i] = new SoaScratchQuat(this.data, SoaScratchPool.Q4_OFFSET + i * 4);
        }
        for (let i = 0; i < SoaScratchPool.M4_COUNT; i++) {
            this.m4Views[i] = new SoaScratchMat4(this.data, SoaScratchPool.M4_OFFSET + i * 16);
        }
    }

    public vec3(slot: number): SoaScratchVec3 {
        return this.v3Views[slot & SoaScratchPool.V3_MASK]!;
    }

    public quat(slot: number): SoaScratchQuat {
        return this.q4Views[slot & SoaScratchPool.Q4_MASK]!;
    }

    public mat4(slot: number): SoaScratchMat4 {
        return this.m4Views[slot & SoaScratchPool.M4_MASK]!;
    }

    public clear(): void { this.data.fill(0); }
}
