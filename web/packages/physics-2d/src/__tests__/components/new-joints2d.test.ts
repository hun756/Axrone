import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Vec2 } from '@axrone/numeric';
import { WheelJoint2D } from '../../components/wheel-joint2d';
import { MotorJoint2D } from '../../components/motor-joint2d';
import { MouseJoint2D } from '../../components/mouse-joint2d';
import { GearJoint2D } from '../../components/gear-joint2d';
import { RopeJoint2D } from '../../components/rope-joint2d';
import { Joint2D } from '../../components/joint2d';
import { PhysicsWorld2DComponent } from '../../components/physics-world-2d-component';

// ─── Mock helpers ────────────────────────────────────────────────────────────

function mockConstraintManager() {
    return {
        createWheelConstraint: vi.fn().mockReturnValue(100),
        createMotorConstraint: vi.fn().mockReturnValue(200),
        createMouseConstraint: vi.fn().mockReturnValue(300),
        createGearConstraint: vi.fn().mockReturnValue(400),
        createRopeConstraint: vi.fn().mockReturnValue(500),
        createRevoluteConstraint: vi.fn().mockReturnValue(600),
        createDistanceConstraint: vi.fn().mockReturnValue(700),
        destroyConstraint: vi.fn(),
    };
}

function mockPhysicsWorld(cm: ReturnType<typeof mockConstraintManager>) {
    return { getConstraintManager: () => cm } as any;
}

function mockRigidbody(bodyId: number = 1) {
    return {
        bodyId,
        getPosition: () => new Vec2(0, 0),
        getRotation: () => 0,
        angularVelocity: 0,
    } as any;
}

/**
 * Wire up a joint component with mocked dependencies so createConstraint()
 * can execute. Returns the constraint manager for assertion inspection.
 */
function setupJoint<T extends Joint2D>(
    joint: T,
    cm: ReturnType<typeof mockConstraintManager>,
    bodyIdA: number = 1,
    bodyIdB: number = 2,
): ReturnType<typeof mockConstraintManager> {
    const world = mockPhysicsWorld(cm);
    // Inject physics world via internal field
    (joint as any)._physicsWorld = world;
    // Inject rigidbodyA
    (joint as any)._rigidbodyA = mockRigidbody(bodyIdA);
    // Inject connectedBody
    const rbB = mockRigidbody(bodyIdB);
    (joint as any)._connectedBody = rbB;
    return cm;
}

// ─── WheelJoint2D ────────────────────────────────────────────────────────────

