const _floatBuf = new Float32Array(1);
const _intBuf = new Int32Array(_floatBuf.buffer);

export function floatToIntBits(x: number): number {
    _floatBuf[0] = x;
    return _intBuf[0]!;
}

export function fmix32(h: number): number {
    h ^= h >>> 16;
    h = Math.imul(h, 0x85ebca6b);
    h ^= h >>> 13;
    h = Math.imul(h, 0xc2b2ae35);
    h ^= h >>> 16;
    return h >>> 0;
}

export function hashCombineFloat(seed: number, value: number): number {
    return Math.imul(seed ^ floatToIntBits(value), 0x01000193);
}

export function hashFloats(seed: number, values: ArrayLike<number>, offset: number = 0, count?: number): number {
    const n = count ?? values.length - offset;
    let h = seed;
    for (let i = 0; i < n; i++) {
        h = hashCombineFloat(h, values[offset + i]!);
    }
    return fmix32(h);
}
