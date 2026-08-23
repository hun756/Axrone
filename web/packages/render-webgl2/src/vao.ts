import { IVec2Like, IVec3Like, IVec4Like } from '@axrone/numeric';
import { ObjectPool, ByteBuffer } from '@axrone/memory';
import type { Brand, TypedArray } from '@axrone/utility';
import { createBufferFactory, IBuffer, IBufferFactory } from './buffer';
import type { ContextSource, IGLContext } from './context';
import { resolveContext } from './context';
import { GLContextError } from './context/errors';
import { DisposalTracker, assertAlive } from './internal/disposable';

const GL = {
    // GL data types
    BYTE: 0x1400,
    UNSIGNED_BYTE: 0x1401,
    SHORT: 0x1402,
    UNSIGNED_SHORT: 0x1403,
    INT: 0x1404,
    UNSIGNED_INT: 0x1405,
    FLOAT: 0x1406,
    HALF_FLOAT: 0x140b,

    // GL buffer usage
    STATIC_DRAW: 0x88e4,
    DYNAMIC_DRAW: 0x88e8,
    STREAM_DRAW: 0x88e0,

    // GL draw primitives
    POINTS: 0x0000,
    LINES: 0x0001,
    LINE_LOOP: 0x0002,
    LINE_STRIP: 0x0003,
    TRIANGLES: 0x0004,
    TRIANGLE_STRIP: 0x0005,
    TRIANGLE_FAN: 0x0006,

    // Half-float encoding bit masks (used in IEEE 754 half-precision conversion)
    HALF_FLOAT_SIGN_BIT: 0x8000,
    HALF_FLOAT_EXPONENT_MASK: 0x7c00,
    HALF_FLOAT_MANTISSA_MASK: 0x200,
    HALF_FLOAT_IMPLICIT_ONE: 0x800000,

    // IEEE 754 float32 bit field masks (used to extract exponent/mantissa during half-float conversion)
    FLOAT32_EXPONENT_BITS: 0xff,
    FLOAT32_MANTISSA_BITS: 0x7fffff,
} as const;

type GLContext = Brand<WebGL2RenderingContext, 'GLContext'>;
type GLBuffer = IBuffer;
type GLVAO = Brand<WebGLVertexArrayObject, 'GLVAO'>;

type GLDataType = typeof GL.BYTE | typeof GL.UNSIGNED_BYTE | typeof GL.SHORT | typeof GL.UNSIGNED_SHORT | typeof GL.INT | typeof GL.UNSIGNED_INT | typeof GL.FLOAT | typeof GL.HALF_FLOAT;
type GLUsage = typeof GL.STATIC_DRAW | typeof GL.DYNAMIC_DRAW | typeof GL.STREAM_DRAW;
type GLPrimitive = typeof GL.POINTS | typeof GL.LINES | typeof GL.LINE_LOOP | typeof GL.LINE_STRIP | typeof GL.TRIANGLES | typeof GL.TRIANGLE_STRIP | typeof GL.TRIANGLE_FAN;
type IndexType = typeof GL.UNSIGNED_BYTE | typeof GL.UNSIGNED_SHORT | typeof GL.UNSIGNED_INT;

type ComponentCount = 1 | 2 | 3 | 4;
type AlignedOffset = Brand<number, 'AlignedOffset'>;
type ByteStride = Brand<number, 'ByteStride'>;
type ResourceID = Brand<number, 'ResourceID'>;

interface TypeInfo {
    readonly bytes: 1 | 2 | 4;
    readonly ctor: new (length: number) => TypedArray;
    readonly align: 1 | 2 | 4;
}

