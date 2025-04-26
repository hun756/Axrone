import {
    CompareResult,
    Comparer,
    ComparerOptions,
    CompareError,
    InvalidOperationError,
    isComparer,
} from '@axrone/utility';

class NumberComparer implements Comparer<number> {
    constructor(private options?: ComparerOptions) {}

    compare(a: number, b: number): CompareResult {
        if (isNaN(a) && isNaN(b)) return 0;
        if (isNaN(a)) return this.options?.nullFirst ? -1 : 1;
        if (isNaN(b)) return this.options?.nullFirst ? 1 : -1;

        if (a === null && b === null) return 0;
        if (a === null) return this.options?.nullFirst ? -1 : 1;
        if (b === null) return this.options?.nullFirst ? 1 : -1;

        const multiplier = this.options?.descending ? -1 : 1;

        if (this.options?.precision !== undefined) {
            const factor = Math.pow(10, this.options.precision);
            a = Math.round(a * factor) / factor;
            b = Math.round(b * factor) / factor;
        }

        if (a < b) return (-1 * multiplier) as CompareResult;
        if (a > b) return (1 * multiplier) as CompareResult;
        return 0;
    }
}

class StringComparer implements Comparer<string> {
    constructor(private options?: ComparerOptions) {}

    compare(a: string, b: string): CompareResult {
        if (a === null && b === null) return 0;
        if (a === null) return this.options?.nullFirst ? -1 : 1;
        if (b === null) return this.options?.nullFirst ? 1 : -1;

        let strA = a;
        let strB = b;

        if (this.options?.ignoreCase) {
            strA = strA.toLowerCase();
            strB = strB.toLowerCase();
        }

        if (this.options?.locale) {
            const multiplier = this.options?.descending ? -1 : 1;
            const result = strA.localeCompare(strB, this.options.locale);
            if (result < 0) return (-1 * multiplier) as CompareResult;
            if (result > 0) return (1 * multiplier) as CompareResult;
            return 0;
        }

        const multiplier = this.options?.descending ? -1 : 1;
        if (strA < strB) return (-1 * multiplier) as CompareResult;
        if (strA > strB) return (1 * multiplier) as CompareResult;
        return 0;
    }
}

class DateComparer implements Comparer<Date> {
    constructor(private options?: ComparerOptions) {}

    compare(a: Date, b: Date): CompareResult {
        if (a === null && b === null) return 0;
        if (a === null) return this.options?.nullFirst ? -1 : 1;
        if (b === null) return this.options?.nullFirst ? 1 : -1;

        const aTime = a.getTime();
        const bTime = b.getTime();

        if (isNaN(aTime) && isNaN(bTime)) return 0;
        if (isNaN(aTime)) return this.options?.nullFirst ? -1 : 1;
        if (isNaN(bTime)) return this.options?.nullFirst ? 1 : -1;

        let timeA = aTime;
        let timeB = bTime;

        if (this.options?.timezone) {
            if (this.options.timezone === 'UTC') {
                timeA = Date.UTC(
                    a.getUTCFullYear(),
                    a.getUTCMonth(),
                    a.getUTCDate(),
                    a.getUTCHours(),
                    a.getUTCMinutes(),
                    a.getUTCSeconds(),
                    a.getUTCMilliseconds()
                );

                timeB = Date.UTC(
                    b.getUTCFullYear(),
                    b.getUTCMonth(),
                    b.getUTCDate(),
                    b.getUTCHours(),
                    b.getUTCMinutes(),
                    b.getUTCSeconds(),
                    b.getUTCMilliseconds()
                );
            }
        }

        const multiplier = this.options?.descending ? -1 : 1;

        if (timeA < timeB) return (-1 * multiplier) as CompareResult;
        if (timeA > timeB) return (1 * multiplier) as CompareResult;
        return 0;
    }
}

class GenericComparer<T> implements Comparer<T> {
    constructor(private keySelector: (item: T) => number | string | Date) {}

