import { describe, expect, it } from 'vitest';
import {
    AnimationCurveLayout,
    AnimationCurveStore,
    AnimationPose,
    AnimationFrame,
    AnimationMask,
    AnimationWorldPose,
    blendFrame,
    blendWeightedFrames,
    applyAdditiveFrame,
} from '../pose';
import { AnimationRig } from '../rig';
import { AnimationValidationError } from '../errors';

describe('AnimationCurveLayout', () => {
    it('empty constructor yields 0 componentCount', () => {
        const layout = new AnimationCurveLayout();
        expect(layout.componentCount).toBe(0);
        expect(layout.bindings).toHaveLength(0);
    });

    it('valid bindings compute correct offsets', () => {
        const layout = new AnimationCurveLayout([
            { id: 'position', componentCount: 3 },
            { id: 'rotation', componentCount: 4 },
        ]);
        expect(layout.componentCount).toBe(7);
        expect(layout.get('position')!.offset).toBe(0);
        expect(layout.get('position')!.componentCount).toBe(3);
        expect(layout.get('rotation')!.offset).toBe(3);
        expect(layout.get('rotation')!.componentCount).toBe(4);
    });

    it('throws on empty id', () => {
        expect(() => new AnimationCurveLayout([{ id: '', componentCount: 3 }])).toThrow(
            AnimationValidationError
        );
    });

    it('throws on non-integer componentCount', () => {
        expect(
            () => new AnimationCurveLayout([{ id: 'x', componentCount: 1.5 }])
        ).toThrow(AnimationValidationError);
    });

    it('throws on zero componentCount', () => {
        expect(
            () => new AnimationCurveLayout([{ id: 'x', componentCount: 0 }])
        ).toThrow(AnimationValidationError);
    });

    it('throws on duplicate id', () => {
        expect(
            () =>
                new AnimationCurveLayout([
                    { id: 'x', componentCount: 3 },
                    { id: 'x', componentCount: 4 },
                ])
        ).toThrow(AnimationValidationError);
    });

    it('has/get methods work correctly', () => {
        const layout = new AnimationCurveLayout([{ id: 'weights', componentCount: 2 }]);
        expect(layout.has('weights')).toBe(true);
        expect(layout.has('missing')).toBe(false);
        expect(layout.get('missing')).toBeUndefined();
    });
});

describe('AnimationCurveStore', () => {
    it('constructor initializes from layout with zeros', () => {
        const layout = new AnimationCurveLayout([{ id: 'x', componentCount: 3 }]);
        const store = new AnimationCurveStore(layout);
        expect(store.values).toHaveLength(3);
        expect(store.values[0]).toBe(0);
    });

    it('constructor applies initialValues', () => {
        const layout = new AnimationCurveLayout([{ id: 'x', componentCount: 3 }]);
        const store = new AnimationCurveStore(layout, [1, 2, 3]);
        expect(store.values[0]).toBe(1);
        expect(store.values[1]).toBe(2);
        expect(store.values[2]).toBe(3);
    });

    it('reset clears to 0 then applies defaults', () => {
        const layout = new AnimationCurveLayout([{ id: 'x', componentCount: 3 }]);
        const store = new AnimationCurveStore(layout, [1, 2, 3]);
        store.reset([10, 20]);
        expect(store.values[0]).toBe(10);
        expect(store.values[1]).toBe(20);
        expect(store.values[2]).toBe(0);
    });

    it('copyFrom throws on mismatched length', () => {
        const layout1 = new AnimationCurveLayout([{ id: 'x', componentCount: 3 }]);
        const layout2 = new AnimationCurveLayout([{ id: 'x', componentCount: 4 }]);
        const store1 = new AnimationCurveStore(layout1);
        const store2 = new AnimationCurveStore(layout2);
        expect(() => store1.copyFrom(store2)).toThrow(AnimationValidationError);
    });

    it('copyFrom copies values', () => {
        const layout = new AnimationCurveLayout([{ id: 'x', componentCount: 3 }]);
        const source = new AnimationCurveStore(layout, [1, 2, 3]);
        const target = new AnimationCurveStore(layout);
        target.copyFrom(source);
        expect(target.values[0]).toBe(1);
        expect(target.values[2]).toBe(3);
    });

    it('read returns subarray for valid id, null for unknown', () => {
        const layout = new AnimationCurveLayout([{ id: 'x', componentCount: 3 }]);
        const store = new AnimationCurveStore(layout, [10, 20, 30]);
        const result = store.read('x');
        expect(result).not.toBeNull();
        expect(result![0]).toBe(10);
        expect(store.read('missing')).toBeNull();
    });

    it('write throws for unknown id', () => {
        const layout = new AnimationCurveLayout([{ id: 'x', componentCount: 3 }]);
        const store = new AnimationCurveStore(layout);
        expect(() => store.write('missing', [1, 2, 3])).toThrow(AnimationValidationError);
    });

    it('write sets values for known id', () => {
        const layout = new AnimationCurveLayout([{ id: 'x', componentCount: 3 }]);
        const store = new AnimationCurveStore(layout);
        store.write('x', [5, 6, 7]);
        expect(store.values[0]).toBe(5);
        expect(store.values[1]).toBe(6);
        expect(store.values[2]).toBe(7);
    });
});

