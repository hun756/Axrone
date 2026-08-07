import type { UIRuntime } from '@axrone/ui/runtime';

/**
 * Pointer bridge for world-space UI.
 *
 * The caller owns ray construction (it has the camera), this module owns the
 * geometry: it intersects the ray with the UI quad and turns the hit into the
 * canvas-space pointer event a {@link UIRuntime} expects. Kept dependency-free
 * so the ui-webgl2 boundary contract stays intact.
 */
export interface UIWorldRay {
    readonly origin: readonly [number, number, number];
    /** Need not be normalized. */
    readonly direction: readonly [number, number, number];
}

export interface UIWorldHit {
    /** Horizontal position across the quad, 0 at the left edge. */
    readonly u: number;
    /** Vertical position down the quad, 0 at the top edge. */
    readonly v: number;
    /** Distance along the ray in world units. */
    readonly distance: number;
}

/** Structural view of an Axrone `Mat4` without importing @axrone/numeric. */
export interface RowMajorMatrixLike {
    readonly data: ArrayLike<number>;
}

/**
 * Converts an Axrone row-major matrix (`Mat4.data`, translation at 3/7/11) into
 * the column-major `Float32Array` the world-space UI APIs and WebGL expect.
 *
 * Engine matrices are row-major and are transposed on their way to the GPU (see
 * SceneUniformWriter); world-space UI callers pass matrices directly, so this
 * helper keeps that conversion in one place.
 */
export function toColumnMajorMatrix(
    source: RowMajorMatrixLike | ArrayLike<number>
): Float32Array {
    const data = 'data' in source ? source.data : source;
    const result = new Float32Array(16);
    for (let row = 0; row < 4; row += 1) {
        for (let column = 0; column < 4; column += 1) {
            result[column * 4 + row] = data[row * 4 + column] ?? 0;
        }
    }
    return result;
}

/** Column-major 4x4 multiply of a point (w = 1), returning the transformed point. */
const transformPoint = (
    matrix: Float32Array,
    x: number,
    y: number,
    z: number
): [number, number, number] => [
    matrix[0] * x + matrix[4] * y + matrix[8] * z + matrix[12],
    matrix[1] * x + matrix[5] * y + matrix[9] * z + matrix[13],
    matrix[2] * x + matrix[6] * y + matrix[10] * z + matrix[14],
];

/** Column-major 4x4 multiply of a direction (w = 0). */
const transformDirection = (
    matrix: Float32Array,
    x: number,
    y: number,
    z: number
): [number, number, number] => [
    matrix[0] * x + matrix[4] * y + matrix[8] * z,
    matrix[1] * x + matrix[5] * y + matrix[9] * z,
    matrix[2] * x + matrix[6] * y + matrix[10] * z,
];

const dot = (
    a: readonly [number, number, number],
    b: readonly [number, number, number]
): number => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

const normalize = (
    vector: readonly [number, number, number]
): [number, number, number] => {
    const length = Math.hypot(vector[0], vector[1], vector[2]);
    return length > 0
        ? [vector[0] / length, vector[1] / length, vector[2] / length]
        : [0, 0, 0];
};

/**
 * Intersects a world ray with a UI quad centered on the model matrix origin,
 * spanning `width` x `height` world units in the matrix' local XY plane.
 *
 * Returns normalized quad coordinates with a top-left origin (matching UI
 * canvas space) or null when the ray misses the quad or runs parallel to it.
 */
export function intersectRayWithUIQuad(
    ray: UIWorldRay,
    modelMatrix: Float32Array,
    width: number,
    height: number
): UIWorldHit | null {
    if (!(width > 0) || !(height > 0)) {
        return null;
    }

    const center = transformPoint(modelMatrix, 0, 0, 0);
    // Local axes carry the entity scale; keep it so a scaled entity scales the UI.
    const axisX = transformDirection(modelMatrix, 1, 0, 0);
    const axisY = transformDirection(modelMatrix, 0, 1, 0);
    const normal = normalize(transformDirection(modelMatrix, 0, 0, 1));
    if (normal[0] === 0 && normal[1] === 0 && normal[2] === 0) {
        return null;
    }

    const direction = normalize(ray.direction);
    const denominator = dot(normal, direction);
    if (Math.abs(denominator) < 1e-8) {
        return null;
    }

    const toCenter: [number, number, number] = [
        center[0] - ray.origin[0],
        center[1] - ray.origin[1],
        center[2] - ray.origin[2],
    ];
    const distance = dot(normal, toCenter) / denominator;
    if (distance < 0) {
        return null;
    }

    const hitPoint: [number, number, number] = [
        ray.origin[0] + direction[0] * distance,
        ray.origin[1] + direction[1] * distance,
        ray.origin[2] + direction[2] * distance,
    ];
    const offset: [number, number, number] = [
        hitPoint[0] - center[0],
        hitPoint[1] - center[1],
        hitPoint[2] - center[2],
    ];

    // Project onto the (possibly scaled) local axes to get local quad units.
    const axisXLengthSquared = dot(axisX, axisX);
    const axisYLengthSquared = dot(axisY, axisY);
    if (axisXLengthSquared <= 0 || axisYLengthSquared <= 0) {
        return null;
    }
    const localX = dot(offset, axisX) / axisXLengthSquared;
    const localY = dot(offset, axisY) / axisYLengthSquared;

    const halfWidth = width / 2;
    const halfHeight = height / 2;
    if (
        localX < -halfWidth ||
        localX > halfWidth ||
        localY < -halfHeight ||
        localY > halfHeight
    ) {
        return null;
    }

    return {
        u: (localX + halfWidth) / width,
        // Local +Y points up, canvas +V points down.
        v: 1 - (localY + halfHeight) / height,
        distance,
    };
}

export interface UIWorldPointerEvent {
    readonly phase: 'move' | 'down' | 'up' | 'leave';
    readonly pointerId?: number;
    readonly button?: number;
    readonly buttons?: number;
}

/**
 * Feeds a world-space hit into a UI runtime as a canvas-space pointer event.
 *
 * `hit === null` dispatches a `move` to a point outside the canvas: the runtime
 * only recomputes hover on `move`, so this is what makes it emit `pointerLeave`
 * for whatever was hovered and clears the hover state. Sending a `leave` phase
 * directly would be dropped, because the runtime resolves that phase through a
 * hit test that necessarily fails on a miss.
 */
export function dispatchWorldPointerToUIRuntime(
    runtime: UIRuntime | null,
    hit: UIWorldHit | null,
    event: UIWorldPointerEvent,
    referenceWidth: number,
    referenceHeight: number
): boolean {
    if (!runtime) {
        return false;
    }
    const phase = hit ? event.phase : 'move';
    // A miss must land outside the canvas: (0, 0) would still sit inside any
    // widget anchored to the top-left corner and would not clear hover state.
    const x = hit ? hit.u * referenceWidth : -1;
    const y = hit ? hit.v * referenceHeight : -1;
    runtime.dispatchInput({
        type: 'pointer',
        phase,
        x,
        y,
        pointerId: event.pointerId ?? 1,
        button: event.button ?? 0,
        buttons: event.buttons ?? (phase === 'down' ? 1 : 0),
        deltaX: 0,
        deltaY: 0,
        altKey: false,
        ctrlKey: false,
        shiftKey: false,
        metaKey: false,
    } as Parameters<UIRuntime['dispatchInput']>[0]);
    return hit !== null;
}