type GLTypeInfo = {
    readonly [GL.BYTE]: TypeInfo & { bytes: 1; ctor: Int8ArrayConstructor; align: 1 };
    readonly [GL.UNSIGNED_BYTE]: TypeInfo & { bytes: 1; ctor: Uint8ArrayConstructor; align: 1 };
    readonly [GL.SHORT]: TypeInfo & { bytes: 2; ctor: Int16ArrayConstructor; align: 2 };
    readonly [GL.UNSIGNED_SHORT]: TypeInfo & { bytes: 2; ctor: Uint16ArrayConstructor; align: 2 };
    readonly [GL.INT]: TypeInfo & { bytes: 4; ctor: Int32ArrayConstructor; align: 4 };
    readonly [GL.UNSIGNED_INT]: TypeInfo & { bytes: 4; ctor: Uint32ArrayConstructor; align: 4 };
    readonly [GL.FLOAT]: TypeInfo & { bytes: 4; ctor: Float32ArrayConstructor; align: 4 };
    readonly [GL.HALF_FLOAT]: TypeInfo & { bytes: 2; ctor: Uint16ArrayConstructor; align: 2 };
};

interface Attribute<
    N extends string = string,
    T extends GLDataType = GLDataType,
    S extends ComponentCount = ComponentCount,
> {
    readonly name: N;
    readonly type: T;
    readonly size: S;
    readonly normalized: boolean;
    readonly divisor: number;
}

type AttributeValue<A extends Attribute> = A['size'] extends 1
    ? number
    : A['size'] extends 2
      ? IVec2Like
      : A['size'] extends 3
        ? IVec3Like
        : A['size'] extends 4
          ? IVec4Like
          : number;

type ComputeStride<T extends readonly Attribute[]> = number;

type AlignOffset<Offset extends number, Align extends number> = AlignedOffset | number;

type ComputeLayout<T extends readonly Attribute[]> = readonly (Attribute & {
    offset: AlignedOffset;
})[];

type VertexData<T extends readonly Attribute[]> = {
    readonly [K in T[number]['name']]: AttributeValue<Extract<T[number], { name: K }>>;
};

interface Layout<T extends readonly Attribute[]> {
    readonly attributes: ComputeLayout<T>;
    readonly stride: ComputeStride<T> & ByteStride;
    readonly usage: GLUsage;
}

interface IndexConfig {
    readonly type: IndexType;
    readonly count: number;
    readonly offset: number;
}

interface DrawCall {
    readonly primitive: GLPrimitive;
    readonly first: number;
    readonly count: number;
    readonly instances: number;
}

interface ResourceDescriptor<T> {
    resource: T | null;
    refCount: number;
    generation: number;
}

interface ResourcePool<T> {
    allocate(resource: T): ResourceID;
    acquire(id: ResourceID): T | null;
    release(id: ResourceID): boolean;
    dispose(): T[];
}

class ResourcePoolAdapter<T> implements ResourcePool<T> {
    private readonly descriptors = new Map<ResourceID, ResourceDescriptor<T>>();
    private readonly free: ResourceID[] = [];
    private readonly descriptorPool: ObjectPool<ResourceDescriptor<T>>;
    private generation = 0;
    private nextId = 1;

    constructor() {
        this.descriptorPool = new ObjectPool<ResourceDescriptor<T>>({
            factory: () => ({ resource: null as any, refCount: 0, generation: 0 }),
            resetHandler: (d) => {
                d.resource = null as any;
                d.refCount = 0;
                d.generation = 0;
            },
            preallocate: false,
        });
    }

    allocate(resource: T): ResourceID {
        const id = (this.free.pop() ?? this.nextId++) as ResourceID;
        const descriptor = this.descriptorPool.acquire();
        descriptor.resource = resource as T;
        descriptor.refCount = 1;
        descriptor.generation = this.generation;
        this.descriptors.set(id, descriptor);
        return id;
    }

    acquire(id: ResourceID): T | null {
        const descriptor = this.descriptors.get(id);
        if (!descriptor || descriptor.generation !== this.generation) return null;

        descriptor.refCount = descriptor.refCount + 1;
        return descriptor.resource as T;
    }

    release(id: ResourceID): boolean {
        const descriptor = this.descriptors.get(id);
        if (!descriptor || descriptor.generation !== this.generation) return false;

        const newRefCount = descriptor.refCount - 1;

        if (newRefCount === 0) {
            this.descriptors.delete(id);
            this.free.push(id);
            this.descriptorPool.release(descriptor);
            return true;
        }

        descriptor.refCount = newRefCount;
        return false;
    }