describe('AnimationPose', () => {
    it('constructor allocates correct buffer sizes', () => {
        const pose = new AnimationPose(3);
        expect(pose.translations).toHaveLength(9);
        expect(pose.rotations).toHaveLength(12);
        expect(pose.scales).toHaveLength(9);
    });

    it('copyFrom throws on bone count mismatch', () => {
        const pose1 = new AnimationPose(2);
        const pose2 = new AnimationPose(3);
        expect(() => pose1.copyFrom(pose2)).toThrow(AnimationValidationError);
    });

    it('copyFrom copies all data', () => {
        const pose1 = new AnimationPose(1);
        const pose2 = new AnimationPose(1);
        pose2.translations[0] = 5;
        pose2.rotations[3] = 0.5;
        pose2.scales[1] = 2;
        pose1.copyFrom(pose2);
        expect(pose1.translations[0]).toBe(5);
        expect(pose1.rotations[3]).toBe(0.5);
        expect(pose1.scales[1]).toBe(2);
    });

    it('reset copies rig rest pose', () => {
        const rig = new AnimationRig({
            bones: [{ name: 'root', translation: [1, 2, 3], scale: [2, 2, 2] }],
        });
        const pose = new AnimationPose(1);
        pose.reset(rig);
        expect(pose.translations[0]).toBe(1);
        expect(pose.translations[1]).toBe(2);
        expect(pose.translations[2]).toBe(3);
        expect(pose.scales[0]).toBe(2);
    });
});

describe('AnimationFrame', () => {
    const rig = new AnimationRig({
        bones: [{ name: 'root', translation: [1, 0, 0] }],
    });
    const curveLayout = new AnimationCurveLayout([{ id: 'blend', componentCount: 1 }]);

    it('constructor resets pose from rig and initializes curves', () => {
        const frame = new AnimationFrame(rig, curveLayout);
        expect(frame.pose.translations[0]).toBe(1);
        expect(frame.curves.values).toHaveLength(1);
    });

    it('reset restores rig rest pose and curve defaults', () => {
        const frame = new AnimationFrame(rig, curveLayout);
        frame.pose.translations[0] = 99;
        frame.curves.values[0] = 42;
        frame.reset(rig, [0.5]);
        expect(frame.pose.translations[0]).toBe(1);
        expect(frame.curves.values[0]).toBe(0.5);
    });

    it('copyFrom copies both pose and curves', () => {
        const frame1 = new AnimationFrame(rig, curveLayout);
        const frame2 = new AnimationFrame(rig, curveLayout);
        frame2.pose.translations[0] = 7;
        frame2.curves.values[0] = 3;
        frame1.copyFrom(frame2);
        expect(frame1.pose.translations[0]).toBe(7);
        expect(frame1.curves.values[0]).toBe(3);
    });
});

