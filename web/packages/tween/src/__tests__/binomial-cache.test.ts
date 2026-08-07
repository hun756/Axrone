import { describe, expect, it } from 'vitest';
import { BINOMIAL } from '../binomial-cache';

describe('binomial-cache', () => {
    describe('Structure', () => {
        it('BINOMIAL[0] = [1]', () => {
            expect(BINOMIAL[0]).toEqual([1]);
        });

        it('BINOMIAL[1] = [1, 1]', () => {
            expect(BINOMIAL[1]).toEqual([1, 1]);
        });

        it('BINOMIAL[2] = [1, 2, 1]', () => {
            expect(BINOMIAL[2]).toEqual([1, 2, 1]);
        });
    });

    describe('Symmetry', () => {
        it('BINOMIAL[n][i] === BINOMIAL[n][n-i] for all rows', () => {
            for (let n = 0; n < BINOMIAL.length; n++) {
                const row = BINOMIAL[n];
                for (let i = 0; i < row.length; i++) {
                    expect(row[i]).toBe(row[row.length - 1 - i]);
                }
            }
        });
    });

    describe('Known values', () => {
        it('BINOMIAL[20][10] = 184756', () => {
            expect(BINOMIAL[20][10]).toBe(184756);
        });

        it('BINOMIAL[10][5] = 252', () => {
            expect(BINOMIAL[10][5]).toBe(252);
        });

        it('BINOMIAL[5][2] = 10', () => {
            expect(BINOMIAL[5][2]).toBe(10);
        });
    });

    describe('Row sums', () => {
        it('each row sums to 2^n', () => {
            for (let n = 0; n < BINOMIAL.length; n++) {
                const row = BINOMIAL[n];
                const sum = row.reduce((acc, val) => acc + val, 0);
                expect(sum).toBe(Math.pow(2, n));
            }
        });
    });

    describe('Row lengths', () => {
        it('row n has n+1 elements', () => {
            for (let n = 0; n < BINOMIAL.length; n++) {
                expect(BINOMIAL[n].length).toBe(n + 1);
            }
        });
    });
});