    compare(a: T, b: T): CompareResult {
        const keyA = this.keySelector(a);
        const keyB = this.keySelector(b);

        if (typeof keyA === 'number' && typeof keyB === 'number') {
            return new NumberComparer().compare(keyA, keyB);
        }

        if (typeof keyA === 'string' && typeof keyB === 'string') {
            return new StringComparer().compare(keyA, keyB);
        }

        if (keyA instanceof Date && keyB instanceof Date) {
            return new DateComparer().compare(keyA, keyB);
        }

        throw new CompareError('Unsupported key type for comparison');
    }
}

class CompositeComparer<T> implements Comparer<T> {
    private comparers: Array<Comparer<T>>;

    constructor(...comparers: Array<Comparer<T>>) {
        if (comparers.length === 0) {
            throw new InvalidOperationError('At least one comparer must be provided');
        }
        this.comparers = comparers;
    }

    compare(a: T, b: T): CompareResult {
        for (const comparer of this.comparers) {
            const result = comparer.compare(a, b);
            if (result !== 0) {
                return result;
            }
        }
        return 0;
    }
}

describe('Comparer Interface Implementation Tests', () => {
    describe('NumberComparer', () => {
        test('basic number comparison', () => {
            const comparer = new NumberComparer();

            expect(comparer.compare(1, 2)).toBe(-1);
            expect(comparer.compare(2, 1)).toBe(1);
            expect(comparer.compare(1, 1)).toBe(0);
        });

        test('descending order option', () => {
            const comparer = new NumberComparer({ descending: true });

            expect(comparer.compare(1, 2)).toBe(1);
            expect(comparer.compare(2, 1)).toBe(-1);
            expect(comparer.compare(1, 1)).toBe(0);
        });

        test('precision option', () => {
            const comparer = new NumberComparer({ precision: 2 });

            // Todo: Fix the precision test cases
            expect(comparer.compare(1.234, 1.236)).toBe(0);
            expect(comparer.compare(1.23, 1.26)).toBe(-1);
            expect(comparer.compare(1.26, 1.23)).toBe(1);
        });

        test('nullFirst option', () => {
            const defaultComparer = new NumberComparer();
            const nullFirstComparer = new NumberComparer({ nullFirst: true });

            expect(defaultComparer.compare(NaN, 1)).toBe(1);
            expect(nullFirstComparer.compare(NaN, 1)).toBe(-1);

            expect(defaultComparer.compare(1, NaN)).toBe(-1);
            expect(nullFirstComparer.compare(1, NaN)).toBe(1);
        });
    });

    describe('StringComparer', () => {
        test('basic string comparison', () => {
            const comparer = new StringComparer();

            expect(comparer.compare('a', 'b')).toBe(-1);
            expect(comparer.compare('b', 'a')).toBe(1);
            expect(comparer.compare('a', 'a')).toBe(0);
        });

        test('ignoreCase option', () => {
            const caseSensitiveComparer = new StringComparer();
            const caseInsensitiveComparer = new StringComparer({ ignoreCase: true });

            expect(caseSensitiveComparer.compare('a', 'A')).toBe(1);
            expect(caseInsensitiveComparer.compare('a', 'A')).toBe(0);

            expect(caseSensitiveComparer.compare('A', 'a')).toBe(-1);
            expect(caseInsensitiveComparer.compare('A', 'a')).toBe(0);
        });

        test('locale option', () => {
            const defaultComparer = new StringComparer();
            const localeComparer = new StringComparer({ locale: 'tr' });

            expect(defaultComparer.compare('i', 'İ')).not.toBe(0);

            expect(localeComparer.compare('i', 'İ')).not.toBe(0);

            const turkishCaseInsensitiveComparer = new StringComparer({
                locale: 'tr',
                ignoreCase: true,
            });
        });

        test('descending option', () => {
            const comparer = new StringComparer({ descending: true });

            expect(comparer.compare('a', 'b')).toBe(1);
            expect(comparer.compare('b', 'a')).toBe(-1);
            expect(comparer.compare('a', 'a')).toBe(0);
        });
    });
});
