import {
    NormalPool,
    BoxMullerTransform,
    DefaultRandomGenerator,
    ErrorCodes,
    RandomGenerator,
    createError,
} from '../box_muller';

class MockRandomGenerator implements RandomGenerator {
    private values: number[];
    private currentIndex: number = 0;

    constructor(values: number[] = [0.5, 0.3, 0.7, 0.1, 0.9, 0.4, 0.6, 0.2, 0.8]) {
        this.values = values;
    }

    next(): number {
        const value = this.values[this.currentIndex];
        this.currentIndex = (this.currentIndex + 1) % this.values.length;
        return value;
    }

    setValues(values: number[]): void {
        this.values = values;
        this.currentIndex = 0;
    }
}

describe('NormalPool', () => {
    let mockRandomGenerator: MockRandomGenerator;

    beforeEach(() => {
        mockRandomGenerator = new MockRandomGenerator();
        jest.spyOn(console, 'error').mockImplementation(() => {});
    });

    afterEach(() => {
        jest.resetAllMocks();
    });

    describe('constructor', () => {
        it('should create a normal pool with default options', () => {
            const pool = NormalPool();
            expect(pool).toBeDefined();
            expect(pool.sample).toBeInstanceOf(Function);
            expect(pool.sampleMany).toBeInstanceOf(Function);
            expect(pool.refill).toBeInstanceOf(Function);
        });

        it('should create a normal pool with custom options', () => {
            const pool = NormalPool({
                mean: 5,
                standardDeviation: 2,
                poolSize: 500,
                randomGenerator: mockRandomGenerator,
            });
            expect(pool).toBeDefined();
        });

        it('should throw when poolSize is not positive', () => {
            expect(() => NormalPool({ poolSize: 0 })).toThrow(
                createError(ErrorCodes.INVALID_PARAMETER, 'poolSize must be positive')
            );

            expect(() => NormalPool({ poolSize: -10 })).toThrow(
                createError(ErrorCodes.INVALID_PARAMETER, 'poolSize must be positive')
            );
        });

        it('should throw when poolSize is not an integer', () => {
            expect(() => NormalPool({ poolSize: 10.5 })).toThrow(
                createError(ErrorCodes.INVALID_PARAMETER, 'poolSize must be an integer')
            );
        });
    });

    describe('sample', () => {
        it('should return a sample from the pool', () => {
            const pool = NormalPool({
                randomGenerator: mockRandomGenerator,
                poolSize: 3,
                algorithm: 'polar',
            });

            const sample1 = pool.sample();
            expect(typeof sample1).toBe('number');
            expect(Number.isFinite(sample1)).toBe(true);
            const sample2 = pool.sample();
            const sample3 = pool.sample();
            const sample4 = pool.sample();
            expect(sample4).not.toBe(sample1);
        });

        it('should refill the pool when all values are consumed', () => {
            const poolSize = 3;
            class TestableNormalPool {
                private values: number[];
                private index: number = 0;
                private poolSize: number;
                public refillCalled: number = 0;

                constructor(poolSize: number) {
                    this.poolSize = poolSize;
                    this.values = Array(poolSize).fill(0.5);
                }

                sample(): number {
                    if (this.index >= this.values.length) {
                        this.refill();
                    }
                    return this.values[this.index++];
                }

                refill(): void {
                    this.refillCalled++;
                    this.values = Array(this.poolSize).fill(0.7);
                    this.index = 0;
                }
            }

            const testablePool = new TestableNormalPool(poolSize);
            for (let i = 0; i < poolSize; i++) {
                testablePool.sample();
            }

            expect(testablePool.refillCalled).toBe(0);
            testablePool.sample();
            expect(testablePool.refillCalled).toBe(1);
        });
    });
    describe('sampleMany', () => {
        it('should return the requested number of samples', () => {
            const pool = NormalPool({
                randomGenerator: mockRandomGenerator,
                poolSize: 10,
            });

            const samples = pool.sampleMany(5);
            expect(Array.isArray(samples)).toBe(true);
            expect(samples.length).toBe(5);

            samples.forEach((sample) => {
                expect(typeof sample).toBe('number');
                expect(Number.isFinite(sample)).toBe(true);
            });
        });

        it('should use values from the pool when count is less than remaining pool size', () => {
            const poolSize = 10;
            const pool = NormalPool({
                randomGenerator: mockRandomGenerator,
                poolSize,
            });

            const distributionSampleManySpy = jest.spyOn(
                BoxMullerTransform({ randomGenerator: mockRandomGenerator }),
                'sampleMany'
            );

            pool.sample();
            pool.sample();
            pool.sample();
            pool.sample();
            const samples = pool.sampleMany(3);
            expect(samples.length).toBe(3);
            expect(distributionSampleManySpy).not.toHaveBeenCalled();
        });

        it('should call the distribution sampleMany when count is greater than remaining pool size', () => {
            const poolSize = 5;
            const mockDist = BoxMullerTransform({ randomGenerator: mockRandomGenerator });
            const sampleManySpy = jest.spyOn(mockDist, 'sampleMany');
            const pool = NormalPool({
                randomGenerator: mockRandomGenerator,
                poolSize,
            });
            const samples = pool.sampleMany(20);
            expect(samples.length).toBe(20);
            expect(samples.every((s) => typeof s === 'number' && Number.isFinite(s))).toBe(true);
        });

        it('should throw when count is not positive', () => {
            const pool = NormalPool();
            expect(() => pool.sampleMany(0)).toThrow(
                createError(ErrorCodes.INVALID_PARAMETER, 'count must be positive')
            );
            expect(() => pool.sampleMany(-5)).toThrow(
                createError(ErrorCodes.INVALID_PARAMETER, 'count must be positive')
            );
        });
        it('should throw when count is not an integer', () => {
            const pool = NormalPool();
            expect(() => pool.sampleMany(3.5)).toThrow(
                createError(ErrorCodes.INVALID_PARAMETER, 'count must be an integer')
            );
        });
    });

    describe('refill', () => {
        it('should refill the pool with new samples', () => {
            const poolSize = 5;
            const pool = NormalPool({
                randomGenerator: mockRandomGenerator,
                poolSize,
            });
            const initialSamples = [];
            for (let i = 0; i < poolSize; i++) {
                initialSamples.push(pool.sample());
            }
            pool.refill();
            const newSamples = [];
            for (let i = 0; i < poolSize; i++) {
                newSamples.push(pool.sample());
            }
            expect(newSamples).not.toEqual(initialSamples);
        });
    });

    describe('algorithm integration', () => {
        it('should work with the standard algorithm', () => {
            const pool = NormalPool({
                algorithm: 'standard',
                randomGenerator: mockRandomGenerator,
                poolSize: 5,
            });
            const samples = pool.sampleMany(10);
            expect(samples.length).toBe(10);
            expect(samples.every((s) => Number.isFinite(s))).toBe(true);
        });

        it('should work with the polar algorithm', () => {
            const pool = NormalPool({
                algorithm: 'polar',
                randomGenerator: mockRandomGenerator,
                poolSize: 5,
            });
            const samples = pool.sampleMany(10);
            expect(samples.length).toBe(10);
            expect(samples.every((s) => Number.isFinite(s))).toBe(true);
        });

        it('should work with the ziggurat algorithm', () => {
            const pool = NormalPool({
                algorithm: 'ziggurat',
                randomGenerator: mockRandomGenerator,
                poolSize: 5,
            });
            const samples = pool.sampleMany(10);
            expect(samples.length).toBe(10);
            expect(samples.every((s) => Number.isFinite(s))).toBe(true);
        });
    });

    describe('custom distribution parameters', () => {
        it('should respect the mean parameter', () => {
            const mean = 10;
            const stdDev = 1;
            const poolSize = 1000;

            const pool = NormalPool({
                mean,
                standardDeviation: stdDev,
                poolSize,
                randomGenerator: new DefaultRandomGenerator(),
            });

            const samples = pool.sampleMany(poolSize);
            const average = samples.reduce((sum, val) => sum + val, 0) / samples.length;
            expect(Math.abs(average - mean)).toBeLessThan(0.5);
        });

        it('should respect the standardDeviation parameter', () => {
            const mean = 0;
            const stdDev = 5;
            const poolSize = 1000;

            const pool = NormalPool({
                mean,
                standardDeviation: stdDev,
                poolSize,
                randomGenerator: new DefaultRandomGenerator(),
            });

            const samples = pool.sampleMany(poolSize);
            const average = samples.reduce((sum, val) => sum + val, 0) / samples.length;
            const squaredDiffs = samples.map((x) => Math.pow(x - average, 2));
            const variance = squaredDiffs.reduce((sum, val) => sum + val, 0) / samples.length;
            const sampleStdDev = Math.sqrt(variance);
            expect(Math.abs(sampleStdDev - stdDev)).toBeLessThan(0.5);
        });
    });

    describe('edge cases', () => {
        it('should handle very small pool sizes', () => {
            const pool = NormalPool({ poolSize: 1 });
            const sample1 = pool.sample();
            const sample2 = pool.sample();
            expect(Number.isFinite(sample1)).toBe(true);
            expect(Number.isFinite(sample2)).toBe(true);
        });

        it('should handle extremely large requested sample counts', () => {
            const pool = NormalPool({ poolSize: 10 });
            const largeCount = 1000;
            const samples = pool.sampleMany(largeCount);
            expect(samples.length).toBe(largeCount);
            expect(samples.every((s) => Number.isFinite(s))).toBe(true);
        });
    });

    describe('performance considerations', () => {
        it('should be more efficient to use the pool for multiple small requests', () => {
            const poolSize = 1000;

            const pool = NormalPool({
                poolSize,
                randomGenerator: mockRandomGenerator,
            });

            const startTime = performance.now();
            for (let i = 0; i < 100; i++) {
                pool.sample();
            }

            const endTime = performance.now();
            const poolDuration = endTime - startTime;
            const distribution = BoxMullerTransform({ randomGenerator: mockRandomGenerator });
            const directStartTime = performance.now();

            for (let i = 0; i < 100; i++) {
                distribution.sample();
            }

            const directEndTime = performance.now();
            const directDuration = directEndTime - directStartTime;
            console.log(`Pool sampling: ${poolDuration}ms, Direct sampling: ${directDuration}ms`);
        });
    });
});
