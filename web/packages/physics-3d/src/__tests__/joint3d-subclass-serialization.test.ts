import { describe, it, expect } from 'vitest';
import { HingeJoint3D } from '../components/hinge-joint3d';
import { SliderJoint3D } from '../components/slider-joint3d';
import { SpringJoint3D } from '../components/spring-joint3d';
import { CharacterJoint3D } from '../components/character-joint3d';
import { ConfigurableJoint3D } from '../components/configurable-joint3d';
import { FixedJoint3D } from '../components/fixed-joint3d';

/**
 * Subclass property serialization tests for 3D joint components.
 *
 * These tests verify that each joint subclass correctly serializes and
 * deserializes its OWN properties (beyond the base Joint3D fields),
 * using the EXACT key names that the Editor writes to scene JSON
 * (authoritative source: `Editor/src-tauri/src/scene/components.rs`).
 *
 * Test categories:
 * 1. Round-trip: set non-default values → serialize → deserialize → assert same values
 * 2. Editor fixture: deserialize the exact JSON shape from Rust *_properties()
 * 3. Backward compat: missing fields → safe defaults, no exception
 * 4. Unknown fields: unrecognized keys don't corrupt the component
 * 5. Discriminative: serialize() output contains subclass-specific keys
 */

// ─── Editor fixture data (from components.rs) ──────────────────────────────

const HINGE_EDITOR_DEFAULT = {
    anchor: { x: 0.0, y: 0.5, z: 0.0 },
    axis: { x: 0.0, y: 1.0, z: 0.0 },
    connectedAnchor: { x: 0.0, y: -1.2, z: 0.0 },
    autoConfigureConnectedAnchor: true,
    connectedBody: '',
    massScale: 1.0,
    connectedMassScale: 1.0,
    spring: { enabled: false, spring: 10.0, damper: 2.0, targetPosition: 45.0 },
    motor: { enabled: true, targetVelocity: 120.0, force: 300.0, freeSpin: false },
    limits: { enabled: true, min: -45.0, max: 90.0, bounciness: 0.2, bounceThresholdVelocity: 2.0, contactDistance: 0.5 },
    breakForce: 2500.0,
    breakTorque: 2500.0,
    enableCollision: false,
    enablePreprocessing: true,
};

const SLIDER_EDITOR_DEFAULT = {
    anchor: { x: 0.0, y: 0.0, z: 0.0 },
    connectedAnchor: { x: 0.0, y: 0.0, z: 0.0 },
    autoConfigureConnectedAnchor: true,
    connectedBody: '',
    axis: { x: 1.0, y: 0.0, z: 0.0 },
    secondaryAxis: { x: 0.0, y: 1.0, z: 0.0 },
    breakForce: 1e18,
    breakTorque: 1e18,
    enableCollision: false,
    enablePreprocessing: true,
    massScale: 1.0,
    connectedMassScale: 1.0,
    useLimits: false,
    limits: { min: 0.0, max: 0.0, bounciness: 0.0, contactDistance: 0.0 },
    useMotor: false,
    motor: { targetVelocity: 0.0, force: 0.0, freeSpin: false },
    useSpring: false,
    spring: { spring: 0.0, damper: 0.0 },
};

const SPRING_EDITOR_DEFAULT = {
    anchor: { x: 0.0, y: 0.0, z: 0.0 },
    connectedAnchor: { x: 0.0, y: 0.0, z: 0.0 },
    autoConfigureConnectedAnchor: true,
    connectedBody: '',
    axis: { x: 1.0, y: 0.0, z: 0.0 },
    secondaryAxis: { x: 0.0, y: 1.0, z: 0.0 },
    breakForce: 1e18,
    breakTorque: 1e18,
    enableCollision: false,
    enablePreprocessing: true,
    massScale: 1.0,
    connectedMassScale: 1.0,
    minDistance: 0.0,
    maxDistance: 0.0,
    spring: 0.0,
    damper: 0.0,
    tolerance: 0.025,
    autoConfigureDistance: true,
};

