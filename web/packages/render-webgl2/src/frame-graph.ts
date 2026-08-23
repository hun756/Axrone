import type { RenderPassKind, ResolvedRenderPass } from '@axrone/render-core/types';
import { GLContextError } from './context/errors';

export type FrameGraphNodeId = string & { readonly __brand: 'FrameGraphNode' };

export interface FrameGraphPassNode {
    readonly id: FrameGraphNodeId;
    readonly pass: ResolvedRenderPass;
    readonly dependencies: ReadonlySet<FrameGraphNodeId>;
    readonly produces: ReadonlySet<string>;
    readonly consumes: ReadonlySet<string>;
}

export interface FrameGraphBuildOptions {
    readonly enableCulling?: boolean;
}

export class FrameGraph {
    private readonly nodes = new Map<FrameGraphNodeId, FrameGraphPassNode>();
    private readonly resourceWriters = new Map<string, FrameGraphNodeId>();
    private readonly resourceReaders = new Map<string, Set<FrameGraphNodeId>>();

    addPass(pass: ResolvedRenderPass, options?: { dependsOn?: readonly FrameGraphNodeId[] }): FrameGraphNodeId {
        const id = `${pass.kind}:${String(pass.name)}:${this.nodes.size}` as FrameGraphNodeId;
        const produces = this.extractProduces(pass);
        const consumes = this.extractConsumes(pass);
        const dependencies = new Set<FrameGraphNodeId>(options?.dependsOn ?? []);

        for (const r of consumes) {
            const writer = this.resourceWriters.get(r);
            if (writer && writer !== id) dependencies.add(writer);
        }

        const node: FrameGraphPassNode = {
            id,
            pass,
            dependencies: Object.freeze(dependencies) as ReadonlySet<FrameGraphNodeId>,
            produces: Object.freeze(produces) as ReadonlySet<string>,
            consumes: Object.freeze(consumes) as ReadonlySet<string>,
        };
        this.nodes.set(id, node);
        for (const r of produces) this.resourceWriters.set(r, id);
        for (const r of consumes) {
            let s = this.resourceReaders.get(r);
            if (!s) {
                s = new Set();
                this.resourceReaders.set(r, s);
            }
            s.add(id);
        }
        return id;
    }

    build(options?: FrameGraphBuildOptions): readonly ResolvedRenderPass[] {
        const enableCulling = options?.enableCulling ?? true;
        const needed = new Set<FrameGraphNodeId>();
        if (enableCulling) {
            const sinks = this.findSinks();
            const stack = [...sinks];
            while (stack.length > 0) {
                const id = stack.pop()!;
                if (needed.has(id)) continue;
                needed.add(id);
                const node = this.nodes.get(id);
                if (!node) continue;
                for (const dep of node.dependencies) stack.push(dep);
            }
        } else {
            for (const id of this.nodes.keys()) needed.add(id);
        }

        const inDegree = new Map<FrameGraphNodeId, number>();
        const adj = new Map<FrameGraphNodeId, FrameGraphNodeId[]>();
        for (const id of needed) {
            inDegree.set(id, 0);
            adj.set(id, []);
        }
        for (const id of needed) {
            const node = this.nodes.get(id)!;
            for (const dep of node.dependencies) {
                if (!needed.has(dep)) continue;
                adj.get(dep)!.push(id);
                inDegree.set(id, (inDegree.get(id) ?? 0) + 1);
            }
        }

        const kindPriority: Record<string, number> = {
            'depth-prepass': 0,
            shadow: 1,
            opaque: 2,
            'reflection-probe': 3,
            'global-illumination': 4,
            volumetric: 5,
            skybox: 6,
            transparent: 7,
            'post-process': 8,
            tonemap: 9,
            present: 10,
            'light-bake': 11,
        };

        const queue: FrameGraphNodeId[] = [];
        for (const [id, deg] of inDegree) if (deg === 0) queue.push(id);

        const sorted: FrameGraphNodeId[] = [];
        while (queue.length > 0) {
            queue.sort((a, b) => {
                const pa = kindPriority[this.nodes.get(a)!.pass.kind] ?? 99;
                const pb = kindPriority[this.nodes.get(b)!.pass.kind] ?? 99;
                return pa - pb;
            });
            const id = queue.shift()!;
            sorted.push(id);
            for (const next of adj.get(id) ?? []) {
                const d = (inDegree.get(next) ?? 1) - 1;
                inDegree.set(next, d);
                if (d === 0) queue.push(next);
            }
        }

        if (sorted.length !== needed.size) {
            throw new GLContextError('INVALID_OPERATION', 'en', { reason: 'FrameGraph: cycle detected in pass dependencies' });
        }

        return sorted.map((id) => this.nodes.get(id)!.pass);
    }

    getResourceLifetime(resource: string): { first: FrameGraphNodeId | null; last: FrameGraphNodeId | null } | null {
        const first = this.resourceWriters.get(resource) ?? null;
        const readers = this.resourceReaders.get(resource);
        if (!readers || readers.size === 0) return first ? { first, last: first } : null;
        let last: FrameGraphNodeId | null = null;
        for (const id of readers) last = id;
        return { first, last };
    }

    getTransients(): string[] {
        const out: string[] = [];
        for (const [r, writer] of this.resourceWriters) {
            const readers = this.resourceReaders.get(r);
            if (!readers || readers.size === 0) continue;
            if (this.nodes.has(writer)) out.push(r);
        }
        return out;
    }

    getBarriers(): Array<{ resource: string; before: FrameGraphNodeId; after: FrameGraphNodeId }> {
        const barriers: Array<{ resource: string; before: FrameGraphNodeId; after: FrameGraphNodeId }> = [];
        for (const [resource, readers] of this.resourceReaders) {
            const writer = this.resourceWriters.get(resource);
            if (!writer) continue;
            for (const reader of readers) {
                if (writer !== reader) barriers.push({ resource, before: writer, after: reader });
            }
        }
        return barriers;
    }

    clear(): void {
        this.nodes.clear();
        this.resourceWriters.clear();
        this.resourceReaders.clear();
    }

    get size(): number {
        return this.nodes.size;
    }

    private findSinks(): FrameGraphNodeId[] {
        const hasOutgoing = new Set<FrameGraphNodeId>();
        for (const node of this.nodes.values()) {
            for (const dep of node.dependencies) hasOutgoing.add(dep);
        }
        const sinks: FrameGraphNodeId[] = [];
        for (const id of this.nodes.keys()) {
            if (!hasOutgoing.has(id)) sinks.push(id);
        }
        if (sinks.length === 0) return [...this.nodes.keys()];
        return sinks;
    }

    private extractProduces(pass: ResolvedRenderPass): Set<string> {
        const s = new Set<string>();
        if (pass.target) s.add(String(pass.target));
        const meta = pass.metadata as Record<string, unknown>;
        for (const k of ['target', 'color', 'depth', 'atlas', 'froxelGrid'] as const) {
            const v = meta[k];
            if (typeof v === 'string') s.add(v);
        }
        return s;
    }

    private extractConsumes(pass: ResolvedRenderPass): Set<string> {
        const s = new Set<string>();
        if (Array.isArray(pass.inputs)) {
            for (const inp of pass.inputs as unknown[]) {
                if (typeof inp === 'string') s.add(inp);
            }
        }
        const meta = pass.metadata as Record<string, unknown>;
        for (const k of ['source', 'exposureHistorySource'] as const) {
            const v = meta[k];
            if (typeof v === 'string') s.add(v);
        }
        return s;
    }
}

export const createFrameGraph = (): FrameGraph => new FrameGraph();
