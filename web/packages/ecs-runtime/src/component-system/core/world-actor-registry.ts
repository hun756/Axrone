import { ActorRegistry } from '../../support/actor-registry';
import type { Entity } from '../types/core';
import type { Actor } from './actor';

export class WorldActorRegistry extends ActorRegistry<Entity, Actor> {
    private readonly _tagIndex = new Map<string, Set<Entity>>();
    private readonly _layerIndex = new Map<number, Set<Entity>>();
    private readonly _actorTagMap = new Map<Entity, string>();
    private readonly _actorLayerMap = new Map<Entity, number>();

    override register(entity: Entity, actor: Actor): void {
        super.register(entity, actor);
        this._indexActor(entity, actor.tag as string, actor.layer as number);
    }

    override unregister(entity: Entity): Actor | undefined {
        this._removeFromIndices(entity);
        return super.unregister(entity);
    }

    override clear(): void {
        super.clear();
        this._tagIndex.clear();
        this._layerIndex.clear();
        this._actorTagMap.clear();
        this._actorLayerMap.clear();
    }

    updateActorTag(entity: Entity, oldTag: string, newTag: string): void {
        this._removeFromTagIndex(entity, oldTag);
        this._addToTagIndex(entity, newTag);
        this._actorTagMap.set(entity, newTag);
    }

    updateActorLayer(entity: Entity, oldLayer: number, newLayer: number): void {
        this._removeFromLayerIndex(entity, oldLayer);
        this._addToLayerIndex(entity, newLayer);
        this._actorLayerMap.set(entity, newLayer);
    }

    getByTag(tag: string): readonly Actor[] {
        const entities = this._tagIndex.get(tag);
        if (!entities || entities.size === 0) return [];
        const result: Actor[] = [];
        for (const entity of entities) {
            const actor = super.get(entity);
            if (actor) result.push(actor);
        }
        return result;
    }

    getByLayer(layer: number): readonly Actor[] {
        const entities = this._layerIndex.get(layer);
        if (!entities || entities.size === 0) return [];
        const result: Actor[] = [];
        for (const entity of entities) {
            const actor = super.get(entity);
            if (actor) result.push(actor);
        }
        return result;
    }

    private _indexActor(entity: Entity, tag: string, layer: number): void {
        this._addToTagIndex(entity, tag);
        this._addToLayerIndex(entity, layer);
        this._actorTagMap.set(entity, tag);
        this._actorLayerMap.set(entity, layer);
    }

    private _removeFromIndices(entity: Entity): void {
        const tag = this._actorTagMap.get(entity);
        if (tag !== undefined) {
            this._removeFromTagIndex(entity, tag);
            this._actorTagMap.delete(entity);
        }
        const layer = this._actorLayerMap.get(entity);
        if (layer !== undefined) {
            this._removeFromLayerIndex(entity, layer);
            this._actorLayerMap.delete(entity);
        }
    }

    private _addToTagIndex(entity: Entity, tag: string): void {
        let set = this._tagIndex.get(tag);
        if (!set) {
            set = new Set();
            this._tagIndex.set(tag, set);
        }
        set.add(entity);
    }

    private _removeFromTagIndex(entity: Entity, tag: string): void {
        const set = this._tagIndex.get(tag);
        if (set) {
            set.delete(entity);
            if (set.size === 0) this._tagIndex.delete(tag);
        }
    }

    private _addToLayerIndex(entity: Entity, layer: number): void {
        let set = this._layerIndex.get(layer);
        if (!set) {
            set = new Set();
            this._layerIndex.set(layer, set);
        }
        set.add(entity);
    }

    private _removeFromLayerIndex(entity: Entity, layer: number): void {
        const set = this._layerIndex.get(layer);
        if (set) {
            set.delete(entity);
            if (set.size === 0) this._layerIndex.delete(layer);
        }
    }
}