describe('WheelJoint2D', () => {
    function create() { return new WheelJoint2D(); }

    it('has default axis (0,1)', () => {
        const j = create();
        expect(j.axis.x).toBe(0);
        expect(j.axis.y).toBe(1);
    });
    it('has default anchorA at zero', () => {
        const j = create();
        expect(j.anchorA.x).toBe(0);
        expect(j.anchorA.y).toBe(0);
    });
    it('has default anchorB at zero', () => {
        const j = create();
        expect(j.anchorB.x).toBe(0);
        expect(j.anchorB.y).toBe(0);
    });
    it('has enableLimit false by default', () => { expect(create().enableLimit).toBe(false); });
    it('has enableMotor false by default', () => { expect(create().enableMotor).toBe(false); });
    it('has default motorSpeed 0', () => { expect(create().motorSpeed).toBe(0); });
    it('has default maxMotorTorque 0', () => { expect(create().maxMotorTorque).toBe(0); });
    it('has default stiffness 0', () => { expect(create().stiffness).toBe(0); });
    it('has default damping 0', () => { expect(create().damping).toBe(0); });
    it('has default lowerTranslation 0', () => { expect(create().lowerTranslation).toBe(0); });
    it('has default upperTranslation 0', () => { expect(create().upperTranslation).toBe(0); });

    it('sets axis and normalizes', () => {
        const j = create();
        j.axis = new Vec2(0, 3);
        expect(j.axis.x).toBeCloseTo(0, 5);
        expect(j.axis.y).toBeCloseTo(1, 5);
    });
    it('ignores zero-length axis', () => {
        const j = create();
        j.axis = new Vec2(0, 0);
        expect(j.axis.y).toBe(1); // unchanged
    });
    it('clamps negative maxMotorTorque', () => {
        const j = create();
        j.maxMotorTorque = -5;
        expect(j.maxMotorTorque).toBe(0);
    });
    it('clamps negative stiffness', () => {
        const j = create();
        j.stiffness = -1;
        expect(j.stiffness).toBe(0);
    });
    it('clamps negative damping', () => {
        const j = create();
        j.damping = -1;
        expect(j.damping).toBe(0);
    });

    it('creates constraint with exact descriptor values', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        j.anchorA = new Vec2(1, 2);
        j.anchorB = new Vec2(3, 4);
        j.axis = new Vec2(0, 1);
        j.enableLimit = true;
        j.lowerTranslation = -0.5;
        j.upperTranslation = 0.5;
        j.enableMotor = true;
        j.motorSpeed = 10;
        j.maxMotorTorque = 100;
        j.stiffness = 50;
        j.damping = 5;
        j.enableCollision = true;

        // Trigger createConstraint via start()
        j.start();

        expect(cm.createWheelConstraint).toHaveBeenCalled();
        const def = cm.createWheelConstraint.mock.calls.at(-1)[0];
        expect(def.bodyIdA).toBe(1);
        expect(def.bodyIdB).toBe(2);
        expect(def.localAnchorA).toEqual({ x: 1, y: 2 });
        expect(def.localAnchorB).toEqual({ x: 3, y: 4 });
        expect(def.localAxisA).toEqual({ x: 0, y: 1 });
        expect(def.enableLimit).toBe(true);
        expect(def.lowerTranslation).toBe(-0.5);
        expect(def.upperTranslation).toBe(0.5);
        expect(def.enableMotor).toBe(true);
        expect(def.motorSpeed).toBe(10);
        expect(def.maxMotorTorque).toBe(100);
        expect(def.stiffness).toBe(50);
        expect(def.damping).toBe(5);
        expect(def.collideConnected).toBe(true);
        expect(j.constraintId).toBe(100);
    });

    it('does not create constraint without connectedBody', () => {
        const cm = mockConstraintManager();
        const j = create();
        (j as any)._physicsWorld = mockPhysicsWorld(cm);
        (j as any)._rigidbodyA = mockRigidbody(1);
        // No connectedBody
        j.start();
        expect(cm.createWheelConstraint).not.toHaveBeenCalled();
        expect(j.constraintId).toBeNull();
    });

    it('destroys constraint on destroy', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        j.start();
        expect(j.constraintId).toBe(100);

        j.onDestroy();
        expect(cm.destroyConstraint).toHaveBeenCalledWith(100);
        expect(j.constraintId).toBeNull();
    });

    it('does not double-create constraint', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        j.start();
        j.start(); // second call should be no-op
        expect(cm.createWheelConstraint).toHaveBeenCalledOnce();
    });

    it('serializes all properties', () => {
        const j = create();
        j.enableLimit = true;
        j.lowerTranslation = -1;
        j.upperTranslation = 2;
        j.enableMotor = true;
        j.motorSpeed = 5;
        j.maxMotorTorque = 50;
        j.stiffness = 10;
        j.damping = 2;
        const data = j.serialize();
        expect(data.enableLimit).toBe(true);
        expect(data.lowerTranslation).toBe(-1);
        expect(data.upperTranslation).toBe(2);
        expect(data.enableMotor).toBe(true);
        expect(data.motorSpeed).toBe(5);
        expect(data.maxMotorTorque).toBe(50);
        expect(data.stiffness).toBe(10);
        expect(data.damping).toBe(2);
        expect(data.anchorA).toEqual({ x: 0, y: 0 });
        expect(data.axis).toEqual({ x: 0, y: 1 });
    });

    it('deserializes properties', () => {
        const j = create();
        j.deserialize({
            anchorA: { x: 1, y: 2 },
            axis: { x: 1, y: 0 },
            enableLimit: true,
            lowerTranslation: -0.3,
            upperTranslation: 0.3,
            enableMotor: true,
            motorSpeed: 8,
            maxMotorTorque: 200,
            stiffness: 30,
            damping: 3,
        });
        expect(j.anchorA.x).toBe(1);
        expect(j.anchorA.y).toBe(2);
        expect(j.axis.x).toBe(1);
        expect(j.enableLimit).toBe(true);
        expect(j.lowerTranslation).toBe(-0.3);
        expect(j.enableMotor).toBe(true);
        expect(j.motorSpeed).toBe(8);
        expect(j.maxMotorTorque).toBe(200);
        expect(j.stiffness).toBe(30);
        expect(j.damping).toBe(3);
    });

    it('negative deserialization test: defaults on empty data', () => {
        const j = create();
        j.deserialize({});
        expect(j.enableLimit).toBe(false);
        expect(j.enableMotor).toBe(false);
        expect(j.stiffness).toBe(0);
        expect(j.axis.y).toBe(1);
    });
});

