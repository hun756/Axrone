export const BUFFER_DEFAULTS = {
    INITIAL_CAPACITY: 32,
    MAX_CAPACITY: 0x7fffffff, // 2^31 - 1
    EXPANSION_FACTOR: 1.5,
    MIN_EXPANSION: 128,
    WORD_SIZE: 4,
    ALIGNMENT: 8,
} as const;

export const POOL_DEFAULTS = {
    BUCKET_COUNT: 32,
} as const;

export const STRING_DEFAULTS = {
    MAX_WRITE_LENGTH: 1024 * 1024,
    ENCODING: 'utf8',
} as const;

export const PERFORMANCE_DEFAULTS = {
    CACHE_SIZE: 64,
} as const;
