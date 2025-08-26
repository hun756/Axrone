import { ICurve } from './interfaces';
import { Random } from '../random';

export class CurveEvaluator {
    private static readonly tempKeys = new Float32Array(1024);
    private static readonly tempTimes = new Float32Array(1024);

    static evaluate(curve: ICurve, time: number, randomSeed: number): number {
        switch (curve.mode) {
            case 0: // Constant
                return curve.constant;
            case 2: // Random
                return (
                    curve.constantMin +
                    (curve.constantMax - curve.constantMin) * new Random(randomSeed).float()
                );
            case 1: // Curve
                return curve.curve
                    ? this.sampleCurve(curve.curve, time, curve.curveLength)
                    : curve.constant;
            case 3: // RandomCurve
                const min = curve.curveMin
                    ? this.sampleCurve(curve.curveMin, time, curve.curveLength)
                    : curve.constantMin;
                const max = curve.curveMax
                    ? this.sampleCurve(curve.curveMax, time, curve.curveLength)
                    : curve.constantMax;
                return min + (max - min) * new Random(randomSeed).float();
            default:
                return curve.constant;
        }
    }

    static evaluateBatch(
        curve: ICurve,
        times: Float32Array,
        randomSeeds: Uint32Array,
        results: Float32Array,
        count: number
    ): void {
        switch (curve.mode) {
            case 0:
                results.fill(curve.constant, 0, count);
                break;
            case 2:
                const range = curve.constantMax - curve.constantMin;
                for (let i = 0; i < count; i++) {
                    results[i] = curve.constantMin + range * new Random(randomSeeds[i]).float();
                }
                break;
            case 1:
                if (curve.curve) {
                    for (let i = 0; i < count; i++) {
                        results[i] = this.sampleCurve(curve.curve, times[i], curve.curveLength);
                    }
                } else {
                    results.fill(curve.constant, 0, count);
                }
                break;
            case 3:
                for (let i = 0; i < count; i++) {
                    const min = curve.curveMin
                        ? this.sampleCurve(curve.curveMin, times[i], curve.curveLength)
                        : curve.constantMin;
                    const max = curve.curveMax
                        ? this.sampleCurve(curve.curveMax, times[i], curve.curveLength)
                        : curve.constantMax;
                    results[i] = min + (max - min) * new Random(randomSeeds[i]).float();
                }
                break;
        }
    }

    private static sampleCurve(curve: Float32Array, time: number, length: number): number {
        if (length === 0) return 0;
        if (length === 1) return curve[0];

        time = Math.max(0, Math.min(1, time));
        const scaledTime = time * (length - 1);
        const index = Math.floor(scaledTime);
        const t = scaledTime - index;

        if (index >= length - 1) return curve[length - 1];

        return curve[index] * (1 - t) + curve[index + 1] * t;
    }
}
