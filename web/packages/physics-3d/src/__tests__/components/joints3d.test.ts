import { describe, it, expect } from 'vitest';
import { FixedJoint3D } from '../../components/fixed-joint3d';
import { HingeJoint3D } from '../../components/hinge-joint3d';
import { SliderJoint3D } from '../../components/slider-joint3d';
import { SpringJoint3D } from '../../components/spring-joint3d';
import { ConfigurableJoint3D } from '../../components/configurable-joint3d';
import { CharacterJoint3D } from '../../components/character-joint3d';
import { Joint3D, JointDriveMode3D } from '../../components/joint3d';

describe('Joint3D base (via FixedJoint3D)', () => {
    function create() { return new FixedJoint3D(); }

    describe('default state', () => {
        it('has constraintId -1 (invalid)', () => { expect(create().constraintId).toBe(-1); });
        it('has connectedBody null', () => { expect(create().connectedBody).toBeNull(); });
        it('has breakForce Infinity', () => { expect(create().breakForce).toBe(Infinity); });
        it('has breakTorque Infinity', () => { expect(create().breakTorque).toBe(Infinity); });
        it('has enableCollision false', () => { expect(create().enableCollision).toBe(false); });
        it('has enablePreprocessing true', () => { expect(create().enablePreprocessing).toBe(true); });
        it('has massScale 1', () => { expect(create().massScale).toBe(1); });
        it('has connectedMassScale 1', () => { expect(create().connectedMassScale).toBe(1); });
        it('has autoConfigureConnectedAnchor true', () => { expect(create().autoConfigureConnectedAnchor).toBe(true); });
    });

    describe('anchor', () => {
        it('defaults to origin', () => {
            const a = create().anchor;
            expect(a.x).toBe(0); expect(a.y).toBe(0); expect(a.z).toBe(0);
        });
        it('sets anchor', () => {
            const j = create();
            j.anchor = { x: 1, y: 2, z: 3 };
            expect(j.anchor.x).toBe(1); expect(j.anchor.y).toBe(2); expect(j.anchor.z).toBe(3);
        });
    });

    describe('axis', () => {
        it('defaults to X axis', () => {
            const a = create().axis;
            expect(a.x).toBe(1); expect(a.y).toBe(0); expect(a.z).toBe(0);
        });
        it('sets and normalizes axis', () => {
            const j = create();
            j.axis = { x: 3, y: 0, z: 0 };
            expect(j.axis.x).toBeCloseTo(1, 5);
        });
        it('ignores zero-length axis', () => {
            const j = create();
            j.axis = { x: 0, y: 0, z: 0 };
            expect(j.axis.x).toBe(1);
        });
    });

    describe('secondaryAxis', () => {
        it('defaults to Y axis', () => {
            const a = create().secondaryAxis;
            expect(a.x).toBe(0); expect(a.y).toBe(1); expect(a.z).toBe(0);
        });
    });

    describe('breakForce / breakTorque', () => {
        it('clamps negative breakForce to 0', () => {
            const j = create();
            j.breakForce = -5;
            expect(j.breakForce).toBe(0);
        });
        it('clamps negative breakTorque to 0', () => {
            const j = create();
            j.breakTorque = -10;
            expect(j.breakTorque).toBe(0);
        });
    });

    describe('massScale', () => {
        it('clamps to minimum 0.0001', () => {
            const j = create();
            j.massScale = 0;
            expect(j.massScale).toBeCloseTo(0.0001, 4);
        });
    });

    describe('currentForce / currentTorque', () => {
        it('returns zero vectors', () => {
            const f = create().currentForce;
            expect(f.x).toBe(0); expect(f.y).toBe(0); expect(f.z).toBe(0);
            const t = create().currentTorque;
            expect(t.x).toBe(0); expect(t.y).toBe(0); expect(t.z).toBe(0);
        });
    });
});

