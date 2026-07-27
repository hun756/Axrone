export const TerrainErrorCode = {
    INVALID_RESOLUTION: 'INVALID_RESOLUTION',
    INVALID_DIMENSIONS: 'INVALID_DIMENSIONS',
    HEIGHTMAP_SIZE_MISMATCH: 'HEIGHTMAP_SIZE_MISMATCH',
    SOURCE_DECODE_FAILED: 'SOURCE_DECODE_FAILED',
    VALIDATION_FAILED: 'VALIDATION_FAILED',
} as const;

export type TerrainErrorCode = (typeof TerrainErrorCode)[keyof typeof TerrainErrorCode];

export class TerrainError extends Error {
    readonly code: TerrainErrorCode;
    readonly timestamp: number;
    readonly details?: Record<string, unknown>;

    constructor(message: string, code: TerrainErrorCode, details?: Record<string, unknown>) {
        super(`Terrain: ${message}`);
        this.name = 'TerrainError';
        this.code = code;
        this.timestamp = Date.now();
        this.details = details;
        Object.setPrototypeOf(this, TerrainError.prototype);
    }
}
