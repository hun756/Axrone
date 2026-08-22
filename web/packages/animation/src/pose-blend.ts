import { clamp, quatAccumulateWeighted, quatFinalizeWeighted, quatIdentity, quatInvert, quatMultiply, quatNormalize, quatSlerp, vec3Lerp } from './math';
import { AnimationFrame, AnimationMask } from './pose-frame';

export const blendFrame = (
    target: AnimationFrame,
    base: AnimationFrame,
    overlay: AnimationFrame,
    weight: number,
    mask?: AnimationMask
): AnimationFrame => {
    const alpha = clamp(weight, 0, 1);
    if (alpha <= 0) {
        return target.copyFrom(base);
    }
    if (alpha >= 1 && !mask) {
        return target.copyFrom(overlay);
    }

    target.copyFrom(base);
    for (let boneIndex = 0; boneIndex < target.pose.boneCount; boneIndex += 1) {
        if (mask && !mask.has(boneIndex)) {
            continue;
        }
        const translationOffset = boneIndex * 3;
        const rotationOffset = boneIndex * 4;
        vec3Lerp(
            target.pose.translations,
            translationOffset,
            base.pose.translations,
            translationOffset,
            overlay.pose.translations,
            translationOffset,
            alpha
        );
        quatSlerp(
            target.pose.rotations,
            rotationOffset,
            base.pose.rotations,
            rotationOffset,
            overlay.pose.rotations,
            rotationOffset,
            alpha
        );
        vec3Lerp(
            target.pose.scales,
            translationOffset,
            base.pose.scales,
            translationOffset,
            overlay.pose.scales,
            translationOffset,
            alpha
        );
    }
    for (let index = 0; index < target.curves.values.length; index += 1) {
        target.curves.values[index] =
            base.curves.values[index]! +
            (overlay.curves.values[index]! - base.curves.values[index]!) * alpha;
    }
    return target;
};

export const blendWeightedFrames = (
    target: AnimationFrame,
    frames: readonly AnimationFrame[],
    weights: readonly number[],
    restFrame: AnimationFrame,
    referenceRotationScratch: Float32Array,
    mask?: AnimationMask
): AnimationFrame => {
    target.copyFrom(restFrame);
    const boneCount = target.pose.boneCount;
    for (let boneIndex = 0; boneIndex < boneCount; boneIndex += 1) {
        if (mask && !mask.has(boneIndex)) {
            continue;
        }
        const translationOffset = boneIndex * 3;
        const rotationOffset = boneIndex * 4;
        let totalWeight = 0;
        let tx = 0;
        let ty = 0;
        let tz = 0;
        let sx = 0;
        let sy = 0;
        let sz = 0;

        for (let frameIndex = 0; frameIndex < frames.length; frameIndex += 1) {
            const frame = frames[frameIndex]!;
            const weight = Math.max(0, weights[frameIndex] ?? 0);
            if (weight <= 0) {
                continue;
            }
            const isFirst = totalWeight <= 0;
            totalWeight += weight;
            tx += frame.pose.translations[translationOffset]! * weight;
            ty += frame.pose.translations[translationOffset + 1]! * weight;
            tz += frame.pose.translations[translationOffset + 2]! * weight;
            sx += frame.pose.scales[translationOffset]! * weight;
            sy += frame.pose.scales[translationOffset + 1]! * weight;
            sz += frame.pose.scales[translationOffset + 2]! * weight;
            quatAccumulateWeighted(
                target.pose.rotations,
                rotationOffset,
                referenceRotationScratch,
                0,
                frame.pose.rotations,
                rotationOffset,
                weight,
                isFirst
            );
        }

        if (totalWeight <= 0) {
            continue;
        }

        const inverseWeight = 1 / totalWeight;
        target.pose.translations[translationOffset] = tx * inverseWeight;
        target.pose.translations[translationOffset + 1] = ty * inverseWeight;
        target.pose.translations[translationOffset + 2] = tz * inverseWeight;
        target.pose.scales[translationOffset] = sx * inverseWeight;
        target.pose.scales[translationOffset + 1] = sy * inverseWeight;
        target.pose.scales[translationOffset + 2] = sz * inverseWeight;
        quatFinalizeWeighted(
            target.pose.rotations,
            rotationOffset,
            target.pose.rotations,
            rotationOffset,
            totalWeight
        );
    }

    for (let curveIndex = 0; curveIndex < target.curves.values.length; curveIndex += 1) {
        let totalWeight = 0;
        let accumulated = 0;
        for (let frameIndex = 0; frameIndex < frames.length; frameIndex += 1) {
            const weight = Math.max(0, weights[frameIndex] ?? 0);
            if (weight <= 0) {
                continue;
            }
            totalWeight += weight;
            accumulated += frames[frameIndex]!.curves.values[curveIndex]! * weight;
        }
        target.curves.values[curveIndex] = totalWeight > 0 ? accumulated / totalWeight : 0;
    }
    return target;
};