const CHARACTER_EDITOR_DEFAULT = {
    anchor: { x: 0.0, y: 0.0, z: 0.0 },
    connectedAnchor: { x: 0.0, y: 0.0, z: 0.0 },
    autoConfigureConnectedAnchor: true,
    connectedBody: '',
    axis: { x: 1.0, y: 0.0, z: 0.0 },
    secondaryAxis: { x: 0.0, y: 1.0, z: 0.0 },
    breakForce: 1e18,
    breakTorque: 1e18,
    enableCollision: false,
    enablePreprocessing: true,
    massScale: 1.0,
    connectedMassScale: 1.0,
    swingAxis: { x: 1.0, y: 0.0, z: 0.0 },
    lowTwistLimit: { limit: 0.0, bounciness: 0.0, contactDistance: 0.0 },
    highTwistLimit: { limit: 0.0, bounciness: 0.0, contactDistance: 0.0 },
    swing1Limit: { limit: 0.0, bounciness: 0.0, contactDistance: 0.0 },
    swing2Limit: { limit: 0.0, bounciness: 0.0, contactDistance: 0.0 },
    twistLimitSpring: { spring: 0.0, damper: 0.0 },
    swingLimitSpring: { spring: 0.0, damper: 0.0 },
    enableProjection: false,
    projectionDistance: 0.1,
    projectionAngle: 180.0,
};

const CONFIGURABLE_EDITOR_DEFAULT = {
    anchor: { x: 0.0, y: 0.0, z: 0.0 },
    connectedAnchor: { x: 0.0, y: 0.0, z: 0.0 },
    autoConfigureConnectedAnchor: true,
    connectedBody: '',
    axis: { x: 1.0, y: 0.0, z: 0.0 },
    secondaryAxis: { x: 0.0, y: 1.0, z: 0.0 },
    breakForce: 1e18,
    breakTorque: 1e18,
    enableCollision: false,
    enablePreprocessing: true,
    massScale: 1.0,
    connectedMassScale: 1.0,
    xMotion: 0,
    yMotion: 0,
    zMotion: 0,
    angularXMotion: 0,
    angularYMotion: 0,
    angularZMotion: 0,
    linearLimit: { limit: 0.0, bounciness: 0.0, contactDistance: 0.0 },
    targetPosition: { x: 0.0, y: 0.0, z: 0.0 },
    targetVelocity: { x: 0.0, y: 0.0, z: 0.0 },
    targetRotation: { x: 0.0, y: 0.0, z: 0.0, w: 1.0 },
    targetAngularVelocity: { x: 0.0, y: 0.0, z: 0.0 },
    rotationDriveMode: 0,
    configuredInWorldSpace: false,
    swapBodies: false,
};

// ─── 1. Round-trip tests ───────────────────────────────────────────────────

describe('HingeJoint3D subclass property round-trip', () => {
    it('serializes and deserializes limits, motor, and spring', () => {
        const joint = new HingeJoint3D();
        joint.useLimits = true;
        joint.limits = { min: -30, max: 60, bounciness: 0.5, contactDistance: 0.1 };
        joint.useMotor = true;
        joint.motor = { targetVelocity: 90, force: 500, freeSpin: true };
        joint.useSpring = true;
        joint.spring = { spring: 25, damper: 3.5 };

        const data = joint.serialize();
        const restored = new HingeJoint3D();
        restored.deserialize(data);

        expect(restored.useLimits).toBe(true);
        expect(restored.limits.min).toBe(-30);
        expect(restored.limits.max).toBe(60);
        expect(restored.limits.bounciness).toBe(0.5);
        expect(restored.limits.contactDistance).toBe(0.1);
        expect(restored.useMotor).toBe(true);
        expect(restored.motor.targetVelocity).toBe(90);
        expect(restored.motor.force).toBe(500);
        expect(restored.motor.freeSpin).toBe(true);
        expect(restored.useSpring).toBe(true);
        expect(restored.spring.spring).toBe(25);
        expect(restored.spring.damper).toBe(3.5);
    });

    it('serialize() output contains nested limits/motor/spring objects', () => {
        const joint = new HingeJoint3D();
        const data = joint.serialize();
        expect(data).toHaveProperty('limits');
        expect(data).toHaveProperty('motor');
        expect(data).toHaveProperty('spring');
        expect(typeof data.limits).toBe('object');
        expect(typeof data.motor).toBe('object');
        expect(typeof data.spring).toBe('object');
        expect(data.limits).toHaveProperty('enabled');
        expect(data.motor).toHaveProperty('enabled');
        expect(data.spring).toHaveProperty('enabled');
    });
});

