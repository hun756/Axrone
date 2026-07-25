import type {
    IDistribution,
    DistributionSample,
    RandomEngineType,
    SeedSource,
    IRandomState,
    RandomResult,
} from '@axrone/random';

import { Random, rand, NormalDistribution as CoreNormalDistribution } from '@axrone/random';
import {
    BoxMullerError,
    ErrorCodes,
    createError,
    validateFinite,
    validateInRange,
    validateInteger,
    validatePositive,
} from './box-muller-errors';
import { DefaultRandomGenerator } from './box-muller-random';

export {
    BoxMullerError,
    ErrorCodes,
    createError,
    validateFinite,
    validateInRange,
    validateInteger,
    validatePositive,
    DefaultRandomGenerator,
};

export type BoxMullerOptions = {
    readonly mean?: number;
    readonly standardDeviation?: number;
    readonly useCache?: boolean;
    readonly algorithm?: 'standard' | 'polar' | 'ziggurat';
    readonly optimizeFor?: 'speed' | 'memory';
    readonly engineType?: RandomEngineType;
    readonly seed?: SeedSource;
};

const DEFAULT_MEAN = 0;
const DEFAULT_STD_DEV = 1;
const DEFAULT_CACHE = true;
const DEFAULT_ALGORITHM: 'standard' | 'polar' | 'ziggurat' = 'polar';
const TWO_PI = 2.0 * Math.PI;

export class BoxMullerNormalDistribution implements IDistribution<number> {
    private readonly _core: CoreNormalDistribution;

    constructor(
        private readonly _mean: number = DEFAULT_MEAN,
        private readonly _stdDev: number = DEFAULT_STD_DEV,
        algorithm: 'standard' | 'polar' | 'ziggurat' = DEFAULT_ALGORITHM,
        useCache: boolean = DEFAULT_CACHE
    ) {
        validateFinite(_mean, 'mean');
        validateFinite(_stdDev, 'standardDeviation');
        validatePositive(_stdDev, 'standardDeviation');
        this._core = new CoreNormalDistribution(_mean, _stdDev, algorithm, useCache);
    }

    public sample = (state: IRandomState): RandomResult<number> => this._core.sample(state);

    public sampleMany = (state: IRandomState, count: number): RandomResult<readonly number[]> => {
        validatePositive(count, 'count');
        validateInteger(count, 'count');
        return this._core.sampleMany!(state, count);
    };

    public sampleWithMetadata = (state: IRandomState): RandomResult<DistributionSample<number>> =>
        this._core.sampleWithMetadata!(state);

    public sampleManyWithMetadata = (
        state: IRandomState,
        count: number
    ): RandomResult<readonly DistributionSample<number>[]> => {
        validatePositive(count, 'count');
        validateInteger(count, 'count');
        return this._core.sampleManyWithMetadata!(state, count);
    };

    public probability = (x: number): number => this._core.probability!(x);
    public cumulativeProbability = (x: number): number => this._core.cumulativeProbability!(x);
    public quantile = (p: number): number => this._core.quantile!(p);
    public mean = (): number => this._mean;
    public variance = (): number => this._stdDev * this._stdDev;
    public standardDeviation = (): number => this._stdDev;
}

export const BoxMullerTransform = (options: BoxMullerOptions = {}): IDistribution<number> => {
    const mean = options.mean ?? DEFAULT_MEAN;
    const stdDev = options.standardDeviation ?? DEFAULT_STD_DEV;
    const useCache = options.useCache ?? DEFAULT_CACHE;
    const algorithm = options.algorithm ?? DEFAULT_ALGORITHM;
    return new BoxMullerNormalDistribution(mean, stdDev, algorithm, useCache);
};

export const StandardNormal = (
    options: Omit<BoxMullerOptions, 'mean' | 'standardDeviation'> = {}
): IDistribution<number> =>
    BoxMullerTransform({
        ...options,
        mean: 0,
        standardDeviation: 1,
    });

export const TransformedDistribution = <TInput, TOutput>(
    source: IDistribution<TInput>,
    transform: (value: TInput) => TOutput
): IDistribution<TOutput> => {
    const sample = (state: IRandomState): RandomResult<TOutput> => {
        const [value, nextState] = source.sample(state);
        return [transform(value), nextState];
    };

    const sampleMany = (state: IRandomState, count: number): RandomResult<readonly TOutput[]> => {
        validatePositive(count, 'count');
        validateInteger(count, 'count');

        if (!source.sampleMany) {
            const result: TOutput[] = [];
            let currentState = state;
            for (let i = 0; i < count; i++) {
                const [value, nextState] = sample(currentState);
                result.push(value);
                currentState = nextState;
            }
            return [result, currentState];
        }

        const [values, nextState] = source.sampleMany(state, count);
        if (!values) {
            throw new Error('Source sampleMany returned undefined values');
        }
        return [values.map(transform), nextState];
    };

    const sampleWithMetadata = source.sampleWithMetadata
        ? (state: IRandomState): RandomResult<DistributionSample<TOutput>> => {
              const [s, nextState] = source.sampleWithMetadata!(state);
              return [
                  {
                      value: transform(s.value),
                      zscore: s.zscore,
                      metadata: s.metadata,
                  },
                  nextState,
              ];
          }
        : undefined;

    const sampleManyWithMetadata = source.sampleManyWithMetadata
        ? (
              state: IRandomState,
              count: number
          ): RandomResult<readonly DistributionSample<TOutput>[]> => {
              validatePositive(count, 'count');
              validateInteger(count, 'count');

              const [samples, nextState] = source.sampleManyWithMetadata!(state, count);
              return [
                  samples.map((s) => ({
                      value: transform(s.value),
                      zscore: s.zscore,
                      metadata: s.metadata,
                  })),
                  nextState,
              ];
          }
        : undefined;

    return { sample, sampleMany, sampleWithMetadata, sampleManyWithMetadata };
};

export const isIDistribution = <T = number>(obj: unknown): obj is IDistribution<T> =>
    obj !== null &&
    typeof obj === 'object' &&
    'sample' in obj &&
    typeof (obj as IDistribution<T>).sample === 'function';

export const BoxMullerFactory = {
    createNormal: (
        mean: number,
        stdDev: number,
        options: Omit<BoxMullerOptions, 'mean' | 'standardDeviation'> = {}
    ): IDistribution<number> =>
        BoxMullerTransform({
            ...options,
            mean,
            standardDeviation: stdDev,
        }),

    createStandard: (
        options: Omit<BoxMullerOptions, 'mean' | 'standardDeviation'> = {}
    ): IDistribution<number> => StandardNormal(options),

    createTransformed: <TOutput>(
        transform: (value: number) => TOutput,
        options: BoxMullerOptions = {}
    ): IDistribution<TOutput> => {
        const source = BoxMullerTransform(options);
        return TransformedDistribution<number, TOutput>(source, transform);
    },
};

export const sampleStandardNormal = (): number => {
    let u1 = rand.float();
    if (u1 <= 0) u1 = Number.MIN_VALUE;
    const u2 = rand.float();
    const mag = Math.sqrt(-2.0 * Math.log(u1));
    const theta = TWO_PI * u2;
    return mag * Math.cos(theta);
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

export default {
    BoxMullerTransform,
    StandardNormal,
    TransformedDistribution,
    BoxMullerNormalDistribution,
    DefaultRandomGenerator,
    BoxMullerFactory,
    isIDistribution,
    ErrorCodes,
    Random,
    CoreNormalDistribution,
};
