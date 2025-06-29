import { isVec2 } from '../../vec2_legacy';
import {
    createValidVec2Objects,
    createInvalidVec2Objects,
    createValidVec2Tuples,
    createInvalidVec2Tuples,
} from '../type-test-helper';

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
});
