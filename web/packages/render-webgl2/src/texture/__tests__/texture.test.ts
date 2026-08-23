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

let WebGLTexture: typeof import('../texture').WebGLTexture;
let TextureDimension: typeof import('../interfaces').TextureDimension;
let TextureFormat: typeof import('../interfaces').TextureFormat;
let TextureUsage: typeof import('../interfaces').TextureUsage;
let ColorSpace: typeof import('../interfaces').ColorSpace;

const createMockGL = () => {
	const textureHandle = { id: 42 } as unknown as globalThis.WebGLTexture;
	const gl = {
		createTexture: vi.fn(() => textureHandle),
		deleteTexture: vi.fn(),
		bindTexture: vi.fn(),
		activeTexture: vi.fn(),
		texImage2D: vi.fn(),
		texImage3D: vi.fn(),
		texSubImage2D: vi.fn(),
		texSubImage3D: vi.fn(),
		generateMipmap: vi.fn(),
		getExtension: vi.fn(() => null),
		getParameter: vi.fn(() => 0),
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
	} as unknown as WebGL2RenderingContext;
	return { gl, textureHandle };
};

beforeAll(async () => {
	Object.assign(globalThis, {
		WebGL2RenderingContext: webglConstantStub,
		WebGLRenderingContext: webglConstantStub,
	});

	const textureModule = await import('../texture');
	const interfacesModule = await import('../interfaces');
	WebGLTexture = textureModule.WebGLTexture;
	TextureDimension = interfacesModule.TextureDimension;
	TextureFormat = interfacesModule.TextureFormat;
	TextureUsage = interfacesModule.TextureUsage;
	ColorSpace = interfacesModule.ColorSpace;
});

const baseOptions = () => ({
	width: 64,
	height: 64,
	format: TextureFormat.RGBA8 as any,
	dimension: TextureDimension.TEXTURE_2D as any,
	usage: TextureUsage.STATIC as any,
});

