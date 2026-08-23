import { describe, expect, it, vi, beforeEach } from 'vitest';
import { Vec4 } from '@axrone/numeric';
import { WebGLTextureSampler, SamplerFactory, SamplerBuilder } from '../sampler';
import { FilterMode, WrapMode, TextureError } from '../interfaces';

const createMockGL = () => {
	const samplerHandle = { id: 1 } as unknown as WebGLSampler;
	return {
		_gl: {
			createSampler: vi.fn(() => samplerHandle),
			deleteSampler: vi.fn(),
			bindSampler: vi.fn(),
			samplerParameteri: vi.fn(),
			samplerParameterf: vi.fn(),
			getExtension: vi.fn(() => null),
			getParameter: vi.fn(() => 16),
			TEXTURE_MIN_FILTER: 0x2801,
			TEXTURE_MAG_FILTER: 0x2800,
			TEXTURE_WRAP_S: 0x2802,
			TEXTURE_WRAP_T: 0x2803,
			TEXTURE_WRAP_R: 0x8072,
			TEXTURE_COMPARE_MODE: 0x884c,
			TEXTURE_COMPARE_FUNC: 0x884d,
			TEXTURE_MIN_LOD: 0x813a,
			TEXTURE_MAX_LOD: 0x813b,
			COMPARE_REF_TO_TEXTURE: 0x884c,
			NONE: 0,
			NEAREST: 0x2600,
			LINEAR: 0x2601,
			NEAREST_MIPMAP_NEAREST: 0x2700,
			LINEAR_MIPMAP_NEAREST: 0x2701,
			NEAREST_MIPMAP_LINEAR: 0x2702,
			LINEAR_MIPMAP_LINEAR: 0x2703,
			REPEAT: 0x2901,
			CLAMP_TO_EDGE: 0x812f,
			MIRRORED_REPEAT: 0x8370,
			NEVER: 0x0200,
			LESS: 0x0201,
			EQUAL: 0x0202,
			LEQUAL: 0x0203,
			GREATER: 0x0204,
			NOTEQUAL: 0x0205,
			GEQUAL: 0x0206,
			ALWAYS: 0x0207,
		} as unknown as WebGL2RenderingContext,
		samplerHandle,
	};
};

const baseOptions = {
	minFilter: FilterMode.LINEAR,
	magFilter: FilterMode.LINEAR,
	wrapS: WrapMode.REPEAT,
	wrapT: WrapMode.REPEAT,
};

