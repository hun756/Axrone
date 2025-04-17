type NumericBounds<T extends number> = {
    readonly min: T;
    readonly max: T;
};

type ValidateNumber<T> = T extends number ? T : never;

export class NumericRangeError extends Error {
    constructor(message: string) {
        super(message);
        this.name = 'NumericRangeError';
        Object.setPrototypeOf(this, NumericRangeError.prototype);
    }
}

const isFiniteNumber = (value: unknown): value is number => 
    typeof value === 'number' && Number.isFinite(value);


const normalizeBounds = <T extends number>(min: T, max: T): Readonly<NumericBounds<T>> => {
    if (!isFiniteNumber(min) || !isFiniteNumber(max)) {
        throw new NumericRangeError('Bounds must be finite numbers');
    }

    return min <= max 
        ? { min, max } as const
        : { min: max, max: min } as const;
};

export const clamp = <T extends number>(
    value: ValidateNumber<T>,
    min: ValidateNumber<T>,
    max: ValidateNumber<T>
): T => {
    if (!isFiniteNumber(value)) {
        throw new NumericRangeError('Value must be a finite number');
    }

    const { min: lowerBound, max: upperBound } = normalizeBounds(min, max);

    return Math.min(Math.max(value, lowerBound), upperBound) as T;
};

export const createBoundedClamp = <T extends number>(
    min: ValidateNumber<T>,
    max: ValidateNumber<T>
): ((value: ValidateNumber<T>) => T) => {
    const { min: lowerBound, max: upperBound } = normalizeBounds(min, max);
    
    return (value: ValidateNumber<T>): T => {
        if (!isFiniteNumber(value)) {
            throw new NumericRangeError('Value must be a finite number');
        }
        return Math.min(Math.max(value, lowerBound), upperBound) as T;
    };
};
