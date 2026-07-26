import { BoxMullerFactory } from './box-muller';

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

// general box-muller optimization
export const standardNormalDist = BoxMullerFactory.createStandard({
    algorithm: 'polar',
    useCache: true,
    optimizeFor: 'speed',
});
