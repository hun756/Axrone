import { Vec3, Vec3ComparisonMode, Vec3Comparer, Vec3EqualityComparer, IVec3Like } from '../vec3';

const EPSILON = 1e-10;
const FLOAT_PRECISION = 1e-6;
const PERFORMANCE_ITERATIONS = 100000;

class Vec3TestDataBuilder {
    static createZero(): Vec3 {
        return new Vec3(0, 0, 0);
    }

    static createUnit(): Vec3 {
        return new Vec3(1, 1, 1);
    }

    static createRandom(scale: number = 1): Vec3 {
        return new Vec3(
            (Math.random() - 0.5) * 2 * scale,
            (Math.random() - 0.5) * 2 * scale,
            (Math.random() - 0.5) * 2 * scale
        );
    }

    static createNormalized(): Vec3 {
        const v = Vec3TestDataBuilder.createRandom(10);
        return v.normalize();
    }

    static createLarge(): Vec3 {
        return new Vec3(Number.MAX_SAFE_INTEGER, Number.MAX_SAFE_INTEGER, Number.MAX_SAFE_INTEGER);
    }

    static createSmall(): Vec3 {
        return new Vec3(Number.MIN_VALUE, Number.MIN_VALUE, Number.MIN_VALUE);
    }

    static createNearZero(): Vec3 {
        return new Vec3(EPSILON / 2, EPSILON / 2, EPSILON / 2);
    }

    static createBatch(count: number): Vec3[] {
        return Array.from({ length: count }, () => Vec3TestDataBuilder.createRandom());
    }
}

expect.extend({
    toBeCloseToVec3(received: Vec3, expected: Vec3, precision = FLOAT_PRECISION) {
        const pass =
            Math.abs(received.x - expected.x) < precision &&
            Math.abs(received.y - expected.y) < precision &&
            Math.abs(received.z - expected.z) < precision;

        return {
            message: () =>
                `expected Vec3(${received.x}, ${received.y}, ${received.z}) to be close to Vec3(${expected.x}, ${expected.y}, ${expected.z})`,
            pass,
        };
    },

    toBeNormalizedVec3(received: Vec3, precision = FLOAT_PRECISION) {
        const length = received.length();
        const pass = Math.abs(length - 1) < precision;

        return {
            message: () => `expected Vec3 to be normalized (length = 1), but length was ${length}`,
            pass,
        };
    },

    toBePerpendicularTo(received: Vec3, other: Vec3, precision = FLOAT_PRECISION) {
        const dotProduct = received.dot(other);
        const pass = Math.abs(dotProduct) < precision;

        return {
            message: () =>
                `expected vectors to be perpendicular (dot product = 0), but dot product was ${dotProduct}`,
            pass,
        };
    },
});

describe('Vec3 Test Suite', () => {});
