import { describe, it, expect } from 'vitest';
import { DistanceJoint2D } from '../components/distance-joint2d';
import { FixedJoint2D } from '../components/fixed-joint2d';
import { HingeJoint2D } from '../components/hinge-joint2d';
import { SliderJoint2D } from '../components/slider-joint2d';
import { SpringJoint2D } from '../components/spring-joint2d';
import { WheelJoint2D } from '../components/wheel-joint2d';
import { MotorJoint2D } from '../components/motor-joint2d';
import { MouseJoint2D } from '../components/mouse-joint2d';
import { GearJoint2D } from '../components/gear-joint2d';
import { RopeJoint2D } from '../components/rope-joint2d';

/**
 * Vec2 serialisation contract tests for 2D joint components.
 *
 * The Editor stores Vec2 values as **arrays** `[x, y]` in scene JSON
 * (source: `Editor/src-tauri/src/scene/components.rs` `*_properties()`).
 * The engine uses `{x, y}` objects (IVec2Like).
 *
 * These tests verify:
 * 1. Array format `[x, y]` is correctly deserialised (P0-1 2D symmetric fix).
 * 2. Object format `{x, y}` still works (backward compatibility / round-trip).
 * 3. Editor fixture (real Rust JSON shape) deserialises correctly.
 * 4. Empty `{}` → safe defaults, no exceptions.
 * 5. Unknown fields → no exceptions.
 * 6. Discriminating: values assert NON-default (would fail without fix).
 *
 * @see joint3d-subclass-serialization.test.ts — 3D counterpart (P0-1).
 */

// ─── Editor fixture data (real JSON shape from Rust *_properties()) ──────────

/**
 * These fixtures are copied verbatim from the Editor's Rust
 * `*_properties()` functions (commit fc2cc126 on feat/2d-joint-editor-layers).
 * DO NOT modify them to match engine expectations — they represent the
 * GROUND TRUTH of what the Editor writes.
 */
const EDITOR_FIXTURES = {
    wheelJoint2d: {
        anchorA: [0.0, 0.0],
        anchorB: [0.0, 0.0],
        axis: [0.0, 1.0],
        enableLimit: false,
        lowerTranslation: 0.0,
        upperTranslation: 0.0,
        enableMotor: false,
        motorSpeed: 0.0,
        maxMotorTorque: 0.0,
        stiffness: 0.0,
        damping: 0.0,
        connectedBody: '',
        enableCollision: false,
        breakForce: 1e18,
        breakTorque: 1e18,
    },
    motorJoint2d: {
        linearOffset: [0.0, 0.0],
        angularOffset: 0.0,
        maxForce: 1.0,
        maxTorque: 1.0,
        correctionFactor: 0.3,
        connectedBody: '',
        enableCollision: false,
        breakForce: 1e18,
        breakTorque: 1e18,
    },
    mouseJoint2d: {
        target: [0.0, 0.0],
        maxForce: 1000.0,
        stiffness: 5.0,
        damping: 0.7,
        connectedBody: '',
        enableCollision: false,
        breakForce: 1e18,
        breakTorque: 1e18,
    },
    gearJoint2d: {
        jointA: '',
        jointB: '',
        ratio: 1.0,
        connectedBody: '',
        enableCollision: false,
        breakForce: 1e18,
        breakTorque: 1e18,
    },
    ropeJoint2d: {
        anchorA: [0.0, 0.0],
        anchorB: [0.0, 0.0],
        maxLength: 1.0,
        connectedBody: '',
        enableCollision: false,
        breakForce: 1e18,
        breakTorque: 1e18,
    },
} as const;

// ─── P0-1: Array format Vec2 deserialisation (discriminating) ───────────────
// Without normalizeVec2Value, `data.anchorA?.x` on array `[0, 0.5]` is
// `undefined`, so the value defaults to 0. These tests assert NON-ZERO
// values that would FAIL without the fix.