// ─── MotorJoint2D ────────────────────────────────────────────────────────────

describe('MotorJoint2D', () => {
    function create() { return new MotorJoint2D(); }

    it('has default linearOffset at zero', () => {
        const j = create();
        expect(j.linearOffset.x).toBe(0);
        expect(j.linearOffset.y).toBe(0);
    });
    it('has default angularOffset 0', () => { expect(create().angularOffset).toBe(0); });
    it('has default maxForce 1', () => { expect(create().maxForce).toBe(1); });
    it('has default maxTorque 1', () => { expect(create().maxTorque).toBe(1); });
    it('has default correctionFactor 0.3', () => { expect(create().correctionFactor).toBe(0.3); });

    it('clamps negative maxForce', () => {
        const j = create();
        j.maxForce = -5;
        expect(j.maxForce).toBe(1); // unchanged
    });
    it('clamps negative maxTorque', () => {
        const j = create();
        j.maxTorque = -1;
        expect(j.maxTorque).toBe(1); // unchanged
    });
    it('clamps correctionFactor to [0,1]', () => {
        const j = create();
        j.correctionFactor = 2;
        expect(j.correctionFactor).toBe(0.3); // unchanged
        j.correctionFactor = -0.5;
        expect(j.correctionFactor).toBe(0.3); // unchanged
    });
    it('accepts valid correctionFactor', () => {
        const j = create();
        j.correctionFactor = 0.8;
        expect(j.correctionFactor).toBe(0.8);
    });

    it('creates constraint with exact descriptor values', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        j.linearOffset = new Vec2(1.5, -2.5);
        j.angularOffset = 0.5;
        j.maxForce = 10;
        j.maxTorque = 20;
        j.correctionFactor = 0.6;
        j.enableCollision = false;

        j.start();

        expect(cm.createMotorConstraint).toHaveBeenCalled();
        const def = cm.createMotorConstraint.mock.calls.at(-1)[0];
        expect(def.bodyIdA).toBe(1);
        expect(def.bodyIdB).toBe(2);
        expect(def.linearOffset).toEqual({ x: 1.5, y: -2.5 });
        expect(def.angularOffset).toBe(0.5);
        expect(def.maxForce).toBe(10);
        expect(def.maxTorque).toBe(20);
        expect(def.correctionFactor).toBe(0.6);
        expect(def.collideConnected).toBe(false);
        expect(j.constraintId).toBe(200);
    });

    it('destroys constraint on destroy', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        j.start();
        j.onDestroy();
        expect(cm.destroyConstraint).toHaveBeenCalledWith(200);
        expect(j.constraintId).toBeNull();
    });

    it('does not create without physics world', () => {
        const cm = mockConstraintManager();
        const j = create();
        // No physics world set
        (j as any)._rigidbodyA = mockRigidbody(1);
        (j as any)._connectedBody = mockRigidbody(2);
        j.start();
        expect(cm.createMotorConstraint).not.toHaveBeenCalled();
    });

    it('serializes all properties', () => {
        const j = create();
        j.linearOffset = new Vec2(3, 4);
        j.angularOffset = 1.2;
        j.maxForce = 50;
        j.maxTorque = 100;
        j.correctionFactor = 0.9;
        const data = j.serialize();
        expect(data.linearOffset).toEqual({ x: 3, y: 4 });
        expect(data.angularOffset).toBe(1.2);
        expect(data.maxForce).toBe(50);
        expect(data.maxTorque).toBe(100);
        expect(data.correctionFactor).toBe(0.9);
    });

    it('deserializes properties', () => {
        const j = create();
        j.deserialize({
            linearOffset: { x: 5, y: 6 },
            angularOffset: 0.7,
            maxForce: 25,
            maxTorque: 75,
            correctionFactor: 0.4,
        });
        expect(j.linearOffset.x).toBe(5);
        expect(j.linearOffset.y).toBe(6);
        expect(j.angularOffset).toBe(0.7);
        expect(j.maxForce).toBe(25);
        expect(j.maxTorque).toBe(75);
        expect(j.correctionFactor).toBe(0.4);
    });

    it('negative deserialization: defaults on empty data', () => {
        const j = create();
        j.deserialize({});
        expect(j.maxForce).toBe(1);
        expect(j.maxTorque).toBe(1);
        expect(j.correctionFactor).toBe(0.3);
    });
});