describe('AnimationMask', () => {
    it('constructor with fill=false has all bits off', () => {
        const mask = new AnimationMask(64);
        expect(mask.has(0)).toBe(false);
        expect(mask.has(31)).toBe(false);
        expect(mask.has(32)).toBe(false);
        expect(mask.has(63)).toBe(false);
    });

    it('constructor with fill=true has all bits on', () => {
        const mask = new AnimationMask(64, true);
        expect(mask.has(0)).toBe(true);
        expect(mask.has(31)).toBe(true);
        expect(mask.has(32)).toBe(true);
        expect(mask.has(63)).toBe(true);
    });

    it('set/has toggles individual bits', () => {
        const mask = new AnimationMask(64);
        mask.set(5, true);
        expect(mask.has(5)).toBe(true);
        expect(mask.has(4)).toBe(false);
        mask.set(5, false);
        expect(mask.has(5)).toBe(false);
    });

    it('handles boundary at bit 32 (second Uint32 bucket)', () => {
        const mask = new AnimationMask(64);
        mask.set(32, true);
        expect(mask.has(32)).toBe(true);
        expect(mask.has(31)).toBe(false);
        expect(mask.has(33)).toBe(false);
    });

    it('fill(true/false) bulk sets all bits', () => {
        const mask = new AnimationMask(40);
        mask.fill(true);
        expect(mask.has(0)).toBe(true);
        expect(mask.has(39)).toBe(true);
        mask.fill(false);
        expect(mask.has(0)).toBe(false);
        expect(mask.has(39)).toBe(false);
    });
});

describe('AnimationWorldPose.update', () => {
    it('root bone copies local directly', () => {
        const rig = new AnimationRig({
            bones: [{ name: 'root', translation: [3, 4, 5] }],
        });
        const pose = new AnimationPose(rig.boneCount).reset(rig);
        const worldPose = new AnimationWorldPose(rig.boneCount).update(rig, pose);
        expect(worldPose.translations[0]).toBeCloseTo(3, 5);
        expect(worldPose.translations[1]).toBeCloseTo(4, 5);
        expect(worldPose.translations[2]).toBeCloseTo(5, 5);
    });

    it('child bone applies parent transform', () => {
        const rig = new AnimationRig({
            bones: [
                { name: 'root', translation: [10, 0, 0] },
                { name: 'child', parent: 'root', translation: [0, 5, 0] },
            ],
        });
        const pose = new AnimationPose(rig.boneCount).reset(rig);
        const worldPose = new AnimationWorldPose(rig.boneCount).update(rig, pose);
        const childOffset = 3;
        expect(worldPose.translations[childOffset]).toBeCloseTo(10, 5);
        expect(worldPose.translations[childOffset + 1]).toBeCloseTo(5, 5);
        expect(worldPose.translations[childOffset + 2]).toBeCloseTo(0, 5);
    });

    it('child scale composes with parent scale', () => {
        const rig = new AnimationRig({
            bones: [
                { name: 'root', scale: [2, 2, 2] },
                { name: 'child', parent: 'root', translation: [1, 0, 0], scale: [3, 3, 3] },
            ],
        });
        const pose = new AnimationPose(rig.boneCount).reset(rig);
        const worldPose = new AnimationWorldPose(rig.boneCount).update(rig, pose);
        // Child translation in world = parent translation + parent rotation * (local translation * parent scale)
        // = (0,0,0) + identity * ((1,0,0) * (2,2,2)) = (2,0,0)
        const childOffset = 3;
        expect(worldPose.translations[childOffset]).toBeCloseTo(2, 5);
        // Scale: parent(2,2,2) * child(3,3,3) = (6,6,6)
        expect(worldPose.scales[childOffset]).toBeCloseTo(6, 5);
    });
});

