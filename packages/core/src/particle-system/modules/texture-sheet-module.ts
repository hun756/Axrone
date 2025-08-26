import { Vec3 } from '@axrone/numeric';
import { BaseModule } from './base-module';
import { ITextureSheetModule, IParticleSOA } from '../interfaces';
import { Curve } from '../types';

export class TextureSheetModule extends BaseModule implements ITextureSheetModule {
    public numTilesX: number = 1;
    public numTilesY: number = 1;
    public animation: number = 0;
    public useRandomRow: boolean = false;
    public frameOverTime: Curve;
    public startFrame: Curve;
    public cycleCount: number = 1;
    public flipU: number = 0;
    public flipV: number = 0;
    public uvChannelMask: number = 0;
    public tiles: Vec3 = new Vec3(1, 1, 1);
    public animationType: number = 0;
    public rowMode: number = 0;
    public sprites: any[] = [];
    public speedRange: Vec3 = new Vec3(0, 1, 0);
    public fps: number = 0;
    public timeMode: number = 0;
    public spriteCount: number = 0;

    constructor(config: Partial<ITextureSheetModule> = {}) {
        super('TextureSheetModule', config.enabled ?? false);
        const defaultCurve: Curve = {
            mode: 0,
            constant: 0,
            constantMin: 0,
            constantMax: 0,
            curveMultiplier: 1,
        };
        this.frameOverTime = config.frameOverTime ?? defaultCurve;
        this.startFrame = config.startFrame ?? defaultCurve;
        Object.assign(this, config);

        this.fps = (config as any).fps ?? this.fps;
        this.timeMode = (config as any).timeMode ?? this.timeMode;
        this.spriteCount = (config as any).spriteCount ?? this.spriteCount;
        this.uvChannelMask = (config as any).uvChannelMask ?? this.uvChannelMask;
        this.animationType = (config as any).mode ?? this.animationType;
    }

    protected onInitialize(): void {}

    protected onUpdate(deltaTime: number, particles: IParticleSOA): void {
        if (!this.enabled) return;
        // Basic placeholder - update per-particle frames if needed
    }

    protected onReset(): void {}
}
