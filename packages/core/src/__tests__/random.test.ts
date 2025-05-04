import {
    createRandom,
    rand,
    RandomEngineType,
    RandomBuilder,
    UniformDistribution,
    IntegerDistribution,
    NormalDistribution,
    ExponentialDistribution,
    PoissonDistribution,
    BernoulliDistribution,
    BinomialDistribution,
    GeometricDistribution,
    IRandomAPI,
    IRandomState,
} from '../random/random';

describe('Random Core API', () => {
    it('is deterministic for same seed and engine', () => {
        const r1 = createRandom(42, RandomEngineType.XOROSHIRO128_PLUS_PLUS);
        const r2 = createRandom(42, RandomEngineType.XOROSHIRO128_PLUS_PLUS);

        expect(r1.float()).toBe(r2.float());
        expect(r1.int(1, 10)).toBe(r2.int(1, 10));
        expect(r1.boolean(0.3)).toBe(r2.boolean(0.3));
    });

    it('setSeed resets sequence', () => {
        const r = createRandom();
        const before = r.float();
        r.setSeed(123);
        const firstA = r.float();
        r.setSeed(123);
        const firstB = r.float();
        expect(firstA).toBe(firstB);
        expect(firstA).not.toBe(before);
    });

    it('getState/setState round-trip reproduces values', () => {
        const r = createRandom(7);
        r.int(0, 100);
        const state = r.getState();
        const a = r.int(0, 100);
        r.setState(state);
        const b = r.int(0, 100);
        expect(a).toBe(b);
    });

    it('setEngine switches engine type and preserves as much state as possible', () => {
        const r = createRandom(100, RandomEngineType.XOROSHIRO128_PLUS_PLUS);
        const before = r.float();
        r.setEngine(RandomEngineType.PCG_XSH_RR);
        const after = r.float();
        expect(r.getState().engine).toBe(RandomEngineType.PCG_XSH_RR);
        expect(typeof after).toBe('number');
        expect(Number.isFinite(after)).toBe(true);
    });

    it('fork creates an independent PRNG', () => {
        const parent = createRandom(99);
        const forked = parent.fork();
        const p1 = parent.int(1, 100);
        const f1 = forked.int(1, 100);
        expect(typeof p1).toBe('number');
        expect(typeof f1).toBe('number');
        const p2 = parent.int(1, 100);
        const f2 = forked.int(1, 100);
        expect(p2).not.toBe(f1);
        expect(f2).not.toBe(p1);
    });
});

describe('Collection methods: pick, weighted, shuffle, sample', () => {
    const seed = 2021;
    let r: IRandomAPI;
    beforeEach(() => {
        r = createRandom(seed);
    });

    it('pick chooses a valid element', () => {
        const arr = ['a', 'b', 'c', 'd'];
        const v = r.pick(arr);
        expect(arr).toContain(v);
    });

    it('weighted picks according to weights', () => {
        const items: [string, number][] = [
            ['x', 0],
            ['y', 1],
            ['z', 0],
        ];
        // only 'y' has positive weight
        expect(r.weighted(items)).toBe('y');
    });

    it('shuffle returns a permutation', () => {
        const arr = [1, 2, 3, 4, 5];
        const s = r.shuffle(arr);
        expect(s.sort()).toEqual(arr);
    });

    it('sample returns correct number of distinct items or full shuffle', () => {
        const arr = [1, 2, 3, 4];
        const few = r.sample(arr, 2);
        expect(few.length).toBe(2);
        const all = r.sample(arr, 10);
        expect(all.sort()).toEqual(arr);
    });
});