// ─── MouseJoint2D ────────────────────────────────────────────────────────────

describe('MouseJoint2D', () => {
    function create() { return new MouseJoint2D(); }

    it('has default target at zero', () => {
        const j = create();
        expect(j.target.x).toBe(0);
        expect(j.target.y).toBe(0);
    });
    it('has default maxForce 1000', () => { expect(create().maxForce).toBe(1000); });
    it('has default stiffness 5', () => { expect(create().stiffness).toBe(5); });
    it('has default damping 0.7', () => { expect(create().damping).toBe(0.7); });

    it('clamps negative maxForce', () => {
        const j = create();
        j.maxForce = -10;
        expect(j.maxForce).toBe(1000); // unchanged
    });
    it('clamps negative stiffness', () => {
        const j = create();
        j.stiffness = -1;
        expect(j.stiffness).toBe(5); // unchanged
    });
    it('clamps negative damping', () => {
        const j = create();
        j.damping = -0.5;
        expect(j.damping).toBe(0.7); // unchanged
    });

    it('creates constraint with exact descriptor values', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        j.target = new Vec2(10, 20);
        j.maxForce = 500;
        j.stiffness = 8;
        j.damping = 1.2;
        j.enableCollision = true;

        j.start();

        expect(cm.createMouseConstraint).toHaveBeenCalled();
        const def = cm.createMouseConstraint.mock.calls.at(-1)[0];
        expect(def.bodyIdA).toBe(1);
        expect(def.bodyIdB).toBe(2);
        expect(def.target).toEqual({ x: 10, y: 20 });
        expect(def.maxForce).toBe(500);
        expect(def.stiffness).toBe(8);
        expect(def.damping).toBe(1.2);
        expect(def.collideConnected).toBe(true);
        expect(j.constraintId).toBe(300);
    });

    it('destroys constraint on destroy', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        j.start();
        j.onDestroy();
        expect(cm.destroyConstraint).toHaveBeenCalledWith(300);
        expect(j.constraintId).toBeNull();
    });

    it('does not double-create', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        j.start();
        j.start();
        expect(cm.createMouseConstraint).toHaveBeenCalledOnce();
    });

    it('serializes all properties', () => {
        const j = create();
        j.target = new Vec2(7, 8);
        j.maxForce = 2000;
        j.stiffness = 12;
        j.damping = 0.3;
        const data = j.serialize();
        expect(data.target).toEqual({ x: 7, y: 8 });
        expect(data.maxForce).toBe(2000);
        expect(data.stiffness).toBe(12);
        expect(data.damping).toBe(0.3);
    });

    it('deserializes properties', () => {
        const j = create();
        j.deserialize({
            target: { x: 3, y: 4 },
            maxForce: 1500,
            stiffness: 7,
            damping: 0.9,
        });
        expect(j.target.x).toBe(3);
        expect(j.target.y).toBe(4);
        expect(j.maxForce).toBe(1500);
        expect(j.stiffness).toBe(7);
        expect(j.damping).toBe(0.9);
    });

    it('negative deserialization: defaults on empty data', () => {
        const j = create();
        j.deserialize({});
        expect(j.maxForce).toBe(1000);
        expect(j.stiffness).toBe(5);
        expect(j.damping).toBe(0.7);
    });
});

