import { Equatable, ICloneable } from '@axrone/utility';

export interface IVec4Like {
    x: number;
    y: number;
    z: number;
}

export class Vec4 implements IVec4Like, ICloneable<Vec4>, Equatable {
    constructor(
        public x: number = 0,
        public y: number = 0,
        public z: number = 0
    ) {}

    equals(other: unknown): boolean {
        throw new Error('Method not implemented.');
    }

    getHashCode(): number {
        throw new Error('Method not implemented.');
    }
    
    clone(): Vec4 {
        throw new Error('Method not implemented.');
    }
}