    dispose(): T[] {
        const resources: T[] = Array.from(this.descriptors.values()).map((d) => d.resource as T);
        this.descriptors.clear();
        this.free.length = 0;
        this.generation++;
        this.nextId = 1;
        try {
            (this.descriptorPool as any)[Symbol.dispose]?.();
        } catch { // best-effort
            // ignore disposal errors
        }
        return resources;
    }

    getResource(id: ResourceID): T | null {
        const descriptor = this.descriptors.get(id);
        if (!descriptor || descriptor.generation !== this.generation) return null;
        return descriptor.resource as T;
    }
}

const TYPE_DESCRIPTORS: GLTypeInfo = {
    [GL.BYTE]: { bytes: 1, ctor: Int8Array, align: 1 },
    [GL.UNSIGNED_BYTE]: { bytes: 1, ctor: Uint8Array, align: 1 },
    [GL.SHORT]: { bytes: 2, ctor: Int16Array, align: 2 },
    [GL.UNSIGNED_SHORT]: { bytes: 2, ctor: Uint16Array, align: 2 },
    [GL.INT]: { bytes: 4, ctor: Int32Array, align: 4 },
    [GL.UNSIGNED_INT]: { bytes: 4, ctor: Uint32Array, align: 4 },
    [GL.FLOAT]: { bytes: 4, ctor: Float32Array, align: 4 },
    [GL.HALF_FLOAT]: { bytes: 2, ctor: Uint16Array, align: 2 },
} as const;

class BufferAllocator {
    private readonly _ctx: IGLContext;
    private readonly gl: WebGL2RenderingContext;
    private readonly factory: IBufferFactory;
    private readonly vertexPool = new ResourcePoolAdapter<IBuffer>();
    private readonly indexPool = new ResourcePoolAdapter<IBuffer>();

    /** @deprecated Passing a raw WebGL2RenderingContext is deprecated. Prefer IGLContext. */


    constructor(source: ContextSource) {
        const ctx = resolveContext(source);
        this._ctx = ctx;
        this.gl = ctx.gl;
        this.factory = createBufferFactory(ctx);
    }

    createVertexBuffer<T extends readonly Attribute[]>(
        layout: Layout<T>,
        vertices: readonly VertexData<T>[]
    ): ResourceID {
        const bb = this.packVertices(layout, vertices);
        const view = bb.toUint8Array();

        const ib = this.factory.createArrayBufferFromData(
            view as unknown as BufferSource,
            layout.usage
        );

        try {
            bb.release();
        } catch { // best-effort
            // ignore pool release errors
        }

        return this.vertexPool.allocate(ib);
    }

    createIndexBuffer(
        data: Uint8Array | Uint16Array | Uint32Array,
        usage: GLUsage = GL.STATIC_DRAW
    ): ResourceID {
        const ib = this.factory.createElementArrayBufferFromData(
            data as unknown as BufferSource,
            usage
        );
        return this.indexPool.allocate(ib);
    }

    private packVertices<T extends readonly Attribute[]>(
        layout: Layout<T>,
        vertices: readonly VertexData<T>[]
    ): ByteBuffer {
        const stride = layout.stride;
        const bb = ByteBuffer.alloc(vertices.length * stride);

        for (let i = 0; i < vertices.length; i++) {
            const vertex = vertices[i];
            const baseOffset = i * stride;

            for (const attr of layout.attributes) {
                const value = vertex[attr.name as keyof typeof vertex];
                this.packAttribute(bb, baseOffset + attr.offset, value, attr);
            }
        }

        bb.flip();
        return bb;
    }

