import { beforeAll, describe, expect, it, vi, beforeEach } from 'vitest';

const webglConstantStub = new Proxy(
	{
		R8: 0x8229, RG8: 0x822b, RGB8: 0x8051, RGBA8: 0x8058,
		R16F: 0x822d, RG16F: 0x822f, RGB16F: 0x881b, RGBA16F: 0x881a,
		R32F: 0x822e, RG32F: 0x8230, RGB32F: 0x8815, RGBA32F: 0x8814,
		DEPTH_COMPONENT16: 0x81a5, DEPTH_COMPONENT24: 0x81a6, DEPTH_COMPONENT32F: 0x8cac,
		DEPTH24_STENCIL8: 0x88f0, DEPTH32F_STENCIL8: 0x8cad,
		RED: 0x1903, RG: 0x8227, RGB: 0x1907, RGBA: 0x1908,
		UNSIGNED_BYTE: 0x1401, HALF_FLOAT: 0x140b, FLOAT: 0x1406,
		DEPTH_COMPONENT: 0x1902, DEPTH_STENCIL: 0x84f9,
		UNSIGNED_SHORT: 0x1403, UNSIGNED_INT: 0x1405,
		UNSIGNED_INT_24_8: 0x84fa, FLOAT_32_UNSIGNED_INT_24_8_REV: 0x8dad,
		SRGB8: 0x8c41, SRGB8_ALPHA8: 0x8c43,
		TEXTURE_2D: 0x0de1, TEXTURE_3D: 0x806f, TEXTURE_CUBE_MAP: 0x8513,
		TEXTURE_2D_ARRAY: 0x8c1a,
		TEXTURE_CUBE_MAP_POSITIVE_X: 0x8515,
		TEXTURE_CUBE_MAP_NEGATIVE_X: 0x8516,
		TEXTURE_CUBE_MAP_POSITIVE_Y: 0x8517,
		TEXTURE_CUBE_MAP_NEGATIVE_Y: 0x8518,
		TEXTURE_CUBE_MAP_POSITIVE_Z: 0x8519,
		TEXTURE_CUBE_MAP_NEGATIVE_Z: 0x851a,
		TEXTURE0: 0x84c0,
		NEAREST: 0x2600, LINEAR: 0x2601,
		NEAREST_MIPMAP_NEAREST: 0x2700, LINEAR_MIPMAP_NEAREST: 0x2701,
		NEAREST_MIPMAP_LINEAR: 0x2702, LINEAR_MIPMAP_LINEAR: 0x2703,
		REPEAT: 0x2901, CLAMP_TO_EDGE: 0x812f, MIRRORED_REPEAT: 0x8370,
		TEXTURE_MIN_FILTER: 0x2801,
		TEXTURE_MAG_FILTER: 0x2800,
		TEXTURE_WRAP_S: 0x2802,
		TEXTURE_WRAP_T: 0x2803,
		TEXTURE_WRAP_R: 0x8072,
		TEXTURE_COMPARE_MODE: 0x884c,
		TEXTURE_COMPARE_FUNC: 0x884d,
		COMPARE_REF_TO_TEXTURE: 0x884c,
		NONE: 0,
		LEQUAL: 0x0203,
		TEXTURE_MIN_LOD: 0x813a,
		TEXTURE_MAX_LOD: 0x813b,
	},
	{
		get: (target, property) => {
			if (typeof property === 'string' && property in target) {
				return target[property as keyof typeof target];
			}
			return 0;
		},
	},
);

let WebGLTextureManager: typeof import('../manager').WebGLTextureManager;
let TextureDimension: typeof import('../interfaces').TextureDimension;
let TextureFormat: typeof import('../interfaces').TextureFormat;
let TextureUsage: typeof import('../interfaces').TextureUsage;
let FilterMode: typeof import('../interfaces').FilterMode;
let WrapMode: typeof import('../interfaces').WrapMode;

