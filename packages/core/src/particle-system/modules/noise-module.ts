import { Vec3 } from '@axrone/numeric';
import { PerlinNoise } from '@axrone/numeric';
import { BaseModule } from './base-module';
import { INoiseModule, IParticleSOA } from '../interfaces';

export class NoiseModule extends BaseModule implements INoiseModule {
    public separateAxes: boolean = false;
    public strength: Vec3 = new Vec3(1, 1, 1);
    public frequency: number = 1;
    public scrollSpeed: Vec3 = new Vec3(0, 0, 0);
    public damping: boolean = false;
    public octaves: number = 1;
    public octaveMultiplier: number = 0.5;
    public octaveScale: number = 2;
    public quality: number = 0;
    public remap: Vec3 = new Vec3(1, 1, 1);
    public remapEnabled: boolean = false;
    public positionAmount: Vec3 = new Vec3(0, 0, 0);
    public rotationAmount: Vec3 = new Vec3(0, 0, 0);
    public sizeAmount: Vec3 = new Vec3(0, 0, 0);

    private _time: number = 0;
    private _noise: PerlinNoise;

    constructor(config: Partial<INoiseModule> = {}) {
        super('NoiseModule', config.enabled ?? false);
        Object.assign(this, config);

        this._noise = new PerlinNoise({
            octaves: this.octaves,
            persistence: this.octaveMultiplier,
            lacunarity: this.octaveScale,
            seed: Math.random() * 1000,
        });

        if ((config as any).strengthCurve) {
            (this as any).strengthCurve = (config as any).strengthCurve;
        }
        if ((config as any).scrollSpeedCurve) {
            (this as any).scrollSpeedCurve = (config as any).scrollSpeedCurve;
        }
        if ((config as any).remapCurve) {
            (this as any).remapCurve = (config as any).remapCurve;
        }
    }

    protected onInitialize(): void {
        this._time = 0;
        this._noise = new PerlinNoise({
            octaves: this.octaves,
            persistence: this.octaveMultiplier,
            lacunarity: this.octaveScale,
            seed: Math.random() * 1000,
        });
    }

    protected onUpdate(deltaTime: number, particles: IParticleSOA): void {
        if (!this.enabled) return;
        this._time += deltaTime;

        const positions = particles.positions;
        const velocities = particles.velocities;
        const active = particles.getActiveIndices();

        for (const idx of active) {
            const off = idx * 3;
            const x = positions[off] * this.frequency + this._time * this.scrollSpeed.x;
            const y = positions[off + 1] * this.frequency + this._time * this.scrollSpeed.y;
            const z = positions[off + 2] * this.frequency + this._time * this.scrollSpeed.z;

            const n = this._noise.noise3D(x, y, z);
            velocities[off] += n * this.strength.x * deltaTime;
            velocities[off + 1] += n * this.strength.y * deltaTime;
            velocities[off + 2] += n * this.strength.z * deltaTime;
        }
    }

    protected onReset(): void {
        this._time = 0;
    }
}
