import { Vector2, Vector3 } from '../vector';

describe('Vector2', () => {
    test('constructor initializes with default values', () => {
        const vec = new Vector2();
        expect(vec.x).toBe(0);
        expect(vec.y).toBe(0);
    });

    test('constructor initializes with provided values', () => {
        const vec = new Vector2(3, 4);
        expect(vec.x).toBe(3);
        expect(vec.y).toBe(4);
    });

    test('add returns a new vector with added components', () => {
        const vec1 = new Vector2(1, 2);
        const vec2 = new Vector2(3, 4);
        const result = vec1.add(vec2);

        expect(result.x).toBe(4);
        expect(result.y).toBe(6);
        // Orijinal vektörlerin değişmediğini kontrol et
        expect(vec1.x).toBe(1);
        expect(vec1.y).toBe(2);
    });

    test('length returns the correct magnitude', () => {
        const vec = new Vector2(3, 4);
        expect(vec.length()).toBe(5);
    });

    test('normalize returns a unit vector', () => {
        const vec = new Vector2(3, 4);
        const normalized = vec.normalize();

        expect(normalized.length()).toBeCloseTo(1);
        expect(normalized.x).toBeCloseTo(0.6);
        expect(normalized.y).toBeCloseTo(0.8);
    });
});

describe('Vector3', () => {
    test('constructor initializes with default values', () => {
        const vec = new Vector3();
        expect(vec.x).toBe(0);
        expect(vec.y).toBe(0);
        expect(vec.z).toBe(0);
    });

    test('constructor initializes with provided values', () => {
        const vec = new Vector3(1, 2, 3);
        expect(vec.x).toBe(1);
        expect(vec.y).toBe(2);
        expect(vec.z).toBe(3);
    });

    test('dot product returns correct scalar value', () => {
        const vec1 = new Vector3(1, 2, 3);
        const vec2 = new Vector3(4, 5, 6);

        // (1*4) + (2*5) + (3*6) = 4 + 10 + 18 = 32
        expect(vec1.dot(vec2)).toBe(32);
    });

    test('cross product returns correct vector', () => {
        const vec1 = new Vector3(1, 0, 0);
        const vec2 = new Vector3(0, 1, 0);
        const result = vec1.cross(vec2);

        expect(result.x).toBe(0);
        expect(result.y).toBe(0);
        expect(result.z).toBe(1);
    });
});
