/**
 * @internal
 * DIAG gate helper — only emits console.warn when the global
 * `__AXRONE_DIAG__` flag is explicitly true. This keeps diagnostic
 * logs out of production builds while allowing dev / e2e harnesses
 * to opt in.
 */
export function diagWarn(message: string): void {
    if (globalThis.__AXRONE_DIAG__ === true) {
        // eslint-disable-next-line no-console
        console.warn(message);
    }
}
