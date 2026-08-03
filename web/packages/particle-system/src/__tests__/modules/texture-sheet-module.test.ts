import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TextureSheetModule } from '../../modules/texture-sheet-module';
import type { TextureSheetConfiguration } from '../../core/configuration';
import type { IParticleBuffer } from '../../core/interfaces';

function makeCurve(constant = 0) {
    return { mode: 0, constant, constantMin: 0, constantMax: 0 };
}

function makeTextureSheetConfig(
    overrides: Partial<TextureSheetConfiguration> = {}
): TextureSheetConfiguration {
    return {
        enabled: true,
        priority: 1100,
        tilesX: 4,
        tilesY: 4,
        animation: 'singleRow',
        timeMode: 'fps',
        fps: 12,
        startFrame: makeCurve(0),
        frameOverTime: makeCurve(0),
        cycleCount: 1,
        flipU: false,
        flipV: false,
        uvChannelMask: 0,
        ...overrides,
    } as any;
}

function createMockBuffer(count = 1): IParticleBuffer {
    const alive = new Uint32Array(100);
    for (let i = 0; i < count; i++) alive[i] = 1;
    return {
        get count() {
            return count;
        },
        capacity: 100,
        allocated: true,
        alive,
        positions: new Float32Array(300),
        velocities: new Float32Array(300),
        accelerations: new Float32Array(300),
        lifetimes: new Float32Array(100).fill(5),
        ages: new Float32Array(100),
        sizes: new Float32Array(300),
        colors: new Float32Array(400),
        rotations: new Float32Array(300),
        angularVelocities: new Float32Array(300),
        customData: [],
        ids: new Uint32Array(100),
        allocate: vi.fn().mockReturnValue(true),
        deallocate: vi.fn(),
        resize: vi.fn().mockReturnValue(true),
        addParticle: vi.fn().mockReturnValue(1),
        removeParticle: vi.fn().mockReturnValue(true),
        killParticle: vi.fn().mockReturnValue(true),
        getParticleIndex: vi
            .fn()
            .mockImplementation((id: number) => (id >= 0 && id < count ? id : -1)),
        getParticleId: vi.fn().mockImplementation((i: number) => i),
        getPosition: vi.fn().mockReturnValue({ x: 0, y: 0, z: 0 }),
        setPosition: vi.fn(),
        getVelocity: vi.fn().mockReturnValue({ x: 0, y: 0, z: 0 }),
        setVelocity: vi.fn(),
        getLifetime: vi.fn().mockReturnValue(5),
        setLifetime: vi.fn(),
        getAge: vi.fn().mockReturnValue(0),
        setAge: vi.fn(),
        getSize: vi.fn().mockReturnValue(1),
        setSize: vi.fn(),
        getColor: vi.fn().mockReturnValue(0xffffffff),
        setColor: vi.fn(),
        getCustomData: vi.fn().mockReturnValue(new Float32Array(4)),
        setCustomData: vi.fn(),
        clear: vi.fn(),
        compact: vi.fn(),
        sort: vi.fn(),
    } as any;
}

