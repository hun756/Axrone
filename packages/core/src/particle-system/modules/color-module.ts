import { BaseModule } from './base-module';
import { IColorModule, IParticleSOA } from '../interfaces';
import { Gradient } from '../types';

export class ColorModule extends BaseModule implements IColorModule {
    public color: Gradient;

    constructor(config: Partial<IColorModule> = {}) {
        super('ColorModule', config.enabled ?? false);

        this.color = config.color ?? {
            mode: 0,
            color: { r: 1, g: 1, b: 1, a: 1 },
            colorMin: { r: 1, g: 1, b: 1, a: 1 },
            colorMax: { r: 1, g: 1, b: 1, a: 1 },
        };
        Object.assign(this, config);
        if ((config as any).speedRange) {
            (this as any).speedRange = (config as any).speedRange;
        }
    }

    protected onInitialize(): void {}

    protected onUpdate(deltaTime: number, particles: IParticleSOA): void {
        if (!this.enabled) return;

        const colors = particles.colors;
        const ages = particles.ages;
        const lifetimes = particles.lifetimes;
        const activeIndices = particles.getActiveIndices();

        for (const index of activeIndices) {
            const colorOffset = index * 4;
            const normalizedAge = Math.min(ages[index] / lifetimes[index], 1.0);

            const color = this.evaluateColor(normalizedAge);

            colors[colorOffset] = color.r;
            colors[colorOffset + 1] = color.g;
            colors[colorOffset + 2] = color.b;
            colors[colorOffset + 3] = color.a;
        }
    }

    protected onReset(): void {}

    private evaluateColor(t: number): { r: number; g: number; b: number; a: number } {
        switch (this.color.mode) {
            case 0:
                return this.color.color;

            case 2:
                return {
                    r: this.lerp(this.color.colorMin.r, this.color.colorMax.r, t),
                    g: this.lerp(this.color.colorMin.g, this.color.colorMax.g, t),
                    b: this.lerp(this.color.colorMin.b, this.color.colorMax.b, t),
                    a: this.lerp(this.color.colorMin.a, this.color.colorMax.a, t),
                };

            default:
                return this.color.color;
        }
    }

    private lerp(a: number, b: number, t: number): number {
        return a + (b - a) * t;
    }

    public setColor(color: { r: number; g: number; b: number; a: number }): void {
        this.color.color = { ...color };
    }

    public setColorRange(
        colorMin: { r: number; g: number; b: number; a: number },
        colorMax: { r: number; g: number; b: number; a: number }
    ): void {
        this.color.mode = 2;
        this.color.colorMin = { ...colorMin };
        this.color.colorMax = { ...colorMax };
    }
}