describe('WebGLTextureSampler', () => {
	let mockGL: ReturnType<typeof createMockGL>;

	beforeEach(() => {
		mockGL = createMockGL();
	});

	describe('constructor', () => {
		it('creates a WebGL sampler and configures it', () => {
			const sampler = new WebGLTextureSampler(mockGL._gl, baseOptions);
			expect(mockGL._gl.createSampler).toHaveBeenCalledTimes(1);
			expect(sampler.nativeHandle).toBe(mockGL.samplerHandle);
			expect(sampler.options.minFilter).toBe(FilterMode.LINEAR);
			expect(sampler.options.magFilter).toBe(FilterMode.LINEAR);
		});

		it('sets filter parameters via samplerParameteri', () => {
			new WebGLTextureSampler(mockGL._gl, baseOptions);
			const calls = (mockGL._gl.samplerParameteri as any).mock.calls;
			const filterCalls = calls.filter(
				(c: any[]) =>
					c[1] === mockGL._gl.TEXTURE_MIN_FILTER || c[1] === mockGL._gl.TEXTURE_MAG_FILTER
			);
			expect(filterCalls.length).toBe(2);
		});

		it('sets wrap parameters for S and T', () => {
			new WebGLTextureSampler(mockGL._gl, baseOptions);
			const calls = (mockGL._gl.samplerParameteri as any).mock.calls;
			const wrapCalls = calls.filter(
				(c: any[]) =>
					c[1] === mockGL._gl.TEXTURE_WRAP_S || c[1] === mockGL._gl.TEXTURE_WRAP_T
			);
			expect(wrapCalls.length).toBe(2);
		});

		it('sets wrap R when provided', () => {
			new WebGLTextureSampler(mockGL._gl, { ...baseOptions, wrapR: WrapMode.CLAMP_TO_EDGE });
			const calls = (mockGL._gl.samplerParameteri as any).mock.calls;
			const wrapRCalls = calls.filter((c: any[]) => c[1] === mockGL._gl.TEXTURE_WRAP_R);
			expect(wrapRCalls.length).toBe(1);
		});

		it('throws when createSampler returns null (context lost)', () => {
			(mockGL._gl.createSampler as any).mockReturnValue(null);
			expect(() => new WebGLTextureSampler(mockGL._gl, baseOptions)).toThrow(
				/Failed to create WebGL sampler/
			);
		});

		it('generates a unique ID with sampler_ prefix', () => {
			const s1 = new WebGLTextureSampler(mockGL._gl, baseOptions);
			const s2 = new WebGLTextureSampler(mockGL._gl, baseOptions);
			expect(s1.id).toMatch(/^sampler_/);
			expect(s1.id).not.toBe(s2.id);
		});
	});

	describe('anisotropy configuration', () => {
		it('applies anisotropy when maxAnisotropy > 1 and extension available', () => {
			const ext = {
				TEXTURE_MAX_ANISOTROPY_EXT: 0x84fe,
				MAX_TEXTURE_MAX_ANISOTROPY_EXT: 0x84ff,
			};
			(mockGL._gl.getExtension as any).mockReturnValue(ext);
			(mockGL._gl.getParameter as any).mockReturnValue(16);

			new WebGLTextureSampler(mockGL._gl, { ...baseOptions, maxAnisotropy: 8 });
			expect(mockGL._gl.getExtension).toHaveBeenCalledWith('EXT_texture_filter_anisotropic');
			const anisoCalls = (mockGL._gl.samplerParameterf as any).mock.calls.filter(
				(c: any[]) => c[1] === ext.TEXTURE_MAX_ANISOTROPY_EXT
			);
			expect(anisoCalls.length).toBe(1);
			expect(anisoCalls[0][2]).toBe(8);
		});

		it('clamps anisotropy to hardware max', () => {
			const ext = {
				TEXTURE_MAX_ANISOTROPY_EXT: 0x84fe,
				MAX_TEXTURE_MAX_ANISOTROPY_EXT: 0x84ff,
			};
			(mockGL._gl.getExtension as any).mockReturnValue(ext);
			(mockGL._gl.getParameter as any).mockReturnValue(4);

			new WebGLTextureSampler(mockGL._gl, { ...baseOptions, maxAnisotropy: 16 });
			const anisoCalls = (mockGL._gl.samplerParameterf as any).mock.calls.filter(
				(c: any[]) => c[1] === ext.TEXTURE_MAX_ANISOTROPY_EXT
			);
			expect(anisoCalls[0][2]).toBe(4);
		});

		it('skips anisotropy when extension is not available', () => {
			(mockGL._gl.getExtension as any).mockReturnValue(null);
			new WebGLTextureSampler(mockGL._gl, { ...baseOptions, maxAnisotropy: 16 });
			const anisoCalls = (mockGL._gl.samplerParameterf as any).mock.calls;
			expect(anisoCalls.length).toBe(0);
		});

		it('skips anisotropy when maxAnisotropy <= 1', () => {
            const before = (mockGL._gl.samplerParameterf as any).mock.calls.length;
            new WebGLTextureSampler(mockGL._gl, { ...baseOptions, maxAnisotropy: 1 });
            const after = (mockGL._gl.samplerParameterf as any).mock.calls.length;
            expect(after - before).toBe(0);
        });
	});

	describe('shadow comparison configuration', () => {
		it('sets compare mode and function for shadow samplers', () => {
			new WebGLTextureSampler(mockGL._gl, {
				...baseOptions,
				compareMode: 'COMPARE_REF_TO_TEXTURE',
				compareFunc: 'LEQUAL',
			});
			const calls = (mockGL._gl.samplerParameteri as any).mock.calls;
			const compareModeCalls = calls.filter(
				(c: any[]) => c[1] === mockGL._gl.TEXTURE_COMPARE_MODE
			);
			expect(compareModeCalls.length).toBe(1);
			expect(compareModeCalls[0][2]).toBe(mockGL._gl.COMPARE_REF_TO_TEXTURE);

			const compareFuncCalls = calls.filter(
				(c: any[]) => c[1] === mockGL._gl.TEXTURE_COMPARE_FUNC
			);
			expect(compareFuncCalls.length).toBe(1);
			expect(compareFuncCalls[0][2]).toBe(mockGL._gl.LEQUAL);
		});

		it('sets compare mode to NONE when compareMode is not set', () => {
			new WebGLTextureSampler(mockGL._gl, baseOptions);
			const calls = (mockGL._gl.samplerParameteri as any).mock.calls;
			const compareModeCalls = calls.filter(
				(c: any[]) => c[1] === mockGL._gl.TEXTURE_COMPARE_MODE
			);
			expect(compareModeCalls[0][2]).toBe(mockGL._gl.NONE);
		});
	});

	describe('LOD configuration', () => {
		it('sets minLod and maxLod via samplerParameterf', () => {
			new WebGLTextureSampler(mockGL._gl, {
				...baseOptions,
				minLod: 0,
				maxLod: 10,
			});
			const calls = (mockGL._gl.samplerParameterf as any).mock.calls;
			const minLodCall = calls.find((c: any[]) => c[1] === mockGL._gl.TEXTURE_MIN_LOD);
			const maxLodCall = calls.find((c: any[]) => c[1] === mockGL._gl.TEXTURE_MAX_LOD);
			expect(minLodCall).toBeDefined();
			expect(minLodCall![2]).toBe(0);
			expect(maxLodCall).toBeDefined();
			expect(maxLodCall![2]).toBe(10);
		});
	});

	describe('bind / unbind', () => {
		it('binds sampler to specified texture unit', () => {
			const sampler = new WebGLTextureSampler(mockGL._gl, baseOptions);
			sampler.bind(3);
			expect(mockGL._gl.bindSampler).toHaveBeenCalledWith(3, mockGL.samplerHandle);
		});

		it('throws when binding without a texture unit', () => {
			const sampler = new WebGLTextureSampler(mockGL._gl, baseOptions);
			expect(() => sampler.bind()).toThrow(/Texture unit is required/);
		});

		it('throws when binding with negative texture unit', () => {
			const sampler = new WebGLTextureSampler(mockGL._gl, baseOptions);
			expect(() => sampler.bind(-1)).toThrow(/Texture unit must be non-negative/);
		});

		it('unbinds from current unit', () => {
			const sampler = new WebGLTextureSampler(mockGL._gl, baseOptions);
			sampler.bind(2);
			sampler.unbind();
			expect(mockGL._gl.bindSampler).toHaveBeenCalledWith(2, null);
		});

		it('unbind is no-op when not bound', () => {
			const sampler = new WebGLTextureSampler(mockGL._gl, baseOptions);
			sampler.unbind();
			expect(mockGL._gl.bindSampler).not.toHaveBeenCalled();
		});
	});

	describe('dispose', () => {
		it('deletes the native sampler', () => {
			const sampler = new WebGLTextureSampler(mockGL._gl, baseOptions);
			sampler.dispose();
			expect(mockGL._gl.deleteSampler).toHaveBeenCalledWith(mockGL.samplerHandle);
		});

		it('is idempotent', () => {
			const sampler = new WebGLTextureSampler(mockGL._gl, baseOptions);
			sampler.dispose();
			sampler.dispose();
			expect(mockGL._gl.deleteSampler).toHaveBeenCalledTimes(1);
		});

		it('throws on bind after dispose', () => {
			const sampler = new WebGLTextureSampler(mockGL._gl, baseOptions);
			sampler.dispose();
			expect(() => sampler.bind(0)).toThrow(/disposed/);
		});

		it('reports isDisposed correctly', () => {
			const sampler = new WebGLTextureSampler(mockGL._gl, baseOptions);
			expect(sampler.isDisposed).toBe(false);
			sampler.dispose();
			expect(sampler.isDisposed).toBe(true);
		});
	});
});

