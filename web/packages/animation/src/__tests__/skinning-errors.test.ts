import { describe, expect, it } from 'vitest';
import { computeSkinningPalette, computeRigSkinningPalette } from '../skinning';
import {
    AnimationError,
    AnimationValidationError,
    AnimationSamplingError,
    AnimationStateMachineError,
    AnimationRetargetingError,
    AnimationIkError,
    assertNever,
} from '../errors';
import { AnimationRig } from '../rig';
import { AnimationPose, AnimationWorldPose } from '../pose';

const IDENTITY_MATRIX_4x4 = [
    1, 0, 0, 0,
    0, 1, 0, 0,
    0, 0, 1, 0,
    0, 0, 0, 1,
];

describe('computeSkinningPalette', () => {
    it('identity mesh matrix produces joint matrices equal to joint world matrices', () => {
        const jointWorld = [
            new Float32Array([
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                5, 10, 15, 1,
            ]),
        ];
        const palette = computeSkinningPalette({
            meshWorldMatrix: IDENTITY_MATRIX_4x4,
            jointWorldMatrices: jointWorld,
        });
        expect(palette[12]).toBeCloseTo(5, 5);
        expect(palette[13]).toBeCloseTo(10, 5);
        expect(palette[14]).toBeCloseTo(15, 5);
    });

    it('singular mesh matrix falls back to identity per joint', () => {
        const singularMesh = new Array(16).fill(0);
        const jointWorld = [
            new Float32Array(IDENTITY_MATRIX_4x4),
        ];
        const palette = computeSkinningPalette({
            meshWorldMatrix: singularMesh,
            jointWorldMatrices: jointWorld,
        });
        // Should write identity for each joint
        expect(palette[0]).toBe(1);
        expect(palette[5]).toBe(1);
        expect(palette[10]).toBe(1);
        expect(palette[15]).toBe(1);
    });

    it('without inverseBindMatrices returns inverse-mesh * joint', () => {
        const meshMatrix = [
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            10, 0, 0, 1,
        ];
        const jointWorld = [
            new Float32Array([
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                15, 0, 0, 1,
            ]),
        ];
        const palette = computeSkinningPalette({
            meshWorldMatrix: meshMatrix,
            jointWorldMatrices: jointWorld,
        });
        // inverse(mesh) * joint = translate(-10) * translate(15) = translate(5)
        expect(palette[12]).toBeCloseTo(5, 4);
    });

    it('with inverseBindMatrices applies them', () => {
        const ibm = new Float32Array(16);
        ibm[0] = 2; ibm[5] = 2; ibm[10] = 2; ibm[15] = 1;
        const jointWorld = [new Float32Array(IDENTITY_MATRIX_4x4)];
        const palette = computeSkinningPalette({
            meshWorldMatrix: IDENTITY_MATRIX_4x4,
            jointWorldMatrices: jointWorld,
            inverseBindMatrices: ibm,
        });
        // identity * identity * ibm = ibm
        expect(palette[0]).toBeCloseTo(2, 5);
        expect(palette[5]).toBeCloseTo(2, 5);
    });

    it('pre-allocated output buffer is reused', () => {
        const out = new Float32Array(16);
        const jointWorld = [new Float32Array(IDENTITY_MATRIX_4x4)];
        const result = computeSkinningPalette({
            meshWorldMatrix: IDENTITY_MATRIX_4x4,
            jointWorldMatrices: jointWorld,
            out,
        });
        expect(result).toBe(out);
    });
});