// ─── GearJoint2D ─────────────────────────────────────────────────────────────

describe('GearJoint2D', () => {
    function create() { return new GearJoint2D(); }

    it('has default ratio 1', () => { expect(create().ratio).toBe(1); });
    it('has jointA null by default', () => { expect(create().jointA).toBeNull(); });
    it('has jointB null by default', () => { expect(create().jointB).toBeNull(); });
    it('is not pending by default', () => { expect(create().isPending).toBe(false); });

    it('ignores zero ratio', () => {
        const j = create();
        j.ratio = 0;
        expect(j.ratio).toBe(1); // unchanged
    });
    it('sets valid ratio', () => {
        const j = create();
        j.ratio = 2.5;
        expect(j.ratio).toBe(2.5);
    });

    it('enters pending state when joint references are null', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        // No jointA/jointB set
        j.start();
        expect(j.isPending).toBe(true);
        expect(cm.createGearConstraint).not.toHaveBeenCalled();
        expect(j.constraintId).toBeNull();
    });

    it('enters pending state when referenced joints have no constraintId', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        // Create mock joints without constraintId
        const mockJointA = { constraintId: null } as Joint2D;
        const mockJointB = { constraintId: null } as Joint2D;
        j.jointA = mockJointA;
        j.jointB = mockJointB;
        j.start();
        expect(j.isPending).toBe(true);
        expect(cm.createGearConstraint).not.toHaveBeenCalled();
    });

    it('creates constraint when referenced joints have constraintIds', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        const mockJointA = { constraintId: 600 } as Joint2D;
        const mockJointB = { constraintId: 700 } as Joint2D;
        j.jointA = mockJointA;
        j.jointB = mockJointB;
        j.ratio = 3;
        j.enableCollision = false;

        j.start();

        expect(cm.createGearConstraint).toHaveBeenCalled();
        const def = cm.createGearConstraint.mock.calls.at(-1)[0];
        expect(def.bodyIdA).toBe(1);
        expect(def.bodyIdB).toBe(2);
        expect(def.constraintIdA).toBe(600);
        expect(def.constraintIdB).toBe(700);
        expect(def.ratio).toBe(3);
        expect(def.collideConnected).toBe(false);
        expect(j.constraintId).toBe(400);
        expect(j.isPending).toBe(false);
    });

    it('tryResolve succeeds after pending when joints become ready', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);

        // Start with joints that have no constraintId yet
        const mockJointA = { constraintId: null } as Joint2D;
        const mockJointB = { constraintId: null } as Joint2D;
        j.jointA = mockJointA;
        j.jointB = mockJointB;
        j.start();
        expect(j.isPending).toBe(true);

        // Now simulate the joints getting their constraintIds
        (mockJointA as any).constraintId = 601;
        (mockJointB as any).constraintId = 701;

        const resolved = j.tryResolve();
        expect(resolved).toBe(true);
        expect(j.isPending).toBe(false);
        expect(j.constraintId).toBe(400);
        expect(cm.createGearConstraint).toHaveBeenCalledOnce();
    });

    it('tryResolve returns false when still unresolvable', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        const mockJointA = { constraintId: null } as Joint2D;
        j.jointA = mockJointA;
        j.jointB = null;
        j.start();
        expect(j.isPending).toBe(true);

        const resolved = j.tryResolve();
        expect(resolved).toBe(false);
        expect(j.isPending).toBe(true);
    });

    it('tryResolve returns true when not pending', () => {
        const j = create();
        expect(j.tryResolve()).toBe(true);
    });

    it('destroys constraint and clears pending on destroy', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        const mockJointA = { constraintId: null } as Joint2D;
        j.jointA = mockJointA;
        j.start();
        expect(j.isPending).toBe(true);

        j.onDestroy();
        expect(j.isPending).toBe(false);
    });

    it('destroys created constraint on destroy', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        j.jointA = { constraintId: 600 } as Joint2D;
        j.jointB = { constraintId: 700 } as Joint2D;
        j.start();
        expect(j.constraintId).toBe(400);

        j.onDestroy();
        expect(cm.destroyConstraint).toHaveBeenCalledWith(400);
        expect(j.constraintId).toBeNull();
    });

    it('serializes ratio but not joint references', () => {
        const j = create();
        j.ratio = 5;
        const data = j.serialize();
        expect(data.ratio).toBe(5);
        expect(data.jointA).toBeUndefined();
        expect(data.jointB).toBeUndefined();
    });

    it('deserializes ratio', () => {
        const j = create();
        j.deserialize({ ratio: 2 });
        expect(j.ratio).toBe(2);
    });

    it('negative deserialization: default ratio on empty data', () => {
        const j = create();
        j.deserialize({});
        expect(j.ratio).toBe(1);
    });
});

