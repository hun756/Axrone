import {
    clone,
    create,
    EPSILON,
    fromAngle,
    fromArray,
    fromDegrees,
    fromPolar,
    fromValues,
    ReadonlyVec2,
    SQRT2,
} from '../../vec2_legacy';

describe('Vector Factory Functions', () => {
    test('create should create vectors with default or given values', () => {
        expect(create()).toEqual({ x: 0, y: 0 });
        expect(create(5)).toEqual({ x: 5, y: 0 });
        expect(create(undefined, 10)).toEqual({ x: 0, y: 10 });
        expect(create(1, 2)).toEqual({ x: 1, y: 2 });
    });

    test('fromValues should create vectors with given values', () => {
        expect(fromValues(3, 4)).toEqual({ x: 3, y: 4 });
        expect(fromValues(-1, 0)).toEqual({ x: -1, y: 0 });
    });

    test('fromArray should create vectors from tuples', () => {
        const tuple: number[] = [5, -6];
        expect(fromArray(tuple)).toEqual({ x: 5, y: -6 });
        expect(fromArray([0, 0])).toEqual({ x: 0, y: 0 });
    });

    test('clone should create a new mutable Vec2 instance from Vec2Like', () => {
        const objVec: ReadonlyVec2 = { x: 1, y: 2 };
        const vecLike: ReadonlyVec2 = { x: 3, y: 4 }; // Extra propert

        const clone1 = clone(objVec);
        expect(clone1).toEqual({ x: 1, y: 2 });
        expect(clone1).not.toBe(objVec);
        clone1.x = 10;
        expect(clone1.x).toBe(10);
        expect(objVec.x).toBe(1);

        const clone2 = clone(vecLike);
        expect(clone2).toEqual({ x: 3, y: 4 });
        clone2.y = 20;
        expect(clone2.y).toBe(20);
        expect(vecLike.y).toBe(4);
    });

    test('fromAngle should create vectors from radians', () => {
        const v0 = fromAngle(0);
        expect(v0.x).toBeCloseTo(1, EPSILON);
        expect(v0.y).toBeCloseTo(0, EPSILON);

        const vPi_2 = fromAngle(Math.PI / 2);
        expect(vPi_2.x).toBeCloseTo(0, EPSILON);
        expect(vPi_2.y).toBeCloseTo(1, EPSILON);

        const vPi = fromAngle(Math.PI, 5);
        expect(vPi.x).toBeCloseTo(-5, EPSILON);
        expect(vPi.y).toBeCloseTo(0, EPSILON);

        const v3Pi_2 = fromAngle((3 * Math.PI) / 2, 2);
        expect(v3Pi_2.x).toBeCloseTo(0, EPSILON);
        expect(v3Pi_2.y).toBeCloseTo(-2, EPSILON);
    });

    test('fromDegrees should create vectors from degrees', () => {
        const v0 = fromDegrees(0);
        expect(v0.x).toBeCloseTo(1, EPSILON);
        expect(v0.y).toBeCloseTo(0, EPSILON);

        const v90 = fromDegrees(90);
        expect(v90.x).toBeCloseTo(0, EPSILON);
        expect(v90.y).toBeCloseTo(1, EPSILON);

        const v180 = fromDegrees(180, 3);
        expect(v180.x).toBeCloseTo(-3, EPSILON);
        expect(v180.y).toBeCloseTo(0, EPSILON);

        const v45 = fromDegrees(45, SQRT2);
        expect(v45.x).toBeCloseTo(1, EPSILON);
        expect(v45.y).toBeCloseTo(1, EPSILON);
    });

    test('fromPolar should create vectors from radius and radians', () => {
        const v1 = fromPolar(5, 0);
        expect(v1.x).toBeCloseTo(5, EPSILON);
        expect(v1.y).toBeCloseTo(0, EPSILON);

        const v2 = fromPolar(1, Math.PI / 2);
        expect(v2.x).toBeCloseTo(0, EPSILON);
        expect(v2.y).toBeCloseTo(1, EPSILON);
    });
});
