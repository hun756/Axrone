import type { IVec2Like } from '@axrone/numeric';
import type { Brand } from '@axrone/utility';

export type { Brand };

export type BrandedFactory<B extends string> = {
    readonly brand: B;
    create<T extends number>(value: T): Brand<T, B>;
    unwrap<B extends string>(branded: Brand<number, B>): number;
};

export function createBrandedFactory<B extends string>(brand: B): BrandedFactory<B> {
    return {
        brand,
        create: <T extends number>(value: T) => value as Brand<T, B>,
        unwrap: (branded) => branded as unknown as number,
    } as const;
}

export type ManagerState = 'idle' | 'active' | 'stepping' | 'disposed';

export type ErrorCode =
    | 'INVALID_STATE'
    | 'NOT_FOUND'
    | 'CAPACITY_EXCEEDED'
    | 'INVALID_ARGUMENT'
    | 'DUPLICATE'
    | 'PERMISSION_DENIED';

export class PhysicsError<TCode extends ErrorCode = ErrorCode> extends Error {
    readonly code: TCode;
    readonly timestamp: number;
    readonly context: Readonly<Record<string, unknown>>;

    constructor(message: string, code: TCode, context: Record<string, unknown> = {}) {
        super(message);
        this.name = 'PhysicsError';
        this.code = code;
        this.timestamp = performance.now();
        this.context = Object.freeze({ ...context });
        Object.setPrototypeOf(this, PhysicsError.prototype);
    }

    withContext(additional: Record<string, unknown>): PhysicsError<TCode> {
        return new PhysicsError<TCode>(this.message, this.code, {
            ...this.context,
            ...additional,
        });
    }
}

export function assertState(state: ManagerState, expected: ManagerState): asserts state is ManagerState {
    if (state !== expected) {
        throw new PhysicsError(
            `Expected state '${expected}', got '${state}'`,
            'INVALID_STATE',
            { expected, actual: state }
        );
    }
}

export function assertFound<T>(value: T | undefined, name: string, id: number): asserts value is T {
    if (value === undefined) {
        throw new PhysicsError(`${name} ${id} not found`, 'NOT_FOUND', { id });
    }
}

export function assertCapacity(current: number, max: number, name: string): void {
    if (current >= max) {
        throw new PhysicsError(
            `${name} capacity exceeded (${current}/${max})`,
            'CAPACITY_EXCEEDED',
            { current, max }
        );
    }
}

export type ReadonlyVec2 = Readonly<IVec2Like>;
export type IVec2Output = IVec2Like;

export type CollisionPairKey = Brand<number, 'CollisionPairKey'>;

export function makeCollisionPairKey(a: number, b: number): CollisionPairKey {
    const lo = a < b ? a : b;
    const hi = a < b ? b : a;
    return (lo * 0x100000 + hi) as CollisionPairKey;
}

export interface ISoAFieldDescriptor {
    readonly offset: number;
    readonly size: number;
}

export type SoASchema = Record<string, ISoAFieldDescriptor>;

export type SoAView<TSchema extends SoASchema> = {
    readonly [K in keyof TSchema]: Float64Array;
};

export abstract class SoAManager<TSchema extends SoASchema> implements Disposable {
    protected readonly _data: Float64Array;
    protected readonly _schema: TSchema;
    protected readonly _stride: number;
    protected readonly _capacity: number;
    protected _count: number = 0;
    protected _state: ManagerState = 'idle';

    constructor(capacity: number, schema: TSchema) {
        this._schema = schema;
        this._capacity = capacity;

        let maxOffset = 0;
        for (const field of Object.values(schema)) {
            maxOffset = Math.max(maxOffset, field.offset + field.size);
        }
        this._stride = maxOffset;
        this._data = new Float64Array(capacity * this._stride);
    }

    protected _getFieldOffset(index: number, field: keyof TSchema): number {
        return index * this._stride + this._schema[field].offset;
    }

    protected _getFieldSize(field: keyof TSchema): number {
        return this._schema[field].size;
    }

    protected _readScalar(index: number, field: keyof TSchema): number {
        return this._data[this._getFieldOffset(index, field)];
    }

    protected _writeScalar(index: number, field: keyof TSchema, value: number): void {
        this._data[this._getFieldOffset(index, field)] = value;
    }

    protected _readVec2(index: number, field: keyof TSchema, out?: IVec2Output): IVec2Output {
        const offset = this._getFieldOffset(index, field);
        if (out) {
            out.x = this._data[offset];
            out.y = this._data[offset + 1];
            return out;
        }
        return { x: this._data[offset], y: this._data[offset + 1] };
    }

    protected _writeVec2(index: number, field: keyof TSchema, value: ReadonlyVec2): void {
        const offset = this._getFieldOffset(index, field);
        this._data[offset] = value.x;
        this._data[offset + 1] = value.y;
    }

    protected _assertActive(): asserts this is { _state: 'active' } {
        assertState(this._state, 'active');
    }

    protected _assertNotDisposed(): asserts this is { _state: ManagerState } {
        if (this._state === 'disposed') {
            throw new PhysicsError('Manager is disposed', 'INVALID_STATE');
        }
    }

    get count(): number { return this._count; }
    get capacity(): number { return this._capacity; }
    get state(): ManagerState { return this._state; }

    activate(): void {
        if (this._state === 'idle') {
            this._state = 'active';
        }
    }

    abstract [Symbol.dispose](): void;
}

export type CollisionMatrix<
    TShapeKind extends string,
    TFn extends (a: any, b: any, ctx: any, manifold: any) => void
> = ReadonlyMap<`${TShapeKind}:${TShapeKind}`, TFn | null>;

export function buildCollisionMatrix<
    TShapeKind extends string,
    TFn extends (a: any, b: any, ctx: any, manifold: any) => void
>(
    entries: ReadonlyArray<[TShapeKind, TShapeKind, TFn | null]>
): CollisionMatrix<TShapeKind, TFn> {
    const matrix = new Map<string, TFn | null>();
    for (const [a, b, fn] of entries) {
        matrix.set(`${a}:${b}`, fn);
    }
    return matrix as CollisionMatrix<TShapeKind, TFn>;
}