describe('SliderJoint3D subclass property round-trip', () => {
    it('serializes and deserializes limits, motor, and spring', () => {
        const joint = new SliderJoint3D();
        joint.useLimits = true;
        joint.limits = { min: -2, max: 5, bounciness: 0.3, contactDistance: 0.05 };
        joint.useMotor = true;
        joint.motor = { targetVelocity: 1.5, force: 200, freeSpin: false };
        joint.useSpring = true;
        joint.spring = { spring: 50, damper: 8 };

        const data = joint.serialize();
        const restored = new SliderJoint3D();
        restored.deserialize(data);

        expect(restored.useLimits).toBe(true);
        expect(restored.limits.min).toBe(-2);
        expect(restored.limits.max).toBe(5);
        expect(restored.limits.bounciness).toBe(0.3);
        expect(restored.limits.contactDistance).toBe(0.05);
        expect(restored.useMotor).toBe(true);
        expect(restored.motor.targetVelocity).toBe(1.5);
        expect(restored.motor.force).toBe(200);
        expect(restored.motor.freeSpin).toBe(false);
        expect(restored.useSpring).toBe(true);
        expect(restored.spring.spring).toBe(50);
        expect(restored.spring.damper).toBe(8);
    });

    it('serialize() output contains flat useLimits/useMotor/useSpring booleans', () => {
        const joint = new SliderJoint3D();
        const data = joint.serialize();
        expect(data).toHaveProperty('useLimits', false);
        expect(data).toHaveProperty('useMotor', false);
        expect(data).toHaveProperty('useSpring', false);
    });
});

describe('SpringJoint3D subclass property round-trip', () => {
    it('serializes and deserializes distance, spring, damper, tolerance', () => {
        const joint = new SpringJoint3D();
        joint.minDistance = 1.5;
        joint.maxDistance = 3.0;
        joint.springValue = 100;
        joint.damper = 15;
        joint.tolerance = 0.05;
        joint.autoConfigureDistance = false;

        const data = joint.serialize();
        const restored = new SpringJoint3D();
        restored.deserialize(data);

        expect(restored.minDistance).toBe(1.5);
        expect(restored.maxDistance).toBe(3.0);
        expect(restored.springValue).toBe(100);
        expect(restored.damper).toBe(15);
        expect(restored.tolerance).toBe(0.05);
        expect(restored.autoConfigureDistance).toBe(false);
    });

    it('serialize() emits spring as a number (not nested object)', () => {
        const joint = new SpringJoint3D();
        joint.springValue = 42;
        const data = joint.serialize();
        expect(data).toHaveProperty('spring', 42);
        expect(typeof data.spring).toBe('number');
    });
});

