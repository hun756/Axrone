import * as vec2 from '../vec2_legacy';
import { Vec2, ReadonlyVec2, HALF_PI } from '../vec2_legacy';

describe('Vector Angle and Rotation Functions', () => {
    const vX: ReadonlyVec2 = Object.freeze({ x: 1, y: 0 });
    const vY: ReadonlyVec2 = Object.freeze({ x: 0, y: 1 });
    const vNegX: ReadonlyVec2 = Object.freeze({ x: -1, y: 0 });
    const vNegY: ReadonlyVec2 = Object.freeze({ x: 0, y: -1 });
    const v11: ReadonlyVec2 = Object.freeze({ x: 1, y: 1 }); // 45 deg, length sqrt(2)
    const v1Neg1: ReadonlyVec2 = Object.freeze({ x: 1, y: -1 }); // -45 deg
    const vZero = vec2.ZERO;

    let out: Vec2;
    beforeEach(() => {
        out = vec2.create();
    });

    // --- Angle ---
    test('angle should return the angle in radians [-PI, PI]', () => {
        expect(vec2.angle(vX)).toBeCloseTo(0);
        expect(vec2.angle(vY)).toBeCloseTo(HALF_PI);
        expect(vec2.angle(vNegX)).toBeCloseTo(Math.PI);
        expect(vec2.angle(vNegY)).toBeCloseTo(-HALF_PI);
        expect(vec2.angle(v11)).toBeCloseTo(Math.PI / 4);
        expect(vec2.angle(v1Neg1)).toBeCloseTo(-Math.PI / 4);
        expect(vec2.angle(vZero)).toBeCloseTo(0); // atan2(0,0) = 0
    });

    test('fastAngle should approximate the angle in radians', () => {
        expect(vec2.fastAngle(vX)).toBeCloseTo(vec2.angle(vX), 3);
        expect(vec2.fastAngle(vY)).toBeCloseTo(vec2.angle(vY), 3);
        expect(vec2.fastAngle(vNegX)).toBeCloseTo(vec2.angle(vNegX), 3);
        expect(vec2.fastAngle(vNegY)).toBeCloseTo(vec2.angle(vNegY), 3);
        expect(vec2.fastAngle(v11)).toBeCloseTo(vec2.angle(v11), 3);
        expect(vec2.fastAngle(v1Neg1)).toBeCloseTo(vec2.angle(v1Neg1), 3);
        expect(vec2.fastAngle(vZero)).toBeCloseTo(0);
    });

    test('angleDeg should return the angle in degrees [-180, 180]', () => {
        expect(vec2.angleDeg(vX)).toBeCloseTo(0);
        expect(vec2.angleDeg(vY)).toBeCloseTo(90);
        expect(vec2.angleDeg(vNegX)).toBeCloseTo(180);
        expect(vec2.angleDeg(vNegY)).toBeCloseTo(-90);
        expect(vec2.angleDeg(v11)).toBeCloseTo(45);
        expect(vec2.angleDeg(v1Neg1)).toBeCloseTo(-45);
    });

    test('angleBetween should return the unsigned angle in radians [0, PI]', () => {
        expect(vec2.angleBetween(vX, vY)).toBeCloseTo(HALF_PI);
        expect(vec2.angleBetween(vX, v11)).toBeCloseTo(Math.PI / 4);
        expect(vec2.angleBetween(vX, vX)).toBeCloseTo(0);
        expect(vec2.angleBetween(vX, vNegX)).toBeCloseTo(Math.PI);
        expect(vec2.angleBetween(vX, vZero)).toBeCloseTo(0); // Handle zero vector
        expect(vec2.angleBetween(vZero, vY)).toBeCloseTo(0); // Handle zero vector
    });

    test('fastAngleBetween should approximate the signed angle in radians [-PI/2, PI/2]', () => {
        expect(vec2.fastAngleBetween(vX, vY)).toBeCloseTo(HALF_PI, 3); // cross=1, dot=0 -> asin(1) = PI/2
        expect(vec2.fastAngleBetween(vX, v11)).toBeCloseTo(Math.PI / 4, 3); // cross=1, dot=1, lenProd=sqrt(2) -> asin(1/sqrt(2)) = PI/4
        expect(vec2.fastAngleBetween(vY, vX)).toBeCloseTo(-HALF_PI, 3); // cross=-1 -> asin(-1) = -PI/2
        expect(vec2.fastAngleBetween(vX, vX)).toBeCloseTo(0, 3);
        // Cannot represent PI using asin, so comparison breaks down
        // expect(vec2.fastAngleBetween(vX, vNegX)).toBeCloseTo(Math.PI, 3); // Fails, cross=0 -> asin(0)=0
    });

    test('angleBetweenSigned should return the signed angle in radians [-PI, PI]', () => {
        expect(vec2.angleBetweenSigned(vX, vY)).toBeCloseTo(HALF_PI); // vX to vY is +90deg CCW
        expect(vec2.angleBetweenSigned(vY, vX)).toBeCloseTo(-HALF_PI); // vY to vX is -90deg CW
        expect(vec2.angleBetweenSigned(vX, v11)).toBeCloseTo(Math.PI / 4);
        expect(vec2.angleBetweenSigned(v11, vX)).toBeCloseTo(-Math.PI / 4);
        expect(vec2.angleBetweenSigned(vX, vX)).toBeCloseTo(0);
        expect(vec2.angleBetweenSigned(vX, vNegX)).toBeCloseTo(Math.PI); // or -PI

        expect(vec2.angleBetweenSigned(vX, vZero)).toBeCloseTo(0);
        expect(vec2.angleBetweenSigned(vZero, vY)).toBeCloseTo(0);
    });

    // --- Rotation ---
    test('rotate should rotate vector around origin', () => {
        vec2.rotate(out, vX, HALF_PI); // Rotate (1,0) by 90 deg
        expect(out.x).toBeCloseTo(0);
        expect(out.y).toBeCloseTo(1);

        vec2.rotate(out, vX, Math.PI); // Rotate (1,0) by 180 deg
        expect(out.x).toBeCloseTo(-1);
        expect(out.y).toBeCloseTo(0);

        vec2.rotate(out, v11, -Math.PI / 4); // Rotate (1,1) by -45 deg -> onto X axis scaled
        expect(out.x).toBeCloseTo(Math.sqrt(2));
        expect(out.y).toBeCloseTo(0);

        vec2.rotate(out, vZero, Math.PI / 3); // Rotate zero vector
        expect(out).toEqual(vZero);
    });

    test('fastRotate should approximate rotation', () => {
        vec2.fastRotate(out, vX, HALF_PI);
        expect(out.x).toBeCloseTo(0);
        expect(out.y).toBeCloseTo(1);
        vec2.fastRotate(out, vX, Math.PI);
        expect(out.x).toBeCloseTo(-1);
        expect(out.y).toBeCloseTo(0);
        vec2.fastRotate(out, vX, 0);
        expect(out.x).toBeCloseTo(1);
        expect(out.y).toBeCloseTo(0);

        const smallAngle = 0.01;
        vec2.rotate(out, v11, smallAngle); // Get accurate result
        const fastOut = vec2.create();
        vec2.fastRotate(fastOut, v11, smallAngle); // Get fast result
        expect(fastOut.x).toBeCloseTo(out.x, 5); // Compare with high precision
        expect(fastOut.y).toBeCloseTo(out.y, 5);

        vec2.rotate(out, v11, Math.PI / 6); // Accurate
        vec2.fastRotate(fastOut, v11, Math.PI / 6); // Fast (should be same as accurate)
        expect(fastOut.x).toBeCloseTo(out.x, 8);
        expect(fastOut.y).toBeCloseTo(out.y, 8);
    });

    test('rotateAround should rotate vector around a specific origin', () => {
        const origin = vec2.create(10, 0); // Origin on X axis
        vec2.rotateAround(out, vX, origin, HALF_PI); // Rotate (1,0) around (10,0) by 90 deg
        // Diff = (1,0) - (10,0) = (-9, 0)
        // Rotate diff = (0, -9)
        // Add origin = (0, -9) + (10, 0) = (10, -9)
        expect(out.x).toBeCloseTo(10);
        expect(out.y).toBeCloseTo(-9);

        vec2.rotateAround(out, origin, vX, Math.PI); // Rotate origin around (1,0)
        // Diff = (10,0) - (1,0) = (9,0)
        // Rotate diff = (-9, 0)
        // Add pivot (vX) = (-9,0) + (1,0) = (-8, 0)
        expect(out.x).toBeCloseTo(-8);
        expect(out.y).toBeCloseTo(0);

        vec2.rotateAround(out, vX, vX, Math.PI / 6);
        expect(out.x).toBeCloseTo(vX.x);
        expect(out.y).toBeCloseTo(vX.y);
    });

    // --- Perpendicular ---
    test('perpendicular should return CCW (+90deg) perpendicular vector', () => {
        vec2.perpendicular(out, vX);
        expect(out.x).toBeCloseTo(0);
        expect(out.y).toBeCloseTo(1); // (1,0) -> (0,1)
        vec2.perpendicular(out, vY);
        expect(out.x).toBeCloseTo(-1);
        expect(out.y).toBeCloseTo(0); // (0,1) -> (-1,0)
        vec2.perpendicular(out, vec2.create(2, 3));
        expect(out).toEqual({ x: -3, y: 2 });
        vec2.perpendicular(out, vZero);
        expect(out).toEqual(vZero);
    });

    test('perpendicularCCW should return CW (-90deg) perpendicular vector (based on original code)', () => {
        vec2.perpendicularCCW(out, vX);
        expect(out.x).toBeCloseTo(0);
        expect(out.y).toBeCloseTo(-1); // (1,0) -> (0,-1)
        vec2.perpendicularCCW(out, vY);
        expect(out.x).toBeCloseTo(1);
        expect(out.y).toBeCloseTo(0); // (0,1) -> (1,0)
        vec2.perpendicularCCW(out, vec2.create(2, 3));
        expect(out).toEqual({ x: 3, y: -2 });
        vec2.perpendicularCCW(out, vZero);
        expect(out).toEqual(vZero);
    });
});
