export interface IVec2Like {
    x: number;
    y: number;
}

export class Vec2 implements IVec2Like {
    constructor(
        public x: number = 0,
        public y: number = 0
    ) {}

    static readonly ZERO: Readonly<Vec2> = Object.freeze(new Vec2(0, 0));
    static readonly ONE: Readonly<Vec2> = Object.freeze(new Vec2(1, 1));
    static readonly NEG_ONE: Readonly<Vec2> = Object.freeze(new Vec2(-1, -1));
    static readonly UNIT_X: Readonly<Vec2> = Object.freeze(new Vec2(1, 0));
    static readonly UNIT_Y: Readonly<Vec2> = Object.freeze(new Vec2(0, 1));
    static readonly UP: Readonly<Vec2> = Object.freeze(new Vec2(0, 1));
    static readonly DOWN: Readonly<Vec2> = Object.freeze(new Vec2(0, -1));
    static readonly LEFT: Readonly<Vec2> = Object.freeze(new Vec2(-1, 0));
    static readonly RIGHT: Readonly<Vec2> = Object.freeze(new Vec2(1, 0));

    static from<T extends IVec2Like>(v: T): Vec2 {
        return new Vec2(v.x, v.y);
    }

    
    static fromArray(arr: ArrayLike<number>, offset: number = 0): Vec2 {
        if (offset < 0) {
            throw new RangeError('Offset cannot be negative');
        }
        
        if (arr.length < offset + 2) {
            throw new RangeError(`Array must have at least ${offset + 2} elements when using offset ${offset}`);
        }
        
        return new Vec2(
            Number(arr[offset]), 
            Number(arr[offset + 1])
        );
    }

    static create(x: number = 0, y: number = 0): Vec2 {
        return new Vec2(x, y);
    }

    static clone<T extends IVec2Like>(v: T): Vec2 {
        return new Vec2(v.x, v.y);
    }
}
