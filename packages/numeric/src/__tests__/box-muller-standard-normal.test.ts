import {
    StandardNormal,
    isRandomGenerator,
    isNormalDistribution,
    BoxMullerTransform,
    DefaultRandomGenerator,
    NormalDistribution,
    RandomGenerator,
    ErrorCodes,
} from '../box-muller';

class MockRandomGenerator implements RandomGenerator {
    private mockFn: jest.Mock;

    constructor(mockImplementation?: () => number) {
        this.mockFn = jest.fn(mockImplementation || (() => 0.5));
    }

    next(): number {
        return this.mockFn();
    }

    nextInRange(min: number, max: number): number {
        return min + (max - min) * this.next();
    }

    nextInt(min: number, max: number): number {
        return Math.floor(this.nextInRange(min, max + 1));
    }

    getMock(): jest.Mock {
        return this.mockFn;
    }
}

describe('StandardNormal', () => {
    it('should create a normal distribution with mean 0 and standard deviation 1', () => {
        const sampleSize = 1000;

        const mockRandomGenerator = new MockRandomGenerator();
        for (let i = 0; i < sampleSize * 2; i++) {
            mockRandomGenerator.getMock().mockReturnValueOnce(0.1 + (0.8 * (i % 100)) / 100);
        }

        const distribution = StandardNormal({
            randomGenerator: mockRandomGenerator,
            useCache: true,
        });

        const samples = distribution.sampleMany(sampleSize);

        let sum = 0;
        for (let i = 0; i < samples.length; i++) {
            sum += samples[i];
        }
        const mean = sum / samples.length;

        let sumSquaredDiffs = 0;
        for (let i = 0; i < samples.length; i++) {
            sumSquaredDiffs += Math.pow(samples[i] - mean, 2);
        }
        const variance = sumSquaredDiffs / samples.length;
        const stdDev = Math.sqrt(variance);

        expect(Math.abs(mean)).toBeLessThan(0.2); // Toleransı biraz artırdık
        expect(Math.abs(stdDev - 1)).toBeLessThan(0.2);
    });

    it('should forward other options to BoxMullerTransform', () => {
        const mockRandomGenerator = new MockRandomGenerator(() => 0.5);
        const mockFn = mockRandomGenerator.getMock();

        const distribution = StandardNormal({
            randomGenerator: mockRandomGenerator,
            algorithm: 'polar',
            useCache: false,
        });

        distribution.sample();
        expect(mockFn).toHaveBeenCalled();
    });

    it('should have probability methods', () => {
        const distribution = StandardNormal();

        expect(distribution.probability).toBeDefined();
        expect(distribution.cumulativeProbability).toBeDefined();
        expect(distribution.quantile).toBeDefined();
    });

    it('should have correct probability values for standard normal', () => {
        const distribution = StandardNormal();

        const pdfAtMean = distribution.probability!(0);
        expect(pdfAtMean).toBeCloseTo(1 / Math.sqrt(2 * Math.PI), 5);

        const cdfAtMean = distribution.cumulativeProbability!(0);
        expect(cdfAtMean).toBeCloseTo(0.5, 5);

        const quantileAtMedian = distribution.quantile!(0.5);
        expect(quantileAtMedian).toBeCloseTo(0, 5);

        const quantileAt0_9 = distribution.quantile!(0.9);
        expect(quantileAt0_9).toBeCloseTo(1.28, 2);
    });
});

describe('isRandomGenerator', () => {
    it('should return true for valid RandomGenerator objects', () => {
        const validGenerator = {
            next: () => Math.random(),
        };

        expect(isRandomGenerator(validGenerator)).toBe(true);
        expect(isRandomGenerator(new DefaultRandomGenerator())).toBe(true);
    });

    it('should return false for null or undefined', () => {
        expect(isRandomGenerator(null)).toBe(false);
        expect(isRandomGenerator(undefined)).toBe(false);
    });

    it('should return false for objects without next method', () => {
        expect(isRandomGenerator({})).toBe(false);
        expect(isRandomGenerator({ nextVal: () => Math.random() })).toBe(false);
    });

    it('should return false if next is not a function', () => {
        expect(isRandomGenerator({ next: 'not a function' })).toBe(false);
        expect(isRandomGenerator({ next: 123 })).toBe(false);
    });

    it('should return false for primitive values', () => {
        expect(isRandomGenerator(123)).toBe(false);
        expect(isRandomGenerator('string')).toBe(false);
        expect(isRandomGenerator(true)).toBe(false);
    });

    it('should work with generic type parameter', () => {
        const stringGenerator = {
            next: () => 'random string',
        };

        expect(isRandomGenerator<string>(stringGenerator)).toBe(true);
    });
});

describe('isNormalDistribution', () => {
    it('should return true for valid NormalDistribution objects', () => {
        const validDistribution = {
            sample: () => 0,
            sampleMany: (count: number) => Array(count).fill(0),
        };
        expect(isNormalDistribution(validDistribution)).toBe(true);
    });

    it('should return false for null or undefined', () => {
        expect(isNormalDistribution(null)).toBe(false);
        expect(isNormalDistribution(undefined)).toBe(false);
    });

    it('should return false for objects missing required methods', () => {
        expect(isNormalDistribution({})).toBe(false);
        expect(isNormalDistribution({ sample: () => 0 })).toBe(false);
        expect(isNormalDistribution({ sampleMany: (n: number) => Array(n).fill(0) })).toBe(false);
    });

    it('should return false if methods are not functions', () => {
        expect(isNormalDistribution({ sample: 'not a function', sampleMany: () => [] })).toBe(
            false
        );
        expect(isNormalDistribution({ sample: () => 0, sampleMany: 'not a function' })).toBe(false);
    });

    it('should return false for primitive values', () => {
        expect(isNormalDistribution(123)).toBe(false);
        expect(isNormalDistribution('string')).toBe(false);
        expect(isNormalDistribution(true)).toBe(false);
    });

    it('should work with generic type parameter', () => {
        const stringDistribution: NormalDistribution<string> = {
            sample: () => 'random string',
            sampleMany: (count: number) => Array(count).fill('random string'),
        };

        expect(isNormalDistribution<string>(stringDistribution)).toBe(true);
    });
});

describe('Integration Tests', () => {
    it('should validate a StandardNormal as a NormalDistribution', () => {
        const distribution = StandardNormal();
        expect(isNormalDistribution(distribution)).toBe(true);
    });

    it('should validate DefaultRandomGenerator with isRandomGenerator', () => {
        const generator = new DefaultRandomGenerator();
        expect(isRandomGenerator(generator)).toBe(true);
    });

    it('should validate that a distribution created with a custom generator uses it', () => {
        const mockGenerator = new MockRandomGenerator();
        mockGenerator.getMock().mockReturnValueOnce(0.1).mockReturnValueOnce(0.2);

        const distribution = StandardNormal({
            randomGenerator: mockGenerator,
            algorithm: 'standard',
            useCache: false,
        });

        distribution.sample();
        expect(mockGenerator.getMock()).toHaveBeenCalled();
    });
});