describe('SamplerFactory', () => {
	let mockGL: ReturnType<typeof createMockGL>;

	beforeEach(() => {
		mockGL = createMockGL();
	});

	describe('createCommonSampler', () => {
		it('creates linear_repeat sampler', () => {
			const sampler = SamplerFactory.createCommonSampler(mockGL._gl, 'linear_repeat');
			expect(sampler).toBeInstanceOf(WebGLTextureSampler);
			expect(sampler.options.minFilter).toBe(FilterMode.LINEAR);
			expect(sampler.options.wrapS).toBe(WrapMode.REPEAT);
		});

		it('creates linear_clamp sampler', () => {
			const sampler = SamplerFactory.createCommonSampler(mockGL._gl, 'linear_clamp');
			expect(sampler.options.wrapS).toBe(WrapMode.CLAMP_TO_EDGE);
			expect(sampler.options.wrapT).toBe(WrapMode.CLAMP_TO_EDGE);
		});

		it('creates nearest_repeat sampler', () => {
			const sampler = SamplerFactory.createCommonSampler(mockGL._gl, 'nearest_repeat');
			expect(sampler.options.minFilter).toBe(FilterMode.NEAREST);
			expect(sampler.options.wrapS).toBe(WrapMode.REPEAT);
		});

		it('creates nearest_clamp sampler', () => {
			const sampler = SamplerFactory.createCommonSampler(mockGL._gl, 'nearest_clamp');
			expect(sampler.options.minFilter).toBe(FilterMode.NEAREST);
			expect(sampler.options.wrapS).toBe(WrapMode.CLAMP_TO_EDGE);
		});

		it('creates trilinear sampler with anisotropy', () => {
			const sampler = SamplerFactory.createCommonSampler(mockGL._gl, 'trilinear');
			expect(sampler.options.minFilter).toBe(FilterMode.LINEAR_MIPMAP_LINEAR);
			expect(sampler.options.maxAnisotropy).toBe(16);
		});

		it('creates shadow sampler with comparison', () => {
			const sampler = SamplerFactory.createCommonSampler(mockGL._gl, 'shadow');
			expect(sampler.options.compareMode).toBe('COMPARE_REF_TO_TEXTURE');
			expect(sampler.options.compareFunc).toBe('LEQUAL');
		});

		it('throws for unknown sampler type', () => {
			expect(() =>
				SamplerFactory.createCommonSampler(mockGL._gl, 'nonexistent' as any)
			).toThrow(/Unknown common sampler type/);
		});
	});

	describe('builder', () => {
		it('returns a SamplerBuilder instance', () => {
			const builder = SamplerFactory.builder();
			expect(builder).toBeInstanceOf(SamplerBuilder);
		});
	});
});

