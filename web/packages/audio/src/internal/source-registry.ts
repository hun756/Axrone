import { toAudioClipSelector, isClipSelectorEqual } from '../asset';
import { AudioSourceError } from '../errors';
import {
    MASTER_AUDIO_BUS_ID,
    normalizeAudioBusId,
    normalizeAudioSourceId,
} from '../reference';
import type {
    AudioAssetSchema,
    AudioBusId,
    AudioSourceDefinition,
    AudioSourceId,
    AudioSourceState,
} from '../types';
import type { InternalPlayback, InternalSource } from './runtime';
import {
    cloneMetadata,
    isFiniteNumber,
    isMetadataEqual,
} from './shared';
import { cloneSpatialization, isSpatializationEqual } from './spatial';

export interface AudioSourceRegistryOptions {
    readonly normalizeGain: (
        value: number,
        code: 'audio.invalid-gain' | 'audio.invalid-distance'
    ) => number;
    readonly normalizePan: (value: number) => number;
    readonly normalizePlaybackRate: (value: number) => number;
    readonly normalizeTime: (value: number) => number;
}

export class AudioSourceRegistry<TSchema extends AudioAssetSchema = AudioAssetSchema> {
    readonly #normalizeGain: AudioSourceRegistryOptions['normalizeGain'];
    readonly #normalizePan: AudioSourceRegistryOptions['normalizePan'];
    readonly #normalizePlaybackRate: AudioSourceRegistryOptions['normalizePlaybackRate'];
    readonly #normalizeTime: AudioSourceRegistryOptions['normalizeTime'];
    readonly #sources = new Map<AudioSourceId, InternalSource<TSchema>>();
    readonly #transientSources = new Set<AudioSourceId>();

    #sourceSequence = 0;
    #oneShotSequence = 0;
    #mutationSequence = 0;

    /**
     * Advances only when an upsert actually changed a field. Lets callers detect real edits
     * without the registry widening its return type, so a per-frame re-upsert of an
     * unchanged descriptor costs nothing beyond the lookup.
     */
    get mutationSequence(): number {
        return this.#mutationSequence;
    }

    constructor(options: AudioSourceRegistryOptions) {
        this.#normalizeGain = options.normalizeGain;
        this.#normalizePan = options.normalizePan;
        this.#normalizePlaybackRate = options.normalizePlaybackRate;
        this.#normalizeTime = options.normalizeTime;
    }

    nextOneShotId(): AudioSourceId {
        this.#oneShotSequence += 1;
        return normalizeAudioSourceId(`oneshot:${this.#oneShotSequence}`);
    }

    markTransient(id: AudioSourceId): void {
        this.#transientSources.add(id);
    }

    isTransient(id: AudioSourceId): boolean {
        return this.#transientSources.has(id);
    }

    upsert(
        definition: AudioSourceDefinition<TSchema>,
        options: {
            readonly requireBus: (id: string) => void;
            readonly reconnectPlaybackOutput?: (
                playback: InternalPlayback<TSchema>,
                nextBusId: string
            ) => void;
        }
    ): InternalSource<TSchema> {
        const id =
            definition.id !== undefined
                ? normalizeAudioSourceId(definition.id)
                : this.#nextManagedId();
        let source = this.#sources.get(id);
        const isNew = source === undefined;

        if (!source) {
            source = {
                id,
                busId: normalizeAudioBusId(definition.busId ?? MASTER_AUDIO_BUS_ID),
                clip: toAudioClipSelector(definition.clip),
                volume: this.#normalizeGain(definition.volume ?? 1, 'audio.invalid-gain'),
                muted: definition.muted ?? false,
                loop: definition.loop ?? false,
                autoplay: definition.autoplay ?? false,
                playbackRate: this.#normalizePlaybackRate(definition.playbackRate ?? 1),
                detuneCents: isFiniteNumber(definition.detuneCents) ? definition.detuneCents : 0,
                pan: this.#normalizePan(definition.pan ?? 0),
                spatial: cloneSpatialization(definition.spatial),
                startOffsetSeconds: this.#normalizeTime(definition.startOffsetSeconds ?? 0),
                metadata: cloneMetadata(definition.metadata),
                playbackState: 'idle',
                currentOffsetSeconds: this.#normalizeTime(definition.startOffsetSeconds ?? 0),
                playSequence: 0,
            };
            this.#sources.set(id, source);
        }

        let changed = isNew;

        if (definition.busId !== undefined) {
            const nextBusId = normalizeAudioBusId(definition.busId);
            options.requireBus(nextBusId);
            if (source.busId !== nextBusId) {
                source.busId = nextBusId;
                changed = true;
                if (source.active) {
                    options.reconnectPlaybackOutput?.(source.active, nextBusId);
                }
            }
        }
        if (definition.clip !== undefined) {
            const nextClip = toAudioClipSelector(definition.clip);
            if (!isClipSelectorEqual(source.clip, nextClip)) {
                source.clip = nextClip;
                changed = true;
            }
        }
        if (definition.volume !== undefined) {
            const nextVolume = this.#normalizeGain(definition.volume, 'audio.invalid-gain');
            if (source.volume !== nextVolume) {
                source.volume = nextVolume;
                changed = true;
            }
        }
        if (definition.muted !== undefined && source.muted !== definition.muted) {
            source.muted = definition.muted;
            changed = true;
        }
        if (definition.loop !== undefined && source.loop !== definition.loop) {
            source.loop = definition.loop;
            changed = true;
        }
        if (definition.autoplay !== undefined && source.autoplay !== definition.autoplay) {
            source.autoplay = definition.autoplay;
            changed = true;
        }
        if (definition.playbackRate !== undefined) {
            const nextRate = this.#normalizePlaybackRate(definition.playbackRate);
            if (source.playbackRate !== nextRate) {
                source.playbackRate = nextRate;
                changed = true;
            }
        }
        if (
            definition.detuneCents !== undefined &&
            isFiniteNumber(definition.detuneCents) &&
            source.detuneCents !== definition.detuneCents
        ) {
            source.detuneCents = definition.detuneCents;
            changed = true;
        }
        if (definition.pan !== undefined) {
            const nextPan = this.#normalizePan(definition.pan);
            if (source.pan !== nextPan) {
                source.pan = nextPan;
                changed = true;
            }
        }
        if (definition.spatial !== undefined) {
            if (!isSpatializationEqual(source.spatial, definition.spatial)) {
                source.spatial = cloneSpatialization(definition.spatial);
                changed = true;
            }
        }
        if (definition.startOffsetSeconds !== undefined) {
            const offset = this.#normalizeTime(definition.startOffsetSeconds);
            if (source.startOffsetSeconds !== offset) {
                source.startOffsetSeconds = offset;
                changed = true;
                if (!source.active) {
                    source.currentOffsetSeconds = offset;
                }
            }
        }
        if (definition.metadata !== undefined && !isMetadataEqual(source.metadata, definition.metadata)) {
            source.metadata = cloneMetadata(definition.metadata);
            changed = true;
        }

        if (changed) {
            this.#mutationSequence += 1;
        }

        return source;
    }