describe('P0-1 2D: Vec2 array format deserialisation (discriminating)', () => {
    it('DistanceJoint2D reads anchorA.y from array (not default 0)', () => {
        const joint = new DistanceJoint2D();
        joint.deserialize({ anchorA: [0, 0.5] });
        expect(joint.anchorA.y).toBe(0.5);
        expect(joint.anchorA.x).toBe(0);
    });

    it('DistanceJoint2D reads anchorB with negative values from array', () => {
        const joint = new DistanceJoint2D();
        joint.deserialize({ anchorB: [-1.5, 2.5] });
        expect(joint.anchorB.x).toBe(-1.5);
        expect(joint.anchorB.y).toBe(2.5);
    });

    it('FixedJoint2D reads anchor.y from array (not default 0)', () => {
        const joint = new FixedJoint2D();
        joint.deserialize({ anchor: [0.0, 0.75] });
        expect(joint.anchor.y).toBe(0.75);
        expect(joint.anchor.x).toBe(0);
    });

    it('HingeJoint2D reads anchor.y from array (not default 0)', () => {
        const joint = new HingeJoint2D();
        // Editor writes: "anchor": [0.0, 0.5]
        joint.deserialize({ anchor: [0.0, 0.5] });
        expect(joint.anchor.y).toBe(0.5);
        expect(joint.anchor.x).toBe(0);
    });

    it('SliderJoint2D reads anchor and axis from array', () => {
        const joint = new SliderJoint2D();
        joint.deserialize({ anchor: [1.0, 2.0], axis: [0.0, 1.0] });
        expect(joint.anchor.x).toBe(1.0);
        expect(joint.anchor.y).toBe(2.0);
        // axis is normalized, so [0,1] → (0,1)
        expect(joint.axis.y).toBeCloseTo(1.0);
        expect(joint.axis.x).toBeCloseTo(0.0);
    });

    it('SliderJoint2D axis defaults to (1,0) when not provided', () => {
        const joint = new SliderJoint2D();
        joint.deserialize({});
        expect(joint.axis.x).toBe(1.0);
        expect(joint.axis.y).toBe(0.0);
    });

    it('SpringJoint2D reads anchorA.y from array (not default 0)', () => {
        const joint = new SpringJoint2D();
        joint.deserialize({ anchorA: [0, 1.5] });
        // SpringJoint2D has no public getter for anchorA — verify via serialize()
        const s = joint.serialize();
        expect(s.anchorA.y).toBe(1.5);
        expect(s.anchorA.x).toBe(0);
    });

    it('SpringJoint2D reads anchorB with negative values from array', () => {
        const joint = new SpringJoint2D();
        joint.deserialize({ anchorB: [-3.0, 4.0] });
        const s = joint.serialize();
        expect(s.anchorB.x).toBe(-3.0);
        expect(s.anchorB.y).toBe(4.0);
    });

    it('WheelJoint2D reads anchorA.y from array (not default 0)', () => {
        const joint = new WheelJoint2D();
        joint.deserialize({ anchorA: [0.0, 0.5] });
        expect(joint.anchorA.y).toBe(0.5);
    });

    it('WheelJoint2D reads axis from array', () => {
        const joint = new WheelJoint2D();
        joint.deserialize({ axis: [1.0, 0.0] });
        expect(joint.axis.x).toBe(1.0);
        expect(joint.axis.y).toBe(0.0);
    });

    it('MotorJoint2D reads linearOffset from array (not default 0)', () => {
        const joint = new MotorJoint2D();
        // Editor writes: "linearOffset": [0.0, 0.0] — test with non-zero
        joint.deserialize({ linearOffset: [1.5, -2.5] });
        expect(joint.linearOffset.x).toBe(1.5);
        expect(joint.linearOffset.y).toBe(-2.5);
    });

    it('MouseJoint2D reads target from array (not default 0)', () => {
        const joint = new MouseJoint2D();
        // Editor writes: "target": [0.0, 0.0] — test with non-zero
        joint.deserialize({ target: [3.0, 7.5] });
        expect(joint.target.x).toBe(3.0);
        expect(joint.target.y).toBe(7.5);
    });

    it('RopeJoint2D reads anchorA.y from array (not default 0)', () => {
        const joint = new RopeJoint2D();
        joint.deserialize({ anchorA: [0.0, 1.0] });
        expect(joint.anchorA.y).toBe(1.0);
    });

    it('RopeJoint2D reads anchorB with negative values from array', () => {
        const joint = new RopeJoint2D();
        joint.deserialize({ anchorB: [-2.0, 3.0] });
        expect(joint.anchorB.x).toBe(-2.0);
        expect(joint.anchorB.y).toBe(3.0);
    });
});