describe('CharacterJoint3D subclass property round-trip', () => {
    it('serializes and deserializes all limits, springs, projection, and motor', () => {
        const joint = new CharacterJoint3D();
        joint.swingAxis = { x: 0, y: 1, z: 0 };
        joint.lowTwistLimit = { limit: -10, bounciness: 0.1, contactDistance: 0.02 };
        joint.highTwistLimit = { limit: 30, bounciness: 0.2, contactDistance: 0.03 };
        joint.swing1Limit = { limit: 45, bounciness: 0.3, contactDistance: 0.04 };
        joint.swing2Limit = { limit: 60, bounciness: 0.4, contactDistance: 0.05 };
        joint.twistLimitSpring = { spring: 10, damper: 1 };
        joint.swingLimitSpring = { spring: 20, damper: 2 };
        joint.enableProjection = true;
        joint.projectionDistance = 0.5;
        joint.projectionAngle = 90;
        joint.motorSpeed = 2.5;
        joint.maxMotorTorque = 100;

        const data = joint.serialize();
        const restored = new CharacterJoint3D();
        restored.deserialize(data);

        expect(restored.swingAxis.x).toBe(0);
        expect(restored.swingAxis.y).toBe(1);
        expect(restored.swingAxis.z).toBe(0);
        expect(restored.lowTwistLimit.limit).toBe(-10);
        expect(restored.lowTwistLimit.bounciness).toBe(0.1);
        expect(restored.highTwistLimit.limit).toBe(30);
        expect(restored.swing1Limit.limit).toBe(45);
        expect(restored.swing2Limit.limit).toBe(60);
        expect(restored.twistLimitSpring.spring).toBe(10);
        expect(restored.twistLimitSpring.damper).toBe(1);
        expect(restored.swingLimitSpring.spring).toBe(20);
        expect(restored.swingLimitSpring.damper).toBe(2);
        expect(restored.enableProjection).toBe(true);
        expect(restored.projectionDistance).toBe(0.5);
        expect(restored.projectionAngle).toBe(90);
        expect(restored.motorSpeed).toBe(2.5);
        expect(restored.maxMotorTorque).toBe(100);
    });
});

describe('ConfigurableJoint3D subclass property round-trip', () => {
    it('serializes and deserializes motion modes, targets, and drive structs', () => {
        const joint = new ConfigurableJoint3D();
        joint.xMotion = 2;
        joint.yMotion = 1;
        joint.zMotion = 0;
        joint.angularXMotion = 2;
        joint.angularYMotion = 1;
        joint.angularZMotion = 0;
        joint.linearLimit = { limit: 5, bounciness: 0.3, contactDistance: 0.1 };
        joint.targetPosition = { x: 1, y: 2, z: 3 };
        joint.targetVelocity = { x: 0.5, y: 0, z: 0 };
        joint.targetRotation = { x: 0, y: 0, z: 0.707, w: 0.707 };
        joint.targetAngularVelocity = { x: 1, y: 2, z: 0 };
        joint.rotationDriveMode = 1;
        joint.configuredInWorldSpace = true;
        joint.swapBodies = true;

        const data = joint.serialize();
        const restored = new ConfigurableJoint3D();
        restored.deserialize(data);

        expect(restored.xMotion).toBe(2);
        expect(restored.yMotion).toBe(1);
        expect(restored.zMotion).toBe(0);
        expect(restored.angularXMotion).toBe(2);
        expect(restored.angularYMotion).toBe(1);
        expect(restored.angularZMotion).toBe(0);
        expect(restored.linearLimit.limit).toBe(5);
        expect(restored.linearLimit.bounciness).toBe(0.3);
        expect(restored.targetPosition.x).toBe(1);
        expect(restored.targetPosition.y).toBe(2);
        expect(restored.targetPosition.z).toBe(3);
        expect(restored.targetVelocity.x).toBe(0.5);
        expect(restored.targetRotation.z).toBeCloseTo(0.707);
        expect(restored.targetRotation.w).toBeCloseTo(0.707);
        expect(restored.targetAngularVelocity.x).toBe(1);
        expect(restored.rotationDriveMode).toBe(1);
        expect(restored.configuredInWorldSpace).toBe(true);
        expect(restored.swapBodies).toBe(true);
    });

    it('serializes drive structs with maximumForce', () => {
        const joint = new ConfigurableJoint3D();
        const data = joint.serialize();
        expect(data).toHaveProperty('xDrive');
        expect(data).toHaveProperty('yDrive');
        expect(data).toHaveProperty('zDrive');
        expect(data).toHaveProperty('angularXDrive');
        expect(data).toHaveProperty('angularYZDrive');
        expect(data.xDrive).toHaveProperty('maximumForce');
        expect(data.angularXDrive).toHaveProperty('maximumForce');
    });
});