const createMockGL = () => {
	let nextTexId = 1;
	let nextSamplerId = 1;
	return {
		gl: {
			createTexture: vi.fn(() => ({ id: nextTexId++ })),
			deleteTexture: vi.fn(),
			bindTexture: vi.fn(),
			activeTexture: vi.fn(),
			texImage2D: vi.fn(),
			texImage3D: vi.fn(),
			texSubImage2D: vi.fn(),
			texSubImage3D: vi.fn(),
			generateMipmap: vi.fn(),
			createSampler: vi.fn(() => ({ id: nextSamplerId++ })),
			deleteSampler: vi.fn(),
			bindSampler: vi.fn(),
			samplerParameteri: vi.fn(),
			samplerParameterf: vi.fn(),
			getExtension: vi.fn(() => null),
			getParameter: vi.fn(() => 16),
			TEXTURE_2D: 0x0de1,
			TEXTURE_3D: 0x806f,
			TEXTURE_CUBE_MAP: 0x8513,
			TEXTURE_2D_ARRAY: 0x8c1a,
			TEXTURE_CUBE_MAP_POSITIVE_X: 0x8515,
			TEXTURE_CUBE_MAP_NEGATIVE_X: 0x8516,
			TEXTURE_CUBE_MAP_POSITIVE_Y: 0x8517,
			TEXTURE_CUBE_MAP_NEGATIVE_Y: 0x8518,
			TEXTURE_CUBE_MAP_POSITIVE_Z: 0x8519,
			TEXTURE_CUBE_MAP_NEGATIVE_Z: 0x851a,
			TEXTURE0: 0x84c0,
			RGBA: 0x1908,
			RGB: 0x1907,
			RED: 0x1903,
			UNSIGNED_BYTE: 0x1401,
			RGBA8: 0x8058,
			TEXTURE_MIN_FILTER: 0x2801,
			TEXTURE_MAG_FILTER: 0x2800,
			TEXTURE_WRAP_S: 0x2802,
			TEXTURE_WRAP_T: 0x2803,
			TEXTURE_WRAP_R: 0x8072,
			TEXTURE_COMPARE_MODE: 0x884c,
			TEXTURE_COMPARE_FUNC: 0x884d,
			COMPARE_REF_TO_TEXTURE: 0x884c,
			NONE: 0,
			NEAREST: 0x2600,
			LINEAR: 0x2601,
			NEAREST_MIPMAP_LINEAR: 0x2703,
			REPEAT: 0x2901,
			CLAMP_TO_EDGE: 0x812f,
			LEQUAL: 0x0203,
			TEXTURE_MIN_LOD: 0x813a,
			TEXTURE_MAX_LOD: 0x813b,
		} as unknown as WebGL2RenderingContext,
	} as const;
};

beforeAll(async () => {
	Object.assign(globalThis, {
		WebGL2RenderingContext: webglConstantStub,
		WebGLRenderingContext: webglConstantStub,
	});

	const managerModule = await import('../manager');
	const interfacesModule = await import('../interfaces');
	WebGLTextureManager = managerModule.WebGLTextureManager;
	TextureDimension = interfacesModule.TextureDimension;
	TextureFormat = interfacesModule.TextureFormat;
	TextureUsage = interfacesModule.TextureUsage;
	FilterMode = interfacesModule.FilterMode;
	WrapMode = interfacesModule.WrapMode;
});

