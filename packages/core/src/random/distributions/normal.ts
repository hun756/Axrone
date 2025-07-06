import { IDistribution, IRandomState, RandomResult, DistributionSample } from '../types';
import { createEngineFactory } from '../engines';

export class NormalDistribution implements IDistribution<number> {
    private cachedValue: number | null = null;

    constructor(
        private readonly _mean: number = 0,
        private readonly _stdDev: number = 1,
        private readonly algorithm: 'standard' | 'polar' | 'ziggurat' = 'polar',
        private readonly useCache: boolean = true
    ) {
        if (!Number.isFinite(_mean) || !Number.isFinite(_stdDev) || _stdDev <= 0) {
            throw new RangeError(
                'Parameters must be finite numbers and standard deviation must be positive'
            );
        }
    }

    public sample = (state: IRandomState): RandomResult<number> => {
        const standardNormal = this.generateStandardNormal(state);
        const value = this._mean + this._stdDev * standardNormal[0];
        return [value, standardNormal[1]];
    };

    public sampleMany = (state: IRandomState, count: number): RandomResult<readonly number[]> => {
        if (count <= 0 || !Number.isInteger(count)) {
            throw new RangeError('Count must be a positive integer');
        }

        if (this.algorithm === 'polar' && this.useCache && count > 1) {
            return this.sampleManyOptimized(state, count);
        }

        const result: number[] = [];
        let currentState = state;

        for (let i = 0; i < count; i++) {
            const [value, nextState] = this.sample(currentState);
            result.push(value);
            currentState = nextState;
        }

        return [result, currentState];
    };

    public sampleWithMetadata = (state: IRandomState): RandomResult<DistributionSample<number>> => {
        const [value, nextState] = this.sample(state);
        const zscore = (value - this._mean) / this._stdDev;

        const sample: DistributionSample<number> = {
            value,
            zscore,
            metadata: {
                algorithm: this.algorithm,
                mean: this._mean,
                standardDeviation: this._stdDev,
                variance: this._stdDev * this._stdDev,
            },
        };

        return [sample, nextState];
    };

    public sampleManyWithMetadata = (
        state: IRandomState,
        count: number
    ): RandomResult<readonly DistributionSample<number>[]> => {
        const [values, nextState] = this.sampleMany(state, count);
        const samples = values.map((value) => ({
            value,
            zscore: (value - this._mean) / this._stdDev,
            metadata: {
                algorithm: this.algorithm,
                mean: this._mean,
                standardDeviation: this._stdDev,
                variance: this._stdDev * this._stdDev,
            },
        }));

        return [samples, nextState];
    };

    public probability = (x: number): number => {
        if (!Number.isFinite(x)) {
            throw new RangeError('Value must be finite');
        }

        const z = (x - this._mean) / this._stdDev;
        const INV_SQRT_TWO_PI = 1.0 / Math.sqrt(2.0 * Math.PI);
        return (INV_SQRT_TWO_PI / this._stdDev) * Math.exp(-0.5 * z * z);
    };

    public cumulativeProbability = (x: number): number => {
        if (!Number.isFinite(x)) {
            throw new RangeError('Value must be finite');
        }

        const z = (x - this._mean) / this._stdDev;
        return 0.5 * (1.0 + this.erf(z / Math.SQRT2));
    };

    public quantile = (p: number): number => {
        if (p < 0 || p > 1 || !Number.isFinite(p)) {
            throw new RangeError('Probability must be between 0 and 1');
        }

        if (p === 0) return -Infinity;
        if (p === 1) return Infinity;
        if (p === 0.5) return this._mean;

        return this._mean + this._stdDev * Math.SQRT2 * this.erfInv(2 * p - 1);
    };

    public mean = (): number => this._mean;
    public variance = (): number => this._stdDev * this._stdDev;
    public standardDeviation = (): number => this._stdDev;

    private generateStandardNormal(state: IRandomState): RandomResult<number> {
        switch (this.algorithm) {
            case 'standard':
                return this.boxMullerStandard(state);
            case 'ziggurat':
                return this.zigguratAlgorithm(state);
            default:
                return this.boxMullerPolar(state);
        }
    }

