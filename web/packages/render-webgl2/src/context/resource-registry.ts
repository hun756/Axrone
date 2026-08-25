import type { IGLContext } from './types';

export interface RecoverableResource {
    readonly id: number | string;
    handleContextLost(): void;
    handleContextRestored(ctx: IGLContext): void;
}

export type ResourceKind = 'buffer' | 'texture' | 'framebuffer' | 'renderbuffer' | 'program' | 'shader' | 'vao' | 'query' | 'custom';

interface RegisteredResource {
    readonly resource: RecoverableResource;
    readonly kind: ResourceKind;
    readonly priority: number;
    readonly sequence: number;
}

export class ResourceRegistry {
    private readonly resources = new Map<number | string, RegisteredResource>();
    private readonly byKind = new Map<ResourceKind, Set<number | string>>();
    private sequence = 0;
    private readonly ctx: IGLContext;
    private readonly unsubscribes: Array<() => void> = [];

    constructor(ctx: IGLContext) {
        this.ctx = ctx;
        this.unsubscribes.push(ctx.onLost(() => this.notifyLost()));
        this.unsubscribes.push(ctx.onRestored(() => this.notifyRestored()));
    }

    register(resource: RecoverableResource, kind: ResourceKind, priority = 0): () => void {
        const key = resource.id;
        const existing = this.resources.get(key);
        if (existing) this.unregister(key);
        this.resources.set(key, {
            resource,
            kind,
            priority,
            sequence: this.sequence++,
        });
        let set = this.byKind.get(kind);
        if (!set) {
            set = new Set();
            this.byKind.set(kind, set);
        }
        set.add(key);
        return () => this.unregister(key);
    }

    unregister(id: number | string): boolean {
        const entry = this.resources.get(id);
        if (!entry) return false;
        this.resources.delete(id);
        this.byKind.get(entry.kind)?.delete(id);
        return true;
    }

    get size(): number {
        return this.resources.size;
    }

    countByKind(kind: ResourceKind): number {
        return this.byKind.get(kind)?.size ?? 0;
    }

    private notifyLost(): void {
        for (const { resource } of this.resources.values()) {
            try {
                resource.handleContextLost();
            } catch {
                // best-effort: isolate resource failures
            }
        }
    }

    private notifyRestored(): void {
        const ordered = [...this.resources.values()].sort((a, b) =>
            a.priority === b.priority ? a.sequence - b.sequence : a.priority - b.priority
        );
        for (const { resource } of ordered) {
            try {
                resource.handleContextRestored(this.ctx);
            } catch {
                // best-effort
            }
        }
        this.ctx.state.reset();
    }

    invalidateWithoutRestore(): void {
        for (const { resource } of this.resources.values()) {
            try {
                resource.handleContextLost();
            } catch {
                // best-effort
            }
        }
    }

    dispose(): void {
        for (const fn of this.unsubscribes.splice(0)) {
            try {
                fn();
            } catch {
                // best-effort
            }
        }
        this.resources.clear();
        this.byKind.clear();
    }
}

export const createResourceRegistry = (ctx: IGLContext): ResourceRegistry =>
    new ResourceRegistry(ctx);
