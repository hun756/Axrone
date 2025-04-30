type Nominal<T, K extends string> = T & { readonly __brand: K };

export type GLBufferTarget =
    | WebGL2RenderingContext['ARRAY_BUFFER']
    | WebGL2RenderingContext['ELEMENT_ARRAY_BUFFER']
    | WebGL2RenderingContext['COPY_READ_BUFFER']
    | WebGL2RenderingContext['COPY_WRITE_BUFFER']
    | WebGL2RenderingContext['TRANSFORM_FEEDBACK_BUFFER']
    | WebGL2RenderingContext['UNIFORM_BUFFER']
    | WebGL2RenderingContext['PIXEL_PACK_BUFFER']
    | WebGL2RenderingContext['PIXEL_UNPACK_BUFFER'];

export type GLBufferUsage =
    | WebGL2RenderingContext['STATIC_DRAW']
    | WebGL2RenderingContext['DYNAMIC_DRAW']
    | WebGL2RenderingContext['STREAM_DRAW']
    | WebGL2RenderingContext['STATIC_READ']
    | WebGL2RenderingContext['DYNAMIC_READ']
    | WebGL2RenderingContext['STREAM_READ']
    | WebGL2RenderingContext['STATIC_COPY']
    | WebGL2RenderingContext['DYNAMIC_COPY']
    | WebGL2RenderingContext['STREAM_COPY'];

export type BufferId = Nominal<WebGLBuffer, 'BufferId'>;

export interface IDisposable {
    readonly dispose: () => void;
    readonly isDisposed: boolean;
}

export interface IBindableTarget<T> {
    readonly bind: () => T;
    readonly unbind: () => T;
}

export interface BufferOptions {
    readonly initialData?: BufferSource | null;
    readonly usage?: GLBufferUsage;
    readonly byteSize?: number;
    readonly label?: string;
}

export interface IBuffer extends IDisposable, IBindableTarget<IBuffer> {
    readonly id: BufferId;
    readonly target: GLBufferTarget;
    readonly byteLength: number;
    readonly usage: GLBufferUsage;
    readonly label: string | null;
    readonly update: <T extends BufferSource>(data: T, offset?: number) => IBuffer;

    readonly updateRange: <T extends BufferSource>(
        data: T,
        dstByteOffset: number,
        srcByteOffset?: number,
        length?: number
    ) => IBuffer;

    readonly resize: <T extends BufferSource | number>(
        dataOrByteSize: T,
        usage?: GLBufferUsage
    ) => IBuffer;

    readonly copyTo: (
        dstBuffer: IBuffer,
        srcOffset?: number,
        dstOffset?: number,
        size?: number
    ) => IBuffer;

    readonly getData: <T extends ArrayBufferView>(
        output: T,
        byteOffset?: number,
        length?: number
    ) => T;

    readonly getSubData: <T extends ArrayBufferView>(
        output: T,
        srcByteOffset: number,
        dstByteOffset?: number,
        length?: number
    ) => T;
}

export interface IBufferPool<T extends BufferSource> extends IDisposable {
    readonly allocate: (size: number, usage?: GLBufferUsage) => IBuffer;
    readonly release: (buffer: IBuffer) => void;
    readonly acquire: (data: T, usage?: GLBufferUsage) => IBuffer;
}

export interface IBufferFactory {
    readonly createBuffer: (target: GLBufferTarget, options?: BufferOptions) => IBuffer;
    readonly createArrayBuffer: (options?: BufferOptions) => IBuffer;
    readonly createElementArrayBuffer: (options?: BufferOptions) => IBuffer;
    readonly createUniformBuffer: (options?: BufferOptions) => IBuffer;

    readonly createBufferFromData: <T extends BufferSource>(
        target: GLBufferTarget,
        data: T,
        usage?: GLBufferUsage
    ) => IBuffer;

    readonly createArrayBufferFromData: <T extends BufferSource>(
        data: T,
        usage?: GLBufferUsage
    ) => IBuffer;

    readonly createElementArrayBufferFromData: <T extends BufferSource>(
        data: T,
        usage?: GLBufferUsage
    ) => IBuffer;

    readonly createUniformBufferFromData: <T extends BufferSource>(
        data: T,
        usage?: GLBufferUsage
    ) => IBuffer;

    readonly createPool: <T extends BufferSource>() => IBufferPool<T>;
}

export type ErrorCode =
    | 'INVALID_OPERATION'
    | 'BUFFER_ALREADY_DISPOSED'
    | 'OUT_OF_MEMORY'
    | 'INVALID_VALUE'
    | 'CONTEXT_LOST'
    | 'UNSUPPORTED_OPERATION';

export class GLError extends Error {
    constructor(
        public readonly message: string,
        public readonly code: ErrorCode,
        public readonly cause?: Error
    ) {
        super(`[WebGL2] ${code}: ${message}`);
        Object.setPrototypeOf(this, new.target.prototype);
        Error.captureStackTrace?.(this, this.constructor);
    }
}
