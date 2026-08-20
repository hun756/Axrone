import { describe, it, expect } from 'vitest';
import * as PhysicsCore from '../index';

describe('physics-core Foundation', () => {
    describe('BodyType Enum and Export Aliasing', () => {
        it('exports BodyType (not BodyTypeEnum) - governance rule', () => {
            expect(PhysicsCore.BodyType).toBeDefined();
            expect(PhysicsCore.BodyType.Static).toBe(0);
            expect(PhysicsCore.BodyType.Kinematic).toBe(1);
            expect(PhysicsCore.BodyType.Dynamic).toBe(2);
        });

        it('BodyType enum has exactly 3 values', () => {
            const values = Object.values(PhysicsCore.BodyType).filter(
                (v) => typeof v === 'number'
            );
            expect(values).toHaveLength(3);
        });

        it('BodyType values are distinct', () => {
            const { Static, Kinematic, Dynamic } = PhysicsCore.BodyType;
            expect(Static).not.toBe(Kinematic);
            expect(Static).not.toBe(Dynamic);
            expect(Kinematic).not.toBe(Dynamic);
        });
    });

    describe('ShapeType Enum', () => {
        it('exports ShapeType with all shape variants', () => {
            expect(PhysicsCore.ShapeType).toBeDefined();
            expect(PhysicsCore.ShapeType.Circle).toBe(0);
            expect(PhysicsCore.ShapeType.Capsule).toBe(1);
            expect(PhysicsCore.ShapeType.Polygon).toBe(2);
            expect(PhysicsCore.ShapeType.Box).toBe(3);
            expect(PhysicsCore.ShapeType.Segment).toBe(4);
            expect(PhysicsCore.ShapeType.Sphere).toBe(5);
            expect(PhysicsCore.ShapeType.Cylinder).toBe(6);
            expect(PhysicsCore.ShapeType.Cone).toBe(7);
            expect(PhysicsCore.ShapeType.ConvexHull).toBe(8);
            expect(PhysicsCore.ShapeType.TriangleMesh).toBe(9);
            expect(PhysicsCore.ShapeType.HeightField).toBe(10);
        });
    });

    describe('ConstraintType Enum', () => {
        it('exports ConstraintType with all constraint variants', () => {
            expect(PhysicsCore.ConstraintType).toBeDefined();
            expect(PhysicsCore.ConstraintType.Distance).toBe(0);
            expect(PhysicsCore.ConstraintType.Revolute).toBe(1);
            expect(PhysicsCore.ConstraintType.Prismatic).toBe(2);
            expect(PhysicsCore.ConstraintType.Weld).toBe(3);
            expect(PhysicsCore.ConstraintType.Wheel).toBe(4);
            expect(PhysicsCore.ConstraintType.Motor).toBe(5);
            expect(PhysicsCore.ConstraintType.Mouse).toBe(6);
            expect(PhysicsCore.ConstraintType.Gear).toBe(7);
            expect(PhysicsCore.ConstraintType.Rope).toBe(8);
        });
    });

    describe('CollisionFilter Enum', () => {
        it('exports CollisionFilter with bitmask values', () => {
            expect(PhysicsCore.CollisionFilter).toBeDefined();
            expect(PhysicsCore.CollisionFilter.None).toBe(0);
            expect(PhysicsCore.CollisionFilter.Default).toBe(1);
            expect(PhysicsCore.CollisionFilter.Static).toBe(2);
            expect(PhysicsCore.CollisionFilter.Dynamic).toBe(4);
            expect(PhysicsCore.CollisionFilter.Kinematic).toBe(8);
            expect(PhysicsCore.CollisionFilter.Trigger).toBe(16);
            expect(PhysicsCore.CollisionFilter.All).toBe(0xffff);
        });

        it('CollisionFilter values are valid bitmasks', () => {
            const { Default, Static, Dynamic, Kinematic, Trigger } = PhysicsCore.CollisionFilter;
            expect(Default & Static).toBe(0);
            expect(Default & Dynamic).toBe(0);
            expect(Static | Dynamic | Kinematic | Trigger).toBe(30);
        });
    });

    describe('SolverFlags Enum', () => {
        it('exports SolverFlags with composite Default value', () => {
            expect(PhysicsCore.SolverFlags).toBeDefined();
            expect(PhysicsCore.SolverFlags.None).toBe(0);
            expect(PhysicsCore.SolverFlags.WarmStarting).toBe(1 << 0);
            expect(PhysicsCore.SolverFlags.ContinuousDetection).toBe(1 << 1);
            expect(PhysicsCore.SolverFlags.SubStepping).toBe(1 << 2);
            expect(PhysicsCore.SolverFlags.SleepingBodies).toBe(1 << 3);
            expect(PhysicsCore.SolverFlags.PositionCorrection).toBe(1 << 4);
            expect(PhysicsCore.SolverFlags.VelocityConstraints).toBe(1 << 5);
        });

        it('SolverFlags.Default is a composite of multiple flags', () => {
            const { Default, WarmStarting, SleepingBodies, PositionCorrection, VelocityConstraints } =
                PhysicsCore.SolverFlags;
            expect(Default).toBe(WarmStarting | SleepingBodies | PositionCorrection | VelocityConstraints);
        });
    });

    describe('BodyFlags Enum', () => {
        it('exports BodyFlags with all flag variants', () => {
            expect(PhysicsCore.BodyFlags).toBeDefined();
            expect(PhysicsCore.BodyFlags.None).toBe(0);
            expect(PhysicsCore.BodyFlags.FixedRotation).toBe(1 << 0);
            expect(PhysicsCore.BodyFlags.Bullet).toBe(1 << 1);
            expect(PhysicsCore.BodyFlags.Sensor).toBe(1 << 2);
            expect(PhysicsCore.BodyFlags.Sleeping).toBe(1 << 3);
            expect(PhysicsCore.BodyFlags.AutoSleep).toBe(1 << 4);
            expect(PhysicsCore.BodyFlags.Awake).toBe(1 << 5);
            expect(PhysicsCore.BodyFlags.Active).toBe(1 << 6);
            expect(PhysicsCore.BodyFlags.Island).toBe(1 << 7);
        });
    });

    describe('Invalid ID Constants', () => {
        it('exports INVALID_BODY_ID as 0', () => {
            expect(PhysicsCore.INVALID_BODY_ID).toBe(0);
        });

        it('exports INVALID_SHAPE_ID as 0', () => {
            expect(PhysicsCore.INVALID_SHAPE_ID).toBe(0);
        });

        it('exports INVALID_CONSTRAINT_ID as 0', () => {
            expect(PhysicsCore.INVALID_CONSTRAINT_ID).toBe(0);
        });

        it('exports INVALID_CONTACT_ID as 0', () => {
            expect(PhysicsCore.INVALID_CONTACT_ID).toBe(0);
        });

        it('all invalid IDs are distinct branded types', () => {
            const { INVALID_BODY_ID, INVALID_SHAPE_ID, INVALID_CONSTRAINT_ID, INVALID_CONTACT_ID } =
                PhysicsCore;
            expect(INVALID_BODY_ID).not.toBe(INVALID_SHAPE_ID);
            expect(INVALID_BODY_ID).not.toBe(INVALID_CONSTRAINT_ID);
            expect(INVALID_BODY_ID).not.toBe(INVALID_CONTACT_ID);
        });
    });

    describe('PhysicsConstants', () => {
        it('exports PhysicsConstants as frozen object', () => {
            expect(PhysicsCore.PhysicsConstants).toBeDefined();
            expect(Object.isFrozen(PhysicsCore.PhysicsConstants)).toBe(true);
        });

        it('PhysicsConstants contains gravity defaults', () => {
            const { DEFAULT_GRAVITY_2D, DEFAULT_GRAVITY_3D } = PhysicsCore.PhysicsConstants;
            expect(DEFAULT_GRAVITY_2D).toEqual({ x: 0, y: -9.81 });
            expect(DEFAULT_GRAVITY_3D).toEqual({ x: 0, y: -9.81, z: 0 });
        });

        it('PhysicsConstants contains velocity limits', () => {
            const { MAX_VELOCITY, MAX_ANGULAR_VELOCITY } = PhysicsCore.PhysicsConstants;
            expect(MAX_VELOCITY).toBe(200.0);
            expect(MAX_ANGULAR_VELOCITY).toBe(250.0);
        });

        it('PhysicsConstants contains numerical tolerances', () => {
            const {
                LINEAR_SLOP,
                ANGULAR_SLOP,
                BAUMGARTE_FACTOR,
                TOI_BAUMGARTE,
                EPSILON,
            } = PhysicsCore.PhysicsConstants;
            expect(LINEAR_SLOP).toBe(0.005);
            expect(ANGULAR_SLOP).toBeCloseTo((2.0 / 180.0) * Math.PI);
            expect(BAUMGARTE_FACTOR).toBe(0.2);
            expect(TOI_BAUMGARTE).toBe(0.75);
            expect(EPSILON).toBe(1e-10);
        });

        it('PhysicsConstants contains solver parameters', () => {
            const {
                MAX_SUB_STEPS,
                MAX_TOI_CONTACTS,
                VELOCITY_THRESHOLD,
                MAX_LINEAR_CORRECTION,
                MAX_ANGULAR_CORRECTION,
            } = PhysicsCore.PhysicsConstants;
            expect(MAX_SUB_STEPS).toBe(8);
            expect(MAX_TOI_CONTACTS).toBe(32);
            expect(VELOCITY_THRESHOLD).toBe(1.0);
            expect(MAX_LINEAR_CORRECTION).toBe(0.2);
            expect(MAX_ANGULAR_CORRECTION).toBeCloseTo((8.0 / 180.0) * Math.PI);
        });

        it('PhysicsConstants contains sleep parameters', () => {
            const { SLEEP_TIME, LINEAR_SLEEP_TOLERANCE, ANGULAR_SLEEP_TOLERANCE } =
                PhysicsCore.PhysicsConstants;
            expect(SLEEP_TIME).toBe(0.5);
            expect(LINEAR_SLEEP_TOLERANCE).toBe(0.01);
            expect(ANGULAR_SLEEP_TOLERANCE).toBeCloseTo((2.0 / 180.0) * Math.PI);
        });
    });

    describe('Raycast Enums (Runtime Values)', () => {
        it('exports RaycastFlags as runtime enum', () => {
            expect(PhysicsCore.RaycastFlags).toBeDefined();
            expect(PhysicsCore.RaycastFlags.None).toBe(0);
            expect(PhysicsCore.RaycastFlags.ClosestOnly).toBe(1 << 0);
            expect(PhysicsCore.RaycastFlags.AllHits).toBe(1 << 1);
            expect(PhysicsCore.RaycastFlags.IgnoreTriggers).toBe(1 << 2);
            expect(PhysicsCore.RaycastFlags.IgnoreBackfaces).toBe(1 << 3);
            expect(PhysicsCore.RaycastFlags.PreciseHitNormal).toBe(1 << 4);
            expect(PhysicsCore.RaycastFlags.SortByDistance).toBe(1 << 5);
            expect(PhysicsCore.RaycastFlags.StopAtFirstHit).toBe(1 << 6);
            expect(PhysicsCore.RaycastFlags.IncludeInactive).toBe(1 << 7);
        });

        it('exports RaycastLayer as runtime enum', () => {
            expect(PhysicsCore.RaycastLayer).toBeDefined();
            expect(PhysicsCore.RaycastLayer.Default).toBe(1 << 0);
            expect(PhysicsCore.RaycastLayer.Static).toBe(1 << 1);
            expect(PhysicsCore.RaycastLayer.Dynamic).toBe(1 << 2);
            expect(PhysicsCore.RaycastLayer.Kinematic).toBe(1 << 3);
            expect(PhysicsCore.RaycastLayer.Trigger).toBe(1 << 4);
            expect(PhysicsCore.RaycastLayer.Character).toBe(1 << 5);
            expect(PhysicsCore.RaycastLayer.Terrain).toBe(1 << 6);
            expect(PhysicsCore.RaycastLayer.Projectile).toBe(1 << 7);
            expect(PhysicsCore.RaycastLayer.Water).toBe(1 << 8);
            expect(PhysicsCore.RaycastLayer.Transparent).toBe(1 << 9);
            expect(PhysicsCore.RaycastLayer.All).toBe(0xffffffff);
        });

        it('RaycastLayer.All includes all layers', () => {
            const { All, Default, Static, Dynamic, Kinematic, Trigger } = PhysicsCore.RaycastLayer;
            expect(All & Default).toBe(Default);
            expect(All & Static).toBe(Static);
            expect(All & Dynamic).toBe(Dynamic);
            expect(All & Kinematic).toBe(Kinematic);
            expect(All & Trigger).toBe(Trigger);
        });

        it('exports RayIntersectionType enum', () => {
            expect(PhysicsCore.RayIntersectionType).toBeDefined();
            expect(PhysicsCore.RayIntersectionType.None).toBe(0);
            expect(PhysicsCore.RayIntersectionType.Entry).toBe(1);
            expect(PhysicsCore.RayIntersectionType.Exit).toBe(2);
            expect(PhysicsCore.RayIntersectionType.Tangent).toBe(3);
        });
    });

    describe('3D-Specific Enums and Constants', () => {
        it('exports ShapeType3D enum', () => {
            expect(PhysicsCore.ShapeType3D).toBeDefined();
            expect(PhysicsCore.ShapeType3D.Sphere).toBe(5);
            expect(PhysicsCore.ShapeType3D.Box).toBe(3);
            expect(PhysicsCore.ShapeType3D.Capsule).toBe(1);
            expect(PhysicsCore.ShapeType3D.Cylinder).toBe(6);
            expect(PhysicsCore.ShapeType3D.Cone).toBe(7);
            expect(PhysicsCore.ShapeType3D.ConvexHull).toBe(8);
            expect(PhysicsCore.ShapeType3D.TriangleMesh).toBe(9);
            expect(PhysicsCore.ShapeType3D.HeightField).toBe(10);
        });

        it('exports ConstraintType3D enum', () => {
            expect(PhysicsCore.ConstraintType3D).toBeDefined();
            expect(PhysicsCore.ConstraintType3D.Fixed).toBe(0);
            expect(PhysicsCore.ConstraintType3D.Spherical).toBe(1);
            expect(PhysicsCore.ConstraintType3D.Hinge).toBe(2);
            expect(PhysicsCore.ConstraintType3D.Slider).toBe(3);
            expect(PhysicsCore.ConstraintType3D.ConeTwist).toBe(4);
            expect(PhysicsCore.ConstraintType3D.Generic).toBe(5);
            expect(PhysicsCore.ConstraintType3D.Spring).toBe(6);
        });

        it('exports 3D invalid ID constants', () => {
            expect(PhysicsCore.INVALID_BODY_ID_3D).toBe(0);
            expect(PhysicsCore.INVALID_SHAPE_ID_3D).toBe(0);
            expect(PhysicsCore.INVALID_CONSTRAINT_ID_3D).toBe(0);
        });
    });

    describe('Collision and Contact Enums', () => {
        it('exports CollisionEventType enum', () => {
            expect(PhysicsCore.CollisionEventType).toBeDefined();
            expect(PhysicsCore.CollisionEventType.Begin).toBe(0);
            expect(PhysicsCore.CollisionEventType.Stay).toBe(1);
            expect(PhysicsCore.CollisionEventType.End).toBe(2);
            expect(PhysicsCore.CollisionEventType.PreSolve).toBe(3);
            expect(PhysicsCore.CollisionEventType.PostSolve).toBe(4);
        });

        it('exports SensorEventType enum', () => {
            expect(PhysicsCore.SensorEventType).toBeDefined();
            expect(PhysicsCore.SensorEventType.Enter).toBe(0);
            expect(PhysicsCore.SensorEventType.Stay).toBe(1);
            expect(PhysicsCore.SensorEventType.Exit).toBe(2);
        });

        it('exports TOIState enum', () => {
            expect(PhysicsCore.TOIState).toBeDefined();
            expect(PhysicsCore.TOIState.Unknown).toBe(0);
            expect(PhysicsCore.TOIState.Failed).toBe(1);
            expect(PhysicsCore.TOIState.Overlapped).toBe(2);
            expect(PhysicsCore.TOIState.Touching).toBe(3);
            expect(PhysicsCore.TOIState.Separated).toBe(4);
        });

        it('exports JointLimitState enum', () => {
            expect(PhysicsCore.JointLimitState).toBeDefined();
            expect(PhysicsCore.JointLimitState.Inactive).toBe(0);
            expect(PhysicsCore.JointLimitState.AtLower).toBe(1);
            expect(PhysicsCore.JointLimitState.AtUpper).toBe(2);
            expect(PhysicsCore.JointLimitState.Equal).toBe(3);
        });
    });

    describe('BroadphaseType Enum', () => {
        it('exports BroadphaseType enum', () => {
            expect(PhysicsCore.BroadphaseType).toBeDefined();
            expect(PhysicsCore.BroadphaseType.BruteForce).toBe(0);
            expect(PhysicsCore.BroadphaseType.SweepAndPrune).toBe(1);
            expect(PhysicsCore.BroadphaseType.DynamicAABBTree).toBe(2);
            expect(PhysicsCore.BroadphaseType.SpatialHash).toBe(3);
            expect(PhysicsCore.BroadphaseType.Quadtree).toBe(4);
            expect(PhysicsCore.BroadphaseType.Octree).toBe(5);
        });
    });

    describe('Export Completeness', () => {
        it('exports all required enums', () => {
            const requiredEnums = [
                'BodyType',
                'ShapeType',
                'ConstraintType',
                'CollisionFilter',
                'SolverFlags',
                'BodyFlags',
                'RaycastFlags',
                'RaycastLayer',
                'RayIntersectionType',
                'ShapeType3D',
                'ConstraintType3D',
                'CollisionEventType',
                'SensorEventType',
                'TOIState',
                'JointLimitState',
                'BroadphaseType',
            ];
            requiredEnums.forEach((enumName) => {
                expect(PhysicsCore[enumName]).toBeDefined();
            });
        });

        it('exports all required constants', () => {
            const requiredConstants = [
                'INVALID_BODY_ID',
                'INVALID_SHAPE_ID',
                'INVALID_CONSTRAINT_ID',
                'INVALID_CONTACT_ID',
                'INVALID_BODY_ID_3D',
                'INVALID_SHAPE_ID_3D',
                'INVALID_CONSTRAINT_ID_3D',
                'PhysicsConstants',
            ];
            requiredConstants.forEach((constName) => {
                expect(PhysicsCore[constName]).toBeDefined();
            });
        });

        it('does not export BodyTypeEnum (governance rule)', () => {
            expect((PhysicsCore as Record<string, unknown>)['BodyTypeEnum']).toBeUndefined();
        });
    });

    describe('Cross-Package Compatibility', () => {
        it('BodyType can be used in type guards', () => {
            const isStatic = (type: number) => type === PhysicsCore.BodyType.Static;
            expect(isStatic(PhysicsCore.BodyType.Static)).toBe(true);
            expect(isStatic(PhysicsCore.BodyType.Dynamic)).toBe(false);
        });

        it('CollisionFilter supports bitwise operations', () => {
            const { Default, Static, Dynamic } = PhysicsCore.CollisionFilter;
            const combined = Default | Static | Dynamic;
            expect(combined & Default).toBe(Default);
            expect(combined & Static).toBe(Static);
            expect(combined & Dynamic).toBe(Dynamic);
        });

        it('SolverFlags can be combined', () => {
            const { WarmStarting, SleepingBodies } = PhysicsCore.SolverFlags;
            const combined = WarmStarting | SleepingBodies;
            expect(combined & WarmStarting).toBe(WarmStarting);
            expect(combined & SleepingBodies).toBe(SleepingBodies);
        });

        it('BodyFlags can be combined', () => {
            const { FixedRotation, Bullet, Sensor } = PhysicsCore.BodyFlags;
            const combined = FixedRotation | Bullet | Sensor;
            expect(combined & FixedRotation).toBe(FixedRotation);
            expect(combined & Bullet).toBe(Bullet);
            expect(combined & Sensor).toBe(Sensor);
        });

        it('RaycastFlags can be combined', () => {
            const { ClosestOnly, IgnoreTriggers, SortByDistance } = PhysicsCore.RaycastFlags;
            const combined = ClosestOnly | IgnoreTriggers | SortByDistance;
            expect(combined & ClosestOnly).toBe(ClosestOnly);
            expect(combined & IgnoreTriggers).toBe(IgnoreTriggers);
            expect(combined & SortByDistance).toBe(SortByDistance);
        });

        it('RaycastLayer supports layer masking', () => {
            const { Default, Static, Dynamic, All } = PhysicsCore.RaycastLayer;
            const mask = Default | Static;
            expect(mask & Default).toBe(Default);
            expect(mask & Static).toBe(Static);
            expect(mask & Dynamic).toBe(0);
            expect(All & Default).toBe(Default);
        });
    });
});
