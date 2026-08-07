import { describe, it, expect } from 'vitest';
import { DistanceJoint2D } from '../../components/distance-joint2d';
import { HingeJoint2D } from '../../components/hinge-joint2d';
import { SliderJoint2D } from '../../components/slider-joint2d';
import { SpringJoint2D } from '../../components/spring-joint2d';
import { FixedJoint2D } from '../../components/fixed-joint2d';

describe('DistanceJoint2D', () => {
    function create() { return new DistanceJoint2D(); }

    it('has default distance 1', () => { expect(create().distance).toBe(1); });
    it('has default stiffness 0', () => { expect(create().stiffness).toBe(0); });
    it('has default damping 0', () => { expect(create().damping).toBe(0); });
    it('has default minDistance 0', () => { expect(create().minDistance).toBe(0); });
    it('has default maxDistance Infinity', () => { expect(create().maxDistance).toBe(Infinity); });

    it('sets distance', () => {
        const j = create(); j.distance = 5;
        expect(j.distance).toBe(5);
    });
    it('ignores negative distance', () => {
        const j = create(); j.distance = -1;
        expect(j.distance).toBe(1);
    });
    it('sets stiffness', () => {
        const j = create(); j.stiffness = 10;
        expect(j.stiffness).toBe(10);
    });
    it('sets damping', () => {
        const j = create(); j.damping = 0.5;
        expect(j.damping).toBe(0.5);
    });

    it('serializes all properties', () => {
        const j = create();
        j.distance = 3; j.stiffness = 5; j.damping = 0.2;
        const data = j.serialize();
        expect(data.distance).toBe(3);
        expect(data.stiffness).toBe(5);
        expect(data.damping).toBe(0.2);
    });

    it('deserializes properties', () => {
        const j = create();
        j.deserialize({ distance: 7, stiffness: 3, damping: 0.1 });
        expect(j.distance).toBe(7);
        expect(j.stiffness).toBe(3);
        expect(j.damping).toBe(0.1);
    });
});

describe('HingeJoint2D', () => {
    function create() { return new HingeJoint2D(); }

    it('has default anchor at zero', () => {
        const a = create().anchor;
        expect(a.x).toBe(0); expect(a.y).toBe(0);
    });
    it('has useMotor false by default', () => { expect(create().useMotor).toBe(false); });
    it('has useLimits false by default', () => { expect(create().useLimits).toBe(false); });
    it('has default motor speed 0', () => { expect(create().motorSpeed).toBe(0); });
    it('has default maxMotorTorque 10000', () => { expect(create().maxMotorTorque).toBe(10000); });

    it('sets anchor', () => {
        const j = create();
        j.anchor = { x: 1, y: 2 } as any;
        expect(j.anchor.x).toBe(1); expect(j.anchor.y).toBe(2);
    });
    it('toggles useMotor', () => {
        const j = create(); j.useMotor = true;
        expect(j.useMotor).toBe(true);
    });
    it('sets motorSpeed', () => {
        const j = create(); j.motorSpeed = 5;
        expect(j.motorSpeed).toBe(5);
    });
    it('clamps negative maxMotorTorque to 0', () => {
        const j = create(); j.maxMotorTorque = -10;
        expect(j.maxMotorTorque).toBe(0);
    });
    it('sets limits', () => {
        const j = create();
        j.limits = { min: -45, max: 45 };
        expect(j.limits.min).toBe(-45); expect(j.limits.max).toBe(45);
    });

    it('getJointAngle returns 0 without connected bodies', () => {
        expect(create().getJointAngle()).toBe(0);
    });
    it('getJointSpeed returns 0 without connected bodies', () => {
        expect(create().getJointSpeed()).toBe(0);
    });

    it('serializes all properties', () => {
        const j = create();
        j.useMotor = true; j.motorSpeed = 3; j.useLimits = true;
        const data = j.serialize();
        expect(data.useMotor).toBe(true);
        expect(data.motorSpeed).toBe(3);
        expect(data.useLimits).toBe(true);
    });
});

describe('SliderJoint2D', () => {
    function create() { return new SliderJoint2D(); }

    it('has default axis (1,0)', () => {
        const a = create().axis;
        expect(a.x).toBe(1); expect(a.y).toBe(0);
    });
    it('has default motorSpeed 0', () => { expect(create().motorSpeed).toBe(0); });
    it('has default maxMotorForce 10000', () => { expect(create().maxMotorForce).toBe(10000); });
    it('has default limits', () => {
        const l = create().limits;
        expect(l.min).toBe(-1); expect(l.max).toBe(1);
    });

    it('sets axis and normalizes', () => {
        const j = create();
        j.axis = { x: 3, y: 0 } as any;
        expect(j.axis.x).toBeCloseTo(1, 5);
    });
    it('clamps negative maxMotorForce to 0', () => {
        const j = create(); j.maxMotorForce = -5;
        expect(j.maxMotorForce).toBe(0);
    });

    it('serializes all properties', () => {
        const j = create();
        j.useMotor = true; j.motorSpeed = 2; j.maxMotorForce = 500;
        const data = j.serialize();
        expect(data.useMotor).toBe(true);
        expect(data.motorSpeed).toBe(2);
        expect(data.maxMotorForce).toBe(500);
    });
});

