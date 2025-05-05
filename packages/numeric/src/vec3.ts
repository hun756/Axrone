import { Equatable, ICloneable } from '@axrone/utility';

export interface IVec3Like {
    x: number;
    y: number;
    z: number;
}

export class Vec3 implements IVec3Like, ICloneable<Vec3>, Equatable {
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
    
    clone(): Vec3 {
        throw new Error('Method not implemented.');
    }
}