describe('blendFrame', () => {
    const rig = new AnimationRig({ bones: [{ name: 'root' }] });
    const curveLayout = new AnimationCurveLayout([{ id: 'w', componentCount: 1 }]);

    it('weight=0 returns base', () => {
        const base = new AnimationFrame(rig, curveLayout);
        const overlay = new AnimationFrame(rig, curveLayout);
        const target = new AnimationFrame(rig, curveLayout);
        base.pose.translations[0] = 10;
        overlay.pose.translations[0] = 20;
        blendFrame(target, base, overlay, 0);
        expect(target.pose.translations[0]).toBeCloseTo(10, 5);
    });

    it('weight=1 returns overlay (no mask)', () => {
        const base = new AnimationFrame(rig, curveLayout);
        const overlay = new AnimationFrame(rig, curveLayout);
        const target = new AnimationFrame(rig, curveLayout);
        base.pose.translations[0] = 10;
        overlay.pose.translations[0] = 20;
        blendFrame(target, base, overlay, 1);
        expect(target.pose.translations[0]).toBeCloseTo(20, 5);
    });

    it('weight=0.5 lerps translations', () => {
        const base = new AnimationFrame(rig, curveLayout);
        const overlay = new AnimationFrame(rig, curveLayout);
        const target = new AnimationFrame(rig, curveLayout);
        base.pose.translations[0] = 0;
        overlay.pose.translations[0] = 10;
        blendFrame(target, base, overlay, 0.5);
        expect(target.pose.translations[0]).toBeCloseTo(5, 5);
    });

    it('mask skips unmasked bones', () => {
        const twoBoneRig = new AnimationRig({
            bones: [{ name: 'a' }, { name: 'b' }],
        });
        const base = new AnimationFrame(twoBoneRig, curveLayout);
        const overlay = new AnimationFrame(twoBoneRig, curveLayout);
        const target = new AnimationFrame(twoBoneRig, curveLayout);
        base.pose.translations[0] = 0;
        overlay.pose.translations[0] = 10;
        base.pose.translations[3] = 100;
        overlay.pose.translations[3] = 200;

        const mask = new AnimationMask(2);
        mask.set(0, true);
        // Only bone 0 is masked; bone 1 should keep base value
        blendFrame(target, base, overlay, 0.5, mask);
        expect(target.pose.translations[0]).toBeCloseTo(5, 5);
        expect(target.pose.translations[3]).toBeCloseTo(100, 5);
    });

    it('curves are linearly interpolated', () => {
        const base = new AnimationFrame(rig, curveLayout);
        const overlay = new AnimationFrame(rig, curveLayout);
        const target = new AnimationFrame(rig, curveLayout);
        base.curves.values[0] = 0;
        overlay.curves.values[0] = 10;
        blendFrame(target, base, overlay, 0.25);
        expect(target.curves.values[0]).toBeCloseTo(2.5, 5);
    });
});

describe('blendWeightedFrames', () => {
    const rig = new AnimationRig({ bones: [{ name: 'root' }] });
    const curveLayout = new AnimationCurveLayout([{ id: 'w', componentCount: 1 }]);

    it('zero total weight returns rest frame', () => {
        const rest = new AnimationFrame(rig, curveLayout);
        rest.pose.translations[0] = 99;
        const frame1 = new AnimationFrame(rig, curveLayout);
        frame1.pose.translations[0] = 10;
        const target = new AnimationFrame(rig, curveLayout);
        blendWeightedFrames(target, [frame1], [0], rest);
        expect(target.pose.translations[0]).toBeCloseTo(99, 5);
    });

    it('single frame at weight 1 copies frame', () => {
        const rest = new AnimationFrame(rig, curveLayout);
        const frame = new AnimationFrame(rig, curveLayout);
        frame.pose.translations[0] = 42;
        const target = new AnimationFrame(rig, curveLayout);
        blendWeightedFrames(target, [frame], [1], rest);
        expect(target.pose.translations[0]).toBeCloseTo(42, 5);
    });

    it('multiple frames weighted average', () => {
        const rest = new AnimationFrame(rig, curveLayout);
        const frame1 = new AnimationFrame(rig, curveLayout);
        const frame2 = new AnimationFrame(rig, curveLayout);
        frame1.pose.translations[0] = 10;
        frame2.pose.translations[0] = 20;
        const target = new AnimationFrame(rig, curveLayout);
        blendWeightedFrames(target, [frame1, frame2], [1, 3], rest);
        expect(target.pose.translations[0]).toBeCloseTo(17.5, 5);
    });

    it('curves weighted average', () => {
        const rest = new AnimationFrame(rig, curveLayout);
        const frame1 = new AnimationFrame(rig, curveLayout);
        const frame2 = new AnimationFrame(rig, curveLayout);
        frame1.curves.values[0] = 100;
        frame2.curves.values[0] = 200;
        const target = new AnimationFrame(rig, curveLayout);
        blendWeightedFrames(target, [frame1, frame2], [1, 1], rest);
        expect(target.curves.values[0]).toBeCloseTo(150, 5);
    });
});

