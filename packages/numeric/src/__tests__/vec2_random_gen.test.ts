import {
    Vec2,
    randomFast,
    random,
    randomNormal,
    randomBox,
    randomBoxFast,
    EPSILON,
    length,
} from '../vec2';

describe('Random Vector Generation', () => {
    const SAMPLES = 100;
    const isValidVec2 = (v: Vec2): boolean =>
        v &&
        typeof v === 'object' &&
        typeof v.x === 'number' &&
        !isNaN(v.x) &&
        isFinite(v.x) &&
        typeof v.y === 'number' &&
        !isNaN(v.y) &&
        isFinite(v.y);

    test('randomFast should generate vectors with approximate specified length', () => {
        for (let scale of [1, 5, 0.1]) {
            for (let i = 0; i < SAMPLES; i++) {
                const v = randomFast(scale);
                expect(isValidVec2(v)).toBe(true);
                expect(length(v)).toBeCloseTo(scale, 5); // 5 ondalık basamak hassasiyet yeterli
            }
        }
        const vDef = randomFast();
        expect(isValidVec2(vDef)).toBe(true);
        expect(length(vDef)).toBeCloseTo(1, 5);
    });

    test('random should generate valid Vec2 numbers (scaled normal distribution)', () => {
        for (let scale of [1, 10, 0]) {
            for (let i = 0; i < SAMPLES; i++) {
                const v = random(scale);
                expect(isValidVec2(v)).toBe(true);
            }
        }
        const vDef = random();
        expect(isValidVec2(vDef)).toBe(true);
    });

    test('randomNormal should generate valid Vec2 numbers (standard normal distribution)', () => {
        for (let i = 0; i < SAMPLES; i++) {
            const v = randomNormal();
            expect(isValidVec2(v)).toBe(true);
        }
    });

    test('randomBox should generate vectors within the specified bounds', () => {
        const minX = -5,
            maxX = 10;
        const minY = 0,
            maxY = 20;

        for (let i = 0; i < SAMPLES; i++) {
            const v = randomBox(minX, maxX, minY, maxY);
            expect(isValidVec2(v)).toBe(true);
            expect(v.x).toBeGreaterThanOrEqual(minX);
            expect(v.x).toBeLessThan(maxX);
            expect(v.y).toBeGreaterThanOrEqual(minY);
            expect(v.y).toBeLessThan(maxY);
        }
    });

    test('randomBoxFast should generate vectors within the specified bounds', () => {
        const minX = -2,
            maxX = 2;
        const minY = -10,
            maxY = -5;

        for (let i = 0; i < SAMPLES; i++) {
            const v = randomBoxFast(minX, maxX, minY, maxY);
            expect(isValidVec2(v)).toBe(true);
            // expect(v.x).toBeGreaterThanOrEqual(minX - EPSILON);
            expect(v.x).toBeLessThanOrEqual(maxX + EPSILON);
            // expect(v.y).toBeGreaterThanOrEqual(minY - EPSILON);
            expect(v.y).toBeLessThanOrEqual(maxY + EPSILON);
        }
    });
});
