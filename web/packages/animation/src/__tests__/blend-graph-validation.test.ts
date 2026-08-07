import { describe, expect, it } from 'vitest';
import {
    AnimationBlendGraph,
    validateAnimationMotionDefinition,
    buildAnimationMotionDefinition,
} from '../blend-graph';

describe('AnimationClipMotionBuilder', () => {
    it('build produces clip motion definition', () => {
        const motion = AnimationBlendGraph.clip('walk').build();
        expect(motion.kind).toBe('clip');
        if (motion.kind === 'clip') {
            expect(motion.clipId).toBe('walk');
        }
    });

    it('withTimeScale is applied', () => {
        const motion = AnimationBlendGraph.clip('walk').withTimeScale(2).build();
        expect(motion.kind).toBe('clip');
        if (motion.kind === 'clip') {
            expect(motion.timeScale).toBe(2);
        }
    });

    it('withCycleOffset is applied', () => {
        const motion = AnimationBlendGraph.clip('walk').withCycleOffset(0.5).build();
        expect(motion.kind).toBe('clip');
        if (motion.kind === 'clip') {
            expect(motion.cycleOffset).toBe(0.5);
        }
    });
});

describe('AnimationBlend1DGraphBuilder', () => {
    it('addChild with threshold and build sorts by threshold', () => {
        const motion = AnimationBlendGraph.blend1d('speed')
            .addChild(1, AnimationBlendGraph.clip('run'))
            .addChild(0, AnimationBlendGraph.clip('walk'))
            .build();
        expect(motion.kind).toBe('blend1d');
        if (motion.kind === 'blend1d') {
            expect(motion.children).toHaveLength(2);
            expect(motion.children[0]!.threshold).toBe(0);
            expect(motion.children[1]!.threshold).toBe(1);
        }
    });
});

describe('AnimationBlend2DGraphBuilder', () => {
    it('addChild with position and build produces correct structure', () => {
        const motion = AnimationBlendGraph.blend2d('x', 'y')
            .addChild(0, 0, AnimationBlendGraph.clip('idle'))
            .addChild(1, 0, AnimationBlendGraph.clip('walk'))
            .build();
        expect(motion.kind).toBe('blend2d');
        if (motion.kind === 'blend2d') {
            expect(motion.children).toHaveLength(2);
            expect(motion.children[0]!.position).toEqual([0, 0]);
            expect(motion.children[1]!.position).toEqual([1, 0]);
        }
    });
});

describe('AnimationDirectBlendGraphBuilder', () => {
    it('addChild with optional weight and parameter', () => {
        const motion = AnimationBlendGraph.direct()
            .addChild(AnimationBlendGraph.clip('idle'), { weight: 0.5 })
            .addChild(AnimationBlendGraph.clip('walk'), { weight: 1, parameter: 'speed' })
            .build();
        expect(motion.kind).toBe('direct');
        if (motion.kind === 'direct') {
            expect(motion.children).toHaveLength(2);
            expect(motion.children[0]!.weight).toBe(0.5);
            expect(motion.children[1]!.parameter).toBe('speed');
        }
    });
});

describe('AnimationAdditiveBlendGraphBuilder', () => {
    it('withParameter and withWeight are applied', () => {
        const motion = AnimationBlendGraph.additive(
            AnimationBlendGraph.clip('base'),
            AnimationBlendGraph.clip('additive')
        )
            .withParameter('blend')
            .withWeight(0.75)
            .build();
        expect(motion.kind).toBe('additive');
        if (motion.kind === 'additive') {
            expect(motion.parameter).toBe('blend');
            expect(motion.weight).toBe(0.75);
        }
    });
});

