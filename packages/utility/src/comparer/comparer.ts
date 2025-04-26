/**
 * Defines a generic comparer interface for comparing two values.
 * @template T - The type of the values to be compared.
 */
export interface Comparer<T> {
    /**
     * Compares two values.
     * @param a - The first value.
     * @param b - The second value.
     * @returns A negative number if a < b, zero if a = b, a positive number if a > b.
     */
    compare(a: T, b: T): number;
}

/**
 * Compares two numbers in either ascending or descending order.
 */
export class NumberComparer implements Comparer<number> {
    /**
     * @param ascending - Determines the sort order. True for ascending, false for descending.
     */
    constructor(private ascending: boolean = true) { }

    /**
     * Compares two numbers.
     * @param a - The first number.
     * @param b - The second number.
     * @returns The difference between the two numbers based on the sort order.
     */
    compare(a: number, b: number): number {
        return this.ascending ? a - b : b - a;
    }
}

/**
 * Compares two strings with optional case sensitivity.
 */
export class StringComparer implements Comparer<string> {
    /**
     * @param caseSensitive - Determines if the comparison is case-sensitive.
     */
    constructor(private caseSensitive: boolean = true) { }

    /**
     * Compares two strings.
     * @param a - The first string.
     * @param b - The second string.
     * @returns A negative number if a < b, zero if a = b, a positive number if a > b.
     */
    compare(a: string, b: string): number {
        if (this.caseSensitive) {
            return a.localeCompare(b);
        } else {
            return a.toLowerCase().localeCompare(b.toLowerCase());
        }
    }
}
