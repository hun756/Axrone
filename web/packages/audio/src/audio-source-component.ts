import { Component } from '@axrone/ecs-runtime';
import type { ComponentConfig } from '@axrone/ecs-runtime';
import type { Transform } from '@axrone/ecs-runtime';
import { script } from '@axrone/ecs-runtime';
import { Vec3 } from '@axrone/numeric';
import { toAudioClipSelector } from './asset';
import { cloneMetadata, isFiniteNumber } from './internal/shared';
import { cloneSpatialization } from './internal/spatial';
import {
    MASTER_AUDIO_BUS_ID,
    cloneAudioVector3,
    normalizeAudioBusId,
    normalizeAudioSourceId,
} from './reference';
import type {
    AudioAssetSchema,
    AudioJsonValue,
    AudioSourceComponentCommand,
    AudioSourceDefinition,
    AudioSourceId,
    AudioSourcePlayRequest,
    AudioSourceState,
    AudioSpatialization,
    AudioStopOptions,
    AudioVector3,
} from './types';

export interface AudioSourceComponentConfig<TSchema extends AudioAssetSchema = AudioAssetSchema>
    extends ComponentConfig {
    readonly sourceId?: AudioSourceId | string;
    readonly busId?: string;
    readonly clip?: AudioSourceDefinition<TSchema>['clip'];
    readonly volume?: number;
    readonly muted?: boolean;
    readonly loop?: boolean;
    readonly autoplay?: boolean;
    readonly playbackRate?: number;
    readonly detuneCents?: number;
    readonly pan?: number;
    readonly spatial?: AudioSpatialization;
    readonly startOffsetSeconds?: number;
    readonly useTransform?: boolean;
    readonly metadata?: Readonly<Record<string, AudioJsonValue>>;
}

@script({
    scriptName: 'AudioSource',
    priority: 810,
    executeInEditMode: true,
    singleton: false,
})
export class AudioSourceComponent<
    TSchema extends AudioAssetSchema = AudioAssetSchema,
