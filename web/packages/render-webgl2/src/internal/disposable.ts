import { createDisposedError } from '../errors';

/**
 * Shared disposal state tracker. Eliminates the copy-pasted _isDisposed / isDisposed / dispose
 * boilerplate across 40+ GPU resource classes.
 */
export class DisposalTracker {
    private _disposed = false;

    public get isDisposed(): boolean {
        return this._disposed;
    }

    /** Mark as disposed. Returns true if this was the first call (transition from alive -> disposed). */
    public markDisposed(): boolean {
        if (this._disposed) return false;
        this._disposed = true;
        return true;
    }

    /** Throw if disposed. Uses the provided error factory for consistent error messages. */
    public assertAlive(errorFactory: (resourceName: string) => Error, resourceName: string): void {
        if (this._disposed) throw errorFactory(resourceName);
    }
}

/**
 * Convenience helper for classes whose disposed-error matches the shared createDisposedError.
 * Classes that throw domain-specific error types (FramebufferError, TextureError, etc.)
 * should call tracker.assertAlive(customFactory, name) instead.
 *
 * Usage:
 *   private readonly _disposal = new DisposalTracker();
 *   public get isDisposed(): boolean { return this._disposal.isDisposed; }
 *   dispose(): void { if (!this._disposal.markDisposed()) return; /* cleanup *\/ }
 */
export const assertAlive = (tracker: DisposalTracker, resourceName: string): void =>
    tracker.assertAlive(createDisposedError, resourceName);
