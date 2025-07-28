import type { ComponentRegistry, ArchetypeId, ArchetypeSignature, BitMask, Entity } from './core';
import type { ComponentPool } from './component';

export interface Archetype<R extends ComponentRegistry> {
    readonly id: ArchetypeId;
    readonly signature: ArchetypeSignature;
    readonly mask: BitMask;
    readonly entities: Entity[];
    readonly components: Map<string, ComponentPool<any>>;
    readonly edges: Map<string, ArchetypeId>;
    entityCount: number;
}

export interface QueryCache {
    readonly queries: Map<string, ArchetypeId[]>;
    readonly bitQueries: Map<BitMask, ArchetypeId[]>;
    invalidate(): void;
}