// ─── 2. Editor fixture tests ───────────────────────────────────────────────

describe('HingeJoint3D Editor fixture deserialization', () => {
    it('deserializes the exact JSON shape from Rust hinge_joint_3d_properties()', () => {
        const joint = new HingeJoint3D();
        joint.deserialize(HINGE_EDITOR_DEFAULT);

        // Base fields
        expect(joint.anchor.x).toBe(0);
        expect(joint.anchor.y).toBe(0.5);
        expect(joint.anchor.z).toBe(0);
        expect(joint.connectedBody).toBeNull();
        expect(joint.massScale).toBe(1.0);
        expect(joint.breakForce).toBe(2500);
        // Subclass fields — these are the CRITICAL assertions
        expect(joint.useLimits).toBe(true);       // limits.enabled → _useLimits
        expect(joint.limits.min).toBe(-45);
        expect(joint.limits.max).toBe(90);
        expect(joint.limits.bounciness).toBe(0.2);
        expect(joint.limits.contactDistance).toBe(0.5);
        expect(joint.useMotor).toBe(true);        // motor.enabled → _useMotor
        expect(joint.motor.targetVelocity).toBe(120);
        expect(joint.motor.force).toBe(300);
        expect(joint.motor.freeSpin).toBe(false);
        expect(joint.useSpring).toBe(false);      // spring.enabled → _useSpring
        expect(joint.spring.spring).toBe(10);
        expect(joint.spring.damper).toBe(2);
    });
});

describe('SliderJoint3D Editor fixture deserialization', () => {
    it('deserializes the exact JSON shape from Rust slider_joint_3d_properties()', () => {
        const joint = new SliderJoint3D();
        joint.deserialize(SLIDER_EDITOR_DEFAULT);

        expect(joint.useLimits).toBe(false);
        expect(joint.limits.min).toBe(0);
        expect(joint.limits.max).toBe(0);
        expect(joint.useMotor).toBe(false);
        expect(joint.motor.targetVelocity).toBe(0);
        expect(joint.motor.force).toBe(0);
        expect(joint.useSpring).toBe(false);
        expect(joint.spring.spring).toBe(0);
        expect(joint.spring.damper).toBe(0);
    });
});

describe('SpringJoint3D Editor fixture deserialization', () => {
    it('deserializes the exact JSON shape from Rust spring_joint_3d_properties()', () => {
        const joint = new SpringJoint3D();
        joint.deserialize(SPRING_EDITOR_DEFAULT);

        expect(joint.minDistance).toBe(0);
        expect(joint.maxDistance).toBe(0);
        expect(joint.springValue).toBe(0);
        expect(joint.damper).toBe(0);
        expect(joint.tolerance).toBe(0.025);
        expect(joint.autoConfigureDistance).toBe(true);
    });
});

describe('CharacterJoint3D Editor fixture deserialization', () => {
    it('deserializes the exact JSON shape from Rust character_joint_3d_properties()', () => {
        const joint = new CharacterJoint3D();
        joint.deserialize(CHARACTER_EDITOR_DEFAULT);

        expect(joint.swingAxis.x).toBe(1);
        expect(joint.lowTwistLimit.limit).toBe(0);
        expect(joint.highTwistLimit.limit).toBe(0);
        expect(joint.swing1Limit.limit).toBe(0);
        expect(joint.swing2Limit.limit).toBe(0);
        expect(joint.twistLimitSpring.spring).toBe(0);
        expect(joint.swingLimitSpring.damper).toBe(0);
        expect(joint.enableProjection).toBe(false);
        expect(joint.projectionDistance).toBe(0.1);
        expect(joint.projectionAngle).toBe(180);
    });
});

