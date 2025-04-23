export type RandomGenerator<T = number> = {
    next(): T;
};

export type DistributionSample<T = number> = {
    readonly value: T;
    readonly zscore: number;
};

export type NormalDistribution<T = number> = {
    readonly sample: () => T;
    readonly sampleMany: (count: number) => readonly T[];
    readonly sampleWithMetadata?: () => DistributionSample<T>;
    readonly sampleManyWithMetadata?: (count: number) => readonly DistributionSample<T>[];
    readonly probability?: (value: number) => number;
    readonly cumulativeProbability?: (value: number) => number;
    readonly quantile?: (p: number) => number;
};

export type BoxMullerOptions<TRandom extends RandomGenerator = DefaultRandomGenerator> = {
    readonly randomGenerator?: TRandom | (() => TRandom);
    readonly mean?: number;
    readonly standardDeviation?: number;
    readonly useCache?: boolean;
    readonly precision?: number;
    readonly algorithm?: 'standard' | 'polar' | 'ziggurat';
    readonly optimizeFor?: 'speed' | 'memory';
};

export class BoxMullerError extends Error {
    readonly code: (typeof ErrorCodes)[keyof typeof ErrorCodes];

    constructor(code: (typeof ErrorCodes)[keyof typeof ErrorCodes], message: string) {
        super(message);
        this.code = code;
        this.name = 'BoxMullerError';

        Object.setPrototypeOf(this, BoxMullerError.prototype);
    }
}

export const ErrorCodes = {
    INVALID_PARAMETER: 'INVALID_PARAMETER',
    RUNTIME_ERROR: 'RUNTIME_ERROR',
    INVALID_STATE: 'INVALID_STATE',
    INVALID_OPERATION: 'INVALID_OPERATION',
    PRECISION_ERROR: 'PRECISION_ERROR',
} as const;

export type PrecisionMode = 'high' | 'standard' | 'low';

export type DistributionMetadata = {
    readonly mean: number;
    readonly standardDeviation: number;
    readonly variance: number;
    readonly algorithm: 'standard' | 'polar' | 'ziggurat';
};

const DEFAULT_MEAN = 0;
const DEFAULT_STD_DEV = 1;
const DEFAULT_CACHE = true;
const DEFAULT_ALGORITHM = 'polar';
const DEFAULT_OPTIMIZATION = 'speed';
const DEFAULT_PRECISION = 'standard';
const TWO_PI = 2.0 * Math.PI;
const SQRT_TWO_PI = Math.sqrt(2.0 * Math.PI);
const INV_SQRT_TWO_PI = 1.0 / SQRT_TWO_PI;
const LN_2 = Math.log(2);
const MAX_ITERATIONS = 100;
const EPSILON = 1e-10;

export const createDefaultRandomGenerator = (): DefaultRandomGenerator =>
    new DefaultRandomGenerator();

export class DefaultRandomGenerator implements RandomGenerator {
    private static instance: DefaultRandomGenerator | null = null;

    static getInstance(): DefaultRandomGenerator {
        if (!DefaultRandomGenerator.instance) {
            DefaultRandomGenerator.instance = new DefaultRandomGenerator();
        }
        return DefaultRandomGenerator.instance;
    }

    next(): number {
        return Math.random();
    }

    nextInRange(min: number, max: number): number {
        return min + (max - min) * this.next();
    }

    nextInt(min: number, max: number): number {
        return Math.floor(this.nextInRange(min, max + 1));
    }
}

export const createError = (
    code: (typeof ErrorCodes)[keyof typeof ErrorCodes],
    message: string
): BoxMullerError => new BoxMullerError(code, message);
export const validatePositive = (value: number, name: string): void | never => {
    if (value <= 0) {
        throw createError(ErrorCodes.INVALID_PARAMETER, `${name} must be positive`);
    }
};

export const validateFinite = (value: number, name: string): void | never => {
    if (!Number.isFinite(value)) {
        throw createError(ErrorCodes.INVALID_PARAMETER, `${name} must be finite`);
    }
};

export const validateInteger = (value: number, name: string): void | never => {
    if (!Number.isInteger(value)) {
        throw createError(ErrorCodes.INVALID_PARAMETER, `${name} must be an integer`);
    }
};

export const validateInRange = (
    value: number,
    min: number,
    max: number,
    name: string
): void | never => {
    if (value < min || value > max) {
        throw createError(
            ErrorCodes.INVALID_PARAMETER,
            `${name} must be between ${min} and ${max}`
        );
    }
};

