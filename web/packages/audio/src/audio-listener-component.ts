import { Component } from '@axrone/ecs-runtime';
import type { ComponentConfig } from '@axrone/ecs-runtime';
import type { Transform } from '@axrone/ecs-runtime';
import { script } from '@axrone/ecs-runtime';
import { Vec3 } from '@axrone/numeric';
import {
    DEFAULT_LISTENER_FORWARD,
    DEFAULT_LISTENER_POSITION,
    DEFAULT_LISTENER_UP,
    clamp,
    cloneMetadata,
    normalizeDspBuffer,
    normalizeSampleRate,
    normalizeVector3,
} from './internal/shared';
import { cloneAudioVector3, normalizeAudioListenerId } from './reference';
import type {
    AudioJsonValue,
    AudioListenerDescriptor,
    AudioListenerId,
    AudioVector3,
} from './types';

export type AudioSpeakerMode = 'mono' | 'stereo' | 'quad' | '5.1' | '7.1';
export type AudioHrtfPlugin = 'none' | 'oculus' | 'steam' | 'resonance' | 'windowsSonic';
export type AudioOcclusionMode = 'raycast' | 'raycastDiffraction' | 'none';
export type AudioReverbPreset =
    | 'off'
    | 'generic'
    | 'room'
    | 'bathroom'
    | 'stoneroom'
    | 'auditorium'
    | 'concertHall'
    | 'cave'
    | 'forest'
    | 'city'
    | 'underwater';
export type AudioVirtualVoiceBehavior = 'playSilent' | 'playInaudible' | 'stopImmediately' | 'swapWithReal';

export interface AudioListenerComponentConfig extends ComponentConfig {
    readonly listenerId?: AudioListenerId | string;
    readonly active?: boolean;
    readonly position?: AudioVector3;
    readonly forward?: AudioVector3;
    readonly up?: AudioVector3;
    readonly useTransform?: boolean;
    readonly globalVolume?: number;
    readonly dopplerFactor?: number;
    readonly speakerMode?: AudioSpeakerMode;
    readonly sampleRate?: number;
    readonly dspBufferSize?: number;
    readonly realVoices?: number;
    readonly virtualVoices?: number;
    readonly virtualVoiceBehavior?: AudioVirtualVoiceBehavior;
    readonly hrtfPlugin?: AudioHrtfPlugin;
    readonly occlusionMode?: AudioOcclusionMode;
    readonly occlusionLayers?: readonly number[];
    readonly reverbPreset?: AudioReverbPreset;
    readonly reverbLevel?: number;
    readonly ambientClipId?: string;
    readonly ambientVolume?: number;
    readonly followCamera?: boolean;
    readonly metadata?: Readonly<Record<string, AudioJsonValue>>;
}