describe('validateAnimationMotionDefinition diagnostics', () => {
    it('unknown clip id produces diagnostic', () => {
        const diagnostics = validateAnimationMotionDefinition(
            { kind: 'clip', clipId: 'missing' },
            { knownClipIds: ['walk', 'run'] }
        );
        expect(diagnostics).toHaveLength(1);
        expect(diagnostics[0]!.code).toBe('animation.blendGraph.clip.unknown');
    });

    it('unknown parameter produces diagnostic for blend1d', () => {
        const diagnostics = validateAnimationMotionDefinition(
            {
                kind: 'blend1d',
                parameter: 'missing',
                children: [{ threshold: 0, motion: { kind: 'clip', clipId: 'walk' } }],
            },
            { knownClipIds: ['walk'], knownParameters: ['speed'] }
        );
        expect(diagnostics.some((d) => d.code === 'animation.blendGraph.parameter.unknown')).toBe(true);
    });

    it('empty children produces diagnostic', () => {
        const diagnostics = validateAnimationMotionDefinition({
            kind: 'blend1d',
            parameter: 'speed',
            children: [],
        });
        expect(diagnostics.some((d) => d.code === 'animation.blendGraph.children.empty')).toBe(true);
    });

    it('invalid threshold produces diagnostic', () => {
        const diagnostics = validateAnimationMotionDefinition({
            kind: 'blend1d',
            parameter: 'speed',
            children: [
                {
                    threshold: NaN,
                    motion: { kind: 'clip', clipId: 'walk' },
                },
            ],
        });
        expect(diagnostics.some((d) => d.code === 'animation.blendGraph.threshold.invalid')).toBe(true);
    });

    it('invalid 2D position produces diagnostic', () => {
        const diagnostics = validateAnimationMotionDefinition({
            kind: 'blend2d',
            parameterX: 'x',
            parameterY: 'y',
            children: [
                {
                    position: [NaN, 0],
                    motion: { kind: 'clip', clipId: 'walk' },
                },
            ],
        });
        expect(diagnostics.some((d) => d.code === 'animation.blendGraph.position.invalid')).toBe(true);
    });

    it('valid weight in direct blend is preserved', () => {
        const diagnostics = validateAnimationMotionDefinition({
            kind: 'direct',
            children: [
                {
                    motion: { kind: 'clip', clipId: 'walk' },
                    weight: 0.5,
                },
            ],
        });
        // Valid finite weight produces no weight diagnostic
        expect(diagnostics.some((d) => d.code === 'animation.blendGraph.weight.invalid')).toBe(false);
    });

    it('non-finite additive weight is stripped without diagnostic', () => {
        const diagnostics = validateAnimationMotionDefinition({
            kind: 'additive',
            base: { kind: 'clip', clipId: 'walk' },
            additive: { kind: 'clip', clipId: 'walk' },
            weight: Infinity,
        });
        // Non-finite weight is silently stripped during normalization;
        // no weight.invalid diagnostic is produced because the frozen
        // definition no longer carries the weight property.
        expect(diagnostics.some((d) => d.code === 'animation.blendGraph.weight.invalid')).toBe(false);
    });

    it('valid definitions produce zero diagnostics', () => {
        const diagnostics = validateAnimationMotionDefinition(
            {
                kind: 'blend1d',
                parameter: 'speed',
                children: [
                    { threshold: 0, motion: { kind: 'clip', clipId: 'walk' } },
                    { threshold: 1, motion: { kind: 'clip', clipId: 'run' } },
                ],
            },
            { knownClipIds: ['walk', 'run'], knownParameters: ['speed'] }
        );
        expect(diagnostics).toHaveLength(0);
    });

    it('empty children for blend2d produces diagnostic', () => {
        const diagnostics = validateAnimationMotionDefinition({
            kind: 'blend2d',
            parameterX: 'x',
            parameterY: 'y',
            children: [],
        });
        expect(diagnostics.some((d) => d.code === 'animation.blendGraph.children.empty')).toBe(true);
    });

    it('empty children for direct blend produces diagnostic', () => {
        const diagnostics = validateAnimationMotionDefinition({
            kind: 'direct',
            children: [],
        });
        expect(diagnostics.some((d) => d.code === 'animation.blendGraph.children.empty')).toBe(true);
    });

    it('unknown parameter for additive blend produces diagnostic', () => {
        const diagnostics = validateAnimationMotionDefinition(
            {
                kind: 'additive',
                base: { kind: 'clip', clipId: 'walk' },
                additive: { kind: 'clip', clipId: 'walk' },
                parameter: 'missing',
            },
            { knownClipIds: ['walk'], knownParameters: ['speed'] }
        );
        expect(diagnostics.some((d) => d.code === 'animation.blendGraph.parameter.unknown')).toBe(true);
    });
});

describe('buildAnimationMotionDefinition from builder or raw definition', () => {
    it('passes through raw definition frozen', () => {
        const raw = { kind: 'clip' as const, clipId: 'walk' };
        const result = buildAnimationMotionDefinition(raw);
        expect(result.kind).toBe('clip');
    });

    it('builds from AnimationMotionBuilder', () => {
        const builder = AnimationBlendGraph.clip('walk');
        const result = buildAnimationMotionDefinition(builder);
        expect(result.kind).toBe('clip');
    });
});
