import { isVec2, isVec2Tuple, isVec2Like } from '../vec2_legacy';

describe('Extended Vec2 Type Validator Tests', () => {
    describe('isVec2 extended tests', () => {
        test('Should handle objects created with Object.create correctly', () => {
            const proto = { x: 1, y: 2 };
            const obj = Object.create(proto);
            expect(isVec2(obj)).toBe(true);

            const directObj = Object.create(null);
            directObj.x = 1;
            directObj.y = 2;
            expect(isVec2(directObj)).toBe(true);
        });

        test('Should handle properties defined with getter methods correctly', () => {
            const obj = {
                _x: 1,
                _y: 2,
                get x() {
                    return this._x;
                },
                get y() {
                    return this._y;
                },
            };
            expect(isVec2(obj)).toBe(true);
        });

        test('Should handle objects wrapped with proxy correctly', () => {
            const target = { x: 1, y: 2 };
            const proxy = new Proxy(target, {});
            expect(isVec2(proxy)).toBe(true);
        });

        test('Should ignore symbols', () => {
            const symbolKey = Symbol('test');
            const obj = {
                x: 1,
                y: 2,
                [symbolKey]: 'value',
            };
            expect(isVec2(obj)).toBe(true);
        });
    });

    describe('isVec2Tuple extended tests', () => {
        test('Should work correctly for different array types', () => {
            expect(isVec2Tuple([1, 2])).toBe(true);

            expect(isVec2Tuple(new Float32Array([1, 2]))).toBe(false);

            const arrayLike = { 0: 1, 1: 2, length: 2 };
            expect(isVec2Tuple(arrayLike)).toBe(false);
        });

        test('Should work correctly for readonly arrays', () => {
            const readonlyArr = Object.freeze([1, 2] as const);
            expect(isVec2Tuple(readonlyArr)).toBe(true);
        });

        test('Should behave appropriately for sparse arrays', () => {
            const sparseArray: number[] = [];
            sparseArray[0] = 1;
            sparseArray[1] = 2;
            expect(isVec2Tuple(sparseArray)).toBe(true);
        });
    });

    describe('isVec2Like extended tests', () => {
        test('Should work correctly for objects that have both Vec2 and Vec2Tuple properties', () => {
            const hybrid = [1, 2] as any;
            hybrid.x = 3;
            hybrid.y = 4;

            expect(isVec2Tuple(hybrid)).toBe(true);

            expect(isVec2(hybrid)).toBe(true);

            expect(isVec2Like(hybrid)).toBe(true);
        });

        test('Should reject number-like values such as BigInt', () => {
            expect(isVec2({ x: BigInt(1), y: 2 } as any)).toBe(false);
            expect(isVec2Tuple([BigInt(1), 2] as any)).toBe(false);
            expect(isVec2Like({ x: BigInt(1), y: 2 } as any)).toBe(false);
        });

        test('Should not accept convertible values', () => {
            const primitiveNumber = 5;

            expect(isVec2(primitiveNumber as any)).toBe(false);
            expect(isVec2Like(primitiveNumber as any)).toBe(false);

            const primitiveString = 'test';
            expect(isVec2(primitiveString as any)).toBe(false);
            expect(isVec2Like(primitiveString as any)).toBe(false);
        });
    });

    describe('edge cases and special situations', () => {
        test('Should handle numeric edge values correctly', () => {
            expect(isVec2({ x: Number.MAX_VALUE, y: Number.MIN_VALUE })).toBe(true);
            expect(isVec2Tuple([Number.MAX_VALUE, Number.MIN_VALUE])).toBe(true);

            expect(isVec2({ x: 1 / 0, y: -1 / 0 })).toBe(true);
            expect(isVec2Tuple([1 / 0, -1 / 0])).toBe(true);
        });

        test('Should access x/y properties found in the prototype chain correctly', () => {
            class BaseVec {
                x = 1;
                y = 2;
            }

            class DerivedVec extends BaseVec {
                z = 3;
            }

            const derivedInstance = new DerivedVec();
            expect(isVec2(derivedInstance)).toBe(true);
            expect(isVec2Like(derivedInstance)).toBe(true);
        });
    });
});