// ─── Object format backward compatibility ────────────────────────────────────

describe('Vec2 object format still works (backward compat / round-trip)', () => {
    it('DistanceJoint2D accepts {x,y} object format', () => {
        const joint = new DistanceJoint2D();
        joint.deserialize({ anchorA: { x: 1.5, y: 2.5 } });
        expect(joint.anchorA.x).toBe(1.5);
        expect(joint.anchorA.y).toBe(2.5);
    });

    it('HingeJoint2D accepts {x,y} object format', () => {
        const joint = new HingeJoint2D();
        joint.deserialize({ anchor: { x: 0.5, y: 1.0 } });
        expect(joint.anchor.x).toBe(0.5);
        expect(joint.anchor.y).toBe(1.0);
    });

    it('MotorJoint2D accepts {x,y} object format', () => {
        const joint = new MotorJoint2D();
        joint.deserialize({ linearOffset: { x: 2.0, y: 3.0 } });
        expect(joint.linearOffset.x).toBe(2.0);
        expect(joint.linearOffset.y).toBe(3.0);
    });

    it('MouseJoint2D accepts {x,y} object format', () => {
        const joint = new MouseJoint2D();
        joint.deserialize({ target: { x: 5.0, y: 10.0 } });
        expect(joint.target.x).toBe(5.0);
        expect(joint.target.y).toBe(10.0);
    });

    it('WheelJoint2D accepts {x,y} object format', () => {
        const joint = new WheelJoint2D();
        joint.deserialize({ anchorA: { x: 1.0, y: 2.0 }, axis: { x: 0.0, y: 1.0 } });
        expect(joint.anchorA.x).toBe(1.0);
        expect(joint.anchorA.y).toBe(2.0);
        expect(joint.axis.y).toBe(1.0);
    });
});

// ─── Round-trip: serialize → deserialize → same values ───────────────────────

