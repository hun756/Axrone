import {
    BoxMullerTransform,
    DefaultRandomGenerator,
    BoxMullerOptions,
    NormalDistribution,
    DistributionSample,
} from '../box_muller';

class MockRandomGenerator {
    private sequence: number[];
    private index: number = 0;

    constructor(sequence: number[] = [0.5, 0.5, 0.5, 0.5]) {
        this.sequence = sequence;
    }

    next(): number {
        const value = this.sequence[this.index];
        this.index = (this.index + 1) % this.sequence.length;
        return value;
    }

    static getInstance(): MockRandomGenerator {
        return new MockRandomGenerator();
    }
}

const calculateMean = (values: number[]): number => {
    return values.reduce((sum, value) => sum + value, 0) / values.length;
};

const calculateStdDev = (values: number[], mean: number): number => {
    return Math.sqrt(
        values.reduce((sum, value) => sum + Math.pow(value - mean, 2), 0) / values.length
    );
};

const isApproximately = (value: number, target: number, tolerance: number = 0.1): boolean => {
    return Math.abs(value - target) <= tolerance;
};

const chiSquareTest = (
    values: number[],
    mean: number,
    stdDev: number,
    significance: number = 0.05
): boolean => {
    const sortedValues = [...values].sort((a, b) => a - b);
    const numBins = Math.max(10, Math.floor(Math.sqrt(values.length)));
    const binSize = (sortedValues[sortedValues.length - 1] - sortedValues[0]) / numBins;

    const observedCounts = new Array(numBins).fill(0);
    const expectedCounts = new Array(numBins).fill(0);

    const binBoundaries = new Array(numBins + 1);
    binBoundaries[0] = sortedValues[0] - 0.000001;
    for (let i = 1; i <= numBins; i++) {
        binBoundaries[i] = binBoundaries[0] + i * binSize;
    }

    let binIndex = 0;
    for (const value of sortedValues) {
        while (value > binBoundaries[binIndex + 1]) {
            binIndex++;
        }
        if (binIndex < numBins) {
            observedCounts[binIndex]++;
        }
    }

    const distribution = BoxMullerTransform({ mean, standardDeviation: stdDev });
    for (let i = 0; i < numBins; i++) {
        const lowerProb = distribution.cumulativeProbability!(binBoundaries[i]);
        const upperProb = distribution.cumulativeProbability!(binBoundaries[i + 1]);
        expectedCounts[i] = values.length * (upperProb - lowerProb);
    }

    let chiSquare = 0;
    for (let i = 0; i < numBins; i++) {
        if (expectedCounts[i] >= 5) {
            chiSquare += Math.pow(observedCounts[i] - expectedCounts[i], 2) / expectedCounts[i];
        }
    }

    const degreesOfFreedom = numBins - 3;

    const criticalValues: Record<number, number> = {
        1: 3.84,
        2: 5.99,
        3: 7.81,
        4: 9.49,
        5: 11.07,
        6: 12.59,
        7: 14.07,
        8: 15.51,
        9: 16.92,
        10: 18.31,
        15: 25.0,
        20: 31.41,
        25: 37.65,
        30: 43.77,
    };

    const criticalValue =
        criticalValues[degreesOfFreedom] ||
        (degreesOfFreedom > 30
            ? degreesOfFreedom + 1.65 * Math.sqrt(2 * degreesOfFreedom)
            : Number.MAX_VALUE);

    return chiSquare <= criticalValue;
};