export const applyAdditiveFrame = (
    target: AnimationFrame,
    base: AnimationFrame,
    additive: AnimationFrame,
    restFrame: AnimationFrame,
    weight: number,
    additiveScratch: AdditiveFrameScratch,
    mask?: AnimationMask
): AnimationFrame => {
    const alpha = clamp(weight, 0, 1);
    target.copyFrom(base);
    if (alpha <= 0) {
        return target;
    }

    for (let boneIndex = 0; boneIndex < target.pose.boneCount; boneIndex += 1) {
        if (mask && !mask.has(boneIndex)) {
            continue;
        }
        const translationOffset = boneIndex * 3;
        const rotationOffset = boneIndex * 4;
        target.pose.translations[translationOffset] +=
            (additive.pose.translations[translationOffset]! - restFrame.pose.translations[translationOffset]!) * alpha;
        target.pose.translations[translationOffset + 1] +=
            (additive.pose.translations[translationOffset + 1]! -
                restFrame.pose.translations[translationOffset + 1]!) *
            alpha;
        target.pose.translations[translationOffset + 2] +=
            (additive.pose.translations[translationOffset + 2]! -
                restFrame.pose.translations[translationOffset + 2]!) *
            alpha;
        target.pose.scales[translationOffset] +=
            (additive.pose.scales[translationOffset]! - restFrame.pose.scales[translationOffset]!) * alpha;
        target.pose.scales[translationOffset + 1] +=
            (additive.pose.scales[translationOffset + 1]! - restFrame.pose.scales[translationOffset + 1]!) * alpha;
        target.pose.scales[translationOffset + 2] +=
            (additive.pose.scales[translationOffset + 2]! - restFrame.pose.scales[translationOffset + 2]!) * alpha;
        quatIdentity(additiveScratch.scaledRotation, 0);
        quatInvert(additiveScratch.inverseRest, 0, restFrame.pose.rotations, rotationOffset);
        quatMultiply(additiveScratch.deltaRotation, 0, additiveScratch.inverseRest, 0, additive.pose.rotations, rotationOffset);
        quatSlerp(additiveScratch.scaledRotation, 0, additiveScratch.scaledRotation, 0, additiveScratch.deltaRotation, 0, alpha);
        quatMultiply(
            target.pose.rotations,
            rotationOffset,
            base.pose.rotations,
            rotationOffset,
            additiveScratch.scaledRotation,
            0
        );
        quatNormalize(target.pose.rotations, rotationOffset, target.pose.rotations, rotationOffset);
    }

    for (let curveIndex = 0; curveIndex < target.curves.values.length; curveIndex += 1) {
        target.curves.values[curveIndex] +=
            (additive.curves.values[curveIndex]! - restFrame.curves.values[curveIndex]!) * alpha;
    }
    return target;
};

export interface AdditiveFrameScratch {
    readonly inverseRest: Float32Array;
    readonly deltaRotation: Float32Array;
    readonly scaledRotation: Float32Array;
}

export const createAdditiveFrameScratch = (): AdditiveFrameScratch => ({
    inverseRest: new Float32Array(4),
    deltaRotation: new Float32Array(4),
    scaledRotation: new Float32Array(4),
});