describe('SamplerBuilder', () => {
	let mockGL: ReturnType<typeof createMockGL>;

	beforeEach(() => {
		mockGL = createMockGL();
	});

	it('builds a sampler with fluent API', () => {
		const sampler = SamplerFactory.builder()
			.minFilter(FilterMode.LINEAR)
			.magFilter(FilterMode.LINEAR)
			.wrapS(WrapMode.REPEAT)
			.wrapT(WrapMode.REPEAT)
			.build(mockGL._gl);

		expect(sampler).toBeInstanceOf(WebGLTextureSampler);
		expect(sampler.options.minFilter).toBe(FilterMode.LINEAR);
	});

	it('supports wrapAll shorthand', () => {
		const sampler = SamplerFactory.builder()
			.minFilter(FilterMode.LINEAR)
			.magFilter(FilterMode.NEAREST)
			.wrapAll(WrapMode.CLAMP_TO_EDGE)
			.build(mockGL._gl);

		expect(sampler.options.wrapS).toBe(WrapMode.CLAMP_TO_EDGE);
		expect(sampler.options.wrapT).toBe(WrapMode.CLAMP_TO_EDGE);
		expect(sampler.options.wrapR).toBe(WrapMode.CLAMP_TO_EDGE);
	});

	it('supports anisotropy', () => {
		const sampler = SamplerFactory.builder()
			.minFilter(FilterMode.LINEAR)
			.magFilter(FilterMode.LINEAR)
			.wrapS(WrapMode.REPEAT)
			.wrapT(WrapMode.REPEAT)
			.anisotropy(8)
			.build(mockGL._gl);

		expect(sampler.options.maxAnisotropy).toBe(8);
	});

	it('supports shadow comparison', () => {
		const sampler = SamplerFactory.builder()
			.minFilter(FilterMode.LINEAR)
			.magFilter(FilterMode.LINEAR)
			.wrapS(WrapMode.CLAMP_TO_EDGE)
			.wrapT(WrapMode.CLAMP_TO_EDGE)
			.shadowComparison('LEQUAL')
			.build(mockGL._gl);

		expect(sampler.options.compareMode).toBe('COMPARE_REF_TO_TEXTURE');
		expect(sampler.options.compareFunc).toBe('LEQUAL');
	});

	it('supports LOD range', () => {
		const sampler = SamplerFactory.builder()
			.minFilter(FilterMode.LINEAR)
			.magFilter(FilterMode.LINEAR)
			.wrapS(WrapMode.REPEAT)
			.wrapT(WrapMode.REPEAT)
			.lodRange(0, 5)
			.build(mockGL._gl);

		expect(sampler.options.minLod).toBe(0);
		expect(sampler.options.maxLod).toBe(5);
	});

	it('supports border color', () => {
		const color = new Vec4(1, 0, 0, 1);
		const sampler = SamplerFactory.builder()
			.minFilter(FilterMode.LINEAR)
			.magFilter(FilterMode.LINEAR)
			.wrapS(WrapMode.REPEAT)
			.wrapT(WrapMode.REPEAT)
			.borderColor(color)
			.build(mockGL._gl);

		expect(sampler.options.borderColor).toBe(color);
	});

	it('supports lodBias', () => {
		const sampler = SamplerFactory.builder()
			.minFilter(FilterMode.LINEAR)
			.magFilter(FilterMode.LINEAR)
			.wrapS(WrapMode.REPEAT)
			.wrapT(WrapMode.REPEAT)
			.lodBias(0.5)
			.build(mockGL._gl);

		expect(sampler.options.lodBias).toBe(0.5);
	});

	it('throws when min filter is missing', () => {
		expect(() =>
			SamplerFactory.builder()
				.magFilter(FilterMode.LINEAR)
				.wrapS(WrapMode.REPEAT)
				.wrapT(WrapMode.REPEAT)
				.build(mockGL._gl)
		).toThrow(/Min and mag filters are required/);
	});

	it('throws when wrap S/T is missing', () => {
		expect(() =>
			SamplerFactory.builder()
				.minFilter(FilterMode.LINEAR)
				.magFilter(FilterMode.LINEAR)
				.build(mockGL._gl)
		).toThrow(/Wrap modes for S and T are required/);
	});
});