    private packAttribute(
        bb: ByteBuffer,
        offset: number,
        value: number | readonly number[] | IVec2Like | IVec3Like | IVec4Like,
        attr: Attribute & { offset: AlignedOffset }
    ): void {
        const info = TYPE_DESCRIPTORS[attr.type];
        if (typeof value === 'number') {
            this.writeValue(bb, offset, value, attr.type);
            return;
        }

        let arr: readonly number[];

        if (Array.isArray(value)) {
            arr = value;
        } else {
            if ('w' in value) {
                const v = value as IVec4Like;
                arr = [v.x, v.y, v.z, v.w];
            } else if ('z' in value) {
                const v = value as IVec3Like;
                arr = [v.x, v.y, v.z];
            } else {
                const v = value as IVec2Like;
                arr = [v.x, v.y];
            }
        }

        for (let i = 0; i < arr.length; i++) {
            this.writeValue(bb, offset + i * info.bytes, arr[i], attr.type);
        }
    }

    private writeValue(bb: ByteBuffer, offset: number, value: number, type: GLDataType): void {
        switch (type) {
            case GL.BYTE:
                bb.seek(offset);
                bb.putInt8(value);
                break;
            case GL.UNSIGNED_BYTE:
                bb.seek(offset);
                bb.putUint8(value);
                break;
            case GL.SHORT:
                bb.seek(offset);
                bb.putInt16(value);
                break;
            case GL.UNSIGNED_SHORT:
                bb.seek(offset);
                bb.putUint16(value);
                break;
            case GL.INT:
                bb.seek(offset);
                bb.putInt32(value);
                break;
            case GL.UNSIGNED_INT:
                bb.seek(offset);
                bb.putUint32(value);
                break;
            case GL.FLOAT:
                bb.seek(offset);
                bb.putFloat32(value);
                break;
            case GL.HALF_FLOAT:
                bb.seek(offset);
                bb.putUint16(this.encodeF16(value));
                break;
        }
    }

    private encodeF16(f32: number): number {
        const fbuf = new ArrayBuffer(4);
        new Float32Array(fbuf)[0] = f32;
        const bits = new Uint32Array(fbuf)[0];

        const s = (bits >>> 16) & GL.HALF_FLOAT_SIGN_BIT;
        const e = ((bits >>> 23) & GL.FLOAT32_EXPONENT_BITS) - 127;
        const m = bits & GL.FLOAT32_MANTISSA_BITS;

        return e === 128
            ? s | GL.HALF_FLOAT_EXPONENT_MASK | (m ? GL.HALF_FLOAT_MANTISSA_MASK : 0)
            : e > 15
              ? s | GL.HALF_FLOAT_EXPONENT_MASK
              : e > -15
                ? s | ((e + 15) << 10) | (m >>> 13)
                : (() => {
                      const shift = -14 - e;
                      return shift > 24 ? s : s | ((m | GL.HALF_FLOAT_IMPLICIT_ONE) >>> (shift + 13));
                  })();
    }

    getVertexBuffer(id: ResourceID): GLBuffer | null {
        return this.vertexPool.acquire(id);
    }

    getIndexBuffer(id: ResourceID): GLBuffer | null {
        return this.indexPool.acquire(id);
    }

    releaseVertexBuffer(id: ResourceID): void {
        const buffer = this.vertexPool.getResource(id);
        if (this.vertexPool.release(id)) {
            if (buffer && !buffer.isDisposed) buffer.dispose();
        }
    }

    releaseIndexBuffer(id: ResourceID): void {
        const buffer = this.indexPool.getResource(id);
        if (this.indexPool.release(id)) {
            if (buffer && !buffer.isDisposed) buffer.dispose();
        }
    }

    dispose(): void {
        for (const buffer of this.vertexPool.dispose()) {
            if (buffer && !buffer.isDisposed) buffer.dispose();
        }
        for (const buffer of this.indexPool.dispose()) {
            if (buffer && !buffer.isDisposed) buffer.dispose();
        }
        this.factory.dispose();
    }
}

class VertexArray<T extends readonly Attribute[]> {
    private readonly _ctx: IGLContext;
    private readonly gl: WebGL2RenderingContext;
    private readonly vao: GLVAO;
    private readonly layout: Layout<T>;
    private readonly allocator: BufferAllocator;
    private readonly vertexBuffer: ResourceID;
    private readonly indexBuffer?: ResourceID;
    private readonly indexConfig?: IndexConfig;
    private readonly vertexCount: number;
    private readonly _disposal = new DisposalTracker();

