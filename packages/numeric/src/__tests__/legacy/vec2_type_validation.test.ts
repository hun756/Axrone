import { isVec2, Vec2, ReadonlyVec2, Scalar } from '../../vec2_legacy';

describe('Vector Type Guards', () => {
    const vecObj: ReadonlyVec2 = Object.freeze({ x: 1, y: 2 });
    const vecTuple: ReadonlyVec2 = Object.freeze({ x: 3, y: 4 });
    const mutableVecObj: Vec2 = { x: 5, y: 6 };

    test('isVec2 should correctly identify Vec2 objects', () => {
        expect(isVec2(vecObj)).toBe(true);
        expect(isVec2(mutableVecObj)).toBe(true);
        expect(isVec2(vecTuple)).toBe(true);
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
});