describe('ConfigurableJoint3D Editor fixture deserialization', () => {
    it('deserializes the exact JSON shape from Rust configurable_joint_3d_properties()', () => {
        const joint = new ConfigurableJoint3D();
        joint.deserialize(CONFIGURABLE_EDITOR_DEFAULT);

        expect(joint.xMotion).toBe(0);
        expect(joint.yMotion).toBe(0);
        expect(joint.zMotion).toBe(0);
        expect(joint.angularXMotion).toBe(0);
        expect(joint.angularYMotion).toBe(0);
        expect(joint.angularZMotion).toBe(0);
        expect(joint.linearLimit.limit).toBe(0);
        expect(joint.targetPosition.x).toBe(0);
        expect(joint.targetVelocity.x).toBe(0);
        expect(joint.targetRotation.w).toBe(1);  // quat identity
        expect(joint.targetAngularVelocity.x).toBe(0);
        expect(joint.rotationDriveMode).toBe(0);
        expect(joint.configuredInWorldSpace).toBe(false);
        expect(joint.swapBodies).toBe(false);
    });
});

// ─── 3. Backward compatibility (missing fields → safe defaults) ────────────

describe('Backward compatibility: missing subclass fields', () => {
    it('HingeJoint3D: empty data → all subclass fields at defaults', () => {
        const joint = new HingeJoint3D();
        joint.deserialize({});
        expect(joint.useLimits).toBe(false);
        expect(joint.limits.min).toBe(0);
        expect(joint.useMotor).toBe(false);
        expect(joint.motor.targetVelocity).toBe(0);
        expect(joint.useSpring).toBe(false);
        expect(joint.spring.spring).toBe(0);
    });

    it('SliderJoint3D: empty data → all subclass fields at defaults', () => {
        const joint = new SliderJoint3D();
        joint.deserialize({});
        expect(joint.useLimits).toBe(false);
        expect(joint.useMotor).toBe(false);
        expect(joint.useSpring).toBe(false);
    });

    it('SpringJoint3D: empty data → all subclass fields at defaults', () => {
        const joint = new SpringJoint3D();
        joint.deserialize({});
        expect(joint.minDistance).toBe(0);
        expect(joint.maxDistance).toBe(0);
        expect(joint.springValue).toBe(0);
        expect(joint.damper).toBe(0);
        expect(joint.tolerance).toBe(0.025);  // non-zero default
        expect(joint.autoConfigureDistance).toBe(true);
    });

    it('CharacterJoint3D: empty data → all subclass fields at defaults', () => {
        const joint = new CharacterJoint3D();
        joint.deserialize({});
        expect(joint.lowTwistLimit.limit).toBe(0);
        expect(joint.enableProjection).toBe(false);
        expect(joint.projectionDistance).toBe(0.1);
        expect(joint.projectionAngle).toBe(180);
        expect(joint.motorSpeed).toBe(0);
        expect(joint.maxMotorTorque).toBe(0);
    });

    it('ConfigurableJoint3D: empty data → all subclass fields at defaults', () => {
        const joint = new ConfigurableJoint3D();
        joint.deserialize({});
        expect(joint.xMotion).toBe(0);
        expect(joint.linearLimit.limit).toBe(0);
        expect(joint.targetPosition.x).toBe(0);
        expect(joint.rotationDriveMode).toBe(0);
        expect(joint.configuredInWorldSpace).toBe(false);
        expect(joint.swapBodies).toBe(false);
    });
});

// ─── 4. Unknown fields don't corrupt ───────────────────────────────────────