    constructor(
        source: ContextSource,
        allocator: BufferAllocator,
        layout: Layout<T>,
        vertexBuffer: ResourceID,
        vertexCount: number,
        indexBuffer?: ResourceID,
        indexConfig?: IndexConfig
    ) {
        const ctx = resolveContext(source);
        this._ctx = ctx;
        this.gl = ctx.gl;
        this.allocator = allocator;
        this.layout = layout;
        this.vertexBuffer = vertexBuffer;
        this.indexBuffer = indexBuffer;
        this.indexConfig = indexConfig;
        this.vertexCount = vertexCount;

        const vao = this.gl.createVertexArray();
        if (!vao) throw new GLContextError('INVALID_OPERATION', 'en', { reason: 'VAO allocation failed' });
        this.vao = vao as GLVAO;

        this.configure(allocator);
    }

    private configure(allocator: BufferAllocator): void {
        this._ctx.state.bindVertexArray(this.vao as WebGLVertexArrayObject);

        const vb = allocator.getVertexBuffer(this.vertexBuffer);
        if (!vb) throw new GLContextError('INVALID_OPERATION', 'en', { reason: 'Invalid vertex buffer' });

        vb.bind();

        this.layout.attributes.forEach((attr, index) => {
            this.gl.enableVertexAttribArray(index);
            this.gl.vertexAttribPointer(
                index,
                attr.size,
                attr.type,
                attr.normalized,
                this.layout.stride,
                attr.offset
            );

            if (attr.divisor > 0) {
                this.gl.vertexAttribDivisor(index, attr.divisor);
            }
        });

        if (this.indexBuffer !== undefined) {
            const ib = allocator.getIndexBuffer(this.indexBuffer);
            if (!ib) throw new GLContextError('INVALID_OPERATION', 'en', { reason: 'Invalid index buffer' });
            ib.bind();
        }

        this._ctx.state.bindVertexArray(null);
    }

    bind(): this {
        assertAlive(this._disposal, 'VertexArray');
        this._ctx.state.bindVertexArray(this.vao as WebGLVertexArrayObject);
        return this;
    }

    unbind(): this {
        this._ctx.state.bindVertexArray(null);
        return this;
    }

    draw(call: DrawCall): this {
        this.bind();

        const count = call.count || (this.indexConfig?.count ?? this.vertexCount);

        if (this.indexConfig) {
            const indexSize = this.getIndexByteSize(this.indexConfig.type);
            const offset = call.first * indexSize;

            call.instances > 0
                ? this.gl.drawElementsInstanced(
                      call.primitive,
                      count,
                      this.indexConfig.type,
                      offset,
                      call.instances
                  )
                : this.gl.drawElements(call.primitive, count, this.indexConfig.type, offset);
        } else {
            call.instances > 0
                ? this.gl.drawArraysInstanced(call.primitive, call.first, count, call.instances)
                : this.gl.drawArrays(call.primitive, call.first, count);
        }

        return this;
    }

    private getIndexByteSize(type: IndexType): number {
        return type === GL.UNSIGNED_BYTE ? 1 : type === GL.UNSIGNED_SHORT ? 2 : 4;
    }

    get isIndexed(): boolean {
        return this.indexConfig !== undefined;
    }

    get primitiveCount(): number {
        return this.indexConfig?.count ?? this.vertexCount;
    }

    dispose(): void {
        if (!this._disposal.markDisposed()) return;
        this.gl.deleteVertexArray(this.vao as WebGLVertexArrayObject);

        // Release the refCounts acquired in configure()
        try { this.allocator.releaseVertexBuffer(this.vertexBuffer); } catch { /* best-effort */ }
        if (this.indexBuffer !== undefined) {
            try { this.allocator.releaseIndexBuffer(this.indexBuffer); } catch { /* best-effort */ }
        }
    }
}

