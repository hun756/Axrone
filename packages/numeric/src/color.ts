import { Equatable, ICloneable } from '@axrone/utility';

interface IColorLike {
    r: number;
    g: number;
    b: number;
    a?: number;
}

const _clampColor = (value: number): number => {
    return Math.max(0, Math.min(1, value));
};

export class Color implements IColorLike, ICloneable<Color>, Equatable {
    constructor(
        public r: number = 0,
        public g: number = 0,
        public b: number = 0,
        public a: number = 1
    ) {
        this.r = _clampColor(r);
        this.g = _clampColor(g);
        this.b = _clampColor(b);
        this.a = _clampColor(a);
    }

    equals(other: unknown): boolean {
        throw new Error('Method not implemented.');
    }

    getHashCode(): number {
        throw new Error('Method not implemented.');
    }

    clone(): Color {
        throw new Error('Method not implemented.');
    }
}
