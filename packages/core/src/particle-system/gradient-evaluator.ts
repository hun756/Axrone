import { IGradient } from './interfaces';
import { IVec4Array, VectorIndex } from './aligned-arrays';
import { Random } from '../random';

export class GradientEvaluator {
    static evaluate(
        gradient: IGradient,
        time: number,
        randomSeed: number
    ): { x: number; y: number; z: number; w: number } {
        switch (gradient.mode) {
            case 0: // Blend
                return this.evaluateBlend(gradient, time);
            case 1: // Fixed
                return this.evaluateFixed(gradient, time);
            case 2: // Random
                return this.evaluateRandom(gradient, randomSeed);
            default:
                return { x: 1, y: 1, z: 1, w: 1 };
        }
    }

    static evaluateBatch(
        gradient: IGradient,
        times: Float32Array,
        randomSeeds: Uint32Array,
        results: IVec4Array,
        count: number
    ): void {
        switch (gradient.mode) {
            case 0:
                for (let i = 0; i < count; i++) {
                    const color = this.evaluateBlend(gradient, times[i]);
                    results.x[i] = color.x;
                    results.y[i] = color.y;
                    results.z![i] = color.z;
                    results.w![i] = color.w;
                }
                break;
            case 1:
                for (let i = 0; i < count; i++) {
                    const color = this.evaluateFixed(gradient, times[i]);
                    results.x[i] = color.x;
                    results.y[i] = color.y;
                    results.z![i] = color.z;
                    results.w![i] = color.w;
                }
                break;
            case 2:
                for (let i = 0; i < count; i++) {
                    const color = this.evaluateRandom(gradient, randomSeeds[i]);
                    results.x[i] = color.x;
                    results.y[i] = color.y;
                    results.z![i] = color.z;
                    results.w![i] = color.w;
                }
                break;
        }
    }

    private static evaluateBlend(gradient: IGradient, time: number) {
        const colorKeyCount = gradient.keyCount;
        if (colorKeyCount === 0) return { x: 1, y: 1, z: 1, w: 1 };

        const keys = gradient.colorKeys;
        let keyIndex = 0;

        while (keyIndex < colorKeyCount - 1 && keys[keyIndex * 5] < time) {
            keyIndex++;
        }

        if (keyIndex === 0) {
            return {
                x: keys[1],
                y: keys[2],
                z: keys[3],
                w: keys[4],
            };
        }

        const prevIndex = (keyIndex - 1) * 5;
        const currIndex = keyIndex * 5;

        const prevTime = keys[prevIndex];
        const currTime = keys[currIndex];
        const t = (time - prevTime) / (currTime - prevTime);
        const it = 1 - t;

        return {
            x: keys[prevIndex + 1] * it + keys[currIndex + 1] * t,
            y: keys[prevIndex + 2] * it + keys[currIndex + 2] * t,
            z: keys[prevIndex + 3] * it + keys[currIndex + 3] * t,
            w: keys[prevIndex + 4] * it + keys[currIndex + 4] * t,
        };
    }

    private static evaluateFixed(gradient: IGradient, time: number) {
        const colorKeyCount = gradient.keyCount;
        if (colorKeyCount === 0) return { x: 1, y: 1, z: 1, w: 1 };

        const keys = gradient.colorKeys;
        let keyIndex = 0;

        while (keyIndex < colorKeyCount - 1 && keys[keyIndex * 5] < time) {
            keyIndex++;
        }

        const index = keyIndex * 5;
        return {
            x: keys[index + 1],
            y: keys[index + 2],
            z: keys[index + 3],
            w: keys[index + 4],
        };
    }

    private static evaluateRandom(gradient: IGradient, randomSeed: number) {
        const colorKeyCount = gradient.keyCount;
        if (colorKeyCount === 0) return { x: 1, y: 1, z: 1, w: 1 };

        const random = new Random(randomSeed).float();
        const keyIndex = Math.floor(random * colorKeyCount);
        const index = keyIndex * 5;

        return {
            x: gradient.colorKeys[index + 1],
            y: gradient.colorKeys[index + 2],
            z: gradient.colorKeys[index + 3],
            w: gradient.colorKeys[index + 4],
        };
    }
}
