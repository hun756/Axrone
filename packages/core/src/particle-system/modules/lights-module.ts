import { BaseModule } from './base-module';
import { ILightsConfig, IParticleSOA, IParticleSystem } from '../interfaces';

export class LightsModule extends BaseModule {
    public config: ILightsConfig;

    constructor(config: Partial<ILightsConfig> = {}) {
        super('LightsModule', config.enabled ?? false);
        this.config = {
            enabled: config.enabled ?? false,
            ratio: (config as any).ratio ?? 0.0,
            useRandomDistribution: (config as any).useRandomDistribution ?? false,
            light: (config as any).light ?? null,
            useParticleColor: (config as any).useParticleColor ?? false,
            sizeAffectsRange: (config as any).sizeAffectsRange ?? false,
            alphaAffectsIntensity: (config as any).alphaAffectsIntensity ?? false,
            range: (config as any).range ?? { mode: 0, constant: 1, constantMin: 1, constantMax: 1, curveLength: 0, preWrapMode: 0, postWrapMode: 0 },
            rangeMultiplier: (config as any).rangeMultiplier ?? 1,
            intensity: (config as any).intensity ?? { mode: 0, constant: 1, constantMin: 1, constantMax: 1, curveLength: 0, preWrapMode: 0, postWrapMode: 0 },
            intensityMultiplier: (config as any).intensityMultiplier ?? 1,
            maxLights: (config as any).maxLights ?? 0,
        } as ILightsConfig;
    }

    protected onInitialize(): void {
        // No-op for now; this module provides configuration and a place to hook rendering systems
    }

    protected onUpdate(deltaTime: number, particles: IParticleSOA): void {
        // No-op: light generation/rendering is handled externally by renderers using this config
    }

    protected onReset(): void {}
}
