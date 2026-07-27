import { describe, expect, it } from 'vitest';
import { AnimationIkLayer } from '../ik';
import { AnimationCurveLayout, AnimationFrame, AnimationPose, AnimationWorldPose } from '../pose';
import { AnimationRetargeter } from '../retargeting';
import { AnimationRig } from '../rig';

describe('AnimationIkLayer edge cases', () => {
    it('stretches a FABRIK chain toward an unreachable target without NaNs', () => {
        const rig = new AnimationRig({
            bones: [
                { name: 'root' },
                { name: 'mid', parent: 'root', translation: [1, 0, 0] },
                { name: 'tip', parent: 'mid', translation: [1, 0, 0] },
            ],
        });
        const pose = new AnimationPose(rig.boneCount).reset(rig);
        const ikLayer = new AnimationIkLayer(rig, {
            id: 'reach',
            jobs: [
                {
                    id: 'stretch',
                    solver: 'fabrik',
                    rootBone: 'root',
                    tipBone: 'tip',
                    targetPosition: [10, 0, 0],
                    maxIterations: 8,
                },
            ],
        });

        ikLayer.apply(pose);
        const worldPose = new AnimationWorldPose(rig.boneCount).update(rig, pose);
        for (let index = 0; index < worldPose.translations.length; index += 1) {
            expect(Number.isFinite(worldPose.translations[index]!)).toBe(true);
        }
        // Chain length is 2, so the tip should extend fully along +X.
        const tipOffset = rig.indexOfBone('tip') * 3;
        expect(worldPose.translations[tipOffset]).toBeCloseTo(2, 3);
        expect(worldPose.translations[tipOffset + 1]).toBeCloseTo(0, 3);
    });

    it('applies preserveTipRotation toward the requested target rotation', () => {
        const rig = new AnimationRig({
            bones: [
                { name: 'root' },
                { name: 'tip', parent: 'root', translation: [1, 0, 0] },
            ],
        });
        const pose = new AnimationPose(rig.boneCount).reset(rig);
        const halfSqrt2 = Math.SQRT1_2;
        const ikLayer = new AnimationIkLayer(rig, {
            id: 'aim',
            jobs: [
                {
                    id: 'orient',
                    solver: 'ccd',
                    rootBone: 'root',
                    tipBone: 'tip',
                    targetPosition: [1, 0, 0],
                    targetRotation: [0, 0, halfSqrt2, halfSqrt2],
                    preserveTipRotation: true,
                    maxIterations: 4,
                },
            ],
        });

        ikLayer.apply(pose);
        const worldPose = new AnimationWorldPose(rig.boneCount).update(rig, pose);
        const tipRotationOffset = rig.indexOfBone('tip') * 4;
        expect(Math.abs(worldPose.rotations[tipRotationOffset + 2]!)).toBeCloseTo(halfSqrt2, 2);
        expect(Math.abs(worldPose.rotations[tipRotationOffset + 3]!)).toBeCloseTo(halfSqrt2, 2);
    });
});

describe('AnimationRetargeter rotation modes', () => {
    it('applies the rest-pose rotation offset in offset mode', () => {
        const halfSqrt2 = Math.SQRT1_2;
        const retargeter = new AnimationRetargeter({
            sourceRig: { bones: [{ name: 'hips' }] },
            targetRig: { bones: [{ name: 'pelvis', rotation: [0, 0, halfSqrt2, halfSqrt2] }] },
            mappings: [
                { sourceBone: 'hips', targetBone: 'pelvis', rotationMode: 'offset' },
            ],
        });
        const sourceRig = new AnimationRig({ bones: [{ name: 'hips' }] });
        const sourceFrame = new AnimationFrame(sourceRig, new AnimationCurveLayout());

        const targetFrame = retargeter.retargetPose(sourceFrame);
        // Source is at rest, so the target must land on its own rest rotation.
        expect(targetFrame.pose.rotations[2]).toBeCloseTo(halfSqrt2, 5);
        expect(targetFrame.pose.rotations[3]).toBeCloseTo(halfSqrt2, 5);
    });

    it('copies source rotations verbatim in copy mode', () => {
        const halfSqrt2 = Math.SQRT1_2;
        const retargeter = new AnimationRetargeter({
            sourceRig: { bones: [{ name: 'hips' }] },
            targetRig: { bones: [{ name: 'pelvis', rotation: [0, 0, halfSqrt2, halfSqrt2] }] },
            mappings: [
                { sourceBone: 'hips', targetBone: 'pelvis', rotationMode: 'copy' },
            ],
        });
        const sourceRig = new AnimationRig({ bones: [{ name: 'hips' }] });
        const sourceFrame = new AnimationFrame(sourceRig, new AnimationCurveLayout());

        const targetFrame = retargeter.retargetPose(sourceFrame);
        expect(targetFrame.pose.rotations[2]).toBeCloseTo(0, 5);
        expect(targetFrame.pose.rotations[3]).toBeCloseTo(1, 5);
    });

    it('creates automatic mappings only for shared bone names', () => {
        const retargeter = new AnimationRetargeter({
            sourceRig: { bones: [{ name: 'hips' }, { name: 'sourceOnly' }] },
            targetRig: { bones: [{ name: 'hips' }, { name: 'targetOnly' }] },
        });
        expect(retargeter.mappings).toHaveLength(1);
        expect(retargeter.mappings[0]?.sourceIndex).toBe(0);
        expect(retargeter.mappings[0]?.targetIndex).toBe(0);
    });
});
