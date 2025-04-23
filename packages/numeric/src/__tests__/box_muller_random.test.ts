import {
    DefaultRandomGenerator,
    createDefaultRandomGenerator,
    type RandomGenerator,
} from '../box_muller';

describe('DefaultRandomGenerator', () => {
    const originalMathRandom = Math.random;
    let mockRandom: jest.Mock;

    beforeEach(() => {
        mockRandom = jest.fn();
        Math.random = mockRandom;
    });

    afterEach(() => {
        Math.random = originalMathRandom;
    });

    test('getInstance should return the same instance (singleton)', () => {
        const instance1 = DefaultRandomGenerator.getInstance();
        const instance2 = DefaultRandomGenerator.getInstance();
        const instance3 = DefaultRandomGenerator.getInstance();

        expect(instance1).toBeInstanceOf(DefaultRandomGenerator);
        expect(instance1).toBe(instance2);
        expect(instance2).toBe(instance3);
    });

    test('next() should call Math.random and return its value', () => {
        mockRandom.mockReturnValue(0.123);
        const generator = DefaultRandomGenerator.getInstance();
        const result = generator.next();

        expect(mockRandom).toHaveBeenCalledTimes(1);
        expect(result).toBe(0.123);

        mockRandom.mockReturnValue(0.987);
        expect(generator.next()).toBe(0.987);
        expect(mockRandom).toHaveBeenCalledTimes(2);
    });

    test('nextInRange(min, max) should return a value within the range [min, max)', () => {
        const min = 5;
        const max = 15;
        const range = max - min;

        const generator = DefaultRandomGenerator.getInstance();

        mockRandom.mockReturnValue(0);
        expect(generator.nextInRange(min, max)).toBe(min);

        mockRandom.mockReturnValue(0.999999999999999);
        const resultNearMax = generator.nextInRange(min, max);
        expect(resultNearMax).toBeGreaterThanOrEqual(min);
        expect(resultNearMax).toBeLessThan(max);

        mockRandom.mockReturnValue(0.5);
        expect(generator.nextInRange(min, max)).toBe(min + range * 0.5);

        mockRandom.mockReturnValue(0.25);
        expect(generator.nextInRange(min, max)).toBe(min + range * 0.25);
    });

    test('nextInt(min, max) should return an integer within the range [min, max]', () => {
        const min = 1;
        const max = 10;

        const generator = DefaultRandomGenerator.getInstance();

        mockRandom.mockReturnValue(0);
        expect(generator.nextInt(min, max)).toBe(min);

        mockRandom.mockReturnValue(0.999999999999999);
        expect(generator.nextInt(min, max)).toBe(max);

        mockRandom.mockReturnValue(0.4);
        expect(generator.nextInt(min, max)).toBe(5);

        mockRandom.mockReturnValue(0.499999999999999);
        expect(generator.nextInt(min, max)).toBe(5);

        mockRandom.mockReturnValue(0.5);
        expect(generator.nextInt(min, max)).toBe(6);

        mockRandom.mockReturnValue(0.000000000000001);
        expect(generator.nextInt(min, max)).toBe(1);

        mockRandom.mockReturnValue(0.8);
        expect(generator.nextInt(min, max)).toBe(9);
    });

    test('createDefaultRandomGenerator should return a new instance each time', () => {
        const generator1 = createDefaultRandomGenerator();
        const generator2 = createDefaultRandomGenerator();

        expect(generator1).toBeInstanceOf(DefaultRandomGenerator);
        expect(generator2).toBeInstanceOf(DefaultRandomGenerator);
        expect(generator1).not.toBe(generator2);
    });

    test('createDefaultRandomGenerator instances should implement RandomGenerator', () => {
        const generator = createDefaultRandomGenerator();
        const typedGenerator: RandomGenerator = generator;
        expect(typedGenerator).toBe(generator);
        expect(typeof typedGenerator.next).toBe('function');
    });
});