describe('computeRigSkinningPalette', () => {
    it('without IBMs uses world matrix directly', () => {
        const rig = new AnimationRig({
            bones: [{ name: 'root', translation: [5, 0, 0] }],
        });
        const pose = new AnimationPose(rig.boneCount).reset(rig);
        const worldPose = new AnimationWorldPose(rig.boneCount).update(rig, pose);
        const palette = computeRigSkinningPalette(rig, worldPose);
        // Column-major: tx at offset 3
        expect(palette[3]).toBeCloseTo(5, 5);
    });

    it('with IBMs multiplies world matrix by IBM', () => {
        const ibm = [
            2, 0, 0, 0,
            0, 2, 0, 0,
            0, 0, 2, 0,
            0, 0, 0, 1,
        ];
        const rig = new AnimationRig({
            bones: [{ name: 'root', translation: [1, 0, 0], inverseBindMatrix: ibm }],
        });
        const pose = new AnimationPose(rig.boneCount).reset(rig);
        const worldPose = new AnimationWorldPose(rig.boneCount).update(rig, pose);
        const palette = computeRigSkinningPalette(rig, worldPose);
        // world matrix has translation(1,0,0), multiplied by IBM scale(2,2,2)
        expect(palette[0]).toBeCloseTo(2, 5);
    });

    it('pre-allocated output buffer is reused', () => {
        const rig = new AnimationRig({ bones: [{ name: 'root' }] });
        const pose = new AnimationPose(rig.boneCount).reset(rig);
        const worldPose = new AnimationWorldPose(rig.boneCount).update(rig, pose);
        const out = new Float32Array(16);
        const result = computeRigSkinningPalette(rig, worldPose, out);
        expect(result).toBe(out);
    });
});

describe('Animation error hierarchy', () => {
    it('AnimationError has correct name, code, message, cause', () => {
        const error = new AnimationError('test message', 'TEST_CODE', 'the cause');
        expect(error.name).toBe('AnimationError');
        expect(error.message).toBe('test message');
        expect(error.code).toBe('TEST_CODE');
        expect(error.cause).toBe('the cause');
        expect(error).toBeInstanceOf(Error);
    });

    it('AnimationValidationError extends AnimationError with correct code', () => {
        const error = new AnimationValidationError('validation failed');
        expect(error).toBeInstanceOf(AnimationError);
        expect(error.name).toBe('AnimationValidationError');
        expect(error.code).toBe('ANIMATION_VALIDATION_ERROR');
    });

    it('AnimationSamplingError has correct code', () => {
        const error = new AnimationSamplingError('sampling failed');
        expect(error).toBeInstanceOf(AnimationError);
        expect(error.name).toBe('AnimationSamplingError');
        expect(error.code).toBe('ANIMATION_SAMPLING_ERROR');
    });

    it('AnimationStateMachineError has correct code', () => {
        const error = new AnimationStateMachineError('state machine failed');
        expect(error).toBeInstanceOf(AnimationError);
        expect(error.name).toBe('AnimationStateMachineError');
        expect(error.code).toBe('ANIMATION_STATE_MACHINE_ERROR');
    });

    it('AnimationRetargetingError has correct code', () => {
        const error = new AnimationRetargetingError('retargeting failed');
        expect(error).toBeInstanceOf(AnimationError);
        expect(error.name).toBe('AnimationRetargetingError');
        expect(error.code).toBe('ANIMATION_RETARGETING_ERROR');
    });

    it('AnimationIkError has correct code', () => {
        const error = new AnimationIkError('ik failed');
        expect(error).toBeInstanceOf(AnimationError);
        expect(error.name).toBe('AnimationIkError');
        expect(error.code).toBe('ANIMATION_IK_ERROR');
    });

    it('error cause is propagated', () => {
        const cause = new Error('root cause');
        const error = new AnimationValidationError('wrapper', cause);
        expect(error.cause).toBe(cause);
    });
});

describe('assertNever', () => {
    it('throws AnimationError with ANIMATION_EXHAUSTIVENESS_ERROR code', () => {
        expect(() => assertNever('unexpected' as never, 'Unhandled kind')).toThrow(AnimationError);
        try {
            assertNever('unexpected' as never, 'Unhandled kind');
        } catch (error) {
            expect((error as AnimationError).code).toBe('ANIMATION_EXHAUSTIVENESS_ERROR');
        }
    });
});
