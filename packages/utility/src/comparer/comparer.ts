export type CompreResult = -1 | 0 | 1;

export type Compreable<T extends string> = number & { readonly _brand: T };

export interface Comparer<T> {
    compare(a: T, b: T): CompreResult;
}

export interface EqualityComparer<T> {
    equals(a: T, b: T): boolean;
    hash(obj: T): number;
}

export interface Equatable {
    equals(other: unknown): boolean;
    hash(): number;
}

export type KeySelector<T, K> = (item: T) => K;
export type PropertyPath<T> = (keyof T & string) | readonly (keyof T & string)[];

export type ExtractPropertyType<T, P extends PropertyPath<T>> = P extends readonly []
    ? T
    : P extends readonly [infer F, ...infer R]
      ? F extends keyof T
          ? R extends PropertyPath<T[F]>
              ? ExtractPropertyType<T[F], R>
              : never
          : never
      : P extends keyof T
        ? T[P]
        : never;

export type DeepPartial<T> = T extends object
    ? {
          [P in keyof T]?: DeepPartial<T[P]>;
      }
    : T;

export type KeysOfType<T, V> = {
    [K in keyof T]-?: T[K] extends V ? K : never;
}[keyof T];

export type ComparerOptions = Readonly<{
    nullFirst?: boolean;
    descending?: boolean;
    ignoreCase?: boolean;
    locale?: string;
    precision?: number;
    timezone?: string;
}>;

export type EqualityComparerOptions = Readonly<{
    ignoreCase?: boolean;
    deep?: boolean;
    strict?: boolean;
    customize?: (objValue: unknown, otherValue: unknown) => boolean;
}>;

export class CompareError extends Error {
    constructor(message: string) {
        super(message);
        this.name = 'CompareError';
        Object.setPrototypeOf(this, CompareError.prototype);
    }
}

export class InvalidOperationError extends Error {
    constructor(message: string) {
        super(message);
        this.name = 'InvalidOperationError';
        Object.setPrototypeOf(this, InvalidOperationError.prototype);
    }
}

// Hash Calculation
const FNV_PRIME = 16777619;
const FNV_OFFSET_BASIS = 2166136261;

function fnvHash(data: string): number {
    let hash = FNV_OFFSET_BASIS;

    for (let i = 0; i < data.length; i++) {
        hash ^= data.charCodeAt(i);
        hash = Math.imul(hash, FNV_PRIME);
    }

    return hash >>> 0;
}

export function isEquatable(obj: unknown): obj is Equatable {
    return (
        obj !== null &&
        typeof obj === 'object' &&
        'equals' in obj &&
        typeof (obj as any).equals === 'function' &&
        'getHashCode' in obj &&
        typeof (obj as any).getHashCode === 'function'
    );
}

export function isComparer<T>(value: unknown): value is Comparer<T> {
    return (
        value !== null &&
        typeof value === 'object' &&
        'compare' in value &&
        typeof (value as any).compare === 'function'
    );
}

export function isEqualityComparer<T>(value: unknown): value is EqualityComparer<T> {
    return (
        value !== null &&
        typeof value === 'object' &&
        'equals' in value &&
        typeof (value as any).equals === 'function' &&
        'hash' in value &&
        typeof (value as any).hash === 'function'
    );
}