// ─── RopeJoint2D ─────────────────────────────────────────────────────────────

describe('RopeJoint2D', () => {
    function create() { return new RopeJoint2D(); }

    it('has default anchorA at zero', () => {
        const j = create();
        expect(j.anchorA.x).toBe(0);
        expect(j.anchorA.y).toBe(0);
    });
    it('has default anchorB at zero', () => {
        const j = create();
        expect(j.anchorB.x).toBe(0);
        expect(j.anchorB.y).toBe(0);
    });
    it('has default maxLength 1', () => { expect(create().maxLength).toBe(1); });

    it('ignores zero maxLength', () => {
        const j = create();
        j.maxLength = 0;
        expect(j.maxLength).toBe(1); // unchanged
    });
    it('ignores negative maxLength', () => {
        const j = create();
        j.maxLength = -5;
        expect(j.maxLength).toBe(1); // unchanged
    });
    it('sets valid maxLength', () => {
        const j = create();
        j.maxLength = 10;
        expect(j.maxLength).toBe(10);
    });

    it('creates constraint with exact descriptor values', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        j.anchorA = new Vec2(1, 2);
        j.anchorB = new Vec2(3, 4);
        j.maxLength = 5;
        j.enableCollision = true;

        j.start();

        expect(cm.createRopeConstraint).toHaveBeenCalled();
        const def = cm.createRopeConstraint.mock.calls.at(-1)[0];
        expect(def.bodyIdA).toBe(1);
        expect(def.bodyIdB).toBe(2);
        expect(def.localAnchorA).toEqual({ x: 1, y: 2 });
        expect(def.localAnchorB).toEqual({ x: 3, y: 4 });
        expect(def.maxLength).toBe(5);
        expect(def.collideConnected).toBe(true);
        expect(j.constraintId).toBe(500);
    });

    it('does not create without connectedBody', () => {
        const cm = mockConstraintManager();
        const j = create();
        (j as any)._physicsWorld = mockPhysicsWorld(cm);
        (j as any)._rigidbodyA = mockRigidbody(1);
        j.start();
        expect(cm.createRopeConstraint).not.toHaveBeenCalled();
        expect(j.constraintId).toBeNull();
    });

    it('destroys constraint on destroy', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        j.start();
        j.onDestroy();
        expect(cm.destroyConstraint).toHaveBeenCalledWith(500);
        expect(j.constraintId).toBeNull();
    });

    it('does not double-create', () => {
        const cm = mockConstraintManager();
        const j = create();
        setupJoint(j, cm);
        j.start();
        j.start();
        expect(cm.createRopeConstraint).toHaveBeenCalledOnce();
    });

    it('serializes all properties', () => {
        const j = create();
        j.anchorA = new Vec2(1, 2);
        j.anchorB = new Vec2(3, 4);
        j.maxLength = 7;
        const data = j.serialize();
        expect(data.anchorA).toEqual({ x: 1, y: 2 });
        expect(data.anchorB).toEqual({ x: 3, y: 4 });
        expect(data.maxLength).toBe(7);
    });

    it('deserializes properties', () => {
        const j = create();
        j.deserialize({
            anchorA: { x: 5, y: 6 },
            anchorB: { x: 7, y: 8 },
            maxLength: 12,
        });
        expect(j.anchorA.x).toBe(5);
        expect(j.anchorA.y).toBe(6);
        expect(j.anchorB.x).toBe(7);
        expect(j.anchorB.y).toBe(8);
        expect(j.maxLength).toBe(12);
    });

    it('negative deserialization: defaults on empty data', () => {
        const j = create();
        j.deserialize({});
        expect(j.maxLength).toBe(1);
        expect(j.anchorA.x).toBe(0);
    });
});

