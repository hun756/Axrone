import { BaseModule } from './base-module';
import { ITrailConfig, IParticleSOA } from '../interfaces';
import { Curve, Gradient } from '../types';

export class TrailModule extends BaseModule {
    public mode: number = 0;
    public ratio: number = 1;
    public lifetime: Curve;
    public lifetimeMultiplier: number = 1;
    public minVertexDistance: number = 0.1;
    public textureMode: number = 0;
    public worldSpace: boolean = true;
    public dieWithParticles: boolean = true;
    public sizeAffectsWidth: boolean = false;
    public sizeAffectsLifetime: boolean = false;
    public inheritParticleColor: boolean = true;
    public colorOverLifetime: Gradient;
    public widthOverTrail: Curve;
    public colorOverTrail: Gradient;
    public generateLightingData: boolean = false;
    public shadowBias: number = 0;
    public splitSubEmitterRibbons: boolean = false;
    public attachRibbonsToTransform: boolean = false;
    public ribbonCount: number = 1;

    constructor(config: Partial<ITrailConfig> = {}) {
        super('TrailModule', config.enabled ?? false);

        const defaultCurve: Curve = {
            mode: 0,
            constant: 1,
            constantMin: 0,
            constantMax: 0,
            curveMultiplier: 1,
        };

        const defaultGradient: Gradient = {
            mode: 0,
            color: { r: 1, g: 1, b: 1, a: 1 },
            colorMin: { r: 1, g: 1, b: 1, a: 1 },
            colorMax: { r: 1, g: 1, b: 1, a: 1 },
        };

        this.mode = (config as any).mode ?? this.mode;
        this.ratio = (config as any).ratio ?? this.ratio;
        this.lifetime = (config as any).lifetime ?? defaultCurve;
        this.lifetimeMultiplier = (config as any).lifetimeMultiplier ?? this.lifetimeMultiplier;
        this.minVertexDistance = (config as any).minVertexDistance ?? this.minVertexDistance;
        this.textureMode = (config as any).textureMode ?? this.textureMode;
        this.worldSpace = (config as any).worldSpace ?? this.worldSpace;
        this.dieWithParticles = (config as any).dieWithParticles ?? this.dieWithParticles;
        this.sizeAffectsWidth = (config as any).sizeAffectsWidth ?? this.sizeAffectsWidth;
        this.sizeAffectsLifetime = (config as any).sizeAffectsLifetime ?? this.sizeAffectsLifetime;
        this.inheritParticleColor =
            (config as any).inheritParticleColor ?? this.inheritParticleColor;
        this.colorOverLifetime = (config as any).colorOverLifetime ?? defaultGradient;
        this.widthOverTrail = (config as any).widthOverTrail ?? defaultCurve;
        this.colorOverTrail = (config as any).colorOverTrail ?? defaultGradient;
        this.generateLightingData =
            (config as any).generateLightingData ?? this.generateLightingData;
        this.shadowBias = (config as any).shadowBias ?? this.shadowBias;
        this.splitSubEmitterRibbons =
            (config as any).splitSubEmitterRibbons ?? this.splitSubEmitterRibbons;
        this.attachRibbonsToTransform =
            (config as any).attachRibbonsToTransform ?? this.attachRibbonsToTransform;
        this.ribbonCount = (config as any).ribbonCount ?? this.ribbonCount;

        // copy extras
        Object.assign(this, config);
    }

    protected onInitialize(): void {
        // initialize trail resources if needed
    }

    protected onUpdate(deltaTime: number, particles: IParticleSOA): void {
        if (!this.enabled) return;
        // placeholder: trail update logic would go here
    }

    protected onReset(): void {
        // reset internal trail state
    }
}
