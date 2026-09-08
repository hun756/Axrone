import {
    BernoulliDistribution,
    BinomialDistribution,
    ExponentialDistribution,
    GeometricDistribution,
    NormalDistribution,
    PoissonDistribution,
} from '../distributions';
import { createEngineFactory } from '../engines';
import type {
    DistributionSample,
    IDistribution,
    IRandomEngine,
    IRandomState,
    RandomEngineType,
} from '../types';

type NormalAlgorithm = 'standard' | 'polar' | 'ziggurat';

interface RandomDistributionHost {
    readonly getState: () => IRandomState;
    readonly setState: (state: IRandomState) => void;
    readonly getNormalAlgorithm: () => NormalAlgorithm;
}

export class RandomDistributionRuntime {
    private _cachedEngine: IRandomEngine | null = null;
    private _cachedEngineType: RandomEngineType | null = null;
    private _boxMullerSpare: number | null = null;
    private _boxMullerMean: number = 0;
    private _boxMullerStdDev: number = 1;

    constructor(private readonly _host: RandomDistributionHost) {}

    private _getEngine(state: IRandomState): IRandomEngine {
        if (this._cachedEngineType !== state.engine) {
            this._cachedEngine = createEngineFactory(state.engine)();
            this._cachedEngineType = state.engine;
        }
        this._cachedEngine!.setState(state);
        return this._cachedEngine!;
    }

    public normal(mean: number = 0, stdDev: number = 1): number {
        // Use cached spare value if params match
        if (this._boxMullerSpare !== null && this._boxMullerMean === mean && this._boxMullerStdDev === stdDev) {
            const spare = this._boxMullerSpare;
            this._boxMullerSpare = null;
            return spare;
        }

        // Draw a pair using Box-Muller, cache the spare
        const state = this._host.getState();
        const engine = this._getEngine(state);

        let u1 = engine.next01();
        if (u1 <= 0) u1 = Number.MIN_VALUE;
        const u2 = engine.next01();

        const r = Math.sqrt(-2.0 * Math.log(u1));
        const theta = 2.0 * Math.PI * u2;

        const z0 = mean + stdDev * r * Math.cos(theta);
        const z1 = mean + stdDev * r * Math.sin(theta);

        // Cache spare for next call
        this._boxMullerSpare = z1;
        this._boxMullerMean = mean;
        this._boxMullerStdDev = stdDev;

        // Update host state
        this._host.setState(engine.getState());

        return z0;
    }

    public exponential(lambda: number = 1): number {
        return this.distribution<number>(new ExponentialDistribution(lambda));
    }

    public poisson(lambda: number): number {
        return this.distribution<number>(new PoissonDistribution(lambda));
    }

    public bernoulli(p: number = 0.5): boolean {
        return this.distribution<boolean>(new BernoulliDistribution(p));
    }

    public binomial(n: number, p: number): number {
        return this.distribution<number>(new BinomialDistribution(n, p));
    }

    public geometric(p: number): number {
        return this.distribution<number>(new GeometricDistribution(p));
    }

    public distribution<T>(distribution: IDistribution<T>): T {
        const [value, nextState] = distribution.sample(this._host.getState());
        this._host.setState(nextState);
        return value;
    }

    public normalWithMetadata(
        mean: number = 0,
        stdDev: number = 1
    ): DistributionSample<number> {
        return this.sampleWithMetadata<number>(
            new NormalDistribution(mean, stdDev, this._host.getNormalAlgorithm())
        );
    }

    public normalMany(
        count: number,
        mean: number = 0,
        stdDev: number = 1
    ): readonly number[] {
        return this.sampleMany<number>(
            new NormalDistribution(mean, stdDev, this._host.getNormalAlgorithm()),
            count
        );
    }

    public normalManyWithMetadata(
        count: number,
        mean: number = 0,
        stdDev: number = 1
    ): readonly DistributionSample<number>[] {
        return this.sampleManyWithMetadata<number>(
            new NormalDistribution(mean, stdDev, this._host.getNormalAlgorithm()),
            count
        );
    }

    public exponentialWithMetadata(lambda: number = 1): DistributionSample<number> {
        return this.sampleWithMetadata<number>(new ExponentialDistribution(lambda));
    }

    public exponentialMany(count: number, lambda: number = 1): readonly number[] {
        return this.sampleMany<number>(new ExponentialDistribution(lambda), count);
    }

    public exponentialManyWithMetadata(
        count: number,
        lambda: number = 1
    ): readonly DistributionSample<number>[] {
        return this.sampleManyWithMetadata<number>(new ExponentialDistribution(lambda), count);
    }

    public poissonWithMetadata(lambda: number): DistributionSample<number> {
        return this.sampleWithMetadata<number>(new PoissonDistribution(lambda));
    }

    public poissonMany(count: number, lambda: number): readonly number[] {
        return this.sampleMany<number>(new PoissonDistribution(lambda), count);
    }

    public poissonManyWithMetadata(
        count: number,
        lambda: number
    ): readonly DistributionSample<number>[] {
        return this.sampleManyWithMetadata<number>(new PoissonDistribution(lambda), count);
    }

    public bernoulliWithMetadata(p: number = 0.5): DistributionSample<boolean> {
        return this.sampleWithMetadata<boolean>(new BernoulliDistribution(p));
    }

    public bernoulliMany(count: number, p: number = 0.5): readonly boolean[] {
        return this.sampleMany<boolean>(new BernoulliDistribution(p), count);
    }

    public bernoulliManyWithMetadata(
        count: number,
        p: number = 0.5
    ): readonly DistributionSample<boolean>[] {
        return this.sampleManyWithMetadata<boolean>(new BernoulliDistribution(p), count);
    }

    public binomialWithMetadata(n: number, p: number): DistributionSample<number> {
        return this.sampleWithMetadata<number>(new BinomialDistribution(n, p));
    }

    public binomialMany(count: number, n: number, p: number): readonly number[] {
        return this.sampleMany<number>(new BinomialDistribution(n, p), count);
    }

    public binomialManyWithMetadata(
        count: number,
        n: number,
        p: number
    ): readonly DistributionSample<number>[] {
        return this.sampleManyWithMetadata<number>(new BinomialDistribution(n, p), count);
    }

    public geometricWithMetadata(p: number): DistributionSample<number> {
        return this.sampleWithMetadata<number>(new GeometricDistribution(p));
    }

    public geometricMany(count: number, p: number): readonly number[] {
        return this.sampleMany<number>(new GeometricDistribution(p), count);
    }

    public geometricManyWithMetadata(
        count: number,
        p: number
    ): readonly DistributionSample<number>[] {
        return this.sampleManyWithMetadata<number>(new GeometricDistribution(p), count);
    }

    private sampleWithMetadata<T>(distribution: IDistribution<T>): DistributionSample<T> {
        const [sample, nextState] = distribution.sampleWithMetadata!(this._host.getState());
        this._host.setState(nextState);
        return sample;
    }

    private sampleMany<T>(distribution: IDistribution<T>, count: number): readonly T[] {
        const [values, nextState] = distribution.sampleMany!(this._host.getState(), count);
        this._host.setState(nextState);
        return values;
    }

    private sampleManyWithMetadata<T>(
        distribution: IDistribution<T>,
        count: number
    ): readonly DistributionSample<T>[] {
        const [samples, nextState] = distribution.sampleManyWithMetadata!(
            this._host.getState(),
            count
        );
        this._host.setState(nextState);
        return samples;
    }
}