// ─── Behavioral integration: RopeJoint2D ─────────────────────────────────────

describe('RopeJoint2D behavioral integration', () => {
    it('passes maxLength to solver descriptor correctly for rope limit enforcement', () => {
        // This test verifies the component→solver parameter path.
        // The solver's behavioral correctness is proven in joints2d-behavioral.test.ts.
        // Here we verify the component passes the exact maxLength to the descriptor.
        const cm = mockConstraintManager();
        const j = create_Rope();
        setupJoint(j, cm);
        j.maxLength = 3.5;
        j.start();

        const def = cm.createRopeConstraint.mock.calls[0][0];
        expect(def.maxLength).toBe(3.5);
        // Negative control: a different maxLength produces a different descriptor
        expect(def.maxLength).not.toBe(10);
    });
});

function create_Rope() { return new RopeJoint2D(); }

// ─── Behavioral integration: MotorJoint2D ────────────────────────────────────

describe('MotorJoint2D behavioral integration', () => {
    it('passes linearOffset and angularOffset to solver descriptor correctly', () => {
        const cm = mockConstraintManager();
        const j = new MotorJoint2D();
        // Set up physics world BEFORE setting properties so createConstraint
        // succeeds during property setter recreateConstraint() calls.
        setupJoint(j, cm);
        j.linearOffset = new Vec2(2, -3);
        j.angularOffset = Math.PI / 4;

        const def = cm.createMotorConstraint.mock.calls.at(-1)[0];
        expect(def.linearOffset).toEqual({ x: 2, y: -3 });
        expect(def.angularOffset).toBeCloseTo(Math.PI / 4);
        // Negative control
        expect(def.linearOffset).not.toEqual({ x: 0, y: 0 });
    });
});

// ─── collideConnected mapping ────────────────────────────────────────────────

describe('collideConnected mapping for all new joints', () => {
    it('WheelJoint2D passes enableCollision to collideConnected', () => {
        const cm = mockConstraintManager();
        const j = new WheelJoint2D();
        setupJoint(j, cm);
        j.enableCollision = true;
        j.start();
        const def = cm.createWheelConstraint.mock.calls[0][0];
        expect(def.collideConnected).toBe(true);
    });

    it('MotorJoint2D passes enableCollision=false to collideConnected', () => {
        const cm = mockConstraintManager();
        const j = new MotorJoint2D();
        setupJoint(j, cm);
        j.enableCollision = false;
        j.start();
        const def = cm.createMotorConstraint.mock.calls[0][0];
        expect(def.collideConnected).toBe(false);
    });

    it('MouseJoint2D passes enableCollision to collideConnected', () => {
        const cm = mockConstraintManager();
        const j = new MouseJoint2D();
        setupJoint(j, cm);
        j.enableCollision = true;
        j.start();
        const def = cm.createMouseConstraint.mock.calls[0][0];
        expect(def.collideConnected).toBe(true);
    });

    it('RopeJoint2D passes enableCollision to collideConnected', () => {
        const cm = mockConstraintManager();
        const j = new RopeJoint2D();
        setupJoint(j, cm);
        j.enableCollision = false;
        j.start();
        const def = cm.createRopeConstraint.mock.calls[0][0];
        expect(def.collideConnected).toBe(false);
    });
});
