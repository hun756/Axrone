export const freezeTuple2 = <T>(a: T, b: T): readonly [T, T] =>
    Object.freeze([a, b]) as readonly [T, T];

export const freezeTuple3 = <T>(a: T, b: T, c: T): readonly [T, T, T] =>
    Object.freeze([a, b, c]) as readonly [T, T, T];

export const freezeTuple4 = <T>(a: T, b: T, c: T, d: T): readonly [T, T, T, T] =>
    Object.freeze([a, b, c, d]) as readonly [T, T, T, T];