describe('WebGLTextureManager', () => {
	let mockGL: ReturnType<typeof createMockGL>;
	let manager: InstanceType<typeof WebGLTextureManager>;

	beforeEach(() => {
		mockGL = createMockGL();
		manager = new WebGLTextureManager(mockGL.gl);
	});

	describe('createTexture', () => {
		it('creates a texture and tracks stats', () => {
			const tex = manager.createTexture({
				width: 64,
				height: 64,
				format: TextureFormat.RGBA8 as any,
				dimension: TextureDimension.TEXTURE_2D as any,
				usage: TextureUsage.STATIC as any,
			});
			expect(tex).toBeDefined();
			expect(tex.width).toBe(64);
			const stats = manager.getStats();
			expect(stats.totalTextures).toBe(1);
		});

		it('increments totalMemoryUsage', () => {
			manager.createTexture({
				width: 64,
				height: 64,
				format: TextureFormat.RGBA8 as any,
				dimension: TextureDimension.TEXTURE_2D as any,
				usage: TextureUsage.STATIC as any,
			});
			const stats = manager.getStats();
			expect(stats.totalMemoryUsage).toBe(64 * 64 * 4);
		});
	});

	describe('createTexture2D', () => {
		it('creates a 2D texture with correct defaults', () => {
			const tex = manager.createTexture2D(128, 128, TextureFormat.RGBA8 as any);
			expect(tex.width).toBe(128);
			expect(tex.height).toBe(128);
			expect(tex.dimension).toBe(TextureDimension.TEXTURE_2D);
		});

		it('creates a 2D texture with data', () => {
			const data = new Uint8Array(4 * 4 * 4);
			const tex = manager.createTexture2D(4, 4, TextureFormat.RGBA8 as any, data);
			expect(tex.width).toBe(4);
		});
	});

	describe('createTexture3D', () => {
		it('creates a 3D texture', () => {
			const tex = manager.createTexture3D(32, 32, 8, TextureFormat.R8 as any);
			expect(tex.width).toBe(32);
			expect(tex.height).toBe(32);
			expect(tex.depth).toBe(8);
			expect(tex.dimension).toBe(TextureDimension.TEXTURE_3D);
		});
	});

	describe('createTextureCube', () => {
		it('creates a cube texture', () => {
			const tex = manager.createTextureCube(64, TextureFormat.RGBA8 as any);
			expect(tex.width).toBe(64);
			expect(tex.height).toBe(64);
			expect(tex.dimension).toBe(TextureDimension.TEXTURE_CUBE);
		});

		it('uploads face data when 6 faces provided', () => {
			const faces = Array.from({ length: 6 }, () => new Uint8Array(4 * 4 * 4));
			const tex = manager.createTextureCube(4, TextureFormat.RGBA8 as any, faces);
			expect(mockGL.gl.texSubImage2D).toHaveBeenCalled();
		});
	});

	describe('createTextureArray', () => {
		it('creates a 2D array texture', () => {
			const tex = manager.createTextureArray(32, 32, 4, TextureFormat.RGBA8 as any);
			expect(tex.dimension).toBe(TextureDimension.TEXTURE_2D_ARRAY);
			expect(tex.arrayLayers).toBe(4);
		});
	});

	describe('createSampler', () => {
		it('creates a sampler', () => {
			const sampler = manager.createSampler({
				minFilter: FilterMode.LINEAR,
				magFilter: FilterMode.LINEAR,
				wrapS: WrapMode.REPEAT,
				wrapT: WrapMode.REPEAT,
			});
			expect(sampler).toBeDefined();
			expect(mockGL.gl.createSampler).toHaveBeenCalled();
		});
	});

	describe('getDefaultSampler', () => {
		it('returns cached sampler for same filter+wrap combo', () => {
			const s1 = manager.getDefaultSampler(FilterMode.LINEAR, WrapMode.REPEAT);
			const s2 = manager.getDefaultSampler(FilterMode.LINEAR, WrapMode.REPEAT);
			expect(s1).toBe(s2);
		});

		it('returns different sampler for different combo', () => {
			const s1 = manager.getDefaultSampler(FilterMode.LINEAR, WrapMode.REPEAT);
			const s2 = manager.getDefaultSampler(FilterMode.NEAREST, WrapMode.CLAMP_TO_EDGE);
			expect(s1).not.toBe(s2);
		});
	});

	describe('cache management', () => {
		it('getTexture returns null for unknown id', () => {
			expect(manager.getTexture('nonexistent')).toBeNull();
		});

		it('cacheTexture and getTexture roundtrip', () => {
			const tex = manager.createTexture2D(4, 4, TextureFormat.RGBA8 as any);
			manager.cacheTexture('my-key', tex);
			expect(manager.getTexture('my-key')).toBe(tex);
		});

		it('removeCachedTexture removes from cache', () => {
			const tex = manager.createTexture2D(4, 4, TextureFormat.RGBA8 as any);
			manager.cacheTexture('key', tex);
			expect(manager.removeCachedTexture('key')).toBe(true);
			expect(manager.getTexture('key')).toBeNull();
		});

		it('removeCachedTexture returns false for missing key', () => {
			expect(manager.removeCachedTexture('missing')).toBe(false);
		});

		it('clearCache disposes all cached textures', () => {
			const tex = manager.createTexture2D(4, 4, TextureFormat.RGBA8 as any);
			manager.cacheTexture('key', tex);
			manager.clearCache();
			expect(tex.isDisposed).toBe(true);
			expect(manager.getTexture('key')).toBeNull();
		});
	});

	describe('default textures', () => {
		it('getWhiteTexture returns a 1x1 texture', () => {
			const tex = manager.getWhiteTexture();
			expect(tex.width).toBe(1);
			expect(tex.height).toBe(1);
		});

		it('returns same instance on repeated calls', () => {
			const t1 = manager.getWhiteTexture();
			const t2 = manager.getWhiteTexture();
			expect(t1).toBe(t2);
		});

		it('getBlackTexture returns a 1x1 texture', () => {
			const tex = manager.getBlackTexture();
			expect(tex.width).toBe(1);
			expect(tex.height).toBe(1);
		});

		it('getNormalTexture returns a 1x1 texture', () => {
			const tex = manager.getNormalTexture();
			expect(tex.width).toBe(1);
			expect(tex.height).toBe(1);
		});

		it('getCheckerboardTexture returns an 8x8 texture', () => {
			const tex = manager.getCheckerboardTexture();
			expect(tex.width).toBe(8);
			expect(tex.height).toBe(8);
		});
	});

	describe('getStats', () => {
		it('returns initial stats with zero textures', () => {
			const stats = manager.getStats();
			expect(stats.totalTextures).toBe(0);
			expect(stats.totalMemoryUsage).toBe(0);
		});

		it('tracks textures by format', () => {
			manager.createTexture2D(4, 4, TextureFormat.RGBA8 as any);
			manager.createTexture2D(8, 8, TextureFormat.RGBA8 as any);
			const stats = manager.getStats();
			expect(stats.texturesByFormat.get(TextureFormat.RGBA8 as any)).toBe(2);
		});

		it('tracks textures by dimension', () => {
			manager.createTexture2D(4, 4, TextureFormat.RGBA8 as any);
			const stats = manager.getStats();
			expect(stats.texturesByDimension.get(TextureDimension.TEXTURE_2D as any)).toBe(1);
		});

		it('tracks textures by usage', () => {
			manager.createTexture2D(4, 4, TextureFormat.RGBA8 as any);
			const stats = manager.getStats();
			expect(stats.texturesByUsage.get(TextureUsage.STATIC as any)).toBe(1);
		});

		it('calculates cache hit rate', () => {
			const tex = manager.createTexture2D(4, 4, TextureFormat.RGBA8 as any);
			manager.cacheTexture('key', tex);
			manager.getTexture('key');
			manager.getTexture('missing');
			const stats = manager.getStats();
			expect(stats.cacheHitRate).toBe(0.5);
		});

		it('finds largest texture', () => {
			manager.createTexture2D(4, 4, TextureFormat.RGBA8 as any);
			const big = manager.createTexture2D(64, 64, TextureFormat.RGBA8 as any);
			manager.cacheTexture('big', big);
			const stats = manager.getStats();
			expect(stats.largestTexture).toBe(big);
		});
	});

	describe('optimizeMemory', () => {
		it('does nothing when under budget', () => {
			manager.createTexture2D(4, 4, TextureFormat.RGBA8 as any);
			manager.optimizeMemory();
			const stats = manager.getStats();
			expect(stats.totalTextures).toBe(1);
		});
	});

	describe('dispose', () => {
		it('disposes all cached and default textures and clears state', () => {
			const tex = manager.createTexture2D(4, 4, TextureFormat.RGBA8 as any);
			manager.cacheTexture('user-tex', tex);
			manager.getWhiteTexture();
			manager.dispose();
			expect(tex.isDisposed).toBe(true);
			const stats = manager.getStats();
			expect(stats.totalTextures).toBe(0);
		});
	});

	describe('builder', () => {
		it('returns a TextureBuilder', () => {
			const builder = manager.builder();
			expect(builder).toBeDefined();
		});

		it('builds a texture via fluent API', () => {
			const tex = manager
				.builder()
				.dimension(TextureDimension.TEXTURE_2D as any)
				.size(32, 32)
				.format(TextureFormat.RGBA8 as any)
				.usage(TextureUsage.STATIC as any)
				.build();
			expect(tex.width).toBe(32);
			expect(tex.height).toBe(32);
		});
	});
});
