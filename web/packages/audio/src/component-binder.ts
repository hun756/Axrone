import { AudioListenerComponent, AudioSourceComponent } from './components';
import { AudioSystem } from './system';
import type {
    AudioAssetSchema,
    AudioSourceComponentCommand,
} from './types';

/**
 * A queued component command that failed while being applied to the AudioSystem.
 * The system already emits `source:error` for playback failures; this is the caller-facing
 * half, so a game loop can surface or count them instead of having them discarded.
 */
export interface AudioBinderCommandFailure<TSchema extends AudioAssetSchema = AudioAssetSchema> {
    readonly component: AudioSourceComponent<TSchema>;
    readonly command: AudioSourceComponentCommand<TSchema>['kind'];
    readonly error: unknown;
}

export class AudioComponentBinder<TSchema extends AudioAssetSchema = AudioAssetSchema> {
    readonly #listeners = new Set<AudioListenerComponent>();
    readonly #sources = new Set<AudioSourceComponent<TSchema>>();

    constructor(readonly system: AudioSystem<TSchema>) {}

    attachListener(component: AudioListenerComponent): this {
        this.#listeners.add(component);
        return this;
    }

    detachListener(component: AudioListenerComponent): boolean {
        return this.#listeners.delete(component);
    }

    attachSource(component: AudioSourceComponent<TSchema>): this {
        this.#sources.add(component);
        return this;
    }

    detachSource(component: AudioSourceComponent<TSchema>): boolean {
        return this.#sources.delete(component);
    }

    clear(): void {
        this.#listeners.clear();
        this.#sources.clear();
    }

    async update(): Promise<readonly AudioBinderCommandFailure<TSchema>[]> {
        const failures: AudioBinderCommandFailure<TSchema>[] = [];

        for (const listener of this.#listeners) {
            this.system.upsertListener(listener.toDescriptor());
            if (listener.active) {
                this.system.setActiveListener(listener.listenerId);
            }
        }

        for (const source of this.#sources) {
            const state = this.system.upsertSource(source.toDescriptor());
            source.syncState(state);
            const commands = source.consumeCommands();
            for (const command of commands) {
                try {
                    await this.#dispatchSourceCommand(source, command);
                } catch (error) {
                    // Recorded, not swallowed: keep draining the queue so one unresolvable
                    // clip cannot stall every other source's commands this frame.
                    failures.push({ component: source, command: command.kind, error });
                }
            }
        }

        this.system.refreshSpatialAudio();
        return failures;
    }

    async #dispatchSourceCommand(
        component: AudioSourceComponent<TSchema>,
        command: AudioSourceComponentCommand<TSchema>
    ): Promise<void> {
        switch (command.kind) {
            case 'play': {
                await this.system.playSource(component.sourceId, command.request);
                const state = this.system.getSource(component.sourceId);
                if (state) component.syncState(state);
                break;
            }
            case 'pause':
                this.system.pauseSource(component.sourceId);
                break;
            case 'resume': {
                await this.system.resumeSource(component.sourceId);
                const state = this.system.getSource(component.sourceId);
                if (state) component.syncState(state);
                break;
            }
            case 'stop':
                this.system.stopSource(component.sourceId, command.options);
                break;
        }
    }
}
