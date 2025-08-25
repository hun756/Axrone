import { IParticleSystemModule, IParticleSOA, IParticleSystem } from '../interfaces';

export abstract class BaseModule implements IParticleSystemModule {
    protected _enabled: boolean = false;
    protected _system: IParticleSystem | null = null;

    constructor(
        public readonly name: string,
        enabled: boolean = false
    ) {
        this._enabled = enabled;
    }

    get enabled(): boolean {
        return this._enabled;
    }

    set enabled(value: boolean) {
        this._enabled = value;
    }

    initialize(system: IParticleSystem): void {
        this._system = system;
        this.onInitialize();
    }

    update(deltaTime: number, particles: IParticleSOA): void {
        if (!this._enabled || !this._system) {
            return;
        }
        this.onUpdate(deltaTime, particles);
    }

    reset(): void {
        this.onReset();
    }

    protected abstract onInitialize(): void;
    protected abstract onUpdate(deltaTime: number, particles: IParticleSOA): void;
    protected abstract onReset(): void;

    protected get system(): IParticleSystem {
        if (!this._system) {
            throw new Error(`Module ${this.name} not initialized`);
        }
        return this._system;
    }
}