describe('HingeJoint3D', () => {
    function create() { return new HingeJoint3D(); }

    it('has useLimits false by default', () => { expect(create().useLimits).toBe(false); });
    it('has useMotor false by default', () => { expect(create().useMotor).toBe(false); });
    it('has useSpring false by default', () => { expect(create().useSpring).toBe(false); });
    it('has angle 0', () => { expect(create().angle).toBe(0); });
    it('has velocity 0', () => { expect(create().velocity).toBe(0); });

    it('sets limits', () => {
        const j = create();
        j.limits = { min: -45, max: 45 };
        expect(j.limits.min).toBe(-45); expect(j.limits.max).toBe(45);
    });
    it('clamps bounciness to [0,1]', () => {
        const j = create();
        j.limits = { bounciness: 2 };
        expect(j.limits.bounciness).toBe(1);
    });
    it('sets motor', () => {
        const j = create();
        j.motor = { targetVelocity: 5, force: 100 };
        expect(j.motor.targetVelocity).toBe(5);
        expect(j.motor.force).toBe(100);
    });
    it('clamps negative motor force to 0', () => {
        const j = create();
        j.motor = { force: -10 };
        expect(j.motor.force).toBe(0);
    });
    it('sets spring', () => {
        const j = create();
        j.spring = { spring: 10, damper: 0.5 };
        expect(j.spring.spring).toBe(10);
        expect(j.spring.damper).toBe(0.5);
    });
});

describe('SpringJoint3D', () => {
    function create() { return new SpringJoint3D(); }

    it('has default minDistance 0', () => { expect(create().minDistance).toBe(0); });
    it('has default maxDistance 0', () => { expect(create().maxDistance).toBe(0); });
    it('has default springValue 0', () => { expect(create().springValue).toBe(0); });
    it('has default damper 0', () => { expect(create().damper).toBe(0); });
    it('has default tolerance 0.025', () => { expect(create().tolerance).toBe(0.025); });
    it('has autoConfigureDistance true', () => { expect(create().autoConfigureDistance).toBe(true); });

    it('sets springValue', () => {
        const j = create(); j.springValue = 10;
        expect(j.springValue).toBe(10);
    });
    it('clamps negative springValue to 0', () => {
        const j = create(); j.springValue = -5;
        expect(j.springValue).toBe(0);
    });
    it('sets damper', () => {
        const j = create(); j.damper = 0.5;
        expect(j.damper).toBe(0.5);
    });
    it('sets minDistance', () => {
        const j = create(); j.minDistance = 2;
        expect(j.minDistance).toBe(2);
    });
    it('sets maxDistance', () => {
        const j = create(); j.maxDistance = 5;
        expect(j.maxDistance).toBe(5);
    });
});

describe('SliderJoint3D', () => {
    function create() { return new SliderJoint3D(); }

    it('can be created', () => {
        expect(() => create()).not.toThrow();
    });
});

describe('ConfigurableJoint3D', () => {
    function create() { return new ConfigurableJoint3D(); }

    describe('linear motion defaults', () => {
        it('has xMotion 0 (locked)', () => { expect(create().xMotion).toBe(0); });
        it('has yMotion 0 (locked)', () => { expect(create().yMotion).toBe(0); });
        it('has zMotion 0 (locked)', () => { expect(create().zMotion).toBe(0); });
    });

    describe('angular motion defaults', () => {
        it('has angularXMotion 0', () => { expect(create().angularXMotion).toBe(0); });
        it('has angularYMotion 0', () => { expect(create().angularYMotion).toBe(0); });
        it('has angularZMotion 0', () => { expect(create().angularZMotion).toBe(0); });
    });

    describe('motion setters', () => {
        it('sets xMotion', () => { const j = create(); j.xMotion = 2; expect(j.xMotion).toBe(2); });
        it('sets yMotion', () => { const j = create(); j.yMotion = 1; expect(j.yMotion).toBe(1); });
        it('sets zMotion', () => { const j = create(); j.zMotion = 2; expect(j.zMotion).toBe(2); });
        it('sets angularXMotion', () => { const j = create(); j.angularXMotion = 2; expect(j.angularXMotion).toBe(2); });
        it('sets angularYMotion', () => { const j = create(); j.angularYMotion = 1; expect(j.angularYMotion).toBe(1); });
        it('sets angularZMotion', () => { const j = create(); j.angularZMotion = 2; expect(j.angularZMotion).toBe(2); });
    });

    describe('linear limit', () => {
        it('has default limit 0', () => { expect(create().linearLimit.limit).toBe(0); });
        it('sets linear limit', () => {
            const j = create();
            j.linearLimit = { limit: 5 };
            expect(j.linearLimit.limit).toBe(5);
        });
    });

    describe('target position', () => {
        it('defaults to origin', () => {
            const tp = create().targetPosition;
            expect(tp.x).toBe(0); expect(tp.y).toBe(0); expect(tp.z).toBe(0);
        });
        it('sets target position', () => {
            const j = create();
            j.targetPosition = { x: 1, y: 2, z: 3 };
            expect(j.targetPosition.x).toBe(1);
            expect(j.targetPosition.y).toBe(2);
            expect(j.targetPosition.z).toBe(3);
        });
    });

    describe('target velocity', () => {
        it('defaults to zero', () => {
            const tv = create().targetVelocity;
            expect(tv.x).toBe(0); expect(tv.y).toBe(0); expect(tv.z).toBe(0);
        });
    });

    describe('target rotation', () => {
        it('defaults to identity quaternion', () => {
            const tr = create().targetRotation;
            expect(tr.x).toBe(0); expect(tr.y).toBe(0); expect(tr.z).toBe(0); expect(tr.w).toBe(1);
        });
    });

    describe('rotation drive mode', () => {
        it('defaults to None', () => { expect(create().rotationDriveMode).toBe(JointDriveMode3D.None); });
        it('sets rotation drive mode', () => {
            const j = create();
            j.rotationDriveMode = JointDriveMode3D.Slerp;
            expect(j.rotationDriveMode).toBe(JointDriveMode3D.Slerp);
        });
    });

    describe('configuration flags', () => {
        it('has configuredInWorldSpace false by default', () => { expect(create().configuredInWorldSpace).toBe(false); });
        it('sets configuredInWorldSpace', () => {
            const j = create(); j.configuredInWorldSpace = true;
            expect(j.configuredInWorldSpace).toBe(true);
        });
        it('has swapBodies false by default', () => { expect(create().swapBodies).toBe(false); });
        it('sets swapBodies', () => {
            const j = create(); j.swapBodies = true;
            expect(j.swapBodies).toBe(true);
        });
    });
});

