import { ByteOrder, TypedArrayMap, PrimitiveTypeMap } from './types';
import { IReadableBuffer } from './interfaces';

export class BufferView<T extends keyof TypedArrayMap> implements IReadableBuffer {
    private readonly buffer: any; // ByteBuffer reference
    private readonly arrayType: T;
    private readonly bytesPerElement: number;

    constructor(buffer: any, arrayType: T) {
        this.buffer = buffer;
        this.arrayType = arrayType;

        switch (arrayType) {
            case 'int8':
            case 'uint8':
                this.bytesPerElement = 1;
                break;
            case 'int16':
            case 'uint16':
                this.bytesPerElement = 2;
                break;
            case 'int32':
            case 'uint32':
            case 'float32':
                this.bytesPerElement = 4;
                break;
            case 'float64':
            case 'bigint64':
            case 'biguint64':
                this.bytesPerElement = 8;
                break;
            default:
                throw new TypeError(`Unknown array type: ${arrayType}`);
        }
    }

    get capacity(): number {
        return Math.floor(this.buffer.capacity / this.bytesPerElement);
    }

    get position(): number {
        return Math.floor(this.buffer.position / this.bytesPerElement);
    }

    get remaining(): number {
        return Math.floor(this.buffer.remaining / this.bytesPerElement);
    }

    get order(): ByteOrder {
        return this.buffer.order;
    }

    get limit(): number {
        return Math.floor(this.buffer.limit / this.bytesPerElement);
    }

    get hasRemaining(): boolean {
        return this.buffer.hasRemaining;
    }

    get isReadOnly(): boolean {
        return this.buffer.isReadOnly;
    }

    getValue(index: number): PrimitiveTypeMap[T] {
        const byteIndex = index * this.bytesPerElement;
        this.buffer.seek(byteIndex);

        switch (this.arrayType) {
            case 'int8':
                return this.buffer.getInt8() as PrimitiveTypeMap[T];
            case 'uint8':
                return this.buffer.getUint8() as PrimitiveTypeMap[T];
            case 'int16':
                return this.buffer.getInt16() as PrimitiveTypeMap[T];
            case 'uint16':
                return this.buffer.getUint16() as PrimitiveTypeMap[T];
            case 'int32':
                return this.buffer.getInt32() as PrimitiveTypeMap[T];
            case 'uint32':
                return this.buffer.getUint32() as PrimitiveTypeMap[T];
            case 'float32':
                return this.buffer.getFloat32() as PrimitiveTypeMap[T];
            case 'float64':
                return this.buffer.getFloat64() as PrimitiveTypeMap[T];
            case 'bigint64':
                return this.buffer.getBigInt64() as PrimitiveTypeMap[T];
            case 'biguint64':
                return this.buffer.getBigUint64() as PrimitiveTypeMap[T];
            default:
                throw new TypeError(`Unknown array type: ${this.arrayType}`);
        }
    }

    setValue(index: number, value: PrimitiveTypeMap[T]): void {
        const byteIndex = index * this.bytesPerElement;
        this.buffer.seek(byteIndex);

        switch (this.arrayType) {
            case 'int8':
                this.buffer.putInt8(value as number);
                break;
            case 'uint8':
                this.buffer.putUint8(value as number);
                break;
            case 'int16':
                this.buffer.putInt16(value as number);
                break;
            case 'uint16':
                this.buffer.putUint16(value as number);
                break;
            case 'int32':
                this.buffer.putInt32(value as number);
                break;
            case 'uint32':
                this.buffer.putUint32(value as number);
                break;
            case 'float32':
                this.buffer.putFloat32(value as number);
                break;
            case 'float64':
                this.buffer.putFloat64(value as number);
                break;
            case 'bigint64':
                this.buffer.putBigInt64(value as bigint);
                break;
            case 'biguint64':
                this.buffer.putBigUint64(value as bigint);
                break;
            default:
                throw new TypeError(`Unknown array type: ${this.arrayType}`);
        }
    }

    getInt8(): number {
        throw new Error('Operation not supported on this view type');
    }
    getUint8(): number {
        throw new Error('Operation not supported on this view type');
    }
    getInt16(): number {
        throw new Error('Operation not supported on this view type');
    }
    getUint16(): number {
        throw new Error('Operation not supported on this view type');
    }
    getInt32(): number {
        throw new Error('Operation not supported on this view type');
    }
    getUint32(): number {
        throw new Error('Operation not supported on this view type');
    }
    getFloat32(): number {
        throw new Error('Operation not supported on this view type');
    }
    getFloat64(): number {
        throw new Error('Operation not supported on this view type');
    }
    getBigInt64(): bigint {
        throw new Error('Operation not supported on this view type');
    }
    getBigUint64(): bigint {
        throw new Error('Operation not supported on this view type');
    }
    getString(): string {
        throw new Error('Operation not supported on this view type');
    }

    slice(begin?: number, end?: number): IReadableBuffer {
        const byteBegin = (begin ?? 0) * this.bytesPerElement;
        const byteEnd = (end ?? this.limit) * this.bytesPerElement;
        const sliced = this.buffer.slice(byteBegin, byteEnd);
        return new BufferView(sliced, this.arrayType);
    }
}
