import { describe, expect, it } from 'vitest';
import { orientQuadTowardCamera } from '../world-quad';

/** Column-major identity matrix. */
const identity = (): Float32Array =>
	new Float32Array([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);

/** Column-major identity with translation. */
const translated = (tx: number, ty: number, tz: number): Float32Array =>
	new Float32Array([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, tx, ty, tz, 1]);

describe('orientQuadTowardCamera', () => {
	it('preserves the model translation', () => {
		const model = translated(10, 20, 30);
		const camera = identity();

		const result = orientQuadTowardCamera(model, camera);

		expect(result[12]).toBeCloseTo(10);
		expect(result[13]).toBeCloseTo(20);
		expect(result[14]).toBeCloseTo(30);
		expect(result[15]).toBeCloseTo(1);
	});

	it('copies camera rotation when model and camera have the same scale', () => {
		const model = identity();
		const camera = identity();

		const result = orientQuadTowardCamera(model, camera);

		// With identity matrices the result should be identity.
		for (let i = 0; i < 16; i += 1) {
			expect(result[i]).toBeCloseTo(camera[i]);
		}
	});

	it('scales axes by modelScale / cameraScale', () => {
		// Model has 2x scale on each axis, camera has 1x.
		const model = new Float32Array([2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1]);
		const camera = identity();

		const result = orientQuadTowardCamera(model, camera);

		// Each axis should be 2x the camera axis (factor = 2/1 = 2).
		expect(result[0]).toBeCloseTo(2); // camera X axis * 2
		expect(result[5]).toBeCloseTo(2); // camera Y axis * 2
		expect(result[10]).toBeCloseTo(2); // camera Z axis * 2
	});

	it('shrinks axes when camera scale is larger', () => {
		const model = identity();
		const camera = new Float32Array([4, 0, 0, 0, 0, 4, 0, 0, 0, 0, 4, 0, 0, 0, 0, 1]);

		const result = orientQuadTowardCamera(model, camera);

		// factor = 1/4 = 0.25
		expect(result[0]).toBeCloseTo(1); // camera[0]=4 * 0.25 = 1
		expect(result[5]).toBeCloseTo(1);
		expect(result[10]).toBeCloseTo(1);
	});

	it('handles zero-scale model axes by falling back to factor=1', () => {
		// A model with a zero-length X axis.
		const model = new Float32Array([0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 5, 5, 5, 1]);
		const camera = identity();

		const result = orientQuadTowardCamera(model, camera);

		// X axis: modelScale=0 → factor=1 (fallback), so result X = camera X * 1
		expect(result[0]).toBeCloseTo(1);
		// Translation preserved
		expect(result[12]).toBeCloseTo(5);
		expect(result[13]).toBeCloseTo(5);
		expect(result[14]).toBeCloseTo(5);
	});

	it('produces a result with w=1 in the last row', () => {
		const model = translated(1, 2, 3);
		const camera = translated(10, 20, 30);

		const result = orientQuadTowardCamera(model, camera);

		expect(result[3]).toBeCloseTo(0);
		expect(result[7]).toBeCloseTo(0);
		expect(result[11]).toBeCloseTo(0);
		expect(result[15]).toBeCloseTo(1);
	});
});
