export type { SoaBufferOptions } from './soa-common';

const SYMBOL_DISPOSE: typeof Symbol.dispose =
    (Symbol as { dispose?: typeof Symbol.dispose }).dispose ??
    (Symbol('Symbol.dispose') as typeof Symbol.dispose);

export interface IDisposable {
    [SYMBOL_DISPOSE](): void;
    dispose(): void;
}

export { SYMBOL_DISPOSE };

export abstract class SoaBuffer<T extends Float32Array | Float64Array> implements IDisposable {
    protected _data: T;
    protected _count: number;
    protected _capacity: number;
    protected readonly _ctor: Float32ArrayConstructor | Float64ArrayConstructor;

    protected constructor(options: SoaBufferOptions, stride: number) {
        this._ctor = options.arrayType ?? Float32Array;
        this._capacity = Math.max(0, options.capacity | 0);
        this._count = 0;
        this._data = new this._ctor(this._capacity * stride) as T;
    }

    public get data(): T { return this._data; }
    public get count(): number { return this._count; }
    public get capacity(): number { return this._capacity; }
    public get byteLength(): number { return this._data.byteLength; }
    public abstract get stride(): number;

    public offsetOf(index: number): number {
        return (index | 0) * this.stride;
    }

    public copyElement(srcIdx: number, target: SoaBuffer<T>, dstIdx: number): void {
        const s = (srcIdx | 0) * this.stride;
        const d = (dstIdx | 0) * this.stride;
        const count = this.stride;
        const sData = this._data;
        const dData = target._data;
        for (let i = 0; i < count; ++i) {
            dData[d + i] = sData[s + i]!;
        }
    }

    public zero(): this {
        this._data.fill(0);
        return this;
    }

    public setCount(count: number): this {
        const next = count | 0;
        if (next > this._capacity) {
            this._grow(next);
        }
        this._count = next;
        return this;
    }

    public ensureCapacity(min: number): this {
        const target = min | 0;
        if (target > this._capacity) {
            this._grow(target);
        }
        return this;
    }

    public dispose(): void {
        this._data = new this._ctor(0) as T;
        this._count = 0;
        this._capacity = 0;
    }

    public [SYMBOL_DISPOSE](): void {
        this.dispose();
    }

    protected _grow(min: number): void {
        const target = Math.max(min | 0, (this._capacity * 2) | 0, 4);
        const stride = this.stride;
        const next = new this._ctor(target * stride) as T;
        next.set(this._data.subarray(0, this._count * stride));
        this._data = next;
        this._capacity = target;
    }
}
