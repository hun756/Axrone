import {
    isVec2,
    isVec2Tuple,
    isVec2Like,
    Vec2,
    ReadonlyVec2,
    Vec2Tuple,
    Vec2Like,
    Scalar,
} from '../vec2';

describe('Vector Type Guards', () => {
    const vecObj: ReadonlyVec2 = Object.freeze({ x: 1, y: 2 });
    const vecTuple: Vec2Tuple = Object.freeze([3, 4]);
    const mutableVecObj: Vec2 = { x: 5, y: 6 };

    test('isVec2 should correctly identify Vec2 objects', () => {
        expect(isVec2(vecObj)).toBe(true);
        expect(isVec2(mutableVecObj)).toBe(true);
        expect(isVec2(vecTuple)).toBe(false);
        expect(isVec2({ x: 1 })).toBe(false);
        expect(isVec2({ y: 2 })).toBe(false);
        expect(isVec2({ x: 1, y: '2' })).toBe(false);
        expect(isVec2([1, 2])).toBe(false);
        expect(isVec2('hello')).toBe(false);
        expect(isVec2(null)).toBe(false);
        expect(isVec2(undefined)).toBe(false);
        expect(isVec2(123)).toBe(false);
        expect(isVec2({ x: NaN, y: 0 })).toBe(true);
        expect(isVec2({ x: Infinity, y: 0 })).toBe(true);
    });

    test('isVec2Tuple should correctly identify Vec2 tuples', () => {
        expect(isVec2Tuple(vecTuple)).toBe(true);
        expect(isVec2Tuple([10, 20])).toBe(true);
        expect(isVec2Tuple(vecObj)).toBe(false);
        expect(isVec2Tuple([1])).toBe(false);
        expect(isVec2Tuple([1, 2, 3])).toBe(false);
        expect(isVec2Tuple([1, '2'])).toBe(false);
        expect(isVec2Tuple({ x: 1, y: 2 })).toBe(false);
        expect(isVec2Tuple('hello')).toBe(false);
        expect(isVec2Tuple(null)).toBe(false);
        expect(isVec2Tuple(undefined)).toBe(false);
    });

    test('isVec2Like should identify both Vec2 objects and tuples', () => {
        expect(isVec2Like(vecObj)).toBe(true);
        expect(isVec2Like(mutableVecObj)).toBe(true);
        expect(isVec2Like(vecTuple)).toBe(true);
        expect(isVec2Like([100, 200])).toBe(true);
        expect(isVec2Like({ x: 1 })).toBe(false);
        expect(isVec2Like([1, 2, 3])).toBe(false);
        expect(isVec2Like('hello')).toBe(false);
        expect(isVec2Like(null)).toBe(false);
    });
});
