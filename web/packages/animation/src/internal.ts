export const freezeTuple2 = <T>(a: T, b: T): readonly [T, T] =>
    Object.freeze([a, b]) as readonly [T, T];

export const freezeTuple3 = <T>(a: T, b: T, c: T): readonly [T, T, T] =>
    Object.freeze([a, b, c]) as readonly [T, T, T];

export const freezeTuple4 = <T>(a: T, b: T, c: T, d: T): readonly [T, T, T, T] =>
    Object.freeze([a, b, c, d]) as readonly [T, T, T, T];

export const isFiniteNumber = (value: unknown): value is number =>
    typeof value === 'number' && Number.isFinite(value);

const EMPTY_SPREAD: Readonly<Record<string, never>> = Object.freeze({}) as Readonly<Record<string, never>>;

export const spreadIfFinite = (key: string, value: unknown): Readonly<Record<string, unknown>> =>
    isFiniteNumber(value) ? Object.freeze({ [key]: value }) : EMPTY_SPREAD;

export const spreadIfNonEmptyString = (key: string, value: unknown): Readonly<Record<string, unknown>> =>
    typeof value === 'string' && value.length > 0 ? Object.freeze({ [key]: value }) : EMPTY_SPREAD;
