import { Vec3 } from '@axrone/numeric';
import { BaseModule } from './base-module';
import { INoiseModule, IParticleSOA } from '../interfaces';

function hash(n: number): number {
    n = (n << 13) ^ n;
    return 1.0 - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824.0;
}

function noise3(x: number, y: number, z: number): number {
    const ix = Math.floor(x),
        iy = Math.floor(y),
        iz = Math.floor(z);
    const fx = x - ix,
        fy = y - iy,
        fz = z - iz;

    const v000 = hash(ix + hash(iy + hash(iz)));
    const v100 = hash(ix + 1 + hash(iy + hash(iz)));
    const v010 = hash(ix + hash(iy + 1 + hash(iz)));
    const v110 = hash(ix + 1 + hash(iy + 1 + hash(iz)));
    const v001 = hash(ix + hash(iy + hash(iz + 1)));
    const v101 = hash(ix + 1 + hash(iy + hash(iz + 1)));
    const v011 = hash(ix + hash(iy + 1 + hash(iz + 1)));
    const v111 = hash(ix + 1 + hash(iy + 1 + hash(iz + 1)));

    const wx = fx * (3 - 2 * fx);
    const wy = fy * (3 - 2 * fy);
    const wz = fz * (3 - 2 * fz);

    function lerp(a: number, b: number, t: number) {
        return a + (b - a) * t;
    }

    const x00 = lerp(v000, v100, wx);
    const x10 = lerp(v010, v110, wx);
    const x01 = lerp(v001, v101, wx);
    const x11 = lerp(v011, v111, wx);

    const y0 = lerp(x00, x10, wy);
    const y1 = lerp(x01, x11, wy);

    return lerp(y0, y1, wz);
}

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

    constructor(config: Partial<INoiseModule> = {}) {
        super('NoiseModule', config.enabled ?? false);
        Object.assign(this, config);
    }

    protected onInitialize(): void {
        this._time = 0;
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

            const n = noise3(x, y, z);
            velocities[off] += (n * 2 - 1) * this.strength.x * deltaTime;
            velocities[off + 1] += (n * 2 - 1) * this.strength.y * deltaTime;
            velocities[off + 2] += (n * 2 - 1) * this.strength.z * deltaTime;
        }
    }

    protected onReset(): void {
        this._time = 0;
    }
}
