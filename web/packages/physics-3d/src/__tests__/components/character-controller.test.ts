import { describe, it, expect } from 'vitest';
import { CharacterController } from '../../components/character-controller';

describe('CharacterController', () => {
    function create() { return new CharacterController(); }

    describe('default state', () => {
        it('has default radius 0.5', () => { expect(create().radius).toBe(0.5); });
        it('has default height 2', () => { expect(create().height).toBe(2); });
        it('has default slopeLimit 45', () => { expect(create().slopeLimit).toBe(45); });
        it('has default stepOffset 0.3', () => { expect(create().stepOffset).toBe(0.3); });
        it('has default skinWidth 0.08', () => { expect(create().skinWidth).toBe(0.08); });
        it('has default minMoveDistance 0.001', () => { expect(create().minMoveDistance).toBe(0.001); });
        it('has enableOverlapRecovery true', () => { expect(create().enableOverlapRecovery).toBe(true); });
        it('has useGravity true', () => { expect(create().useGravity).toBe(true); });
        it('has detectCollisions true', () => { expect(create().detectCollisions).toBe(true); });
        it('has isGrounded false', () => { expect(create().isGrounded).toBe(false); });
        it('has zero velocity', () => {
            const v = create().velocity;
            expect(v.x).toBe(0); expect(v.y).toBe(0); expect(v.z).toBe(0);
        });
        it('has center at origin', () => {
            const c = create().center;
            expect(c.x).toBe(0); expect(c.y).toBe(0); expect(c.z).toBe(0);
        });
    });

    describe('radius', () => {
        it('sets radius', () => {
            const cc = create(); cc.radius = 1;
            expect(cc.radius).toBe(1);
        });
        it('clamps minimum radius to 0.01', () => {
            const cc = create(); cc.radius = 0;
            expect(cc.radius).toBe(0.01);
        });
    });

    describe('height', () => {
        it('sets height', () => {
            const cc = create(); cc.height = 3;
            expect(cc.height).toBe(3);
        });
        it('clamps minimum height to radius * 2', () => {
            const cc = create(); cc.height = 0;
            expect(cc.height).toBe(cc.radius * 2);
        });
    });

    describe('slopeLimit', () => {
        it('sets slopeLimit', () => {
            const cc = create(); cc.slopeLimit = 60;
            expect(cc.slopeLimit).toBe(60);
        });
        it('clamps to [0, 90]', () => {
            const cc = create(); cc.slopeLimit = 100;
            expect(cc.slopeLimit).toBe(90);
            cc.slopeLimit = -10;
            expect(cc.slopeLimit).toBe(0);
        });
    });

    describe('stepOffset', () => {
        it('sets stepOffset', () => {
            const cc = create(); cc.stepOffset = 0.5;
            expect(cc.stepOffset).toBe(0.5);
        });
        it('clamps to minimum 0', () => {
            const cc = create(); cc.stepOffset = -1;
            expect(cc.stepOffset).toBe(0);
        });
    });

    describe('skinWidth', () => {
        it('sets skinWidth', () => {
            const cc = create(); cc.skinWidth = 0.1;
            expect(cc.skinWidth).toBe(0.1);
        });
        it('clamps minimum to 0.001', () => {
            const cc = create(); cc.skinWidth = 0;
            expect(cc.skinWidth).toBe(0.001);
        });
    });

    describe('minMoveDistance', () => {
        it('sets minMoveDistance', () => {
            const cc = create(); cc.minMoveDistance = 0.01;
            expect(cc.minMoveDistance).toBe(0.01);
        });
        it('clamps to minimum 0', () => {
            const cc = create(); cc.minMoveDistance = -1;
            expect(cc.minMoveDistance).toBe(0);
        });
    });

    describe('flags', () => {
        it('toggles useGravity', () => {
            const cc = create(); cc.useGravity = false;
            expect(cc.useGravity).toBe(false);
        });
        it('toggles detectCollisions', () => {
            const cc = create(); cc.detectCollisions = false;
            expect(cc.detectCollisions).toBe(false);
        });
        it('toggles enableOverlapRecovery', () => {
            const cc = create(); cc.enableOverlapRecovery = false;
            expect(cc.enableOverlapRecovery).toBe(false);
        });
    });

    describe('center', () => {
        it('sets center', () => {
            const cc = create();
            cc.center = { x: 1, y: 2, z: 3 };
            expect(cc.center.x).toBe(1);
            expect(cc.center.y).toBe(2);
            expect(cc.center.z).toBe(3);
        });
    });

    describe('move without initialization', () => {
        it('returns 0 (no collision flags) when not initialized', () => {
            const cc = create();
            // Without calling initialize(), move should return 0
            const flags = (cc as any).move({ x: 1, y: 0, z: 0 });
            expect(flags).toBe(0);
        });
    });

    describe('simpleMove without initialization', () => {
        it('returns false when not initialized', () => {
            const cc = create();
            expect(cc.simpleMove({ x: 1, y: 0, z: 0 })).toBe(false);
        });
    });
});
