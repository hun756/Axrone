// Export types
export * from './types';

// Export constants and utilities
export * from './constants';
export * from './seed-utils';

// Export engines
export * from './engines';

// Export distributions
export * from './distributions';

// Export main API
export * from './random-api';

// Create convenience exports
import { Random, RandomBuilder } from './random-api';
import { RandomEngineType } from './types';

export const createRandom = (
    seed?: any,
    engineType: RandomEngineType = RandomEngineType.XOROSHIRO128_PLUS_PLUS
) => {
    return new Random(seed, engineType);
};

/** Lazy global `rand` instance — created on first access for tree-shaking. */
let _lazyRand: Random | undefined;
export const rand: Random = new Proxy({} as Random, {
    get(_target, prop, receiver) {
        if (!_lazyRand) {
            _lazyRand = new Random();
        }
        const value = Reflect.get(_lazyRand, prop, _lazyRand);
        return typeof value === 'function' ? value.bind(_lazyRand) : value;
    },
});
export default rand;
