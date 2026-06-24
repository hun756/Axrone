export {
    ComparerError,
    InvalidOperationError,
    FNV_PRIME,
    FNV_OFFSET_BASIS,
    isEquatable,
    isComparer,
    isEqualityComparer,
    hashString,
    hashObject,
} from './shared';
export type {
    CompareResult,
    Comparable,
    Comparer,
    OrderKey,
    EqualityComparer,
    Equatable,
    KeySelector,
    PropertyPath,
    ExtractPropertyType,
    DeepPartial,
    KeysOfType,
    ComparerOptions,
    EqualityComparerOptions,
} from './shared';

export {
    DefaultEqualityComparer,
    DeepEqualityComparer,
    equality,
} from './equality';

export {
    DefaultComparer,
    ReverseComparer,
    CompositeComparer,
    KeyComparer,
    StringComparer,
    NumberComparer,
    DateComparer,
    comparer,
    createOrderKey,
    createPropertyAccessor,
    sorted,
    min,
    max,
} from './comparer';

export {
    ComparisonError,
    createPredicates,
    createOperators,
    floatUtils,
    constants,
    equals,
    notEquals,
    lessThan,
    greaterThan,
    lessThanOrEqual,
    greaterThanOrEqual,
    eq,
    neq,
    lt,
    gt,
    lte,
    gte,
    compare,
    FloatComparer,
    createComparer,
} from './fp-compare';
export type {
    ComparisonResult,
    ComparisonContext,
    ComparisonStrategy,
    Numeric,
    InfinityHandlingMode,
    ComparisonPredicates,
    ComparisonOperators,
    FloatingPointUtils,
} from './fp-compare';

export { CustomComparer, ObjectPropertyComparer, ComparerFactory, EquatableBase } from './comparer-legacy';
export type { IEquatable } from './comparer-legacy';
export { FpCompare } from './fp-compare-legacy';