> extends Component<AudioSourceComponentConfig<TSchema>> {
    private _sourceId: AudioSourceId;
    private _busId = MASTER_AUDIO_BUS_ID;
    private _clip?: AudioSourceDefinition<TSchema>['clip'];
    private _volume: number;
    private _muted: boolean;
    private _loop: boolean;
    private _autoplay: boolean;
    private _playbackRate: number;
    private _detuneCents: number;
    private _pan: number;
    private _spatial?: AudioSpatialization;
    private _startOffsetSeconds: number;
    private _useTransform: boolean;
    private _metadata: Readonly<Record<string, AudioJsonValue>>;
    private readonly _pendingCommands: AudioSourceComponentCommand<TSchema>[] = [];
    private _autoplayPending: boolean;
    private _lastKnownState: AudioSourceState<TSchema>['playbackState'] = 'idle';

    constructor(config: AudioSourceComponentConfig<TSchema> = {}) {
        super(config);
        this._sourceId = normalizeAudioSourceId(config.sourceId ?? this.id);
        this._busId = normalizeAudioBusId(config.busId ?? MASTER_AUDIO_BUS_ID);
        this._clip = config.clip;
        this._volume = isFiniteNumber(config.volume) ? config.volume : 1;
        this._muted = config.muted ?? false;
        this._loop = config.loop ?? false;
        this._autoplay = config.autoplay ?? false;
        this._playbackRate = isFiniteNumber(config.playbackRate) ? config.playbackRate : 1;
        this._detuneCents = isFiniteNumber(config.detuneCents) ? config.detuneCents : 0;
        this._pan = isFiniteNumber(config.pan) ? config.pan : 0;
        this._spatial = cloneSpatialization(config.spatial);
        this._startOffsetSeconds = isFiniteNumber(config.startOffsetSeconds)
            ? config.startOffsetSeconds
            : 0;
        this._useTransform = config.useTransform ?? true;
        this._metadata = cloneMetadata(config.metadata);
        this._autoplayPending = this._autoplay;
    }

    get sourceId(): AudioSourceId {
        return this._sourceId;
    }

    set sourceId(value: AudioSourceId | string) {
        this._sourceId = normalizeAudioSourceId(value);
    }

    get busId(): string {
        return this._busId;
    }

    set busId(value: string) {
        this._busId = normalizeAudioBusId(value);
    }

    get clip(): AudioSourceDefinition<TSchema>['clip'] | undefined {
        return this._clip;
    }

    set clip(value: AudioSourceDefinition<TSchema>['clip'] | undefined) {
        this._clip = value;
    }

    get volume(): number {
        return this._volume;
    }

    set volume(value: number) {
        this._volume = value;
    }

    get muted(): boolean {
        return this._muted;
    }

    set muted(value: boolean) {
        this._muted = value;
    }

    get loop(): boolean {
        return this._loop;
    }

    set loop(value: boolean) {
        this._loop = value;
    }

    get autoplay(): boolean {
        return this._autoplay;
    }

    set autoplay(value: boolean) {
        this._autoplay = value;
        if (value) {
            this._autoplayPending = true;
        }
    }

    get playbackRate(): number {
        return this._playbackRate;
    }

    set playbackRate(value: number) {
        this._playbackRate = value;
    }

    get detuneCents(): number {
        return this._detuneCents;
    }

    set detuneCents(value: number) {
        this._detuneCents = value;
    }

    get pan(): number {
        return this._pan;
    }

    set pan(value: number) {
        this._pan = value;
    }

    get spatial(): AudioSpatialization | undefined {
        return cloneSpatialization(this._spatial);
    }

    set spatial(value: AudioSpatialization | undefined) {
        this._spatial = cloneSpatialization(value);
    }

    get startOffsetSeconds(): number {
        return this._startOffsetSeconds;
    }

    set startOffsetSeconds(value: number) {
        this._startOffsetSeconds = value;
    }

    get useTransform(): boolean {
        return this._useTransform;
    }

    set useTransform(value: boolean) {
        this._useTransform = value;
    }

    get metadata(): Readonly<Record<string, AudioJsonValue>> {
        return this._metadata;
    }

    set metadata(value: Readonly<Record<string, AudioJsonValue>>) {
        this._metadata = cloneMetadata(value);
    }

    get playbackState(): AudioSourceState<TSchema>['playbackState'] {
        return this._lastKnownState;
    }

    play(request?: AudioSourcePlayRequest<TSchema>): void {
        this._pendingCommands.push({ kind: 'play', request });
    }

    pause(): void {
        this._pendingCommands.push({ kind: 'pause' });
    }

    resume(): void {
        this._pendingCommands.push({ kind: 'resume' });
    }

    stop(options?: AudioStopOptions): void {
        this._pendingCommands.push({ kind: 'stop', options });
    }

    override onEnable(): void {
        if (this._autoplay) {
            this._autoplayPending = true;
        }
    }

    override onDisable(): void {
        this.stop();
    }

    consumeCommands(): readonly AudioSourceComponentCommand<TSchema>[] {
        const commands = this._pendingCommands.splice(0);
        if (this._autoplayPending) {
            commands.unshift({ kind: 'play' });
            this._autoplayPending = false;
        }
        return Object.freeze(commands);
    }

    syncState(state: AudioSourceState<TSchema>): void {
        this._lastKnownState = state.playbackState;
    }

    toDescriptor(): AudioSourceDefinition<TSchema> {
        const transform = this._useTransform ? (this.transform as Transform | undefined) : undefined;
        let spatial = cloneSpatialization(this._spatial);

        if (transform) {
            const position = cloneAudioVector3(transform.worldPosition);
            const orientation = cloneAudioVector3(
                transform.worldRotation.rotateVector(Vec3.BACK, Vec3.create()) as AudioVector3
            );

            if (!spatial) {
                spatial = {
                    mode: '3d',
                    position,
                    orientation,
                };
            } else if (spatial.mode === '2d') {
                spatial = {
                    ...spatial,
                    position,
                };
            } else {
                spatial = {
                    ...spatial,
                    position,
                    orientation,
                };
            }
        }

        return {
            id: this._sourceId,
            busId: this._busId,
            clip: this._clip,
            volume: this._volume,
            muted: this._muted || !this.enabled,
            loop: this._loop,
            autoplay: this._autoplay,
            playbackRate: this._playbackRate,
            detuneCents: this._detuneCents,
            pan: this._pan,
            spatial,
            startOffsetSeconds: this._startOffsetSeconds,
            metadata: this._metadata,
        };
    }

    serialize(): Record<string, unknown> {
        return {
            sourceId: this._sourceId,
            busId: this._busId,
            clip: toAudioClipSelector(this._clip),
            volume: this._volume,
            muted: this._muted,
            loop: this._loop,
            autoplay: this._autoplay,
            playbackRate: this._playbackRate,
            detuneCents: this._detuneCents,
            pan: this._pan,
            spatial: cloneSpatialization(this._spatial),
            startOffsetSeconds: this._startOffsetSeconds,
            useTransform: this._useTransform,
            metadata: this._metadata,
        };
    }

    deserialize(data: Record<string, any>): void {
        if (typeof data.sourceId === 'string') {
            this.sourceId = data.sourceId;
        }
        if (typeof data.busId === 'string') {
            this.busId = data.busId;
        }
        if ('clip' in data) {
            this.clip = data.clip;
        }
        if (isFiniteNumber(data.volume)) {
            this.volume = data.volume;
        }
        if (typeof data.muted === 'boolean') {
            this.muted = data.muted;
        }
        if (typeof data.loop === 'boolean') {
            this.loop = data.loop;
        }
        if (typeof data.autoplay === 'boolean') {
            this.autoplay = data.autoplay;
        }
        if (isFiniteNumber(data.playbackRate)) {
            this.playbackRate = data.playbackRate;
        }
        if (isFiniteNumber(data.detuneCents)) {
            this.detuneCents = data.detuneCents;
        }
        if (isFiniteNumber(data.pan)) {
            this.pan = data.pan;
        }
        if (data.spatial) {
            this.spatial = data.spatial;
        }
        if (isFiniteNumber(data.startOffsetSeconds)) {
            this.startOffsetSeconds = data.startOffsetSeconds;
        }
        if (typeof data.useTransform === 'boolean') {
            this.useTransform = data.useTransform;
        }
        if (data.metadata && typeof data.metadata === 'object') {
            this.metadata = data.metadata;
        }
    }

    clone(): this {
        return new AudioSourceComponent<TSchema>({
            sourceId: this._sourceId,
            busId: this._busId,
            clip: this._clip,
            volume: this._volume,
            muted: this._muted,
            loop: this._loop,
            autoplay: this._autoplay,
            playbackRate: this._playbackRate,
            detuneCents: this._detuneCents,
            pan: this._pan,
            spatial: this._spatial,
            startOffsetSeconds: this._startOffsetSeconds,
            useTransform: this._useTransform,
            metadata: this._metadata,
            enabled: this.enabled,
        }) as this;
    }
}