export const BoxMullerTransform = <TRandom extends RandomGenerator = DefaultRandomGenerator>(
    options: BoxMullerOptions<TRandom> = {}
): NormalDistribution => {
    const generator =
        typeof options.randomGenerator === 'function'
            ? options.randomGenerator()
            : (options.randomGenerator ?? DefaultRandomGenerator.getInstance());

    const mean = options.mean ?? DEFAULT_MEAN;
    const stdDev = options.standardDeviation ?? DEFAULT_STD_DEV;
    const useCache = options.useCache ?? DEFAULT_CACHE;
    const algorithm = options.algorithm ?? DEFAULT_ALGORITHM;
    const isOptimizedForSpeed =
        options.optimizeFor === 'speed' || options.optimizeFor === undefined;

    validateFinite(mean, 'mean');
    validateFinite(stdDev, 'standardDeviation');
    validatePositive(stdDev, 'standardDeviation');

    let hasCache = false;
    let cachedValue = 0;

    const metadata: DistributionMetadata = {
        mean,
        standardDeviation: stdDev,
        variance: stdDev * stdDev,
        algorithm,
    };

    const transformStandard = (): number => {
        if (useCache && hasCache) {
            hasCache = false;
            return cachedValue;
        }

        const u1 = generator.next();
        if (u1 <= 0) return transformStandard();

        const u2 = generator.next();
        const r = Math.sqrt(-2.0 * Math.log(u1));
        const theta = TWO_PI * u2;

        const z0 = r * Math.cos(theta);

        if (useCache) {
            cachedValue = r * Math.sin(theta);
            hasCache = true;
        }

        return z0;
    };

    const transformPolar = (): number => {
        if (useCache && hasCache) {
            hasCache = false;
            return cachedValue;
        }

        let x: number, y: number, s: number;

        do {
            x = 2.0 * generator.next() - 1.0;
            y = 2.0 * generator.next() - 1.0;
            s = x * x + y * y;
        } while (s >= 1.0 || s === 0);

        const scale = Math.sqrt((-2.0 * Math.log(s)) / s);

        if (useCache) {
            cachedValue = y * scale;
            hasCache = true;
        }

        return x * scale;
    };

    const transformZiggurat = (): number => {
        let u, v, x, y, q;

        do {
            u = 2.0 * generator.next() - 1.0;
            v = 1.7156 * (2.0 * generator.next() - 1.0);

            x = u - 0.449871;
            y = Math.abs(v) + 0.386595;
            q = x * x + y * (0.196 * y - 0.25472 * x);
        } while (q > 0.27597 && (q > 0.27846 || v * v > -4.0 * Math.log(u) * u * u));

        return v / u;
    };

    const generateRandomNormal = (): number => {
        if (algorithm === 'standard') return transformStandard();
        if (algorithm === 'ziggurat') return transformZiggurat();
        return transformPolar();
    };

    const sample = (): number => mean + stdDev * generateRandomNormal();

    const createSample = (value: number): DistributionSample => ({
        value,
        zscore: (value - mean) / stdDev,
    });

    const sampleWithMetadata = (): DistributionSample => {
        const value = sample();
        return createSample(value);
    };

    const sampleMany = (count: number): readonly number[] => {
        validatePositive(count, 'count');
        validateInteger(count, 'count');

        if (isOptimizedForSpeed && useCache) {
            const result = new Array<number>(count);

            for (let i = 0; i < count - (count % 2); i += 2) {
                const u1 = generator.next();
                if (u1 <= 0) {
                    i -= 2;
                    continue;
                }

                const u2 = generator.next();
                const r = Math.sqrt(-2.0 * Math.log(u1));
                const theta = TWO_PI * u2;

                result[i] = mean + stdDev * r * Math.cos(theta);
                result[i + 1] = mean + stdDev * r * Math.sin(theta);
            }

            if (count % 2 !== 0) {
                result[count - 1] = sample();
            }

            return result;
        }

        const result = new Array<number>(count);
        for (let i = 0; i < count; i++) {
            result[i] = sample();
        }

        return result;
    };

    const sampleManyWithMetadata = (count: number): readonly DistributionSample[] => {
        const values = sampleMany(count);
        const samples = new Array<DistributionSample>(count);

        for (let i = 0; i < count; i++) {
            samples[i] = createSample(values[i]);
        }

        return samples;
    };

    const probability = (x: number): number => {
        validateFinite(x, 'value');
        const z = (x - mean) / stdDev;
        return (INV_SQRT_TWO_PI / stdDev) * Math.exp(-0.5 * z * z);
    };

    const cumulativeProbability = (x: number): number => {
        validateFinite(x, 'value');
        const z = (x - mean) / stdDev;
        return 0.5 * (1.0 + erf(z / Math.SQRT2));
    };

    const quantile = (p: number): number => {
        validateFinite(p, 'probability');
        validateInRange(p, 0, 1, 'probability');

        if (p === 0) return -Infinity;
        if (p === 1) return Infinity;
        if (p === 0.5) return mean;

        return mean + stdDev * Math.SQRT2 * erfInv(2 * p - 1);
    };

    const erf = (x: number): number => {
        const sign = x >= 0 ? 1 : -1;
        x = Math.abs(x);

        const a1 = 0.254829592;
        const a2 = -0.284496736;
        const a3 = 1.421413741;
        const a4 = -1.453152027;
        const a5 = 1.061405429;
        const p = 0.3275911;

        const t = 1.0 / (1.0 + p * x);
        const y = 1.0 - ((((a5 * t + a4) * t + a3) * t + a2) * t + a1) * t * Math.exp(-x * x);

        return sign * y;
    };

    const erfInv = (x: number): number => {
        const sign = x >= 0 ? 1 : -1;
        x = Math.abs(x);

        const a = 0.147;
        const y = 2.0 / (Math.PI * a) + Math.log(1.0 - x * x) / 2.0;
        const s1 = Math.sqrt(Math.sqrt(y * y - Math.log(1.0 - x * x) / a) - y);

        return sign * s1;
    };

    return {
        sample,
        sampleMany,
        sampleWithMetadata,
        sampleManyWithMetadata,
        probability,
        cumulativeProbability,
        quantile,
    };
};