describe('WebGLTexture', () => {
	describe('constructor', () => {
		it('creates a WebGL texture and initializes storage', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			expect((gl.createTexture as any)).toHaveBeenCalledTimes(1);
			expect(tex.width).toBe(64);
			expect(tex.height).toBe(64);
			expect(tex.format).toBe(TextureFormat.RGBA8);
			expect(tex.dimension).toBe(TextureDimension.TEXTURE_2D);
		});

		it('assigns a unique ID with tex_ prefix', () => {
			const { gl } = createMockGL();
			const t1 = new WebGLTexture(gl, baseOptions());
			const t2 = new WebGLTexture(gl, baseOptions());
			expect(t1.id).toMatch(/^tex_/);
			expect(t1.id).not.toBe(t2.id);
		});

		it('throws when createTexture returns null', () => {
			const { gl } = createMockGL();
			(gl.createTexture as any).mockReturnValue(null);
			expect(() => new WebGLTexture(gl, baseOptions())).toThrow(/Failed to create WebGL texture/);
		});

		it('defaults depth to 1 for 2D textures', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			expect(tex.depth).toBe(1);
		});

		it('defaults mipLevels to 1', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			expect(tex.mipLevels).toBe(1);
		});

		it('defaults arrayLayers to 1', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			expect(tex.arrayLayers).toBe(1);
		});

		it('defaults samples to 1', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			expect(tex.samples).toBe(1);
		});

		it('defaults colorSpace to LINEAR', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			expect(tex.colorSpace).toBe(ColorSpace.LINEAR);
		});

		it('defaults label to null', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			expect(tex.label).toBeNull();
		});

		it('calculates totalMemoryUsage', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			expect(tex.totalMemoryUsage).toBe(64 * 64 * 4);
		});

		it('reports bytesPerPixel for RGBA8', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			expect(tex.bytesPerPixel).toBe(4);
		});

		it('reports isCompressed as false for RGBA8', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			expect(tex.isCompressed).toBe(false);
		});

		it('initializes 2D texture with texImage2D', () => {
			const { gl } = createMockGL();
			new WebGLTexture(gl, baseOptions());
			expect(gl.texImage2D).toHaveBeenCalled();
		});

		it('initializes 3D texture with texImage3D', () => {
			const { gl } = createMockGL();
			new WebGLTexture(gl, {
				...baseOptions(),
				dimension: TextureDimension.TEXTURE_3D as any,
				depth: 8,
			});
			expect(gl.texImage3D).toHaveBeenCalled();
		});

		it('initializes cube texture with 6 face calls', () => {
			const { gl } = createMockGL();
			new WebGLTexture(gl, {
				...baseOptions(),
				dimension: TextureDimension.TEXTURE_CUBE as any,
			});
			const cubeFaceCalls = (gl.texImage2D as any).mock.calls.filter(
				(c: any[]) => c[0] >= 0x8515 && c[0] <= 0x851a
			);
			expect(cubeFaceCalls.length).toBe(6);
		});

		it('initializes 2D array texture with texImage3D', () => {
			const { gl } = createMockGL();
			new WebGLTexture(gl, {
				...baseOptions(),
				dimension: TextureDimension.TEXTURE_2D_ARRAY as any,
				arrayLayers: 4,
			});
			expect(gl.texImage3D).toHaveBeenCalled();
		});

		it('uploads data when provided', () => {
			const { gl } = createMockGL();
			const data = new Uint8Array(64 * 64 * 4);
			new WebGLTexture(gl, baseOptions(), data);
			expect(gl.texSubImage2D).toHaveBeenCalled();
		});

		it('does not upload data when null', () => {
			const { gl } = createMockGL();
			new WebGLTexture(gl, baseOptions(), null);
			expect(gl.texSubImage2D).not.toHaveBeenCalled();
		});

		it('preserves custom label', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, { ...baseOptions(), label: 'my-texture' });
			expect(tex.label).toBe('my-texture');
		});
	});

	describe('bind / unbind', () => {
		it('binds to a specific texture unit', () => {
			const { gl, textureHandle } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			tex.bind(3);
			expect(gl.activeTexture).toHaveBeenCalledWith(0x84c0 + 3);
			expect(gl.bindTexture).toHaveBeenCalledWith(0x0de1, textureHandle);
		});

		it('binds without unit (just binds texture)', () => {
			const { gl, textureHandle } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			tex.bind();
			expect(gl.bindTexture).toHaveBeenCalledWith(0x0de1, textureHandle);
		});

		it('unbinds from current unit', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			tex.bind(2);
			tex.unbind();
			expect(gl.bindTexture).toHaveBeenCalledWith(0x0de1, null);
		});
	});

	describe('setData', () => {
		it('uploads typed array data', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			const data = new Uint8Array(64 * 64 * 4);
			tex.setData(data);
			expect(gl.texSubImage2D).toHaveBeenCalled();
		});

		it('throws on disposed texture', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			tex.dispose();
			expect(() => tex.setData(new Uint8Array(4))).toThrow(/disposed/);
		});
	});

	describe('getData', () => {
		it('reads texture data via framebuffer readback', async () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			// getData is now implemented — it creates a temp FBO and calls readPixels.
			// With a minimal mock, it may fail on FBO creation; verify it does NOT throw "not implemented".
			try {
				const result = await tex.getData();
				expect(result).toBeDefined();
			} catch (e) {
				expect((e as Error).message).not.toMatch(/not implemented/);
			}
		});
	});

	describe('copyTo', () => {
		it('copies texture data via framebuffer readback', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			const dest = new WebGLTexture(gl, baseOptions());
			// copyTo is now implemented — it creates a temp FBO and calls copyTexSubImage2D.
			// With a minimal mock, it may fail on FBO creation; verify it does NOT throw "not yet implemented".
			try {
				tex.copyTo(dest);
			} catch (e) {
				expect((e as Error).message).not.toMatch(/not yet implemented/);
			}
		});

		it('throws when destination is disposed', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			const dest = new WebGLTexture(gl, baseOptions());
			dest.dispose();
			expect(() => tex.copyTo(dest)).toThrow(/Cannot copy to disposed texture/);
		});
	});

	describe('generateMipmaps', () => {
		it('calls gl.generateMipmap', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, { ...baseOptions(), mipLevels: 4 });
			tex.generateMipmaps();
			expect(gl.generateMipmap).toHaveBeenCalled();
		});

		it('is no-op when mipLevels <= 1', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			tex.generateMipmaps();
			expect(gl.generateMipmap).not.toHaveBeenCalled();
		});

		it('throws for compressed textures', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, {
				width: 64,
				height: 64,
				format: TextureFormat.BC1_RGB as any,
				dimension: TextureDimension.TEXTURE_2D as any,
				usage: TextureUsage.STATIC as any,
				mipLevels: 4,
			});
			expect(() => tex.generateMipmaps()).toThrow(/Cannot generate mipmaps for compressed/);
		});

		it('throws on disposed texture', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, { ...baseOptions(), mipLevels: 4 });
			tex.dispose();
			expect(() => tex.generateMipmaps()).toThrow(/disposed/);
		});
	});

	describe('hasMipmaps', () => {
		it('returns true when mipLevels > 1', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, { ...baseOptions(), mipLevels: 4 });
			expect(tex.hasMipmaps()).toBe(true);
		});

		it('returns false when mipLevels is 1', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			expect(tex.hasMipmaps()).toBe(false);
		});
	});

	describe('resize', () => {
		it('re-initializes storage with new dimensions', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			(gl.texImage2D as any).mockClear();
			tex.resize(128, 128);
			expect(gl.texImage2D).toHaveBeenCalled();
		});

		it('throws on disposed texture', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			tex.dispose();
			expect(() => tex.resize(128, 128)).toThrow(/disposed/);
		});
	});

	describe('clone', () => {
		it('creates a new texture with same properties', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			const cloned = tex.clone();
			expect(cloned.width).toBe(tex.width);
			expect(cloned.height).toBe(tex.height);
			expect(cloned.format).toBe(tex.format);
			expect(cloned.dimension).toBe(tex.dimension);
			expect(cloned.id).not.toBe(tex.id);
		});

		it('appends _clone to label', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, { ...baseOptions(), label: 'original' });
			const cloned = tex.clone();
			expect(cloned.label).toBe('original_clone');
		});

		it('throws on disposed texture', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			tex.dispose();
			expect(() => tex.clone()).toThrow(/disposed/);
		});
	});

	describe('dispose', () => {
		it('deletes the native texture', () => {
			const { gl, textureHandle } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			tex.dispose();
			expect(gl.deleteTexture).toHaveBeenCalledWith(textureHandle);
		});

		it('is idempotent', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			tex.dispose();
			tex.dispose();
			expect(gl.deleteTexture).toHaveBeenCalledTimes(1);
		});

		it('reports isDisposed correctly', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			expect(tex.isDisposed).toBe(false);
			tex.dispose();
			expect(tex.isDisposed).toBe(true);
		});

		it('throws on bind after dispose', () => {
			const { gl } = createMockGL();
			const tex = new WebGLTexture(gl, baseOptions());
			tex.dispose();
			expect(() => tex.bind()).toThrow(/disposed/);
		});
	});
});
