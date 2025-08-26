export type ISimdVec3 = {
    readonly buffer: ArrayBuffer;
    readonly byteOffset: number;
    readonly byteLength: number;
    readonly x: Float32Array;
    readonly y: Float32Array;
    readonly z: Float32Array;
    readonly w?: Float32Array;
};

export type ISimdVec4 = {
    readonly buffer: ArrayBuffer;
    readonly byteOffset: number;
    readonly byteLength: number;
    readonly x: Float32Array;
    readonly y: Float32Array;
    readonly z: Float32Array;
    readonly w: Float32Array;
};

export class AlignedArrays {
    static createSimdVec3(capacity: number): ISimdVec3 {
        const buffer = new ArrayBuffer(capacity * 16);
        const x = new Float32Array(buffer, 0, capacity);
        const y = new Float32Array(buffer, capacity * 4, capacity);
        const z = new Float32Array(buffer, capacity * 8, capacity);
        const w = new Float32Array(buffer, capacity * 12, capacity);

        return { buffer, byteOffset: 0, byteLength: buffer.byteLength, x, y, z, w };
    }

    static createSimdVec4(capacity: number): ISimdVec4 {
        const buffer = new ArrayBuffer(capacity * 16);
        const x = new Float32Array(buffer, 0, capacity);
        const y = new Float32Array(buffer, capacity * 4, capacity);
        const z = new Float32Array(buffer, capacity * 8, capacity);
        const w = new Float32Array(buffer, capacity * 12, capacity);

        return { buffer, byteOffset: 0, byteLength: buffer.byteLength, x, y, z, w };
    }
}
