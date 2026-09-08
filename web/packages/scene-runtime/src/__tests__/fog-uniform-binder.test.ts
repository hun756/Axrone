import { Vec3 } from '@axrone/numeric';
import { describe, expect, it, vi } from 'vitest';
import { SceneFogUniformBinder } from '../fog-uniform-binder';
import type { SceneFogState } from '../fog-state';
import type { SceneShaderResource } from '../shader-registry';
import type { SceneUniformWriteTarget } from '../uniform-writer';

const createMockWriteTarget = (): SceneUniformWriteTarget & {
    writes: Map<string, unknown>;
} => {
    const writes = new Map<string, unknown>();
    return {
        writes,
        write: vi.fn((_shader: SceneShaderResource, name: string, value: unknown) => {
            writes.set(name, value);
        }),
    };
};

const createMockShader = (): SceneShaderResource =>
    ({
        id: 'test-shader',
        program: {} as WebGLProgram,
        uniformLocations: new Map(),
        uniformTypes: new Map(),
        uniformNames: [],
    }) as unknown as SceneShaderResource;

const createDisabledFog = (): SceneFogState => ({
    enabled: false,
    color: new Vec3(0.5, 0.6, 0.7),
    mode: 1,
    density: 0.015,
    range: [0, 300],
});

const createEnabledFog = (): SceneFogState => ({
    enabled: true,
    color: new Vec3(0.533, 0.6, 0.667),
    mode: 2,
    density: 0.03,
    range: [10, 200],
});

describe('SceneFogUniformBinder', () => {
    it('writes u_FogEnabled=0 and returns early when fog is disabled', () => {
        const target = createMockWriteTarget();
        const shader = createMockShader();
        const binder = new SceneFogUniformBinder(target);

        binder.apply(shader, createDisabledFog());

        expect(target.writes.get('u_FogEnabled')).toBe(0);
        expect(target.writes.has('u_FogColor')).toBe(false);
        expect(target.writes.has('u_FogMode')).toBe(false);
        expect(target.writes.has('u_FogDensity')).toBe(false);
        expect(target.writes.has('u_FogStartEnd')).toBe(false);
    });

    it('writes all fog uniforms when fog is enabled', () => {
        const target = createMockWriteTarget();
        const shader = createMockShader();
        const binder = new SceneFogUniformBinder(target);
        const fog = createEnabledFog();

        binder.apply(shader, fog);

        expect(target.writes.get('u_FogEnabled')).toBe(1);
        expect(target.writes.get('u_FogColor')).toBe(fog.color);
        expect(target.writes.get('u_FogMode')).toBe(2);
        expect(target.writes.get('u_FogDensity')).toBe(0.03);
        expect(target.writes.get('u_FogStartEnd')).toEqual([10, 200]);
    });

    it('writes u_FogEnabled=1 when fog transitions from disabled to enabled', () => {
        const target = createMockWriteTarget();
        const shader = createMockShader();
        const binder = new SceneFogUniformBinder(target);

        binder.apply(shader, createDisabledFog());
        expect(target.writes.get('u_FogEnabled')).toBe(0);

        target.writes.clear();
        binder.apply(shader, createEnabledFog());
        expect(target.writes.get('u_FogEnabled')).toBe(1);
    });

    it('writes correct mode for linear fog', () => {
        const target = createMockWriteTarget();
        const shader = createMockShader();
        const binder = new SceneFogUniformBinder(target);

        binder.apply(shader, {
            ...createEnabledFog(),
            mode: 0,
        });

        expect(target.writes.get('u_FogMode')).toBe(0);
    });

    it('calls write on the uniform target for each uniform', () => {
        const target = createMockWriteTarget();
        const shader = createMockShader();
        const binder = new SceneFogUniformBinder(target);

        binder.apply(shader, createEnabledFog());

        // 5 uniforms: u_FogEnabled, u_FogColor, u_FogMode, u_FogDensity, u_FogStartEnd
        expect(target.write).toHaveBeenCalledTimes(5);
    });

    it('calls write only once for disabled fog', () => {
        const target = createMockWriteTarget();
        const shader = createMockShader();
        const binder = new SceneFogUniformBinder(target);

        binder.apply(shader, createDisabledFog());

        expect(target.write).toHaveBeenCalledTimes(1);
    });
});
