import { isVec2, isVec2Tuple, isVec2Like } from '../../vec2_legacy';
import {
    createValidVec2Objects,
    createInvalidVec2Objects,
    createValidVec2Tuples,
    createInvalidVec2Tuples,
} from '../type_test_helper';

describe('Vec2 Type Validators with Test Generators', () => {
    describe('isVec2', () => {
        test('should return true for valid Vec2 objects', () => {
            const validVec2s = createValidVec2Objects();
            validVec2s.forEach((vec2) => {
                expect(isVec2(vec2)).toBe(true);
            });
        });

        test('should return false for invalid Vec2 objects', () => {
            const invalidVec2s = createInvalidVec2Objects();
            invalidVec2s.forEach((nonVec2) => {
                expect(isVec2(nonVec2)).toBe(false);
            });
        });
    });

    describe('isVec2Tuple', () => {
        test('should return true for valid Vec2Tuples', () => {
            const validTuples = createValidVec2Tuples();
            validTuples.forEach((tuple) => {
                expect(isVec2Tuple(tuple)).toBe(true);
            });
        });

        test('should return false for invalid Vec2Tuples', () => {
            const invalidTuples = createInvalidVec2Tuples();
            invalidTuples.forEach((nonTuple) => {
                expect(isVec2Tuple(nonTuple)).toBe(false);
            });
        });
    });

    describe('isVec2Like', () => {
        test('should return true for valid Vec2 or Vec2Tuple', () => {
            const validVec2s = createValidVec2Objects();
            const validTuples = createValidVec2Tuples();

            [...validVec2s, ...validTuples].forEach((vec2Like) => {
                expect(isVec2Like(vec2Like)).toBe(true);
            });
        });

        test('should return false for invalid Vec2 and Vec2Tuple', () => {
            const invalidVec2s = createInvalidVec2Objects().filter(
                (item) =>
                    !Array.isArray(item)
            );
            const invalidTuples = createInvalidVec2Tuples().filter(
                (item) =>
                    !(
                        item !== null &&
                        typeof item === 'object' &&
                        'x' in (item as any) &&
                        'y' in (item as any)
                    )
            );

            const invalidBoth = [...invalidVec2s, ...invalidTuples].filter(
                (item) => !isVec2(item) && !isVec2Tuple(item)
            );

            invalidBoth.forEach((nonVec2Like) => {
                expect(isVec2Like(nonVec2Like)).toBe(false);
            });
        });
    });
});
