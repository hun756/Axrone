type Brand<K, T> = K & { readonly __brand: T };
type Epsilon = Brand<number, 'Epsilon'>;
type ULPsTolerance = Brand<number, 'ULPsTolerance'>;

export type ComparisonStrategy = 'absolute' | 'relative' | 'ulps' | 'combined';
export type ComparisonResult = -1 | 0 | 1;
export type Numeric = number | bigint;
export type InfinityHandlingMode = 'strict' | 'signed' | 'equal';

export interface ComparisonContext<T extends Numeric = number> {
    readonly strategy: ComparisonStrategy;
    readonly epsilon: Epsilon;
    readonly relativeEpsilon: Epsilon;
    readonly absoluteEpsilon: Epsilon;
    readonly ulpsTolerance: ULPsTolerance;
    readonly treatNaNAsEqual: boolean;
    readonly infinityHandling: InfinityHandlingMode;
    readonly safetyChecks: boolean;
    readonly compare: (a: T, b: T) => ComparisonResult;
}

export interface ComparerOptions {
    readonly strategy?: ComparisonStrategy;
    readonly epsilon?: number;
    readonly relativeEpsilon?: number;
    readonly absoluteEpsilon?: number;
    readonly ulpsTolerance?: number;
    readonly treatNaNAsEqual?: boolean;
    readonly infinityHandling?: InfinityHandlingMode;
    readonly safetyChecks?: boolean;
}

const FLOAT64_BYTE_SIZE = 8;
const DEFAULT_EPSILON = 1e-10;
const DEFAULT_ULPS_TOLERANCE = 1;
const DEFAULT_STRATEGY: ComparisonStrategy = 'combined';
const DEFAULT_INFINITY_HANDLING: InfinityHandlingMode = 'signed';
const MAX_ULPS_DISTANCE = Number.MAX_SAFE_INTEGER;

const enum FloatBitMasks {
    SIGN_MASK = 0x80000000,
    EXPONENT_MASK = 0x7ff00000,
    MANTISSA_HIGH_MASK = 0x000fffff,
}

const enum FloatBitShifts {
    SIGN_SHIFT = 31,
    EXPONENT_SHIFT = 20,
}