const VALID_SPEAKER_MODES: readonly AudioSpeakerMode[] = ['mono', 'stereo', 'quad', '5.1', '7.1'];
const VALID_HRTF_PLUGINS: readonly AudioHrtfPlugin[] = ['none', 'oculus', 'steam', 'resonance', 'windowsSonic'];
const VALID_OCCLUSION_MODES: readonly AudioOcclusionMode[] = ['raycast', 'raycastDiffraction', 'none'];
const VALID_REVERB_PRESETS: readonly AudioReverbPreset[] = [
    'off', 'generic', 'room', 'bathroom', 'stoneroom', 'auditorium',
    'concertHall', 'cave', 'forest', 'city', 'underwater',
];
const VALID_VIRTUAL_BEHAVIORS: readonly AudioVirtualVoiceBehavior[] = [
    'playSilent', 'playInaudible', 'stopImmediately', 'swapWithReal',
];

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
    private _globalVolume: number;
    private _dopplerFactor: number;
    private _speakerMode: AudioSpeakerMode;
    private _sampleRate: number;
    private _dspBufferSize: number;
    private _realVoices: number;
    private _virtualVoices: number;
    private _virtualVoiceBehavior: AudioVirtualVoiceBehavior;
    private _hrtfPlugin: AudioHrtfPlugin;
    private _occlusionMode: AudioOcclusionMode;
    private _occlusionLayers: number[];
    private _reverbPreset: AudioReverbPreset;
    private _reverbLevel: number;
    private _ambientClipId: string;
    private _ambientVolume: number;
    private _followCamera: boolean;
    private _metadata: Readonly<Record<string, AudioJsonValue>>;

    constructor(config: AudioListenerComponentConfig = {}) {
        super(config);
        this._listenerId = normalizeAudioListenerId(config.listenerId ?? 'default');
        this._active = config.active ?? true;
        this._position = normalizeVector3(config.position, DEFAULT_LISTENER_POSITION);
        this._forward = normalizeVector3(config.forward, DEFAULT_LISTENER_FORWARD);
        this._up = normalizeVector3(config.up, DEFAULT_LISTENER_UP);
        this._useTransform = config.useTransform ?? true;
        this._globalVolume = clamp(config.globalVolume ?? 1.0, 0, 1);
        this._dopplerFactor = clamp(config.dopplerFactor ?? 1.0, 0, 5);
        this._speakerMode = AudioListenerComponent._normalizeSpeakerMode(config.speakerMode);
        this._sampleRate = AudioListenerComponent._normalizeSampleRate(config.sampleRate ?? 48000);
        this._dspBufferSize = AudioListenerComponent._normalizeDspBuffer(config.dspBufferSize ?? 1024);
        this._realVoices = clamp(config.realVoices ?? 32, 1, 256);
        this._virtualVoices = clamp(config.virtualVoices ?? 128, 1, 1024);
        this._virtualVoiceBehavior = AudioListenerComponent._normalizeVirtualBehavior(config.virtualVoiceBehavior);
        this._hrtfPlugin = AudioListenerComponent._normalizeHrtf(config.hrtfPlugin);
        this._occlusionMode = AudioListenerComponent._normalizeOcclusion(config.occlusionMode);
        this._occlusionLayers = Array.isArray(config.occlusionLayers) ? [...config.occlusionLayers] : [0, 3, 6, 7];
        this._reverbPreset = AudioListenerComponent._normalizeReverb(config.reverbPreset);
        this._reverbLevel = clamp(config.reverbLevel ?? 0, 0, 1);
        this._ambientClipId = config.ambientClipId ?? '';
        this._ambientVolume = clamp(config.ambientVolume ?? 0.5, 0, 1);
        this._followCamera = config.followCamera ?? true;
        this._metadata = cloneMetadata(config.metadata);
    }

    /* ---- normalizers ---- */

    private static _normalizeSpeakerMode(value: unknown): AudioSpeakerMode {
        return typeof value === 'string' && VALID_SPEAKER_MODES.includes(value as AudioSpeakerMode)
            ? (value as AudioSpeakerMode)
            : 'stereo';
    }

    private static _normalizeSampleRate(value: unknown): number {
        return typeof value === 'number' ? normalizeSampleRate(value) : 48000;
    }

    private static _normalizeDspBuffer(value: unknown): number {
        return typeof value === 'number' ? normalizeDspBuffer(value) : 1024;
    }

    private static _normalizeHrtf(value: unknown): AudioHrtfPlugin {
        return typeof value === 'string' && VALID_HRTF_PLUGINS.includes(value as AudioHrtfPlugin)
            ? (value as AudioHrtfPlugin)
            : 'none';
    }

    private static _normalizeOcclusion(value: unknown): AudioOcclusionMode {
        return typeof value === 'string' && VALID_OCCLUSION_MODES.includes(value as AudioOcclusionMode)
            ? (value as AudioOcclusionMode)
            : 'raycastDiffraction';
    }

    private static _normalizeReverb(value: unknown): AudioReverbPreset {
        return typeof value === 'string' && VALID_REVERB_PRESETS.includes(value as AudioReverbPreset)
            ? (value as AudioReverbPreset)
            : 'off';
    }

    private static _normalizeVirtualBehavior(value: unknown): AudioVirtualVoiceBehavior {
        return typeof value === 'string' && VALID_VIRTUAL_BEHAVIORS.includes(value as AudioVirtualVoiceBehavior)
            ? (value as AudioVirtualVoiceBehavior)
            : 'playSilent';
    }

    /* ---- getters / setters ---- */

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

    get globalVolume(): number {
        return this._globalVolume;
    }

    set globalVolume(value: number) {
        this._globalVolume = clamp(value, 0, 1);
    }

    get dopplerFactor(): number {
        return this._dopplerFactor;
    }

    set dopplerFactor(value: number) {
        this._dopplerFactor = clamp(value, 0, 5);
    }

    get speakerMode(): AudioSpeakerMode {
        return this._speakerMode;
    }

    set speakerMode(value: AudioSpeakerMode) {
        this._speakerMode = AudioListenerComponent._normalizeSpeakerMode(value);
    }

    get sampleRate(): number {
        return this._sampleRate;
    }

    set sampleRate(value: number) {
        this._sampleRate = AudioListenerComponent._normalizeSampleRate(value);
    }

    get dspBufferSize(): number {
        return this._dspBufferSize;
    }

    set dspBufferSize(value: number) {
        this._dspBufferSize = AudioListenerComponent._normalizeDspBuffer(value);
    }

    get realVoices(): number {
        return this._realVoices;
    }

    set realVoices(value: number) {
        this._realVoices = clamp(value, 1, 256);
    }

    get virtualVoices(): number {
        return this._virtualVoices;
    }

    set virtualVoices(value: number) {
        this._virtualVoices = clamp(value, 1, 1024);
    }

    get virtualVoiceBehavior(): AudioVirtualVoiceBehavior {
        return this._virtualVoiceBehavior;
    }

    set virtualVoiceBehavior(value: AudioVirtualVoiceBehavior) {
        this._virtualVoiceBehavior = AudioListenerComponent._normalizeVirtualBehavior(value);
    }

    get hrtfPlugin(): AudioHrtfPlugin {
        return this._hrtfPlugin;
    }

    set hrtfPlugin(value: AudioHrtfPlugin) {
        this._hrtfPlugin = AudioListenerComponent._normalizeHrtf(value);
    }

    get occlusionMode(): AudioOcclusionMode {
        return this._occlusionMode;
    }

    set occlusionMode(value: AudioOcclusionMode) {
        this._occlusionMode = AudioListenerComponent._normalizeOcclusion(value);
    }

    get occlusionLayers(): readonly number[] {
        return this._occlusionLayers;
    }

    set occlusionLayers(value: readonly number[]) {
        this._occlusionLayers = Array.isArray(value) ? [...value] : [];
    }

    get reverbPreset(): AudioReverbPreset {
        return this._reverbPreset;
    }

    set reverbPreset(value: AudioReverbPreset) {
        this._reverbPreset = AudioListenerComponent._normalizeReverb(value);
    }

    get reverbLevel(): number {
        return this._reverbLevel;
    }

    set reverbLevel(value: number) {
        this._reverbLevel = clamp(value, 0, 1);
    }

    get ambientClipId(): string {
        return this._ambientClipId;
    }

    set ambientClipId(value: string) {
        this._ambientClipId = typeof value === 'string' ? value : '';
    }

    get ambientVolume(): number {
        return this._ambientVolume;
    }

    set ambientVolume(value: number) {
        this._ambientVolume = clamp(value, 0, 1);
    }

    get followCamera(): boolean {
        return this._followCamera;
    }

    set followCamera(value: boolean) {
        this._followCamera = Boolean(value);
    }

    get metadata(): Readonly<Record<string, AudioJsonValue>> {
        return this._metadata;
    }

    set metadata(value: Readonly<Record<string, AudioJsonValue>>) {
        this._metadata = cloneMetadata(value);
    }

    /* ---- descriptor / serialization ---- */

    toDescriptor(): AudioListenerDescriptor {
        const base: AudioListenerDescriptor = {
            id: this._listenerId,
            active: this._active,
            enabled: this.enabled,
            position: cloneAudioVector3(this._position),
            forward: cloneAudioVector3(this._forward),
            up: cloneAudioVector3(this._up),
            globalVolume: this._globalVolume,
            dopplerFactor: this._dopplerFactor,
            speakerMode: this._speakerMode,
            sampleRate: this._sampleRate,
            dspBufferSize: this._dspBufferSize,
            realVoices: this._realVoices,
            virtualVoices: this._virtualVoices,
            virtualVoiceBehavior: this._virtualVoiceBehavior,
            hrtfPlugin: this._hrtfPlugin,
            occlusionMode: this._occlusionMode,
            occlusionLayers: [...this._occlusionLayers],
            reverbPreset: this._reverbPreset,
            reverbLevel: this._reverbLevel,
            ambientClipId: this._ambientClipId,
            ambientVolume: this._ambientVolume,
            followCamera: this._followCamera,
            metadata: this._metadata,
        };

        const transform = this._useTransform ? (this.transform as Transform | undefined) : undefined;
        if (!transform) {
            return base;
        }

        return {
            ...base,
            position: cloneAudioVector3(transform.worldPosition),
            forward: cloneAudioVector3(
                transform.worldRotation.rotateVector(Vec3.BACK, Vec3.create()) as AudioVector3
            ),
            up: cloneAudioVector3(
                transform.worldRotation.rotateVector(Vec3.UP, Vec3.create()) as AudioVector3
            ),
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
            globalVolume: this._globalVolume,
            dopplerFactor: this._dopplerFactor,
            speakerMode: this._speakerMode,
            sampleRate: this._sampleRate,
            dspBufferSize: this._dspBufferSize,
            realVoices: this._realVoices,
            virtualVoices: this._virtualVoices,
            virtualVoiceBehavior: this._virtualVoiceBehavior,
            hrtfPlugin: this._hrtfPlugin,
            occlusionMode: this._occlusionMode,
            occlusionLayers: [...this._occlusionLayers],
            reverbPreset: this._reverbPreset,
            reverbLevel: this._reverbLevel,
            ambientClipId: this._ambientClipId,
            ambientVolume: this._ambientVolume,
            followCamera: this._followCamera,
            metadata: this._metadata,
        };
    }

    deserialize(data: Record<string, any>): void {
        if (typeof data.listenerId === 'string') this.listenerId = data.listenerId;
        if (typeof data.active === 'boolean') this.active = data.active;
        if (typeof data.enabled === 'boolean') this.enabled = data.enabled;
        if (data.position) this.position = data.position;
        if (data.forward) this.forward = data.forward;
        if (data.up) this.up = data.up;
        if (typeof data.useTransform === 'boolean') this.useTransform = data.useTransform;
        if (typeof data.globalVolume === 'number') this.globalVolume = data.globalVolume;
        if (typeof data.dopplerFactor === 'number') this.dopplerFactor = data.dopplerFactor;
        if (typeof data.speakerMode === 'string') this.speakerMode = AudioListenerComponent._normalizeSpeakerMode(data.speakerMode);
        if (typeof data.sampleRate === 'number') this.sampleRate = data.sampleRate;
        if (typeof data.dspBufferSize === 'number') this.dspBufferSize = data.dspBufferSize;
        if (typeof data.realVoices === 'number') this.realVoices = data.realVoices;
        if (typeof data.virtualVoices === 'number') this.virtualVoices = data.virtualVoices;
        if (typeof data.virtualVoiceBehavior === 'string') this.virtualVoiceBehavior = AudioListenerComponent._normalizeVirtualBehavior(data.virtualVoiceBehavior);
        if (typeof data.hrtfPlugin === 'string') this.hrtfPlugin = AudioListenerComponent._normalizeHrtf(data.hrtfPlugin);
        if (typeof data.occlusionMode === 'string') this.occlusionMode = AudioListenerComponent._normalizeOcclusion(data.occlusionMode);
        if (Array.isArray(data.occlusionLayers)) this.occlusionLayers = data.occlusionLayers;
        if (typeof data.reverbPreset === 'string') this.reverbPreset = AudioListenerComponent._normalizeReverb(data.reverbPreset);
        if (typeof data.reverbLevel === 'number') this.reverbLevel = data.reverbLevel;
        if (typeof data.ambientClipId === 'string') this.ambientClipId = data.ambientClipId;
        if (typeof data.ambientVolume === 'number') this.ambientVolume = data.ambientVolume;
        if (typeof data.followCamera === 'boolean') this.followCamera = data.followCamera;
        if (data.metadata && typeof data.metadata === 'object') this.metadata = data.metadata;
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
            globalVolume: this._globalVolume,
            dopplerFactor: this._dopplerFactor,
            speakerMode: this._speakerMode,
            sampleRate: this._sampleRate,
            dspBufferSize: this._dspBufferSize,
            realVoices: this._realVoices,
            virtualVoices: this._virtualVoices,
            virtualVoiceBehavior: this._virtualVoiceBehavior,
            hrtfPlugin: this._hrtfPlugin,
            occlusionMode: this._occlusionMode,
            occlusionLayers: this._occlusionLayers,
            reverbPreset: this._reverbPreset,
            reverbLevel: this._reverbLevel,
            ambientClipId: this._ambientClipId,
            ambientVolume: this._ambientVolume,
            followCamera: this._followCamera,
            metadata: this._metadata,
        }) as this;
    }
}
