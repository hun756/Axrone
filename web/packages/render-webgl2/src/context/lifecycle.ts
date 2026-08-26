import type { ContextEvent, Unsubscribe } from './types';

export type LifecycleListener = (event: ContextEvent) => void;

export class ContextLifecycle {
    readonly #canvas: HTMLCanvasElement;
    readonly #gl: WebGL2RenderingContext;
    readonly #listeners = new Set<LifecycleListener>();
    #isLost = false;
    #isDisposed = false;
    readonly #onLostBound: (event: Event) => void;
    readonly #onRestoredBound: (event: Event) => void;

    constructor(gl: WebGL2RenderingContext, canvas: HTMLCanvasElement) {
        this.#gl = gl;
        this.#canvas = canvas;
        this.#onLostBound = (event: Event) => this.#handleLost(event);
        this.#onRestoredBound = (event: Event) => this.#handleRestored(event);
        try {
            (this.#canvas as unknown as { addEventListener?: (type: string, listener: EventListener) => void })?.addEventListener?.(
                'webglcontextlost',
                this.#onLostBound as EventListener
            );
            (this.#canvas as unknown as { addEventListener?: (type: string, listener: EventListener) => void })?.addEventListener?.(
                'webglcontextrestored',
                this.#onRestoredBound as EventListener
            );
        } catch { /* best-effort */ }
    }

    public get isLost(): boolean {
        return this.#isLost;
    }

    public get isDisposed(): boolean {
        return this.#isDisposed;
    }

    public syncLostState(): boolean {
        try {
            const lost = this.#gl.isContextLost();
            this.#isLost = lost;
            return lost;
        } catch { // best-effort
            return this.#isLost;
        }
    }

    public subscribe(listener: LifecycleListener): Unsubscribe {
        this.#listeners.add(listener);
        return () => {
            this.#listeners.delete(listener);
        };
    }

    public onLost(listener: (event: Event) => void): Unsubscribe {
        const wrapped: LifecycleListener = (e) => {
            if (e.kind === 'lost') listener(e.event);
        };
        this.#listeners.add(wrapped);
        return () => this.#listeners.delete(wrapped);
    }

    public onRestored(listener: (event: Event) => void): Unsubscribe {
        const wrapped: LifecycleListener = (e) => {
            if (e.kind === 'restored') listener(e.event);
        };
        this.#listeners.add(wrapped);
        return () => this.#listeners.delete(wrapped);
    }

    public onDisposed(listener: () => void): Unsubscribe {
        const wrapped: LifecycleListener = (e) => {
            if (e.kind === 'disposed') listener();
        };
        this.#listeners.add(wrapped);
        return () => this.#listeners.delete(wrapped);
    }

    public notifyRestored(event: Event): void {
        this.#isLost = false;
        this.#emit({ kind: 'restored', event });
    }

    public dispose(): void {
        if (this.#isDisposed) return;
        this.#isDisposed = true;
        try {
            (this.#canvas as unknown as { removeEventListener?: (type: string, listener: EventListener) => void })?.removeEventListener?.(
                'webglcontextlost',
                this.#onLostBound as EventListener
            );
            (this.#canvas as unknown as { removeEventListener?: (type: string, listener: EventListener) => void })?.removeEventListener?.(
                'webglcontextrestored',
                this.#onRestoredBound as EventListener
            );
        } catch { // best-effort
        }
        this.#emit({ kind: 'disposed' });
        this.#listeners.clear();
    }

    #handleLost = (event: Event): void => {
        try {
            event.preventDefault();
        } catch { // best-effort
        }
        this.#isLost = true;
        this.#emit({ kind: 'lost', event });
    };

    #handleRestored = (event: Event): void => {
        this.#isLost = false;
        this.#emit({ kind: 'restored', event });
    };

    #emit = (event: ContextEvent): void => {
        for (const listener of [...this.#listeners]) {
            try {
                listener(event);
            } catch { // best-effort
            }
        }
    };
}

export const createLifecycle = (gl: WebGL2RenderingContext, canvas: HTMLCanvasElement): ContextLifecycle =>
    new ContextLifecycle(gl, canvas);