    reassignBus(
        previousBusId: AudioBusId | string,
        nextBusId: AudioBusId | string,
        reconnectPlaybackOutput: (
            playback: InternalPlayback<TSchema>,
            nextBusId: string
        ) => void
    ): void {
        const from = normalizeAudioBusId(previousBusId);
        const to = normalizeAudioBusId(nextBusId);

        for (const source of this.#sources.values()) {
            if (source.busId !== from) {
                continue;
            }

            source.busId = to;
            if (source.active) {
                reconnectPlaybackOutput(source.active, to);
            }
        }
    }

    remove(id: AudioSourceId | string): InternalSource<TSchema> | undefined {
        const normalizedId = normalizeAudioSourceId(id);
        const source = this.#sources.get(normalizedId);
        if (!source) {
            return undefined;
        }

        this.#transientSources.delete(normalizedId);
        this.#sources.delete(normalizedId);
        return source;
    }

    require(id: AudioSourceId | string): InternalSource<TSchema> {
        const normalizedId = normalizeAudioSourceId(id);
        const source = this.#sources.get(normalizedId);
        if (!source) {
            throw new AudioSourceError(
                'audio.source.missing',
                `Audio source ${normalizedId} does not exist`,
                normalizedId
            );
        }
        return source;
    }

    get(id: AudioSourceId | string): InternalSource<TSchema> | undefined {
        return this.#sources.get(normalizeAudioSourceId(id));
    }

    list(): readonly InternalSource<TSchema>[] {
        return Object.freeze([...this.#sources.values()]);
    }

    values(): IterableIterator<InternalSource<TSchema>> {
        return this.#sources.values();
    }

    clear(): readonly InternalSource<TSchema>[] {
        const sources = [...this.#sources.values()];
        this.#sources.clear();
        this.#transientSources.clear();
        return Object.freeze(sources);
    }

    #nextManagedId(): AudioSourceId {
        let nextId: AudioSourceId;
        do {
            this.#sourceSequence += 1;
            nextId = normalizeAudioSourceId(`source:${this.#sourceSequence}`);
        } while (this.#sources.has(nextId));

        return nextId;
    }

    snapshot(
        source: InternalSource<TSchema>,
        resolveCurrentOffset: (source: InternalSource<TSchema>) => number
    ): AudioSourceState<TSchema> {
        return Object.freeze({
            id: source.id,
            busId: source.busId,
            clip: source.clip,
            volume: source.volume,
            muted: source.muted,
            loop: source.loop,
            autoplay: source.autoplay,
            playbackRate: source.playbackRate,
            detuneCents: source.detuneCents,
            pan: source.pan,
            spatial: source.spatial ? cloneSpatialization(source.spatial) : undefined,
            startOffsetSeconds: source.startOffsetSeconds,
            metadata: source.metadata,
            playbackState: source.playbackState,
            currentOffsetSeconds: resolveCurrentOffset(source),
            durationSeconds: source.durationSeconds,
            playSequence: source.playSequence,
        });
    }
}
