import { IDistribution, IRandomState, RandomResult } from '../types';
import { validateInteger } from '../constants';
import { createEngineFactory } from '../engines';

export class IntegerDistribution implements IDistribution<number> {
    constructor(
        private readonly min: number,
        private readonly max: number
    ) {
        validateInteger(min, 'min');
        validateInteger(max, 'max');

        if (min > max) {
            throw new RangeError('Min must be less than or equal to max');
        }
    }

    public sample = (state: IRandomState): RandomResult<number> => {
        const engine = createEngineFactory(state.engine)();
        engine.setState(state);

        let value: number;
        const range = this.max - this.min + 1;

        if (range <= 0) {
            throw new RangeError('Range is too large and would cause integer overflow');
        }

        if (range <= 0x100000000) {
            value = this.min + Math.floor(range * engine.next01());
        } else {
            // Use BigInt for 64-bit composition to avoid precision loss above 2^53
            const bigRange = BigInt(range);
            const limit = (BigInt(1) << 64n) - ((BigInt(1) << 64n) % bigRange);
            let x: bigint;
            do {
                const lo = BigInt(engine.nextUint32());
                const hi = BigInt(engine.nextUint32());
                x = (hi << 32n) | lo;
            } while (x >= limit);

            value = this.min + Number(x % bigRange);
        }

        return [value, engine.getState()];
    };
}