export const TransformedDistribution = <TInput, TOutput>(
    source: NormalDistribution<TInput>,
    transform: (value: TInput) => TOutput
): NormalDistribution<TOutput> => {
    const sample = (): TOutput => transform(source.sample());

    const sampleMany = (count: number): readonly TOutput[] => {
        validatePositive(count, 'count');
        validateInteger(count, 'count');

        return source.sampleMany(count).map(transform);
    };

    const sampleWithMetadata = source.sampleWithMetadata
        ? (): DistributionSample<TOutput> => {
              const sample = source.sampleWithMetadata!();
              return {
                  value: transform(sample.value),
                  zscore: sample.zscore,
              };
          }
        : undefined;

    const sampleManyWithMetadata = source.sampleManyWithMetadata
        ? (count: number): readonly DistributionSample<TOutput>[] => {
              validatePositive(count, 'count');
              validateInteger(count, 'count');

              return source.sampleManyWithMetadata!(count).map((sample) => ({
                  value: transform(sample.value),
                  zscore: sample.zscore,
              }));
          }
        : undefined;

    return {
        sample,
        sampleMany,
        sampleWithMetadata,
        sampleManyWithMetadata,
        probability: source.probability,
        cumulativeProbability: source.cumulativeProbability,
        quantile: source.quantile,
    };
};

export const NormalPool = <T extends RandomGenerator = DefaultRandomGenerator>(
    options: BoxMullerOptions<T> & { poolSize?: number } = {}
): NormalDistribution & { refill: () => void } => {
    const distribution = BoxMullerTransform(options);
    const poolSize = options.poolSize ?? 1000;

    validatePositive(poolSize, 'poolSize');
    validateInteger(poolSize, 'poolSize');

    let values: number[] = distribution.sampleMany(poolSize) as number[];
    let index = 0;

    const refill = (): void => {
        values = distribution.sampleMany(poolSize) as number[];
        index = 0;
    };

    const sample = (): number => {
        if (index >= values.length) {
            refill();
        }
        return values[index++];
    };

    const sampleMany = (count: number): readonly number[] => {
        validatePositive(count, 'count');
        validateInteger(count, 'count');

        if (count <= poolSize - index) {
            const result = values.slice(index, index + count);
            index += count;
            return result;
        }

        return distribution.sampleMany(count);
    };

    return {
        ...distribution,
        sample,
        sampleMany,
        refill,
    };
};
