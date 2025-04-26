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