describe('Round-trip serialize → deserialize preserves Vec2 values', () => {
    it('DistanceJoint2D round-trips anchorA/anchorB', () => {
        const original = new DistanceJoint2D();
        original.deserialize({ anchorA: [1.5, 2.5], anchorB: [-0.5, 3.0] });
        const serialized = original.serialize();
        const restored = new DistanceJoint2D();
        restored.deserialize(serialized);
        expect(restored.anchorA.x).toBe(1.5);
        expect(restored.anchorA.y).toBe(2.5);
        expect(restored.anchorB.x).toBe(-0.5);
        expect(restored.anchorB.y).toBe(3.0);
    });

    it('HingeJoint2D round-trips anchor', () => {
        const original = new HingeJoint2D();
        original.deserialize({ anchor: [0.5, 1.5] });
        const serialized = original.serialize();
        const restored = new HingeJoint2D();
        restored.deserialize(serialized);
        expect(restored.anchor.x).toBe(0.5);
        expect(restored.anchor.y).toBe(1.5);
    });

    it('MotorJoint2D round-trips linearOffset', () => {
        const original = new MotorJoint2D();
        original.deserialize({ linearOffset: [2.0, -1.0] });
        const serialized = original.serialize();
        const restored = new MotorJoint2D();
        restored.deserialize(serialized);
        expect(restored.linearOffset.x).toBe(2.0);
        expect(restored.linearOffset.y).toBe(-1.0);
    });

    it('MouseJoint2D round-trips target', () => {
        const original = new MouseJoint2D();
        original.deserialize({ target: [5.0, 10.0] });
        const serialized = original.serialize();
        const restored = new MouseJoint2D();
        restored.deserialize(serialized);
        expect(restored.target.x).toBe(5.0);
        expect(restored.target.y).toBe(10.0);
    });

    it('WheelJoint2D round-trips anchorA/anchorB/axis', () => {
        const original = new WheelJoint2D();
        original.deserialize({ anchorA: [1.0, 2.0], anchorB: [3.0, 4.0], axis: [0.0, 1.0] });
        const serialized = original.serialize();
        const restored = new WheelJoint2D();
        restored.deserialize(serialized);
        expect(restored.anchorA.x).toBe(1.0);
        expect(restored.anchorA.y).toBe(2.0);
        expect(restored.anchorB.x).toBe(3.0);
        expect(restored.anchorB.y).toBe(4.0);
        expect(restored.axis.x).toBe(0.0);
        expect(restored.axis.y).toBe(1.0);
    });

    it('SliderJoint2D round-trips anchor and axis', () => {
        const original = new SliderJoint2D();
        original.deserialize({ anchor: [1.0, 2.0], axis: [0.0, 1.0] });
        const serialized = original.serialize();
        const restored = new SliderJoint2D();
        restored.deserialize(serialized);
        expect(restored.anchor.x).toBe(1.0);
        expect(restored.anchor.y).toBe(2.0);
        expect(restored.axis.y).toBeCloseTo(1.0);
    });

    it('RopeJoint2D round-trips anchorA/anchorB', () => {
        const original = new RopeJoint2D();
        original.deserialize({ anchorA: [1.0, 2.0], anchorB: [3.0, 4.0] });
        const serialized = original.serialize();
        const restored = new RopeJoint2D();
        restored.deserialize(serialized);
        expect(restored.anchorA.x).toBe(1.0);
        expect(restored.anchorA.y).toBe(2.0);
        expect(restored.anchorB.x).toBe(3.0);
        expect(restored.anchorB.y).toBe(4.0);
    });

    it('FixedJoint2D round-trips anchor', () => {
        const original = new FixedJoint2D();
        original.deserialize({ anchor: [0.5, 1.5] });
        const serialized = original.serialize();
        const restored = new FixedJoint2D();
        restored.deserialize(serialized);
        expect(restored.anchor.x).toBe(0.5);
        expect(restored.anchor.y).toBe(1.5);
    });

    it('SpringJoint2D round-trips anchorA/anchorB', () => {
        const original = new SpringJoint2D();
        original.deserialize({ anchorA: [1.0, 2.0], anchorB: [3.0, 4.0] });
        const serialized = original.serialize();
        const restored = new SpringJoint2D();
        restored.deserialize(serialized);
        const s = restored.serialize();
        expect(s.anchorA.x).toBe(1.0);
        expect(s.anchorA.y).toBe(2.0);
        expect(s.anchorB.x).toBe(3.0);
        expect(s.anchorB.y).toBe(4.0);
    });
});

// ─── Editor fixture deserialisation (real Rust JSON shape) ───────────────────

describe('Editor fixture deserialisation (real Rust *_properties() shape)', () => {
    it('WheelJoint2D deserializes Editor default fixture without error', () => {
        const joint = new WheelJoint2D();
        expect(() => joint.deserialize(EDITOR_FIXTURES.wheelJoint2d as any)).not.toThrow();
        expect(joint.anchorA.x).toBe(0);
        expect(joint.anchorA.y).toBe(0);
        expect(joint.axis.y).toBe(1.0);
    });

    it('MotorJoint2D deserializes Editor default fixture without error', () => {
        const joint = new MotorJoint2D();
        expect(() => joint.deserialize(EDITOR_FIXTURES.motorJoint2d as any)).not.toThrow();
        expect(joint.linearOffset.x).toBe(0);
        expect(joint.linearOffset.y).toBe(0);
        expect(joint.correctionFactor).toBe(0.3);
    });

    it('MouseJoint2D deserializes Editor default fixture without error', () => {
        const joint = new MouseJoint2D();
        expect(() => joint.deserialize(EDITOR_FIXTURES.mouseJoint2d as any)).not.toThrow();
        expect(joint.target.x).toBe(0);
        expect(joint.target.y).toBe(0);
        expect(joint.maxForce).toBe(1000.0);
        expect(joint.stiffness).toBe(5.0);
    });

    it('GearJoint2D deserializes Editor default fixture without error', () => {
        const joint = new GearJoint2D();
        expect(() => joint.deserialize(EDITOR_FIXTURES.gearJoint2d as any)).not.toThrow();
        expect(joint.ratio).toBe(1.0);
    });

    it('RopeJoint2D deserializes Editor default fixture without error', () => {
        const joint = new RopeJoint2D();
        expect(() => joint.deserialize(EDITOR_FIXTURES.ropeJoint2d as any)).not.toThrow();
        expect(joint.maxLength).toBe(1.0);
    });

    it('WheelJoint2D deserializes non-default Editor fixture with correct Vec2', () => {
        const joint = new WheelJoint2D();
        joint.deserialize({
            ...EDITOR_FIXTURES.wheelJoint2d,
            anchorA: [1.5, 2.5],
            anchorB: [0.5, -1.0],
            axis: [1.0, 0.0],
        } as any);
        expect(joint.anchorA.x).toBe(1.5);
        expect(joint.anchorA.y).toBe(2.5);
        expect(joint.anchorB.x).toBe(0.5);
        expect(joint.anchorB.y).toBe(-1.0);
        expect(joint.axis.x).toBe(1.0);
        expect(joint.axis.y).toBe(0.0);
    });
});

