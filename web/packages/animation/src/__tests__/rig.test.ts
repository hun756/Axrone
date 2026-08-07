import { describe, expect, it } from 'vitest';
import { AnimationRig } from '../rig';
import { AnimationValidationError } from '../errors';

describe('AnimationRig constructor validation', () => {
    it('throws on empty bones array', () => {
        expect(() => new AnimationRig({ bones: [] })).toThrow(AnimationValidationError);
    });

    it('throws on non-array bones', () => {
        expect(() => new AnimationRig({ bones: undefined as never })).toThrow(AnimationValidationError);
    });

    it('throws on empty bone name', () => {
        expect(() => new AnimationRig({ bones: [{ name: '' }] })).toThrow(AnimationValidationError);
    });

    it('throws on duplicate bone name', () => {
        expect(
            () =>
                new AnimationRig({
                    bones: [{ name: 'hips' }, { name: 'hips' }],
                })
        ).toThrow(AnimationValidationError);
    });

    it('throws on missing parent reference by name', () => {
        expect(
            () =>
                new AnimationRig({
                    bones: [{ name: 'child', parent: 'nonexistent' }],
                })
        ).toThrow(AnimationValidationError);
    });

    it('throws on self-parenting', () => {
        expect(
            () =>
                new AnimationRig({
                    bones: [{ name: 'root', parent: 'root' }],
                })
        ).toThrow(AnimationValidationError);
    });

    it('throws on out-of-range parent index', () => {
        expect(
            () =>
                new AnimationRig({
                    bones: [{ name: 'root' }, { name: 'child', parent: 5 }],
                })
        ).toThrow(AnimationValidationError);
    });
});

describe('AnimationRig bone lookup', () => {
    const rig = new AnimationRig({
        bones: [
            { name: 'hips' },
            { name: 'spine', parent: 'hips', translation: [0, 1, 0] },
            { name: 'head', parent: 'spine', translation: [0, 0.5, 0] },
        ],
    });

    it('hasBone returns true for existing bones', () => {
        expect(rig.hasBone('hips')).toBe(true);
        expect(rig.hasBone('spine')).toBe(true);
    });

    it('hasBone returns false for missing bones', () => {
        expect(rig.hasBone('tail')).toBe(false);
    });

    it('indexOfBone returns correct index', () => {
        expect(rig.indexOfBone('hips')).toBe(0);
        expect(rig.indexOfBone('spine')).toBe(1);
        expect(rig.indexOfBone('head')).toBe(2);
    });

    it('indexOfBone throws for unknown bone', () => {
        expect(() => rig.indexOfBone('missing')).toThrow(AnimationValidationError);
    });

    it('tryIndexOfBone returns undefined for unknown bone', () => {
        expect(rig.tryIndexOfBone('missing')).toBeUndefined();
    });

    it('tryIndexOfBone returns index for known bone', () => {
        expect(rig.tryIndexOfBone('hips')).toBe(0);
    });

    it('getBoneName returns correct name', () => {
        expect(rig.getBoneName(0)).toBe('hips');
        expect(rig.getBoneName(2)).toBe('head');
    });

    it('getBoneName throws for out-of-range index', () => {
        expect(() => rig.getBoneName(10)).toThrow(AnimationValidationError);
    });
});

describe('AnimationRig hierarchy', () => {
    const rig = new AnimationRig({
        bones: [
            { name: 'hips' },
            { name: 'spine', parent: 'hips' },
            { name: 'head', parent: 'spine' },
            { name: 'leftArm', parent: 'spine' },
        ],
    });

    it('parentIndices are correct for roots and children', () => {
        expect(rig.parentIndices[0]).toBe(-1); // hips is root
        expect(rig.parentIndices[1]).toBe(0); // spine -> hips
        expect(rig.parentIndices[2]).toBe(1); // head -> spine
        expect(rig.parentIndices[3]).toBe(1); // leftArm -> spine
    });

    it('childIndices groups children correctly', () => {
        expect(Array.from(rig.childIndices[0]!)).toEqual([1]); // hips -> [spine]
        expect(Array.from(rig.childIndices[1]!)).toEqual([2, 3]); // spine -> [head, leftArm]
        expect(Array.from(rig.childIndices[2]!)).toEqual([]); // head -> []
        expect(Array.from(rig.childIndices[3]!)).toEqual([]); // leftArm -> []
    });

    it('rootIndices contains only root bones', () => {
        expect(Array.from(rig.rootIndices)).toEqual([0]);
    });

    it('evaluationOrder respects parent-before-child', () => {
        const order = Array.from(rig.evaluationOrder);
        expect(order.indexOf(0)).toBeLessThan(order.indexOf(1));
        expect(order.indexOf(1)).toBeLessThan(order.indexOf(2));
        expect(order.indexOf(1)).toBeLessThan(order.indexOf(3));
    });

    it('getParentIndex returns correct parent or -1 for root', () => {
        expect(rig.getParentIndex(0)).toBe(-1);
        expect(rig.getParentIndex(1)).toBe(0);
        expect(rig.getParentIndex(2)).toBe(1);
    });
});

