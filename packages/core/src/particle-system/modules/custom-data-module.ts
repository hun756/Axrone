import { BaseModule } from './base-module';
import { ICustomDataConfig, IParticleSOA } from '../interfaces';

export class CustomDataModule extends BaseModule {
    public config: ICustomDataConfig;

    constructor(config: Partial<ICustomDataConfig> = {}) {
        super('CustomDataModule', config.enabled ?? false);
        this.config = {
            enabled: config.enabled ?? false,
            mode0: (config as any).mode0 ?? 0,
            vectorComponentCount0: (config as any).vectorComponentCount0 ?? 4,
            color0: (config as any).color0 ?? {
                mode: 0,
                colorKeys: new Float32Array(),
                alphaKeys: new Float32Array(),
                keyCount: 0,
                blendMode: 0,
            },
            vector0: (config as any).vector0 ?? [
                {
                    mode: 0,
                    constant: 0,
                    constantMin: 0,
                    constantMax: 0,
                    curveLength: 0,
                    preWrapMode: 0,
                    postWrapMode: 0,
                },
                {
                    mode: 0,
                    constant: 0,
                    constantMin: 0,
                    constantMax: 0,
                    curveLength: 0,
                    preWrapMode: 0,
                    postWrapMode: 0,
                },
                {
                    mode: 0,
                    constant: 0,
                    constantMin: 0,
                    constantMax: 0,
                    curveLength: 0,
                    preWrapMode: 0,
                    postWrapMode: 0,
                },
                {
                    mode: 0,
                    constant: 0,
                    constantMin: 0,
                    constantMax: 0,
                    curveLength: 0,
                    preWrapMode: 0,
                    postWrapMode: 0,
                },
            ],
            mode1: (config as any).mode1 ?? 0,
            vectorComponentCount1: (config as any).vectorComponentCount1 ?? 4,
            color1: (config as any).color1 ?? {
                mode: 0,
                colorKeys: new Float32Array(),
                alphaKeys: new Float32Array(),
                keyCount: 0,
                blendMode: 0,
            },
            vector1: (config as any).vector1 ?? [
                {
                    mode: 0,
                    constant: 0,
                    constantMin: 0,
                    constantMax: 0,
                    curveLength: 0,
                    preWrapMode: 0,
                    postWrapMode: 0,
                },
                {
                    mode: 0,
                    constant: 0,
                    constantMin: 0,
                    constantMax: 0,
                    curveLength: 0,
                    preWrapMode: 0,
                    postWrapMode: 0,
                },
                {
                    mode: 0,
                    constant: 0,
                    constantMin: 0,
                    constantMax: 0,
                    curveLength: 0,
                    preWrapMode: 0,
                    postWrapMode: 0,
                },
                {
                    mode: 0,
                    constant: 0,
                    constantMin: 0,
                    constantMax: 0,
                    curveLength: 0,
                    preWrapMode: 0,
                    postWrapMode: 0,
                },
            ],
        } as ICustomDataConfig;
    }

    protected onInitialize(): void {}

    protected onUpdate(deltaTime: number, particles: IParticleSOA): void {
        // Module doesn't modify data by default; leaves custom arrays for renderers or user code
    }

    protected onReset(): void {}

    public setCustomData1(
        particles: IParticleSOA,
        index: number,
        x: number,
        y: number,
        z: number,
        w: number
    ): void {
        const off = index * 4;
        particles.customData1[off] = x;
        particles.customData1[off + 1] = y;
        particles.customData1[off + 2] = z;
        particles.customData1[off + 3] = w;
    }

    public setCustomData2(
        particles: IParticleSOA,
        index: number,
        x: number,
        y: number,
        z: number,
        w: number
    ): void {
        const off = index * 4;
        particles.customData2[off] = x;
        particles.customData2[off + 1] = y;
        particles.customData2[off + 2] = z;
        particles.customData2[off + 3] = w;
    }
}
