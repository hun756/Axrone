import { Component } from '@axrone/ecs-runtime';
import type { ComponentConfig } from '@axrone/ecs-runtime';
import type { Transform } from '@axrone/ecs-runtime';
import { script } from '@axrone/ecs-runtime';
import { Vec3 } from '@axrone/numeric';
import {
    DEFAULT_LISTENER_FORWARD,
    DEFAULT_LISTENER_POSITION,
    DEFAULT_LISTENER_UP,
    cloneMetadata,
    normalizeVector3,
} from './internal/shared';
import { cloneAudioVector3, normalizeAudioListenerId } from './reference';
import type {
    AudioJsonValue,
    AudioListenerDescriptor,
    AudioListenerId,
    AudioVector3,
} from './types';

export interface AudioListenerComponentConfig extends ComponentConfig {
    readonly listenerId?: AudioListenerId | string;
    readonly active?: boolean;
    readonly position?: AudioVector3;
    readonly forward?: AudioVector3;
    readonly up?: AudioVector3;
    readonly useTransform?: boolean;
    readonly metadata?: Readonly<Record<string, AudioJsonValue>>;
}

@script({
    scriptName: 'AudioListener',
    priority: 800,
    executeInEditMode: true,
    singleton: false,
})
export class AudioListenerComponent extends Component<AudioListenerComponentConfig> {
    private _listenerId: AudioListenerId;
    private _active: boolean;
    private _position: AudioVector3;
    private _forward: AudioVector3;
    private _up: AudioVector3;
    private _useTransform: boolean;
    private _metadata: Readonly<Record<string, AudioJsonValue>>;

    constructor(config: AudioListenerComponentConfig = {}) {
        super(config);
        this._listenerId = normalizeAudioListenerId(config.listenerId ?? 'default');
        this._active = config.active ?? true;
        this._position = normalizeVector3(config.position, DEFAULT_LISTENER_POSITION);
        this._forward = normalizeVector3(config.forward, DEFAULT_LISTENER_FORWARD);
        this._up = normalizeVector3(config.up, DEFAULT_LISTENER_UP);
        this._useTransform = config.useTransform ?? true;
        this._metadata = cloneMetadata(config.metadata);
    }

    get listenerId(): AudioListenerId {
        return this._listenerId;
    }

    set listenerId(value: AudioListenerId | string) {
        this._listenerId = normalizeAudioListenerId(value);
    }

    get active(): boolean {
        return this._active;
    }

    set active(value: boolean) {
        this._active = value;
    }

    get useTransform(): boolean {
        return this._useTransform;
    }

    set useTransform(value: boolean) {
        this._useTransform = value;
    }

    get position(): AudioVector3 {
        return cloneAudioVector3(this._position);
    }

    set position(value: AudioVector3) {
        this._position = normalizeVector3(value, DEFAULT_LISTENER_POSITION);
    }

    get forward(): AudioVector3 {
        return cloneAudioVector3(this._forward);
    }

    set forward(value: AudioVector3) {
        this._forward = normalizeVector3(value, DEFAULT_LISTENER_FORWARD);
    }

    get up(): AudioVector3 {
        return cloneAudioVector3(this._up);
    }

    set up(value: AudioVector3) {
        this._up = normalizeVector3(value, DEFAULT_LISTENER_UP);
    }

    get metadata(): Readonly<Record<string, AudioJsonValue>> {
        return this._metadata;
    }

    set metadata(value: Readonly<Record<string, AudioJsonValue>>) {
        this._metadata = cloneMetadata(value);
    }

    toDescriptor(): AudioListenerDescriptor {
        const transform = this._useTransform ? (this.transform as Transform | undefined) : undefined;
        if (transform) {
            const position = cloneAudioVector3(transform.worldPosition);
            const forward = cloneAudioVector3(
                transform.worldRotation.rotateVector(Vec3.BACK, Vec3.create()) as AudioVector3
            );
            const up = cloneAudioVector3(
                transform.worldRotation.rotateVector(Vec3.UP, Vec3.create()) as AudioVector3
            );
            return {
                id: this._listenerId,
                active: this._active,
                enabled: this.enabled,
                position,
                forward,
                up,
                metadata: this._metadata,
            };
        }

        return {
            id: this._listenerId,
            active: this._active,
            enabled: this.enabled,
            position: cloneAudioVector3(this._position),
            forward: cloneAudioVector3(this._forward),
            up: cloneAudioVector3(this._up),
            metadata: this._metadata,
        };
    }

    serialize(): Record<string, unknown> {
        return {
            listenerId: this._listenerId,
            active: this._active,
            enabled: this.enabled,
            position: cloneAudioVector3(this._position),
            forward: cloneAudioVector3(this._forward),
            up: cloneAudioVector3(this._up),
            useTransform: this._useTransform,
            metadata: this._metadata,
        };
    }

    deserialize(data: Record<string, any>): void {
        if (typeof data.listenerId === 'string') {
            this.listenerId = data.listenerId;
        }
        if (typeof data.active === 'boolean') {
            this.active = data.active;
        }
        if (typeof data.enabled === 'boolean') {
            this.enabled = data.enabled;
        }
        if (data.position) {
            this.position = data.position;
        }
        if (data.forward) {
            this.forward = data.forward;
        }
        if (data.up) {
            this.up = data.up;
        }
        if (typeof data.useTransform === 'boolean') {
            this.useTransform = data.useTransform;
        }
        if (data.metadata && typeof data.metadata === 'object') {
            this.metadata = data.metadata;
        }
    }

    clone(): this {
        return new AudioListenerComponent({
            listenerId: this._listenerId,
            active: this._active,
            enabled: this.enabled,
            position: this._position,
            forward: this._forward,
            up: this._up,
            useTransform: this._useTransform,
            metadata: this._metadata,
        }) as this;
    }
}