// ─── Empty / unknown field resilience ────────────────────────────────────────

describe('Empty and unknown field resilience', () => {
    it('DistanceJoint2D: empty {} → safe defaults, no exception', () => {
        const joint = new DistanceJoint2D();
        expect(() => joint.deserialize({})).not.toThrow();
        expect(joint.anchorA.x).toBe(0);
        expect(joint.anchorA.y).toBe(0);
        expect(joint.anchorB.x).toBe(0);
        expect(joint.anchorB.y).toBe(0);
    });

    it('HingeJoint2D: empty {} → safe defaults, no exception', () => {
        const joint = new HingeJoint2D();
        expect(() => joint.deserialize({})).not.toThrow();
        expect(joint.anchor.x).toBe(0);
        expect(joint.anchor.y).toBe(0);
    });

    it('WheelJoint2D: empty {} → safe defaults, no exception', () => {
        const joint = new WheelJoint2D();
        expect(() => joint.deserialize({})).not.toThrow();
        expect(joint.anchorA.x).toBe(0);
        expect(joint.axis.y).toBe(1.0); // default fallback for axis
    });

    it('MotorJoint2D: empty {} → safe defaults, no exception', () => {
        const joint = new MotorJoint2D();
        expect(() => joint.deserialize({})).not.toThrow();
        expect(joint.linearOffset.x).toBe(0);
        expect(joint.linearOffset.y).toBe(0);
    });

    it('MouseJoint2D: empty {} → safe defaults, no exception', () => {
        const joint = new MouseJoint2D();
        expect(() => joint.deserialize({})).not.toThrow();
        expect(joint.target.x).toBe(0);
        expect(joint.target.y).toBe(0);
    });

    it('DistanceJoint2D: unknown fields → no exception', () => {
        const joint = new DistanceJoint2D();
        expect(() => joint.deserialize({
            unknownField: 'garbage',
            anotherUnknown: 42,
            anchorA: [1.0, 2.0],
        })).not.toThrow();
        expect(joint.anchorA.x).toBe(1.0);
        expect(joint.anchorA.y).toBe(2.0);
    });

    it('HingeJoint2D: unknown fields → no exception', () => {
        const joint = new HingeJoint2D();
        expect(() => joint.deserialize({
            unknownField: 'garbage',
            anchor: [0.5, 1.0],
        })).not.toThrow();
        expect(joint.anchor.y).toBe(1.0);
    });

    it('WheelJoint2D: unknown fields → no exception', () => {
        const joint = new WheelJoint2D();
        expect(() => joint.deserialize({
            unknownField: 'garbage',
            anchorA: [1.0, 2.0],
        })).not.toThrow();
        expect(joint.anchorA.y).toBe(2.0);
    });
});

// ─── Scalar property round-trip (non-Vec2 coverage) ─────────────────────────

