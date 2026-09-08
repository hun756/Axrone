export const SOA_EPSILON = 1e-6;
export const SOA_SLERP_THRESHOLD = 0.9995;

export interface SoaBufferOptions {
    capacity: number;
    arrayType?: Float32ArrayConstructor | Float64ArrayConstructor;
}