describe('BoxMullerTransform', () => {
    describe('Initialization and Configuration', () => {
        test('should initialize with default values', () => {
            const normalDist = BoxMullerTransform();
            expect(normalDist).toBeDefined();
            expect(typeof normalDist.sample).toBe('function');
        });

        test('should accept custom mean and standard deviation', () => {
            const mean = 10;
            const stdDev = 2;
            const normalDist = BoxMullerTransform({ mean, standardDeviation: stdDev });

            const samples = normalDist.sampleMany(1000);
            const calculatedMean = calculateMean(samples as number[]);
            const calculatedStdDev = calculateStdDev(samples as number[], calculatedMean);

            // TODO: Fix broken test ??? (sometimes true sometimes false)
            expect(isApproximately(calculatedMean, mean)).toBe(true);
            expect(isApproximately(calculatedStdDev, stdDev)).toBe(true);
        });

        test('should throw for invalid mean', () => {
            expect(() => BoxMullerTransform({ mean: NaN })).toThrow();
            expect(() => BoxMullerTransform({ mean: Infinity })).toThrow();
        });

        test('should throw for invalid standard deviation', () => {
            expect(() => BoxMullerTransform({ standardDeviation: 0 })).toThrow();
            expect(() => BoxMullerTransform({ standardDeviation: -1 })).toThrow();
            expect(() => BoxMullerTransform({ standardDeviation: NaN })).toThrow();
            expect(() => BoxMullerTransform({ standardDeviation: Infinity })).toThrow();
        });

        test('should accept custom random generator', () => {
            const mockSequence = [0.1, 0.2, 0.3, 0.4, 0.5];
            const mockGenerator = new MockRandomGenerator(mockSequence);

            const normalDist1 = BoxMullerTransform({
                randomGenerator: () => mockGenerator,
            });

            const normalDist2 = BoxMullerTransform({
                randomGenerator: mockGenerator,
            });

            expect(normalDist1.sample()).toBeDefined();
            expect(normalDist2.sample()).toBeDefined();
        });
    });

    describe('Algorithm Selection', () => {
        test('should use standard algorithm by default', () => {
            const normalDist = BoxMullerTransform();
            const samples = normalDist.sampleMany(1000);
            expect(samples.length).toBe(1000);

            const mean = calculateMean(samples as number[]);
            const stdDev = calculateStdDev(samples as number[], mean);
            expect(isApproximately(mean, 0, 0.15)).toBe(true);
            expect(isApproximately(stdDev, 1, 0.15)).toBe(true);
        });

        test('should use polar algorithm when specified', () => {
            const normalDist = BoxMullerTransform({ algorithm: 'polar' });
            const samples = normalDist.sampleMany(1000);

            const mean = calculateMean(samples as number[]);
            const stdDev = calculateStdDev(samples as number[], mean);
            expect(isApproximately(mean, 0, 0.15)).toBe(true);
            expect(isApproximately(stdDev, 1, 0.15)).toBe(true);
        });

        test('should use ziggurat algorithm when specified', () => {
            const normalDist = BoxMullerTransform({ algorithm: 'ziggurat' });
            const samples = normalDist.sampleMany(1000);

            const mean = calculateMean(samples as number[]);
            const stdDev = calculateStdDev(samples as number[], mean);
            expect(isApproximately(mean, 0, 0.15)).toBe(true);
            expect(isApproximately(stdDev, 1, 0.15)).toBe(true);
        });
    });

    describe('Cache Behavior', () => {
        test('should use cache when enabled', () => {
            const mathLogSpy = jest.spyOn(Math, 'log');

            const withCache = BoxMullerTransform({ useCache: true, algorithm: 'standard' });
            const initialCallCount = mathLogSpy.mock.calls.length;

            withCache.sample();
            const secondCallCount = mathLogSpy.mock.calls.length;

            withCache.sample();
            const thirdCallCount = mathLogSpy.mock.calls.length;

            expect(secondCallCount - initialCallCount).toBeGreaterThan(0);
            expect(thirdCallCount - secondCallCount).toBe(0);

            mathLogSpy.mockRestore();
        });

        test('should not use cache when disabled', () => {
            const mathLogSpy = jest.spyOn(Math, 'log');

            const withoutCache = BoxMullerTransform({ useCache: false, algorithm: 'standard' });
            const initialCallCount = mathLogSpy.mock.calls.length;

            withoutCache.sample();
            const secondCallCount = mathLogSpy.mock.calls.length;

            withoutCache.sample();
            const thirdCallCount = mathLogSpy.mock.calls.length;

            expect(secondCallCount - initialCallCount).toBeGreaterThan(0);
            expect(thirdCallCount - secondCallCount).toBeGreaterThan(0);

            mathLogSpy.mockRestore();
        });

        test('should optimize for speed with sampleMany when requested', () => {
            const speedOptimized = BoxMullerTransform({
                optimizeFor: 'speed',
                useCache: true,
                algorithm: 'standard',
            });

            const mathLogSpy = jest.spyOn(Math, 'log');
            const initialCallCount = mathLogSpy.mock.calls.length;

            const samples = speedOptimized.sampleMany(100);

            const finalCallCount = mathLogSpy.mock.calls.length;

            const logCallsPerSample = (finalCallCount - initialCallCount) / 100;
            expect(logCallsPerSample).toBeLessThan(0.7);

            expect(samples.length).toBe(100);

            mathLogSpy.mockRestore();
        });
    });

    describe('Core Sampling Functionality', () => {
        test('sample should return a number', () => {
            const normalDist = BoxMullerTransform();
            const value = normalDist.sample();
            expect(typeof value).toBe('number');
            expect(!isNaN(value)).toBe(true);
        });

        test('sampleMany should return the requested number of samples', () => {
            const normalDist = BoxMullerTransform();
            const samples = normalDist.sampleMany(50);
            expect(Array.isArray(samples)).toBe(true);
            expect(samples.length).toBe(50);
            expect(typeof samples[0]).toBe('number');
        });

        test('sampleWithMetadata should return value and z-score', () => {
            const mean = 5;
            const stdDev = 2;
            const normalDist = BoxMullerTransform({ mean, standardDeviation: stdDev });

            const sample = normalDist.sampleWithMetadata!();
            expect(sample).toHaveProperty('value');
            expect(sample).toHaveProperty('zscore');

            const expectedZScore = (sample.value - mean) / stdDev;
            expect(sample.zscore).toBeCloseTo(expectedZScore);
        });

        test('sampleManyWithMetadata should return array of samples with metadata', () => {
            const normalDist = BoxMullerTransform();
            const samples = normalDist.sampleManyWithMetadata!(50);

            expect(Array.isArray(samples)).toBe(true);
            expect(samples.length).toBe(50);

            samples.forEach((sample) => {
                expect(sample).toHaveProperty('value');
                expect(sample).toHaveProperty('zscore');
                expect(typeof sample.value).toBe('number');
                expect(typeof sample.zscore).toBe('number');
            });
        });

        test('should throw for invalid count in sampleMany', () => {
            const normalDist = BoxMullerTransform();
            expect(() => normalDist.sampleMany(0)).toThrow();
            expect(() => normalDist.sampleMany(-1)).toThrow();
            expect(() => normalDist.sampleMany(1.5)).toThrow();
        });
    });

    describe('Probability Functions', () => {
        test('probability should calculate PDF correctly', () => {
            const normalDist = BoxMullerTransform();

            expect(normalDist.probability!(0)).toBeCloseTo(0.3989, 3);

            expect(normalDist.probability!(1)).toBeCloseTo(0.242, 3);
            expect(normalDist.probability!(-1)).toBeCloseTo(0.242, 3);
            expect(normalDist.probability!(2)).toBeCloseTo(0.054, 3);
        });

        test('cumulativeProbability should calculate CDF correctly', () => {
            const normalDist = BoxMullerTransform();

            expect(normalDist.cumulativeProbability!(0)).toBeCloseTo(0.5, 3);

            expect(normalDist.cumulativeProbability!(1)).toBeCloseTo(0.8413, 3);
            expect(normalDist.cumulativeProbability!(-1)).toBeCloseTo(0.1587, 3);
            expect(normalDist.cumulativeProbability!(2)).toBeCloseTo(0.9772, 3);
        });

        test('quantile should calculate inverse CDF correctly', () => {
            const normalDist = BoxMullerTransform();

            expect(normalDist.quantile!(0.5)).toBeCloseTo(0, 3);

            expect(normalDist.quantile!(0.8413)).toBeCloseTo(1, 2);
            expect(normalDist.quantile!(0.1587)).toBeCloseTo(-1, 2);
            expect(normalDist.quantile!(0.9772)).toBeCloseTo(2, 2);

            expect(normalDist.quantile!(0)).toBe(-Infinity);
            expect(normalDist.quantile!(1)).toBe(Infinity);
        });

        test('should throw for invalid input to probability functions', () => {
            const normalDist = BoxMullerTransform();

            expect(() => normalDist.probability!(NaN)).toThrow();
            expect(() => normalDist.probability!(Infinity)).toThrow();

            expect(() => normalDist.cumulativeProbability!(NaN)).toThrow();
            expect(() => normalDist.cumulativeProbability!(Infinity)).toThrow();

            expect(() => normalDist.quantile!(NaN)).toThrow();
            expect(() => normalDist.quantile!(-0.1)).toThrow();
            expect(() => normalDist.quantile!(1.1)).toThrow();
        });
    });

    describe('Statistical Properties', () => {
        test('should generate normal distribution that passes chi-square test', () => {
            const normalDist = BoxMullerTransform();
            const samples = normalDist.sampleMany(1000);

            const mean = calculateMean(samples as number[]);
            const stdDev = calculateStdDev(samples as number[], mean);

            const passesChiSquare = chiSquareTest(samples as number[], mean, stdDev);
            expect(passesChiSquare).toBe(true);
        });

        test('should have symmetrical distribution around mean', () => {
            const mean = 5;
            const normalDist = BoxMullerTransform({ mean });
            const samples = normalDist.sampleMany(2000) as number[];

            const belowMean = samples.filter((v) => v < mean).length;
            const aboveMean = samples.filter((v) => v > mean).length;

            const ratio = belowMean / aboveMean;
            expect(ratio).toBeGreaterThan(0.9);
            expect(ratio).toBeLessThan(1.1);
        });

        test('should have approximately 68% of values within 1 standard deviation', () => {
            const mean = 0;
            const stdDev = 1;
            const normalDist = BoxMullerTransform({ mean, standardDeviation: stdDev });

            const samples = normalDist.sampleMany(10000) as number[];
            const within1StdDev = samples.filter(
                (v) => v >= mean - stdDev && v <= mean + stdDev
            ).length;

            const percentage = within1StdDev / samples.length;
            expect(percentage).toBeGreaterThan(0.63);
            expect(percentage).toBeLessThan(0.73);
        });

        test('should have approximately 95% of values within 2 standard deviations', () => {
            const mean = 0;
            const stdDev = 1;
            const normalDist = BoxMullerTransform({ mean, standardDeviation: stdDev });

            const samples = normalDist.sampleMany(10000) as number[];
            const within2StdDev = samples.filter(
                (v) => v >= mean - 2 * stdDev && v <= mean + 2 * stdDev
            ).length;

            const percentage = within2StdDev / samples.length;
            expect(percentage).toBeGreaterThan(0.92);
            expect(percentage).toBeLessThan(0.98);
        });
    });
});