class VAORegistry {
    private readonly _ctx: IGLContext;
    private readonly gl: WebGL2RenderingContext;
    private readonly allocator: BufferAllocator;
    private readonly registry = new ResourcePoolAdapter<VertexArray<any>>();

    /** @deprecated Passing a raw WebGL2RenderingContext is deprecated. Prefer IGLContext. */


    constructor(source: ContextSource) {
        const ctx = resolveContext(source);
        this._ctx = ctx;
        this.gl = ctx.gl;
        this.allocator = new BufferAllocator(ctx);
    }

    create<T extends readonly Attribute[]>(
        layout: Layout<T>,
        vertices: readonly VertexData<T>[],
        indices?: Uint8Array | Uint16Array | Uint32Array
    ): ResourceID {
        const vertexBufferId = this.allocator.createVertexBuffer(layout, vertices);

        let indexBufferId: ResourceID | undefined;
        let indexConfig: IndexConfig | undefined;

        if (indices) {
            indexBufferId = this.allocator.createIndexBuffer(indices);
            indexConfig = {
                type:
                    indices instanceof Uint8Array
                        ? GL.UNSIGNED_BYTE
                        : indices instanceof Uint16Array
                          ? GL.UNSIGNED_SHORT
                          : GL.UNSIGNED_INT,
                count: indices.length,
                offset: 0,
            };
        }

        const vao = new VertexArray(
            this._ctx,
            this.allocator,
            layout,
            vertexBufferId,
            vertices.length,
            indexBufferId,
            indexConfig
        );

        return this.registry.allocate(vao);
    }

    get<T extends readonly Attribute[]>(id: ResourceID): VertexArray<T> | null {
        return this.registry.acquire(id);
    }

    release(id: ResourceID): void {
        const vao = this.registry.getResource(id);
        if (this.registry.release(id)) {
            if (vao) vao.dispose();
        }
    }

    dispose(): void {
        for (const vao of this.registry.dispose()) {
            vao.dispose();
        }
        this.allocator.dispose();
    }
}

const attr = <N extends string, T extends GLDataType, S extends ComponentCount>(
    name: N,
    type: T,
    size: S,
    normalized: boolean = false,
    divisor: number = 0
): Attribute<N, T, S> => ({ name, type, size, normalized, divisor });

const layout = <T extends readonly Attribute[]>(
    attributes: T,
    usage: GLUsage = GL.STATIC_DRAW
): Layout<T> => {
    let offset = 0;
    const layoutAttrs = attributes.map((a) => {
        const info = TYPE_DESCRIPTORS[a.type];
        const aligned =
            offset % info.align === 0 ? offset : offset + (info.align - (offset % info.align));
        const result = { ...a, offset: aligned as AlignedOffset };
        offset = aligned + info.bytes * a.size;
        return result;
    }) as ComputeLayout<T>;

    return {
        attributes: layoutAttrs,
        stride: offset as ComputeStride<T> & ByteStride,
        usage,
    };
};

const draw = (
    primitive: GLPrimitive,
    first: number = 0,
    count: number = 0,
    instances: number = 0
): DrawCall => ({ primitive, first, count, instances });

const createVAOFactory = () => {
    const registries = new WeakMap<IGLContext, VAORegistry>();

    return (source: ContextSource) => {
        const ctx = resolveContext(source);
        let registry = registries.get(ctx);
        if (!registry) {
            registry = new VAORegistry(ctx);
            registries.set(ctx, registry);
        }
        return registry;
    };
};

export type {
    GLContext,
    ContextSource,
    GLBuffer,
    GLVAO,
    GLDataType,
    GLUsage,
    GLPrimitive,
    IndexType,
    ComponentCount,
    AlignedOffset,
    ByteStride,
    ResourceID,
    Attribute,
    AttributeValue,
    VertexData,
    Layout,
    IndexConfig,
    DrawCall,
    ComputeStride,
    ComputeLayout,
    TypeInfo,
    GLTypeInfo,
};

export { VertexArray, VAORegistry, BufferAllocator, attr, layout, draw, createVAOFactory, GL };