describe('Scalar property round-trip for all joints', () => {
    it('HingeJoint2D round-trips motor and limits', () => {
        const joint = new HingeJoint2D();
        joint.deserialize({
            useMotor: true,
            motorSpeed: 5.0,
            maxMotorTorque: 200,
            useLimits: true,
            limits: { min: -45, max: 90 },
        });
        expect(joint.useMotor).toBe(true);
        expect(joint.motorSpeed).toBe(5.0);
        expect(joint.maxMotorTorque).toBe(200);
        expect(joint.useLimits).toBe(true);
        expect(joint.limits.min).toBe(-45);
        expect(joint.limits.max).toBe(90);
    });

    it('SliderJoint2D round-trips motor and limits', () => {
        const joint = new SliderJoint2D();
        joint.deserialize({
            useMotor: true,
            motorSpeed: 3.0,
            maxMotorForce: 500,
            useLimits: true,
            limits: { min: -2, max: 5 },
        });
        expect(joint.useMotor).toBe(true);
        expect(joint.motorSpeed).toBe(3.0);
        expect(joint.maxMotorForce).toBe(500);
        expect(joint.useLimits).toBe(true);
        expect(joint.limits.min).toBe(-2);
        expect(joint.limits.max).toBe(5);
    });

    it('WheelJoint2D round-trips all scalar properties', () => {
        const joint = new WheelJoint2D();
        joint.deserialize({
            enableLimit: true,
            lowerTranslation: -0.5,
            upperTranslation: 0.5,
            enableMotor: true,
            motorSpeed: 10.0,
            maxMotorTorque: 100,
            stiffness: 50,
            damping: 5,
        });
        expect(joint.enableLimit).toBe(true);
        expect(joint.lowerTranslation).toBe(-0.5);
        expect(joint.upperTranslation).toBe(0.5);
        expect(joint.enableMotor).toBe(true);
        expect(joint.motorSpeed).toBe(10.0);
        expect(joint.maxMotorTorque).toBe(100);
        expect(joint.stiffness).toBe(50);
        expect(joint.damping).toBe(5);
    });

    it('MotorJoint2D round-trips all scalar properties', () => {
        const joint = new MotorJoint2D();
        joint.deserialize({
            angularOffset: 1.5,
            maxForce: 10,
            maxTorque: 5,
            correctionFactor: 0.5,
        });
        expect(joint.angularOffset).toBe(1.5);
        expect(joint.maxForce).toBe(10);
        expect(joint.maxTorque).toBe(5);
        expect(joint.correctionFactor).toBe(0.5);
    });

    it('MouseJoint2D round-trips all scalar properties', () => {
        const joint = new MouseJoint2D();
        joint.deserialize({
            maxForce: 2000,
            stiffness: 10,
            damping: 1.5,
        });
        expect(joint.maxForce).toBe(2000);
        expect(joint.stiffness).toBe(10);
        expect(joint.damping).toBe(1.5);
    });

    it('GearJoint2D round-trips ratio', () => {
        const joint = new GearJoint2D();
        joint.deserialize({ ratio: 2.5 });
        expect(joint.ratio).toBe(2.5);
    });

    it('RopeJoint2D round-trips maxLength', () => {
        const joint = new RopeJoint2D();
        joint.deserialize({ maxLength: 5.0 });
        expect(joint.maxLength).toBe(5.0);
    });

    it('DistanceJoint2D round-trips all scalar properties', () => {
        const joint = new DistanceJoint2D();
        joint.deserialize({
            distance: 3.0,
            minDistance: 0.5,
            maxDistance: 10.0,
            stiffness: 20,
            damping: 2,
            autoConfigureDistance: false,
        });
        expect(joint.distance).toBe(3.0);
        expect(joint.minDistance).toBe(0.5);
        expect(joint.maxDistance).toBe(10.0);
        expect(joint.stiffness).toBe(20);
        expect(joint.damping).toBe(2);
        // autoConfigureDistance has no public getter — verify via serialize()
        expect(joint.serialize().autoConfigureDistance).toBe(false);
    });

    it('SpringJoint2D round-trips all scalar properties', () => {
        const joint = new SpringJoint2D();
        joint.deserialize({
            distance: 2.0,
            stiffness: 15,
            damping: 1.0,
            autoConfigureDistance: false,
        });
        expect(joint.distance).toBe(2.0);
        expect(joint.stiffness).toBe(15);
        expect(joint.damping).toBe(1.0);
        // autoConfigureDistance has no public getter — verify via serialize()
        expect(joint.serialize().autoConfigureDistance).toBe(false);
    });

    it('FixedJoint2D round-trips all scalar properties', () => {
        const joint = new FixedJoint2D();
        joint.deserialize({
            dampingRatio: 0.5,
            frequency: 10,
        });
        expect(joint.dampingRatio).toBe(0.5);
        expect(joint.frequency).toBe(10);
    });
});
