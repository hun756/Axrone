import { BoxMullerFactory } from './box-muller';
import type { EqualityComparer } from '@axrone/utility';
import { Fnv1a32 } from '@axrone/hash';

export const EPSILON: number = 1e-6;
/** Epsilon for geometric comparisons (shapes-2d, collision detection). */
export const GEOMETRIC_EPSILON = 1e-9;
/** Epsilon for constraint/iterative solvers (physics-2d, physics-core). */
export const SOLVER_EPSILON = 1e-6;
/** Epsilon for physics simulations (random distributions, physics bodies). */
export const PHYSICS_EPSILON = 1e-10;
export const PI_2 = Math.PI * 2;
export const DEG_TO_RAD = Math.PI / 180;
export const RAD_TO_DEG = 180 / Math.PI;
export const SQRT2 = Math.SQRT2;
export const HALF_PI = Math.PI / 2;
export const INV_PI = 1 / Math.PI;

/**
 * Epsilon-based floating-point equality check.
 * Uses the engine's canonical EPSILON (1e-6) by default.
 * Zero-allocation — safe for hot paths.
 */
export const floatEquals = (a: number, b: number, epsilon: number = EPSILON): boolean =>
    Math.abs(a - b) < epsilon;

/**
 * Ensures a number is finite (not NaN or Infinity).
 * Throws a descriptive error if the value is not finite.
 * Use at API boundaries to catch invalid inputs early.
 */
export const ensureFinite = (value: number, name: string = 'value'): number => {
    if (!Number.isFinite(value)) {
        throw new Error(`${name} must be a finite number, got ${value}`);
    }
    return value;
};

/**
 * EqualityComparer<number> with configurable epsilon.
 * Hash uses FNV-1a with float quantization (same strategy as Vec2/Vec3/Vec4).
 */
export class NumberEqualityComparer implements EqualityComparer<number> {
    private readonly _epsilon: number;

    constructor(epsilon: number = EPSILON) {
        this._epsilon = epsilon;
    }

    equals(a: number, b: number): boolean {
        return a === b || Math.abs(a - b) < this._epsilon;
    }

    hash(obj: number): number {
        return new Fnv1a32().updateF32(obj).digest();
    }
}

// general box-muller optimization
export const standardNormalDist = BoxMullerFactory.createStandard({
    algorithm: 'polar',
    useCache: true,
    optimizeFor: 'speed',
});