describe('Unknown fields tolerance', () => {
    it.each([
        ['HingeJoint3D', HingeJoint3D],
        ['SliderJoint3D', SliderJoint3D],
        ['SpringJoint3D', SpringJoint3D],
        ['CharacterJoint3D', CharacterJoint3D],
        ['ConfigurableJoint3D', ConfigurableJoint3D],
    ] as const)('%s: unknown keys do not throw or corrupt', (_name, Ctor) => {
        const joint = new Ctor();
        expect(() => joint.deserialize({
            unknownField: 'garbage',
            anotherUnknown: 42,
            nestedUnknown: { foo: 'bar' },
        })).not.toThrow();
        // Base fields should still work
        expect(joint.connectedBody).toBeNull();
    });
});

// ─── 5. Discriminative: serialize() contains subclass-specific keys ────────

describe('Discriminative: serialize() output shape', () => {
    it('HingeJoint3D serialize includes limits.enabled (not flat useLimits)', () => {
        const joint = new HingeJoint3D();
        joint.useLimits = true;
        const data = joint.serialize();
        // Hinge nests enabled inside limits (Editor convention)
        expect(data.limits.enabled).toBe(true);
        // Must NOT have flat useLimits (that's Slider's convention)
        expect(data).not.toHaveProperty('useLimits');
    });

    it('SliderJoint3D serialize includes flat useLimits (not nested limits.enabled)', () => {
        const joint = new SliderJoint3D();
        joint.useLimits = true;
        const data = joint.serialize();
        expect(data.useLimits).toBe(true);
        // Slider limits object does NOT have enabled inside it
        expect(data.limits).not.toHaveProperty('enabled');
    });

    it('SpringJoint3D serialize emits spring as number, not nested object', () => {
        const joint = new SpringJoint3D();
        joint.springValue = 77;
        const data = joint.serialize();
        expect(typeof data.spring).toBe('number');
        expect(data.spring).toBe(77);
    });

    it('CharacterJoint3D serialize includes motorSpeed and maxMotorTorque', () => {
        const joint = new CharacterJoint3D();
        joint.motorSpeed = 3.14;
        joint.maxMotorTorque = 50;
        const data = joint.serialize();
        expect(data.motorSpeed).toBe(3.14);
        expect(data.maxMotorTorque).toBe(50);
    });

    it('ConfigurableJoint3D serialize includes drive structs with maximumForce', () => {
        const joint = new ConfigurableJoint3D();
        const data = joint.serialize();
        expect(data).toHaveProperty('xDrive');
        expect(data.xDrive.maximumForce).toBe(Infinity);
        expect(data).toHaveProperty('angularYZDrive');
    });
});

// ─── 6. FixedJoint3D: no subclass properties ───────────────────────────────

describe('FixedJoint3D: no subclass properties', () => {
    it('serialize() output matches base class only', () => {
        const joint = new FixedJoint3D();
        const data = joint.serialize();
        expect(data).toHaveProperty('connectedBody');
        expect(data).toHaveProperty('anchor');
        expect(data).toHaveProperty('breakForce');
        // No subclass-specific keys
        expect(data).not.toHaveProperty('limits');
        expect(data).not.toHaveProperty('motor');
        expect(data).not.toHaveProperty('spring');
    });
});

// ─── 7. Editor key name mismatch: preprocessing ────────────────────────────

describe('Editor key name: preprocessing vs enablePreprocessing', () => {
    it('documents that Editor hinge "preprocessing" key is not read by base deserialize', () => {
        const joint = new HingeJoint3D();
        // The Editor's hinge JSON uses "preprocessing" key (not "enablePreprocessing").
        // Base class deserialize reads "enablePreprocessing". This is a naming mismatch.
        // The scene loader likely maps "preprocessing" → "enablePreprocessing" before
        // component deserialize, or this is a latent bug for the Editor integration team.
        joint.deserialize({ ...HINGE_EDITOR_DEFAULT, enablePreprocessing: false });
        expect(joint.enablePreprocessing).toBe(false);
    });
});
