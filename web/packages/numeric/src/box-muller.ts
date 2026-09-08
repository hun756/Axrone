import { rand } from '@axrone/random';

const TWO_PI = 2.0 * Math.PI;

// Cached sample for polar Box-Muller: produces 2 values per (u1,u2) pair,
// storing the unused sin() result for the next call. Eliminates wasted work.
let _cachedNormal: number | null = null;

export const sampleStandardNormal = (): number => {
    if (_cachedNormal !== null) {
        const cached = _cachedNormal;
        _cachedNormal = null;
        return cached;
    }
    let u1 = rand.float();
    while (u1 <= 0) u1 = rand.float();
    const u2 = rand.float();
    const mag = Math.sqrt(-2.0 * Math.log(u1));
    const theta = TWO_PI * u2;
    const z0 = mag * Math.cos(theta);
    _cachedNormal = mag * Math.sin(theta);
    return z0;
};

export const sampleBoundedNormal = (min: number = -1, max: number = 1): number => {
    const v = sampleStandardNormal();
    return v < min ? min : v > max ? max : v;
};

export const sampleNormalInRange = (center: number, range: number): number => {
    const half = range * 0.5;
    const stdDev = range / 6;
    return center + Math.max(-half, Math.min(half, sampleStandardNormal() * stdDev));
};

export const sampleUniform = (): number => rand.float();

export const sampleUniformRange = (min: number, max: number): number => rand.floatBetween(min, max);
