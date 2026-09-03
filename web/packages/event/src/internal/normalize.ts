/**
 * Numeric option normalization shared by `EventEmitter` and `EventScheduler`.
 *
 * Centralizing these guards means both modules agree on the meaning of
 * "non-finite", "negative", and "fractional" inputs, and prevents the two
 * files from drifting their definitions of edge cases (e.g. what does
 * `Infinity` mean for a buffer size, what does a negative `retryDelay`
 * clamp to).
 *
 * Conventions used by every helper below:
 * - `undefined`, `NaN`, and other non-numbers are invalid and resolve to
 *   `fallback`, so callers always receive a usable finite number
 * - floats are truncated with `Math.trunc` rather than rounded
 * - none of the helpers throw; the caller decides whether a bad value is
 *   a hard error (e.g. `EventScheduler` re-validates finite `Infinity` for
 *   `concurrencyLimit` after normalization)
 *
 * Functions that intentionally accept `Infinity` (max-listener cap,
 * concurrency limit) live in this file as wrappers so the "honor Infinity"
 * policy is documented in exactly one place.
 */

export type BufferOverflowPolicy = 'throw' | 'drop-oldest' | 'drop-newest';

/**
 * Normalize a positive integer count (>= 1).
 *
 * `Infinity` is treated as invalid: a count of `Infinity` is almost always
 * a caller bug, and the callers that really want "unlimited" should use
 * the `normalizeConcurrency` / `normalizeMaxListeners` variants below,
 * which carry that semantic explicitly.
 */
export function normalizePositiveInteger(value: number | undefined, fallback: number): number {
    if (typeof value !== 'number' || !Number.isFinite(value)) {
        return fallback;
    }
    return Math.max(1, Math.trunc(value));
}

/**
 * Normalize a non-negative integer count (>= 0).
 *
 * Same `Infinity` policy as {@link normalizePositiveInteger}.
 */
export function normalizeNonNegativeInteger(value: number | undefined, fallback: number): number {
    if (typeof value !== 'number' || !Number.isFinite(value)) {
        return fallback;
    }
    return Math.max(0, Math.trunc(value));
}

/**
 * Normalize a duration / interval in milliseconds (>= 0).
 *
 * `Infinity` collapses to `0`, which the GC timer in `EventEmitter`
 * interprets as "do not start" (see the `gcIntervalMs > 0` guard in the
 * `EventEmitter` constructor). This matches the historical behavior of
 * the scheduler-side `normalizeDuration`.
 */
export function normalizeDuration(value: number | undefined, fallback: number): number {
    if (value === Infinity) {
        return 0;
    }
    if (typeof value !== 'number' || !Number.isFinite(value)) {
        return fallback;
    }
    return Math.max(0, Math.trunc(value));
}

/**
 * Normalize a buffer-overflow policy. Unknown values fall back rather
 * than throwing, so a stray value at runtime cannot break an emitter
 * that is already in a paused / draining state.
 */
export function normalizeBufferOverflow(
    value: unknown,
    fallback: BufferOverflowPolicy
): BufferOverflowPolicy {
    if (value === 'throw' || value === 'drop-oldest' || value === 'drop-newest') {
        return value;
    }
    return fallback;
}

/**
 * Normalize a `maxListeners` option. `Infinity` is honored: it means
 * "never warn and disable the listener-count cap entirely".
 */
export function normalizeMaxListeners(value: number | undefined, fallback: number): number {
    if (value === Infinity) {
        return Infinity;
    }
    return normalizeNonNegativeInteger(value, fallback);
}

/**
 * Normalize a `concurrencyLimit` option. `Infinity` is honored: it means
 * "unlimited" and lets the fast path in `EventEmitter.emit()` skip the
 * per-listener scheduler overhead.
 *
 * Also treats `value === undefined && fallback === Infinity` as
 * "unlimited", matching the historical local helper in `EventEmitter`.
 */
export function normalizeConcurrency(value: number | undefined, fallback: number): number {
    if (value === Infinity || (value === undefined && fallback === Infinity)) {
        return value ?? fallback;
    }
    return normalizePositiveInteger(value, fallback);
}
