import { Component, script } from '@axrone/ecs-runtime';

export interface TilemapCollider2DConfig {
    readonly mapId?: string;
    /** Layer whose non-zero collision masks become static bodies. */
    readonly collisionLayer?: string;
    readonly oneWay?: boolean;
    readonly enabled?: boolean;
}

/**
 * Declares that an actor's map contributes static collision. The actual body
 * building lives in the collision builder and never in `@axrone/physics-2d`.
 */
@script({
    scriptName: 'TilemapCollider2D',
    priority: 90,
    executeInEditMode: true,
    singleton: false,
    trackInstances: false,
})
export class TilemapCollider2D extends Component {
    #mapId = '';
    #collisionLayer = '';
    #oneWay = false;

    constructor(config: TilemapCollider2DConfig = {}) {
        super();
        this.#mapId = config.mapId ?? '';
        this.#collisionLayer = config.collisionLayer ?? '';
        this.#oneWay = config.oneWay ?? false;
        if (config.enabled === false) this.enabled = false;
    }

    get mapId(): string {
        return this.#mapId;
    }

    set mapId(value: string) {
        if (this.#mapId === value) return;
        this.#mapId = value;
        this.touch();
    }

    get collisionLayer(): string {
        return this.#collisionLayer;
    }

    set collisionLayer(value: string) {
        if (this.#collisionLayer === value) return;
        this.#collisionLayer = value;
        this.touch();
    }

    get oneWay(): boolean {
        return this.#oneWay;
    }

    set oneWay(value: boolean) {
        if (this.#oneWay === value) return;
        this.#oneWay = value;
        this.touch();
    }

    override serialize(): Record<string, unknown> {
        return {
            mapId: this.#mapId,
            collisionLayer: this.#collisionLayer,
            oneWay: this.#oneWay,
            enabled: this.enabled,
        };
    }

    override deserialize(data: Record<string, unknown>): void {
        if (typeof data.mapId === 'string') this.#mapId = data.mapId;
        if (typeof data.collisionLayer === 'string') {
            this.#collisionLayer = data.collisionLayer;
        }
        if (typeof data.oneWay === 'boolean') this.#oneWay = data.oneWay;
        if (typeof data.enabled === 'boolean') this.enabled = data.enabled;
        this.touch();
    }
}