describe('applyAdditiveFrame', () => {
    const rig = new AnimationRig({ bones: [{ name: 'root' }] });
    const curveLayout = new AnimationCurveLayout([{ id: 'w', componentCount: 1 }]);

    it('weight=0 returns base unchanged', () => {
        const base = new AnimationFrame(rig, curveLayout);
        const additive = new AnimationFrame(rig, curveLayout);
        const rest = new AnimationFrame(rig, curveLayout);
        const target = new AnimationFrame(rig, curveLayout);
        base.pose.translations[0] = 5;
        additive.pose.translations[0] = 100;
        applyAdditiveFrame(target, base, additive, rest, 0);
        expect(target.pose.translations[0]).toBeCloseTo(5, 5);
    });

    it('additive at rest produces no change', () => {
        const base = new AnimationFrame(rig, curveLayout);
        const additive = new AnimationFrame(rig, curveLayout);
        const rest = new AnimationFrame(rig, curveLayout);
        const target = new AnimationFrame(rig, curveLayout);
        base.pose.translations[0] = 5;
        // additive == rest, so delta is 0
        applyAdditiveFrame(target, base, additive, rest, 1);
        expect(target.pose.translations[0]).toBeCloseTo(5, 5);
    });

    it('weight=1 applies full additive delta', () => {
        const base = new AnimationFrame(rig, curveLayout);
        const additive = new AnimationFrame(rig, curveLayout);
        const rest = new AnimationFrame(rig, curveLayout);
        const target = new AnimationFrame(rig, curveLayout);
        base.pose.translations[0] = 5;
        additive.pose.translations[0] = 8;
        rest.pose.translations[0] = 3;
        applyAdditiveFrame(target, base, additive, rest, 1);
        // target = base + (additive - rest) * 1 = 5 + (8 - 3) = 10
        expect(target.pose.translations[0]).toBeCloseTo(10, 5);
    });

    it('curves additive delta', () => {
        const base = new AnimationFrame(rig, curveLayout);
        const additive = new AnimationFrame(rig, curveLayout);
        const rest = new AnimationFrame(rig, curveLayout);
        const target = new AnimationFrame(rig, curveLayout);
        base.curves.values[0] = 10;
        additive.curves.values[0] = 15;
        rest.curves.values[0] = 5;
        applyAdditiveFrame(target, base, additive, rest, 0.5);
        // target = 10 + (15 - 5) * 0.5 = 15
        expect(target.curves.values[0]).toBeCloseTo(15, 5);
    });

    it('mask skips unmasked bones', () => {
        const twoBoneRig = new AnimationRig({
            bones: [{ name: 'a' }, { name: 'b' }],
        });
        const base = new AnimationFrame(twoBoneRig, curveLayout);
        const additive = new AnimationFrame(twoBoneRig, curveLayout);
        const rest = new AnimationFrame(twoBoneRig, curveLayout);
        const target = new AnimationFrame(twoBoneRig, curveLayout);
        base.pose.translations[0] = 5;
        base.pose.translations[3] = 50;
        additive.pose.translations[0] = 10;
        additive.pose.translations[3] = 100;
        rest.pose.translations[0] = 0;
        rest.pose.translations[3] = 0;

        const mask = new AnimationMask(2);
        mask.set(0, true);
        applyAdditiveFrame(target, base, additive, rest, 1, mask);
        // Masked bone 0: target = base(5) + (additive(10) - rest(0)) * weight(1) = 15
        expect(target.pose.translations[0]).toBeCloseTo(15, 5);
        // Bone 1 not masked, should keep base value
        expect(target.pose.translations[3]).toBeCloseTo(50, 5);
    });
});