describe('AnimationRig rest pose', () => {
    it('populates rest transforms from definition', () => {
        const rig = new AnimationRig({
            bones: [
                { name: 'root', translation: [1, 2, 3], rotation: [0, 0, 0, 1], scale: [2, 2, 2] },
            ],
        });
        expect(rig.restTranslations[0]).toBe(1);
        expect(rig.restTranslations[1]).toBe(2);
        expect(rig.restTranslations[2]).toBe(3);
        expect(rig.restRotations[3]).toBe(1); // w component
        expect(rig.restScales[0]).toBe(2);
    });

    it('defaults to identity when transforms not specified', () => {
        const rig = new AnimationRig({ bones: [{ name: 'root' }] });
        expect(rig.restTranslations[0]).toBe(0);
        expect(rig.restTranslations[1]).toBe(0);
        expect(rig.restTranslations[2]).toBe(0);
        expect(rig.restRotations[0]).toBe(0);
        expect(rig.restRotations[1]).toBe(0);
        expect(rig.restRotations[2]).toBe(0);
        expect(rig.restRotations[3]).toBe(1);
        expect(rig.restScales[0]).toBe(1);
        expect(rig.restScales[1]).toBe(1);
        expect(rig.restScales[2]).toBe(1);
    });
});

describe('AnimationRig inverse bind matrices', () => {
    it('is null when no bone provides IBM', () => {
        const rig = new AnimationRig({ bones: [{ name: 'root' }] });
        expect(rig.inverseBindMatrices).toBeNull();
    });

    it('is populated when any bone provides IBM', () => {
        const ibm = new Array(16).fill(0);
        ibm[0] = 1;
        ibm[5] = 1;
        ibm[10] = 1;
        ibm[15] = 1;
        const rig = new AnimationRig({
            bones: [{ name: 'root', inverseBindMatrix: ibm }],
        });
        expect(rig.inverseBindMatrices).not.toBeNull();
        expect(rig.inverseBindMatrices!.length).toBe(16);
    });

    it('throws on wrong-length IBM', () => {
        expect(
            () =>
                new AnimationRig({
                    bones: [{ name: 'root', inverseBindMatrix: [1, 2, 3] }],
                })
        ).toThrow(AnimationValidationError);
    });

    it('fills identity for bones without IBM when any bone has one', () => {
        const ibm = [
            2, 0, 0, 0,
            0, 2, 0, 0,
            0, 0, 2, 0,
            0, 0, 0, 1,
        ];
        const rig = new AnimationRig({
            bones: [
                { name: 'root' },
                { name: 'child', parent: 'root', inverseBindMatrix: ibm },
            ],
        });
        // Root should have identity (default fill)
        expect(rig.inverseBindMatrices![0]).toBe(1);
        expect(rig.inverseBindMatrices![5]).toBe(1);
        expect(rig.inverseBindMatrices![10]).toBe(1);
        expect(rig.inverseBindMatrices![15]).toBe(1);
        // Child should have the custom IBM
        expect(rig.inverseBindMatrices![16]).toBe(2);
    });
});

describe('AnimationRig createRestMatrixPalette', () => {
    it('root bone matrix is identity when no rest transform', () => {
        const rig = new AnimationRig({ bones: [{ name: 'root' }] });
        const palette = rig.createRestMatrixPalette();
        // Identity matrix: diagonal = 1, rest = 0
        expect(palette[0]).toBe(1);
        expect(palette[5]).toBe(1);
        expect(palette[10]).toBe(1);
        expect(palette[15]).toBe(1);
        expect(palette[1]).toBe(0);
        expect(palette[12]).toBe(0);
    });

    it('root bone matrix reflects rest translation', () => {
        const rig = new AnimationRig({
            bones: [{ name: 'root', translation: [5, 10, 15] }],
        });
        const palette = rig.createRestMatrixPalette();
        // Column-major layout: tx at offset 3, ty at offset 7, tz at offset 11
        expect(palette[3]).toBeCloseTo(5, 5);
        expect(palette[7]).toBeCloseTo(10, 5);
        expect(palette[11]).toBeCloseTo(15, 5);
    });

    it('child bone matrix includes parent translation', () => {
        const rig = new AnimationRig({
            bones: [
                { name: 'root', translation: [1, 0, 0] },
                { name: 'child', parent: 'root', translation: [0, 2, 0] },
            ],
        });
        const palette = rig.createRestMatrixPalette();
        // Child world translation should be parent(1,0,0) + child(0,2,0) = (1,2,0)
        const childOffset = 16; // second bone
        expect(palette[childOffset + 3]).toBeCloseTo(1, 5);
        expect(palette[childOffset + 7]).toBeCloseTo(2, 5);
        expect(palette[childOffset + 11]).toBeCloseTo(0, 5);
    });
});

describe('AnimationRig properties', () => {
    it('boneCount reflects definition', () => {
        const rig = new AnimationRig({
            bones: [{ name: 'a' }, { name: 'b' }, { name: 'c' }],
        });
        expect(rig.boneCount).toBe(3);
    });

    it('boneNames are frozen and correct', () => {
        const rig = new AnimationRig({
            bones: [{ name: 'hips' }, { name: 'spine', parent: 'hips' }],
        });
        expect(rig.boneNames).toEqual(['hips', 'spine']);
        expect(Object.isFrozen(rig.boneNames)).toBe(true);
    });

    it('accepts parent by numeric index', () => {
        const rig = new AnimationRig({
            bones: [{ name: 'root' }, { name: 'child', parent: 0 }],
        });
        expect(rig.parentIndices[1]).toBe(0);
    });

    it('generates a default id when none provided', () => {
        const rig = new AnimationRig({ bones: [{ name: 'root' }] });
        expect(rig.id).toBeTruthy();
    });

    it('uses provided id', () => {
        const rig = new AnimationRig({ id: 'myRig', bones: [{ name: 'root' }] });
        expect(rig.id).toBe('myRig');
    });
});