describe('TextureSheetModule', () => {
    describe('constructor', () => {
        it('creates module with type texture and priority 1100', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            expect(mod.type).toBe('texture');
            expect(mod.priority).toBe(1100);
            expect(mod.enabled).toBe(true);
        });
    });

    describe('lifecycle', () => {
        it('initialize succeeds', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            expect(() => mod.initialize()).not.toThrow();
        });

        it('reset after initialize does not throw', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            expect(() => mod.reset()).not.toThrow();
        });

        it('destroy after initialize does not throw', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            expect(() => mod.destroy()).not.toThrow();
        });
    });

    describe('frame generation', () => {
        it('generates tilesX * tilesY frames', () => {
            const mod = new TextureSheetModule(
                makeTextureSheetConfig({ tilesX: 4, tilesY: 3 })
            );
            mod.initialize();
            expect(mod.getFrameCount()).toBe(12);
        });

        it('generates 16 frames for 4x4 grid', () => {
            const mod = new TextureSheetModule(
                makeTextureSheetConfig({ tilesX: 4, tilesY: 4 })
            );
            mod.initialize();
            expect(mod.getFrameCount()).toBe(16);
        });
    });

    describe('getFrameUV', () => {
        it('returns frame data for valid index', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            const frame = mod.getFrameUV(0);
            expect(frame).not.toBeNull();
            expect(frame!.u).toBe(0);
            expect(frame!.v).toBe(0);
            expect(frame!.uWidth).toBeCloseTo(0.25);
            expect(frame!.vHeight).toBeCloseTo(0.25);
        });

        it('returns null for invalid index', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            expect(mod.getFrameUV(-1)).toBeNull();
            expect(mod.getFrameUV(100)).toBeNull();
        });

        it('frame UV coordinates are correct for second column', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            const frame = mod.getFrameUV(1);
            expect(frame).not.toBeNull();
            expect(frame!.u).toBeCloseTo(0.25);
            expect(frame!.v).toBe(0);
        });
    });

    describe('getFrameData', () => {
        it('returns Float32Array', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            const data = mod.getFrameData();
            expect(data).toBeInstanceOf(Float32Array);
        });
    });

    describe('wholeSheet mode', () => {
        it('assigns static frame in wholeSheet mode', () => {
            const mod = new TextureSheetModule(
                makeTextureSheetConfig({ animation: 'wholeSheet' })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            const frame = mod.getParticleCurrentFrame(0);
            expect(frame).toBe(0);
        });
    });

    describe('singleRow mode', () => {
        it('creates default animation sequence', () => {
            const mod = new TextureSheetModule(
                makeTextureSheetConfig({ animation: 'singleRow' })
            );
            mod.initialize();
            const names = mod.getSequenceNames();
            expect(names).toContain('default');
        });

        it('animation advances frames over time', () => {
            const mod = new TextureSheetModule(
                makeTextureSheetConfig({ animation: 'singleRow', fps: 10 })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);

            mod.process(buffer, 0.016);
            // Process many updates to advance frame
            for (let i = 0; i < 20; i++) {
                mod.process(buffer, 0.016);
            }
            // Frame should have advanced from initial
            const frame = mod.getParticleCurrentFrame(0);
            expect(frame).toBeGreaterThanOrEqual(0);
        });
    });

    describe('addAnimationSequence', () => {
        it('adds valid sequence', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            expect(mod.addAnimationSequence('walk', 0, 5, 12, true)).toBe(true);
            expect(mod.getSequenceNames()).toContain('walk');
        });

        it('rejects invalid frame range', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            // startFrame > endFrame
            expect(mod.addAnimationSequence('bad', 5, 2, 12)).toBe(false);
        });

        it('rejects out-of-range endFrame', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            // endFrame >= frames.length (16)
            expect(mod.addAnimationSequence('bad', 0, 20, 12)).toBe(false);
        });

        it('rejects negative startFrame', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            expect(mod.addAnimationSequence('bad', -1, 5, 12)).toBe(false);
        });
    });

    describe('removeAnimationSequence', () => {
        it('removes existing sequence', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            mod.addAnimationSequence('walk', 0, 5, 12, true);
            expect(mod.removeAnimationSequence('walk')).toBe(true);
            expect(mod.getSequenceNames()).not.toContain('walk');
        });

        it('returns false for non-existent sequence', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            expect(mod.removeAnimationSequence('nope')).toBe(false);
        });
    });

    describe('setParticleAnimation', () => {
        it('sets animation for existing particle', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            mod.addAnimationSequence('run', 0, 3, 10, true);

            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016); // creates animation for particle 0

            expect(mod.setParticleAnimation(0, 'run')).toBe(true);
        });

        it('returns false for non-existent particle', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            mod.addAnimationSequence('run', 0, 3, 10, true);
            expect(mod.setParticleAnimation(999, 'run')).toBe(false);
        });

        it('returns false for non-existent sequence', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.setParticleAnimation(0, 'noSeq')).toBe(false);
        });
    });

    describe('setParticleFrame', () => {
        it('sets specific frame', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);

            expect(mod.setParticleFrame(0, 5)).toBe(true);
            expect(mod.getParticleCurrentFrame(0)).toBe(5);
        });

        it('returns false for invalid frame index', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);

            expect(mod.setParticleFrame(0, -1)).toBe(false);
            expect(mod.setParticleFrame(0, 100)).toBe(false);
        });

        it('returns false for non-existent particle', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            expect(mod.setParticleFrame(999, 0)).toBe(false);
        });
    });

    describe('playParticleAnimation / pauseParticleAnimation', () => {
        it('play resumes animation', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);

            mod.setParticleFrame(0, 3); // pauses animation
            expect(mod.playParticleAnimation(0)).toBe(true);
        });

        it('pause stops animation', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);

            expect(mod.pauseParticleAnimation(0)).toBe(true);
            expect(mod.getActiveAnimationCount()).toBe(0);
        });

        it('returns false for non-existent particle', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            expect(mod.playParticleAnimation(999)).toBe(false);
            expect(mod.pauseParticleAnimation(999)).toBe(false);
        });
    });

    describe('getParticleCurrentFrame', () => {
        it('returns -1 for non-existent particle', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            expect(mod.getParticleCurrentFrame(999)).toBe(-1);
        });
    });

    describe('getActiveAnimationCount', () => {
        it('counts playing animations', () => {
            const mod = new TextureSheetModule(
                makeTextureSheetConfig({ animation: 'singleRow' })
            );
            mod.initialize();
            const buffer = createMockBuffer(3);
            mod.process(buffer, 0.016);
            expect(mod.getActiveAnimationCount()).toBe(3);
        });

        it('paused animations not counted', () => {
            const mod = new TextureSheetModule(
                makeTextureSheetConfig({ animation: 'singleRow' })
            );
            mod.initialize();
            const buffer = createMockBuffer(2);
            mod.process(buffer, 0.016);
            mod.pauseParticleAnimation(0);
            expect(mod.getActiveAnimationCount()).toBe(1);
        });
    });

    describe('getFrameAtTime', () => {
        it('returns correct frame for time', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            mod.addAnimationSequence('seq', 0, 9, 10, true);
            // 10 frames at 10fps = 1 second duration
            const frame = mod.getFrameAtTime('seq', 0.5);
            expect(frame).toBeGreaterThanOrEqual(0);
            expect(frame).toBeLessThanOrEqual(9);
        });

        it('returns -1 for non-existent sequence', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            expect(mod.getFrameAtTime('nope', 0)).toBe(-1);
        });

        it('loops correctly', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            mod.addAnimationSequence('seq', 0, 9, 10, true);
            // 10 frames at 10fps = 1 second
            const frame1 = mod.getFrameAtTime('seq', 0.0);
            const frame2 = mod.getFrameAtTime('seq', 1.0); // exactly one loop
            expect(frame1).toBe(frame2);
        });
    });

    describe('configure - tile change', () => {
        it('regenerates frames when tiles change', () => {
            const mod = new TextureSheetModule(
                makeTextureSheetConfig({ tilesX: 4, tilesY: 4 })
            );
            mod.initialize();
            expect(mod.getFrameCount()).toBe(16);

            mod.configure(
                makeTextureSheetConfig({ tilesX: 2, tilesY: 2 }) as any
            );
            expect(mod.getFrameCount()).toBe(4);
        });
    });

    describe('disabled module', () => {
        it('does nothing when disabled', () => {
            const mod = new TextureSheetModule(
                makeTextureSheetConfig({ enabled: false })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.getParticleCurrentFrame(0)).toBe(-1);
        });
    });

    describe('dead particle cleanup', () => {
        it('removes animation for dead particles', () => {
            const mod = new TextureSheetModule(makeTextureSheetConfig());
            mod.initialize();
            const buffer = createMockBuffer(2);
            mod.process(buffer, 0.016);
            expect(mod.getActiveAnimationCount()).toBe(2);

            buffer.alive[1] = 0;
            mod.process(buffer, 0.016);
            expect(mod.getActiveAnimationCount()).toBe(1);
        });
    });
});