describe('SpringJoint2D', () => {
    function create() { return new SpringJoint2D(); }

    it('has default distance 1', () => { expect(create().distance).toBe(1); });
    it('has default stiffness 10', () => { expect(create().stiffness).toBe(10); });
    it('has default damping 0.5', () => { expect(create().damping).toBe(0.5); });

    it('sets distance', () => {
        const j = create(); j.distance = 3;
        expect(j.distance).toBe(3);
    });
    it('ignores negative distance', () => {
        const j = create(); j.distance = -1;
        expect(j.distance).toBe(1);
    });
    it('sets stiffness', () => {
        const j = create(); j.stiffness = 20;
        expect(j.stiffness).toBe(20);
    });
    it('sets damping', () => {
        const j = create(); j.damping = 1.5;
        expect(j.damping).toBe(1.5);
    });

    it('serializes properties', () => {
        const j = create();
        j.stiffness = 15; j.damping = 0.8;
        const data = j.serialize();
        expect(data.stiffness).toBe(15);
        expect(data.damping).toBe(0.8);
    });
});

describe('FixedJoint2D', () => {
    function create() { return new FixedJoint2D(); }

    it('has default anchor at zero', () => {
        const a = create().anchor;
        expect(a.x).toBe(0); expect(a.y).toBe(0);
    });
    it('has default dampingRatio 0.7', () => { expect(create().dampingRatio).toBe(0.7); });
    it('has default frequency 0', () => { expect(create().frequency).toBe(0); });

    it('sets anchor', () => {
        const j = create();
        j.anchor = { x: 2, y: 3 } as any;
        expect(j.anchor.x).toBe(2); expect(j.anchor.y).toBe(3);
    });
    it('sets dampingRatio within [0,1]', () => {
        const j = create();
        j.dampingRatio = 0.5;
        expect(j.dampingRatio).toBe(0.5);
    });
    it('ignores dampingRatio outside [0,1]', () => {
        const j = create();
        j.dampingRatio = 2;
        expect(j.dampingRatio).toBe(0.7);
        j.dampingRatio = -1;
        expect(j.dampingRatio).toBe(0.7);
    });
    it('sets frequency', () => {
        const j = create(); j.frequency = 5;
        expect(j.frequency).toBe(5);
    });
    it('ignores negative frequency', () => {
        const j = create(); j.frequency = -1;
        expect(j.frequency).toBe(0);
    });

    it('serializes properties', () => {
        const j = create();
        j.dampingRatio = 0.3; j.frequency = 10;
        const data = j.serialize();
        expect(data.dampingRatio).toBe(0.3);
        expect(data.frequency).toBe(10);
    });
});

describe('Joint2D base properties (via DistanceJoint2D)', () => {
    function create() { return new DistanceJoint2D(); }

    it('constraintId is null before start', () => {
        expect(create().constraintId).toBeNull();
    });
    it('connectedBody is null by default', () => {
        expect(create().connectedBody).toBeNull();
    });
    it('enableCollision is false by default', () => {
        expect(create().enableCollision).toBe(false);
    });
    it('breakForce defaults to Infinity', () => {
        expect(create().breakForce).toBe(Infinity);
    });
    it('breakTorque defaults to Infinity', () => {
        expect(create().breakTorque).toBe(Infinity);
    });

    it('sets breakForce (clamps negative to 0)', () => {
        const j = create();
        j.breakForce = 100;
        expect(j.breakForce).toBe(100);
        j.breakForce = -5;
        expect(j.breakForce).toBe(0);
    });
    it('sets breakTorque (clamps negative to 0)', () => {
        const j = create();
        j.breakTorque = 50;
        expect(j.breakTorque).toBe(50);
    });

    it('serializes base joint properties', () => {
        const j = create();
        j.enableCollision = true;
        j.breakForce = 200;
        const data = j.serialize();
        expect(data.enableCollision).toBe(true);
        expect(data.breakForce).toBe(200);
    });

    it('deserializes base joint properties', () => {
        const j = create();
        j.deserialize({ enableCollision: true, breakForce: 300, breakTorque: 100 });
        expect(j.enableCollision).toBe(true);
        expect(j.breakForce).toBe(300);
        expect(j.breakTorque).toBe(100);
    });
});