    private boxMullerStandard(state: IRandomState): RandomResult<number> {
        const engine = createEngineFactory(state.engine)();
        engine.setState(state);

        if (this.useCache && this.cachedValue !== null) {
            const cached = this.cachedValue;
            this.cachedValue = null;
            return [cached, engine.getState()];
        }

        let u1: number;
        let attempts = 0;
        const MAX_ATTEMPTS = 50;

        do {
            u1 = engine.next01();
            attempts++;
            if (u1 <= 1e-10) {
                u1 = 1e-10;
                break;
            }
            if (attempts > MAX_ATTEMPTS) {
                throw new Error('Failed to generate valid random number');
            }
        } while (u1 <= 0);

        const u2 = engine.next01();
        const r = Math.sqrt(-2.0 * Math.log(u1));
        const theta = 2.0 * Math.PI * u2;

        const z0 = r * Math.cos(theta);

        if (this.useCache) {
            this.cachedValue = r * Math.sin(theta);
        }

        return [z0, engine.getState()];
    }

    private boxMullerPolar(state: IRandomState): RandomResult<number> {
        const engine = createEngineFactory(state.engine)();
        engine.setState(state);

        if (this.useCache && this.cachedValue !== null) {
            const cached = this.cachedValue;
            this.cachedValue = null;
            return [cached, engine.getState()];
        }

        let x: number, y: number, s: number;
        let attempts = 0;
        const MAX_ATTEMPTS = 50;

        do {
            x = 2.0 * engine.next01() - 1.0;
            y = 2.0 * engine.next01() - 1.0;
            s = x * x + y * y;
            attempts++;

            if (attempts > MAX_ATTEMPTS) {
                s = 0.99;
                break;
            }
        } while (s >= 1.0 || s === 0);

        const scale = Math.sqrt((-2.0 * Math.log(s)) / s);

        if (this.useCache) {
            this.cachedValue = y * scale;
        }

        return [x * scale, engine.getState()];
    }

    private zigguratAlgorithm(state: IRandomState): RandomResult<number> {
        const engine = createEngineFactory(state.engine)();
        engine.setState(state);

        let u: number, v: number, x: number, y: number, q: number;
        let attempts = 0;
        const MAX_ATTEMPTS = 50;

        do {
            u = 2.0 * engine.next01() - 1.0;
            v = 1.7156 * (2.0 * engine.next01() - 1.0);

            x = u - 0.449871;
            y = Math.abs(v) + 0.386595;
            q = x * x + y * (0.196 * y - 0.25472 * x);

            attempts++;
            if (attempts > MAX_ATTEMPTS) {
                return [0, engine.getState()];
            }
        } while (q > 0.27597 && (q > 0.27846 || v * v > -4.0 * Math.log(u) * u * u));

        return [v / u, engine.getState()];
    }

    private sampleManyOptimized(
        state: IRandomState,
        count: number
    ): RandomResult<readonly number[]> {
        const engine = createEngineFactory(state.engine)();
        engine.setState(state);
        const result: number[] = new Array(count);
        let maxAttempts = Math.min(count * 5, 500);

        for (let i = 0; i < count - (count % 2); ) {
            const u1 = engine.next01();

            if (u1 <= 0) {
                maxAttempts--;
                if (maxAttempts <= 0) break;
                continue;
            }

            const u2 = engine.next01();
            const r = Math.sqrt(-2.0 * Math.log(u1));
            const theta = 2.0 * Math.PI * u2;

            result[i] = this._mean + this._stdDev * r * Math.cos(theta);
            if (i + 1 < count) {
                result[i + 1] = this._mean + this._stdDev * r * Math.sin(theta);
            }
            i += 2;
        }

        for (let i = count - (count % 2); i < count; i++) {
            const [value, _] = this.sample(engine.getState());
            result[i] = value;
        }

        return [result, engine.getState()];
    }

    private erf(x: number): number {
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
    }

    private erfInv(x: number): number {
        const EPSILON = 1e-10;

        if (Math.abs(x) >= 1.0 - EPSILON) {
            return x >= 0 ? 8.0 : -8.0;
        }

        const sign = x >= 0 ? 1 : -1;
        x = Math.abs(x);

        const a = 0.147;
        const y = 2.0 / (Math.PI * a) + Math.log(1.0 - x * x) / 2.0;
        const s1 = Math.sqrt(Math.sqrt(y * y - Math.log(1.0 - x * x) / a) - y);

        return sign * s1;
    }
}
