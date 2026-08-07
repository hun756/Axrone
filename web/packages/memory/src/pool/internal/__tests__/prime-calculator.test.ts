import { describe, expect, it } from 'vitest';
import { nextPrime } from '../prime-calculator';

describe('PrimeCalculator', () => {
    describe('nextPrime()', () => {
        it('returns 2 for 0', () => {
            expect(nextPrime(0)).toBe(2);
        });

        it('returns 2 for 1', () => {
            expect(nextPrime(1)).toBe(2);
        });

        it('returns 2 for 2', () => {
            expect(nextPrime(2)).toBe(2);
        });

        it('returns 3 for 3', () => {
            expect(nextPrime(3)).toBe(3);
        });

        it('returns 5 for 4', () => {
            expect(nextPrime(4)).toBe(5);
        });

        it('returns 5 for 5', () => {
            expect(nextPrime(5)).toBe(5);
        });

        it('returns 7 for 6', () => {
            expect(nextPrime(6)).toBe(7);
        });

        it('returns 11 for 10', () => {
            expect(nextPrime(10)).toBe(11);
        });

        it('returns 13 for 12', () => {
            expect(nextPrime(12)).toBe(13);
        });

        it('returns 17 for 14', () => {
            expect(nextPrime(14)).toBe(17);
        });

        it('returns 23 for 20', () => {
            expect(nextPrime(20)).toBe(23);
        });

        it('returns 97 for 90', () => {
            expect(nextPrime(90)).toBe(97);
        });

        it('returns 101 for 100', () => {
            expect(nextPrime(100)).toBe(101);
        });

        it('handles large numbers', () => {
            expect(nextPrime(1000)).toBe(1009);
        });

        it('returns prime for negative input', () => {
            expect(nextPrime(-5)).toBe(2);
        });
    });
});
