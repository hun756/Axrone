import { Random } from '../random';

export class PerlinNoise3D {
    private readonly permutation: Uint8Array;
    private readonly gradients: Float32Array;

    constructor(seed: number = 0) {
        this.permutation = new Uint8Array(512);
        this.gradients = new Float32Array(256 * 3);

    const random = new Random(seed);

        for (let i = 0; i < 256; i++) {
            this.permutation[i] = i;

            const angle1 = random.float() * Math.PI * 2;
            const angle2 = random.float() * Math.PI;

            this.gradients[i * 3] = Math.sin(angle2) * Math.cos(angle1);
            this.gradients[i * 3 + 1] = Math.sin(angle2) * Math.sin(angle1);
            this.gradients[i * 3 + 2] = Math.cos(angle2);
        }

        for (let i = 255; i > 0; i--) {
            const j = Math.floor(random.float() * (i + 1));
            [this.permutation[i], this.permutation[j]] = [this.permutation[j], this.permutation[i]];
        }

        for (let i = 0; i < 256; i++) {
            this.permutation[256 + i] = this.permutation[i];
        }
    }

    sample(x: number, y: number, z: number): number {
        const X = Math.floor(x) & 255;
        const Y = Math.floor(y) & 255;
        const Z = Math.floor(z) & 255;

        x -= Math.floor(x);
        y -= Math.floor(y);
        z -= Math.floor(z);

        const u = this.fade(x);
        const v = this.fade(y);
        const w = this.fade(z);

        const A = this.permutation[X] + Y;
        const AA = this.permutation[A] + Z;
        const AB = this.permutation[A + 1] + Z;
        const B = this.permutation[X + 1] + Y;
        const BA = this.permutation[B] + Z;
        const BB = this.permutation[B + 1] + Z;

        return this.lerp(
            w,
            this.lerp(
                v,
                this.lerp(
                    u,
                    this.grad(this.permutation[AA], x, y, z),
                    this.grad(this.permutation[BA], x - 1, y, z)
                ),
                this.lerp(
                    u,
                    this.grad(this.permutation[AB], x, y - 1, z),
                    this.grad(this.permutation[BB], x - 1, y - 1, z)
                )
            ),
            this.lerp(
                v,
                this.lerp(
                    u,
                    this.grad(this.permutation[AA + 1], x, y, z - 1),
                    this.grad(this.permutation[BA + 1], x - 1, y, z - 1)
                ),
                this.lerp(
                    u,
                    this.grad(this.permutation[AB + 1], x, y - 1, z - 1),
                    this.grad(this.permutation[BB + 1], x - 1, y - 1, z - 1)
                )
            )
        );
    }

    private fade(t: number): number {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    private lerp(t: number, a: number, b: number): number {
        return a + t * (b - a);
    }

    private grad(hash: number, x: number, y: number, z: number): number {
        const gi = (hash & 255) * 3;
        return this.gradients[gi] * x + this.gradients[gi + 1] * y + this.gradients[gi + 2] * z;
    }

    sampleOctaves(
        x: number,
        y: number,
        z: number,
        octaves: number,
        persistence: number,
        scale: number
    ): number {
        let value = 0;
        let amplitude = 1;
        let frequency = scale;
        let maxValue = 0;

        for (let i = 0; i < octaves; i++) {
            value += this.sample(x * frequency, y * frequency, z * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= 2;
        }

        return value / maxValue;
    }
}