describe('CharacterJoint3D', () => {
    function create() { return new CharacterJoint3D(); }

    describe('swing axis', () => {
        it('defaults to X axis', () => {
            const sa = create().swingAxis;
            expect(sa.x).toBe(1); expect(sa.y).toBe(0); expect(sa.z).toBe(0);
        });
        it('sets and normalizes swing axis', () => {
            const j = create();
            j.swingAxis = { x: 0, y: 3, z: 0 };
            expect(j.swingAxis.y).toBeCloseTo(1, 5);
        });
        it('ignores zero-length swing axis', () => {
            const j = create();
            j.swingAxis = { x: 0, y: 0, z: 0 };
            expect(j.swingAxis.x).toBe(1);
        });
    });

    describe('twist limits', () => {
        it('has default lowTwistLimit', () => {
            expect(create().lowTwistLimit.limit).toBeDefined();
        });
        it('sets lowTwistLimit', () => {
            const j = create();
            j.lowTwistLimit = { limit: -30 };
            expect(j.lowTwistLimit.limit).toBe(-30);
        });
        it('sets highTwistLimit', () => {
            const j = create();
            j.highTwistLimit = { limit: 30 };
            expect(j.highTwistLimit.limit).toBe(30);
        });
    });

    describe('swing limits', () => {
        it('sets swing1Limit', () => {
            const j = create();
            j.swing1Limit = { limit: 45 };
            expect(j.swing1Limit.limit).toBe(45);
        });
        it('sets swing2Limit', () => {
            const j = create();
            j.swing2Limit = { limit: 60 };
            expect(j.swing2Limit.limit).toBe(60);
        });
    });

    describe('limit springs', () => {
        it('sets twistLimitSpring', () => {
            const j = create();
            j.twistLimitSpring = { spring: 10, damper: 0.5 };
            expect(j.twistLimitSpring.spring).toBe(10);
            expect(j.twistLimitSpring.damper).toBe(0.5);
        });
        it('sets swingLimitSpring', () => {
            const j = create();
            j.swingLimitSpring = { spring: 20, damper: 1 };
            expect(j.swingLimitSpring.spring).toBe(20);
        });
    });

    describe('projection', () => {
        it('has enableProjection false by default', () => { expect(create().enableProjection).toBe(false); });
        it('sets enableProjection', () => {
            const j = create(); j.enableProjection = true;
            expect(j.enableProjection).toBe(true);
        });
        it('has default projectionDistance 0.1', () => { expect(create().projectionDistance).toBe(0.1); });
        it('clamps negative projectionDistance to 0', () => {
            const j = create(); j.projectionDistance = -1;
            expect(j.projectionDistance).toBe(0);
        });
        it('has default projectionAngle 180', () => { expect(create().projectionAngle).toBe(180); });
        it('clamps negative projectionAngle to 0', () => {
            const j = create(); j.projectionAngle = -10;
            expect(j.projectionAngle).toBe(0);
        });
    });
});
