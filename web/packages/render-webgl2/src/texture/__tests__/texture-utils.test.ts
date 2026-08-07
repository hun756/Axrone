import { beforeAll, describe, expect, it } from 'vitest';
import { Vec4 } from '@axrone/numeric';

const webglConstantStub = new Proxy(
	{
		R8: 0x8229,
		RG8: 0x822b,
		RGB8: 0x8051,
		RGBA8: 0x8058,
		R16F: 0x822d,
		RG16F: 0x822f,
		RGB16F: 0x881b,
		RGBA16F: 0x881a,
		R32F: 0x822e,
		RG32F: 0x8230,
		RGB32F: 0x8815,
		RGBA32F: 0x8814,
		DEPTH_COMPONENT16: 0x81a5,
		DEPTH_COMPONENT24: 0x81a6,
		DEPTH_COMPONENT32F: 0x8cac,
		DEPTH24_STENCIL8: 0x88f0,
		DEPTH32F_STENCIL8: 0x8cad,
		RED: 0x1903,
		RG: 0x8227,
		RGB: 0x1907,
		RGBA: 0x1908,
		UNSIGNED_BYTE: 0x1401,
		HALF_FLOAT: 0x140b,
		FLOAT: 0x1406,
		DEPTH_COMPONENT: 0x1902,
		DEPTH_STENCIL: 0x84f9,
		UNSIGNED_SHORT: 0x1403,
		UNSIGNED_INT: 0x1405,
		UNSIGNED_INT_24_8: 0x84fa,
		FLOAT_32_UNSIGNED_INT_24_8_REV: 0x8dad,
		SRGB8: 0x8c41,
		SRGB8_ALPHA8: 0x8c43,
		TEXTURE_2D: 0x0de1,
		TEXTURE_3D: 0x806f,
		TEXTURE_CUBE_MAP: 0x8513,
		TEXTURE_2D_ARRAY: 0x8c1a,
		NEAREST: 0x2600,
		LINEAR: 0x2601,
		NEAREST_MIPMAP_NEAREST: 0x2700,
		LINEAR_MIPMAP_NEAREST: 0x2701,
		NEAREST_MIPMAP_LINEAR: 0x2702,
		LINEAR_MIPMAP_LINEAR: 0x2703,
		REPEAT: 0x2901,
		CLAMP_TO_EDGE: 0x812f,
		MIRRORED_REPEAT: 0x8370,
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

let TextureFormatInfo: typeof import('../utils').TextureFormatInfo;
let TextureWebGLConstants: typeof import('../utils').TextureWebGLConstants;
let TextureUtils: typeof import('../utils').TextureUtils;
let TextureValidation: typeof import('../utils').TextureValidation;

beforeAll(async () => {
	Object.assign(globalThis, {
		WebGL2RenderingContext: webglConstantStub,
		WebGLRenderingContext: webglConstantStub,
	});

	const mod = await import('../utils');
	TextureFormatInfo = mod.TextureFormatInfo;
	TextureWebGLConstants = mod.TextureWebGLConstants;
	TextureUtils = mod.TextureUtils;
	TextureValidation = mod.TextureValidation;
});

const TextureFormat = {
	R8: 'R8',
	RG8: 'RG8',
	RGB8: 'RGB8',
	RGBA8: 'RGBA8',
	R16F: 'R16F',
	RG16F: 'RG16F',
	RGB16F: 'RGB16F',
	RGBA16F: 'RGBA16F',
	R32F: 'R32F',
	RG32F: 'RG32F',
	RGB32F: 'RGB32F',
	RGBA32F: 'RGBA32F',
	DEPTH_COMPONENT16: 'DEPTH_COMPONENT16',
	DEPTH_COMPONENT24: 'DEPTH_COMPONENT24',
	DEPTH_COMPONENT32F: 'DEPTH_COMPONENT32F',
	DEPTH24_STENCIL8: 'DEPTH24_STENCIL8',
	DEPTH32F_STENCIL8: 'DEPTH32F_STENCIL8',
	BC1_RGB: 'BC1_RGB',
	BC1_RGBA: 'BC1_RGBA',
	BC2_RGBA: 'BC2_RGBA',
	BC3_RGBA: 'BC3_RGBA',
	BC4_R: 'BC4_R',
	BC5_RG: 'BC5_RG',
	BC6H_RGB_UF16: 'BC6H_RGB_UF16',
	BC6H_RGB_SF16: 'BC6H_RGB_SF16',
	BC7_RGBA: 'BC7_RGBA',
	ASTC_4x4: 'ASTC_4x4',
	ASTC_12x12: 'ASTC_12x12',
} as Record<string, string>;

const TextureDimension = {
	TEXTURE_1D: '1D',
	TEXTURE_2D: '2D',
	TEXTURE_3D: '3D',
	TEXTURE_CUBE: 'CUBE',
	TEXTURE_2D_ARRAY: '2D_ARRAY',
	TEXTURE_CUBE_ARRAY: 'CUBE_ARRAY',
} as Record<string, string>;

const TextureUsage = {
	STATIC: 'STATIC',
	DYNAMIC: 'DYNAMIC',
	STREAM: 'STREAM',
	RENDER_TARGET: 'RENDER_TARGET',
	DEPTH_BUFFER: 'DEPTH_BUFFER',
	COMPUTE: 'COMPUTE',
} as Record<string, string>;

const FilterMode = {
	NEAREST: 'NEAREST',
	LINEAR: 'LINEAR',
	NEAREST_MIPMAP_NEAREST: 'NEAREST_MIPMAP_NEAREST',
	LINEAR_MIPMAP_NEAREST: 'LINEAR_MIPMAP_NEAREST',
	NEAREST_MIPMAP_LINEAR: 'NEAREST_MIPMAP_LINEAR',
	LINEAR_MIPMAP_LINEAR: 'LINEAR_MIPMAP_LINEAR',
} as Record<string, string>;

const WrapMode = {
	REPEAT: 'REPEAT',
	CLAMP_TO_EDGE: 'CLAMP_TO_EDGE',
	CLAMP_TO_BORDER: 'CLAMP_TO_BORDER',
	MIRRORED_REPEAT: 'MIRRORED_REPEAT',
} as Record<string, string>;

const ColorSpace = {
	LINEAR: 'LINEAR',
	SRGB: 'SRGB',
} as Record<string, string>;

describe('TextureFormatInfo', () => {
	describe('getFormatInfo — uncompressed formats', () => {
		it('returns correct properties for R8', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.R8 as any);
			expect(info.bytesPerPixel).toBe(1);
			expect(info.channels).toBe(1);
			expect(info.compressed).toBe(false);
			expect(info.floatingPoint).toBe(false);
			expect(info.depth).toBe(false);
			expect(info.stencil).toBe(false);
			expect(info.srgb).toBe(false);
		});

		it('returns correct properties for RG8', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.RG8 as any);
			expect(info.bytesPerPixel).toBe(2);
			expect(info.channels).toBe(2);
			expect(info.compressed).toBe(false);
		});

		it('returns correct properties for RGB8', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.RGB8 as any);
			expect(info.bytesPerPixel).toBe(3);
			expect(info.channels).toBe(3);
			expect(info.compressed).toBe(false);
		});

		it('returns correct properties for RGBA8', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.RGBA8 as any);
			expect(info.bytesPerPixel).toBe(4);
			expect(info.channels).toBe(4);
			expect(info.compressed).toBe(false);
			expect(info.floatingPoint).toBe(false);
		});

		it('returns correct properties for R16F', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.R16F as any);
			expect(info.bytesPerPixel).toBe(2);
			expect(info.channels).toBe(1);
			expect(info.floatingPoint).toBe(true);
			expect(info.compressed).toBe(false);
		});

		it('returns correct properties for RGBA16F', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.RGBA16F as any);
			expect(info.bytesPerPixel).toBe(8);
			expect(info.channels).toBe(4);
			expect(info.floatingPoint).toBe(true);
		});

		it('returns correct properties for R32F', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.R32F as any);
			expect(info.bytesPerPixel).toBe(4);
			expect(info.floatingPoint).toBe(true);
		});

		it('returns correct properties for RGBA32F', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.RGBA32F as any);
			expect(info.bytesPerPixel).toBe(16);
			expect(info.channels).toBe(4);
			expect(info.floatingPoint).toBe(true);
		});
	});

	describe('getFormatInfo — depth formats', () => {
		it('identifies DEPTH_COMPONENT16 as depth', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.DEPTH_COMPONENT16 as any);
			expect(info.depth).toBe(true);
			expect(info.stencil).toBe(false);
			expect(info.bytesPerPixel).toBe(2);
		});

		it('identifies DEPTH_COMPONENT24 as depth', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.DEPTH_COMPONENT24 as any);
			expect(info.depth).toBe(true);
			expect(info.bytesPerPixel).toBe(4);
		});

		it('identifies DEPTH_COMPONENT32F as depth + floating point', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.DEPTH_COMPONENT32F as any);
			expect(info.depth).toBe(true);
			expect(info.floatingPoint).toBe(true);
		});

		it('identifies DEPTH24_STENCIL8 as depth + stencil', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.DEPTH24_STENCIL8 as any);
			expect(info.depth).toBe(true);
			expect(info.stencil).toBe(true);
			expect(info.channels).toBe(2);
			expect(info.bytesPerPixel).toBe(4);
		});

		it('identifies DEPTH32F_STENCIL8 as depth + stencil + float', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.DEPTH32F_STENCIL8 as any);
			expect(info.depth).toBe(true);
			expect(info.stencil).toBe(true);
			expect(info.floatingPoint).toBe(true);
			expect(info.bytesPerPixel).toBe(8);
		});
	});

	describe('getFormatInfo — compressed formats', () => {
		it('identifies BC1_RGB as compressed with correct block size', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.BC1_RGB as any);
			expect(info.compressed).toBe(true);
			expect(info.blockSize).toBe(8);
			expect(info.blockWidth).toBe(4);
			expect(info.blockHeight).toBe(4);
			expect(info.channels).toBe(3);
		});

		it('identifies BC3_RGBA as compressed with 16-byte blocks', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.BC3_RGBA as any);
			expect(info.compressed).toBe(true);
			expect(info.blockSize).toBe(16);
			expect(info.channels).toBe(4);
		});

		it('identifies BC7_RGBA as compressed', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.BC7_RGBA as any);
			expect(info.compressed).toBe(true);
			expect(info.blockSize).toBe(16);
		});

		it('identifies ASTC_4x4 as compressed', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.ASTC_4x4 as any);
			expect(info.compressed).toBe(true);
			expect(info.blockSize).toBe(16);
			expect(info.blockWidth).toBe(4);
			expect(info.blockHeight).toBe(4);
		});

		it('identifies ASTC_12x12 with correct block dimensions', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.ASTC_12x12 as any);
			expect(info.compressed).toBe(true);
			expect(info.blockWidth).toBe(12);
			expect(info.blockHeight).toBe(12);
		});

		it('identifies BC6H_RGB_UF16 as compressed + floating point', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.BC6H_RGB_UF16 as any);
			expect(info.compressed).toBe(true);
			expect(info.floatingPoint).toBe(true);
		});
	});

	describe('getFormatInfo — sRGB color space', () => {
		it('returns sRGB internal format for RGB8 + SRGB', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.RGB8 as any, ColorSpace.SRGB as any);
			expect(info.srgb).toBe(true);
			expect(info.internalFormat).toBe(webglConstantStub.SRGB8);
		});

		it('returns sRGB internal format for RGBA8 + SRGB', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.RGBA8 as any, ColorSpace.SRGB as any);
			expect(info.srgb).toBe(true);
			expect(info.internalFormat).toBe(webglConstantStub.SRGB8_ALPHA8);
		});

		it('does not apply sRGB override for float formats', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.RGBA16F as any, ColorSpace.SRGB as any);
			expect(info.srgb).toBe(false);
		});

		it('does not apply sRGB override for depth formats', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.DEPTH_COMPONENT24 as any, ColorSpace.SRGB as any);
			expect(info.srgb).toBe(false);
		});

		it('does not apply sRGB override for compressed formats', () => {
			const info = TextureFormatInfo.getFormatInfo(TextureFormat.BC1_RGB as any, ColorSpace.SRGB as any);
			expect(info.srgb).toBe(false);
		});
	});

	describe('getFormatInfo — error handling', () => {
		it('throws TextureError for unsupported format', () => {
			expect(() => TextureFormatInfo.getFormatInfo('NONEXISTENT' as any)).toThrow(
				/Unsupported texture format/
			);
		});
	});

	describe('convenience query methods', () => {
		it('getBytesPerPixel returns correct values', () => {
			expect(TextureFormatInfo.getBytesPerPixel(TextureFormat.RGBA8 as any)).toBe(4);
			expect(TextureFormatInfo.getBytesPerPixel(TextureFormat.R8 as any)).toBe(1);
			expect(TextureFormatInfo.getBytesPerPixel(TextureFormat.RGB16F as any)).toBe(6);
		});

		it('getChannelCount returns correct values', () => {
			expect(TextureFormatInfo.getChannelCount(TextureFormat.R8 as any)).toBe(1);
			expect(TextureFormatInfo.getChannelCount(TextureFormat.RGBA8 as any)).toBe(4);
			expect(TextureFormatInfo.getChannelCount(TextureFormat.DEPTH24_STENCIL8 as any)).toBe(2);
		});

		it('isCompressed identifies compressed formats', () => {
			expect(TextureFormatInfo.isCompressed(TextureFormat.RGBA8 as any)).toBe(false);
			expect(TextureFormatInfo.isCompressed(TextureFormat.BC1_RGB as any)).toBe(true);
			expect(TextureFormatInfo.isCompressed(TextureFormat.ASTC_4x4 as any)).toBe(true);
		});

		it('isFloatingPoint identifies float formats', () => {
			expect(TextureFormatInfo.isFloatingPoint(TextureFormat.RGBA8 as any)).toBe(false);
			expect(TextureFormatInfo.isFloatingPoint(TextureFormat.RGBA16F as any)).toBe(true);
			expect(TextureFormatInfo.isFloatingPoint(TextureFormat.R32F as any)).toBe(true);
		});

		it('isDepth identifies depth formats', () => {
			expect(TextureFormatInfo.isDepth(TextureFormat.RGBA8 as any)).toBe(false);
			expect(TextureFormatInfo.isDepth(TextureFormat.DEPTH_COMPONENT24 as any)).toBe(true);
			expect(TextureFormatInfo.isDepth(TextureFormat.DEPTH24_STENCIL8 as any)).toBe(true);
		});

		it('hasStencil identifies stencil formats', () => {
			expect(TextureFormatInfo.hasStencil(TextureFormat.RGBA8 as any)).toBe(false);
			expect(TextureFormatInfo.hasStencil(TextureFormat.DEPTH24_STENCIL8 as any)).toBe(true);
			expect(TextureFormatInfo.hasStencil(TextureFormat.DEPTH32F_STENCIL8 as any)).toBe(true);
		});

		it('getSupportedFormats returns all registered formats', () => {
			const formats = TextureFormatInfo.getSupportedFormats();
			expect(formats.length).toBeGreaterThan(15);
		});

		it('getCompressedFormats returns only compressed formats', () => {
			const compressed = TextureFormatInfo.getCompressedFormats();
			expect(compressed.length).toBeGreaterThan(0);
			for (const fmt of compressed) {
				expect(TextureFormatInfo.isCompressed(fmt)).toBe(true);
			}
		});
	});
});

describe('TextureWebGLConstants', () => {
	describe('getDimensionConstant', () => {
		it('returns correct constants for each dimension', () => {
			expect(TextureWebGLConstants.getDimensionConstant(TextureDimension.TEXTURE_2D as any)).toBe(
				webglConstantStub.TEXTURE_2D
			);
			expect(TextureWebGLConstants.getDimensionConstant(TextureDimension.TEXTURE_3D as any)).toBe(
				webglConstantStub.TEXTURE_3D
			);
			expect(TextureWebGLConstants.getDimensionConstant(TextureDimension.TEXTURE_CUBE as any)).toBe(
				webglConstantStub.TEXTURE_CUBE_MAP
			);
			expect(TextureWebGLConstants.getDimensionConstant(TextureDimension.TEXTURE_2D_ARRAY as any)).toBe(
				webglConstantStub.TEXTURE_2D_ARRAY
			);
		});

		it('throws for unsupported dimension', () => {
			expect(() => TextureWebGLConstants.getDimensionConstant('INVALID' as any)).toThrow(
				/Unsupported texture dimension/
			);
		});
	});

	describe('getFilterConstant', () => {
		it('returns correct constants for all filter modes', () => {
			expect(TextureWebGLConstants.getFilterConstant(FilterMode.NEAREST as any)).toBe(
				webglConstantStub.NEAREST
			);
			expect(TextureWebGLConstants.getFilterConstant(FilterMode.LINEAR as any)).toBe(
				webglConstantStub.LINEAR
			);
			expect(TextureWebGLConstants.getFilterConstant(FilterMode.LINEAR_MIPMAP_LINEAR as any)).toBe(
				webglConstantStub.LINEAR_MIPMAP_LINEAR
			);
		});

		it('throws for unsupported filter mode', () => {
			expect(() => TextureWebGLConstants.getFilterConstant('INVALID' as any)).toThrow(
				/Unsupported filter mode/
			);
		});
	});

	describe('getWrapConstant', () => {
		it('returns correct constants for all wrap modes', () => {
			expect(TextureWebGLConstants.getWrapConstant(WrapMode.REPEAT as any)).toBe(
				webglConstantStub.REPEAT
			);
			expect(TextureWebGLConstants.getWrapConstant(WrapMode.CLAMP_TO_EDGE as any)).toBe(
				webglConstantStub.CLAMP_TO_EDGE
			);
			expect(TextureWebGLConstants.getWrapConstant(WrapMode.MIRRORED_REPEAT as any)).toBe(
				webglConstantStub.MIRRORED_REPEAT
			);
		});

		it('returns CLAMP_TO_BORDER constant (0x812d)', () => {
			expect(TextureWebGLConstants.getWrapConstant(WrapMode.CLAMP_TO_BORDER as any)).toBe(0x812d);
		});

		it('throws for unsupported wrap mode', () => {
			expect(() => TextureWebGLConstants.getWrapConstant('INVALID' as any)).toThrow(
				/Unsupported wrap mode/
			);
		});
	});
});

describe('TextureUtils', () => {
	describe('calculateMemoryUsage', () => {
		it('calculates memory for a simple RGBA8 texture', () => {
			const usage = TextureUtils.calculateMemoryUsage(256, 256, 1, TextureFormat.RGBA8 as any, 1);
			expect(usage).toBe(256 * 256 * 4);
		});

		it('calculates memory for RGB8 texture', () => {
			const usage = TextureUtils.calculateMemoryUsage(128, 128, 1, TextureFormat.RGB8 as any, 1);
			expect(usage).toBe(128 * 128 * 3);
		});

		it('calculates memory with multiple mip levels', () => {
			const usage = TextureUtils.calculateMemoryUsage(256, 256, 1, TextureFormat.RGBA8 as any, 2);
			const mip0 = 256 * 256 * 4;
			const mip1 = 128 * 128 * 4;
			expect(usage).toBe(mip0 + mip1);
		});

		it('calculates memory for 3D texture', () => {
			const usage = TextureUtils.calculateMemoryUsage(64, 64, 64, TextureFormat.R8 as any, 1);
			expect(usage).toBe(64 * 64 * 64 * 1);
		});

		it('calculates memory for compressed BC1 format', () => {
			const usage = TextureUtils.calculateMemoryUsage(256, 256, 1, TextureFormat.BC1_RGB as any, 1);
			const blocksWide = Math.ceil(256 / 4);
			const blocksHigh = Math.ceil(256 / 4);
			expect(usage).toBe(blocksWide * blocksHigh * 8);
		});

		it('accumulates memory across all mip levels', () => {
			const usage = TextureUtils.calculateMemoryUsage(256, 256, 1, TextureFormat.RGBA8 as any, 20);
			const mip0 = 256 * 256 * 4;
			expect(usage).toBeGreaterThan(mip0);
			expect(usage).toBeLessThan(mip0 * 2);
		});
	});

	describe('calculateMaxMipLevels', () => {
		it('returns 1 for 1x1 texture', () => {
			expect(TextureUtils.calculateMaxMipLevels(1, 1)).toBe(1);
		});

		it('returns correct levels for power-of-two textures', () => {
			expect(TextureUtils.calculateMaxMipLevels(256, 256)).toBe(9);
			expect(TextureUtils.calculateMaxMipLevels(512, 512)).toBe(10);
			expect(TextureUtils.calculateMaxMipLevels(1024, 1024)).toBe(11);
		});

		it('handles non-power-of-two dimensions', () => {
			expect(TextureUtils.calculateMaxMipLevels(100, 100)).toBe(7);
		});

		it('uses the largest dimension for 3D textures', () => {
			expect(TextureUtils.calculateMaxMipLevels(64, 64, 256)).toBe(9);
		});
	});

	describe('getMipDimensions', () => {
		it('returns original dimensions at mip 0', () => {
			const dims = TextureUtils.getMipDimensions(256, 128, 1, 0);
			expect(dims).toEqual({ width: 256, height: 128, depth: 1 });
		});

		it('halves dimensions at each mip level', () => {
			const dims = TextureUtils.getMipDimensions(256, 256, 1, 1);
			expect(dims).toEqual({ width: 128, height: 128, depth: 1 });
		});

		it('clamps minimum dimension to 1', () => {
			const dims = TextureUtils.getMipDimensions(256, 256, 1, 10);
			expect(dims.width).toBe(1);
			expect(dims.height).toBe(1);
			expect(dims.depth).toBe(1);
		});

		it('correctly reduces depth for 3D textures', () => {
			const dims = TextureUtils.getMipDimensions(64, 64, 64, 2);
			expect(dims).toEqual({ width: 16, height: 16, depth: 16 });
		});
	});

	describe('validateDimensions', () => {
		it('accepts valid 2D dimensions', () => {
			expect(() =>
				TextureUtils.validateDimensions(256, 256, 1, TextureDimension.TEXTURE_2D as any)
			).not.toThrow();
		});

		it('rejects zero width', () => {
			expect(() =>
				TextureUtils.validateDimensions(0, 256, 1, TextureDimension.TEXTURE_2D as any)
			).toThrow(/dimensions must be positive/);
		});

		it('rejects negative height', () => {
			expect(() =>
				TextureUtils.validateDimensions(256, -1, 1, TextureDimension.TEXTURE_2D as any)
			).toThrow(/dimensions must be positive/);
		});

		it('requires equal width and height for cube textures', () => {
			expect(() =>
				TextureUtils.validateDimensions(256, 128, 1, TextureDimension.TEXTURE_CUBE as any)
			).toThrow(/Cube textures must have equal width and height/);
		});

		it('accepts equal dimensions for cube textures', () => {
			expect(() =>
				TextureUtils.validateDimensions(256, 256, 1, TextureDimension.TEXTURE_CUBE as any)
			).not.toThrow();
		});

		it('requires height=1 and depth=1 for 1D textures', () => {
			expect(() =>
				TextureUtils.validateDimensions(256, 2, 1, TextureDimension.TEXTURE_1D as any)
			).toThrow(/1D textures/);
		});
	});

	describe('isFormatCompatible', () => {
		it('returns true for standard format + 2D', () => {
			expect(TextureUtils.isFormatCompatible(TextureFormat.RGBA8 as any, TextureDimension.TEXTURE_2D as any)).toBe(true);
		});

		it('returns false for depth format + 3D', () => {
			expect(TextureUtils.isFormatCompatible(TextureFormat.DEPTH_COMPONENT24 as any, TextureDimension.TEXTURE_3D as any)).toBe(false);
		});
	});

	describe('generateTextureId', () => {
		it('generates unique IDs', () => {
			const id1 = TextureUtils.generateTextureId();
			const id2 = TextureUtils.generateTextureId();
			expect(id1).not.toBe(id2);
		});

		it('starts with tex_ prefix', () => {
			expect(TextureUtils.generateTextureId()).toMatch(/^tex_/);
		});
	});

	describe('calculateTextureHash', () => {
		it('produces same hash for same options', () => {
			const options = {
				width: 256,
				height: 256,
				format: TextureFormat.RGBA8 as any,
				dimension: TextureDimension.TEXTURE_2D as any,
				usage: TextureUsage.STATIC as any,
			};
			const hash1 = TextureUtils.calculateTextureHash(options);
			const hash2 = TextureUtils.calculateTextureHash(options);
			expect(hash1).toBe(hash2);
		});

		it('produces different hash for different dimensions', () => {
			const base = {
				width: 256,
				height: 256,
				format: TextureFormat.RGBA8 as any,
				dimension: TextureDimension.TEXTURE_2D as any,
				usage: TextureUsage.STATIC as any,
			};
			const modified = { ...base, width: 512 };
			expect(TextureUtils.calculateTextureHash(base)).not.toBe(TextureUtils.calculateTextureHash(modified));
		});
	});

	describe('getDefaultSamplerOptions', () => {
		it('returns trilinear + anisotropy for STATIC usage', () => {
			const opts = TextureUtils.getDefaultSamplerOptions(TextureUsage.STATIC as any);
			expect(opts.minFilter).toBe(FilterMode.LINEAR_MIPMAP_LINEAR);
			expect(opts.magFilter).toBe(FilterMode.LINEAR);
			expect(opts.wrapS).toBe(WrapMode.REPEAT);
			expect(opts.maxAnisotropy).toBe(16);
		});

		it('returns linear + clamp for RENDER_TARGET usage', () => {
			const opts = TextureUtils.getDefaultSamplerOptions(TextureUsage.RENDER_TARGET as any);
			expect(opts.minFilter).toBe(FilterMode.LINEAR);
			expect(opts.wrapS).toBe(WrapMode.CLAMP_TO_EDGE);
			expect(opts.wrapT).toBe(WrapMode.CLAMP_TO_EDGE);
		});

		it('returns comparison sampler for DEPTH_BUFFER usage', () => {
			const opts = TextureUtils.getDefaultSamplerOptions(TextureUsage.DEPTH_BUFFER as any);
			expect(opts.minFilter).toBe(FilterMode.NEAREST);
			expect(opts.compareMode).toBe('COMPARE_REF_TO_TEXTURE');
			expect(opts.compareFunc).toBe('LEQUAL');
		});

		it('returns linear + repeat for DYNAMIC usage (default branch)', () => {
			const opts = TextureUtils.getDefaultSamplerOptions(TextureUsage.DYNAMIC as any);
			expect(opts.minFilter).toBe(FilterMode.LINEAR);
			expect(opts.wrapS).toBe(WrapMode.REPEAT);
		});
	});

	describe('colorToVec4', () => {
		it('parses 6-digit hex color', () => {
			const color = TextureUtils.colorToVec4('#ff0000');
			expect(color.x).toBeCloseTo(1, 3);
			expect(color.y).toBeCloseTo(0, 3);
			expect(color.z).toBeCloseTo(0, 3);
			expect(color.w).toBeCloseTo(1, 3);
		});

		it('parses 8-digit hex color with alpha', () => {
			const color = TextureUtils.colorToVec4('#ff000080');
			expect(color.x).toBeCloseTo(1, 3);
			expect(color.w).toBeCloseTo(128 / 255, 3);
		});

		it('returns white for non-hex input', () => {
			const color = TextureUtils.colorToVec4('red');
			expect(color).toBeInstanceOf(Vec4);
			expect(color.x).toBe(1);
			expect(color.y).toBe(1);
			expect(color.z).toBe(1);
			expect(color.w).toBe(1);
		});
	});

	describe('getOptimalFormat', () => {
		it('returns DEPTH_COMPONENT24 for depth buffer usage', () => {
			const mockGL = { getExtension: () => null } as any;
			const format = TextureUtils.getOptimalFormat(mockGL, TextureUsage.DEPTH_BUFFER as any);
			expect(format).toBe(TextureFormat.DEPTH_COMPONENT24);
		});

		it('returns RGBA16F when preferFloat + hasAlpha', () => {
			const mockGL = { getExtension: () => null } as any;
			const format = TextureUtils.getOptimalFormat(mockGL, TextureUsage.STATIC as any, true, true);
			expect(format).toBe(TextureFormat.RGBA16F);
		});

		it('returns RGB16F when preferFloat + no alpha', () => {
			const mockGL = { getExtension: () => null } as any;
			const format = TextureUtils.getOptimalFormat(mockGL, TextureUsage.STATIC as any, false, true);
			expect(format).toBe(TextureFormat.RGB16F);
		});

		it('returns RGBA8 when hasAlpha and no float preference', () => {
			const mockGL = { getExtension: () => null } as any;
			const format = TextureUtils.getOptimalFormat(mockGL, TextureUsage.STATIC as any, true, false);
			expect(format).toBe(TextureFormat.RGBA8);
		});

		it('returns RGB8 when no alpha and no float preference', () => {
			const mockGL = { getExtension: () => null } as any;
			const format = TextureUtils.getOptimalFormat(mockGL, TextureUsage.STATIC as any, false, false);
			expect(format).toBe(TextureFormat.RGB8);
		});
	});
});

describe('TextureValidation', () => {
	describe('validateCreateOptions', () => {
		it('accepts valid create options', () => {
			expect(() =>
				TextureValidation.validateCreateOptions({
					width: 256,
					height: 256,
					format: TextureFormat.RGBA8 as any,
					dimension: TextureDimension.TEXTURE_2D as any,
					usage: TextureUsage.STATIC as any,
				})
			).not.toThrow();
		});

		it('rejects zero width', () => {
			expect(() =>
				TextureValidation.validateCreateOptions({
					width: 0,
					height: 256,
					format: TextureFormat.RGBA8 as any,
					dimension: TextureDimension.TEXTURE_2D as any,
					usage: TextureUsage.STATIC as any,
				})
			).toThrow();
		});

		it('rejects too many mip levels', () => {
			expect(() =>
				TextureValidation.validateCreateOptions({
					width: 4,
					height: 4,
					format: TextureFormat.RGBA8 as any,
					dimension: TextureDimension.TEXTURE_2D as any,
					usage: TextureUsage.STATIC as any,
					mipLevels: 10,
				})
			).toThrow(/Too many mip levels/);
		});

		it('rejects array layers < 1', () => {
			expect(() =>
				TextureValidation.validateCreateOptions({
					width: 64,
					height: 64,
					format: TextureFormat.RGBA8 as any,
					dimension: TextureDimension.TEXTURE_2D_ARRAY as any,
					usage: TextureUsage.STATIC as any,
					arrayLayers: 0,
				})
			).toThrow(/Array layers must be at least 1/);
		});

		it('rejects invalid sample count', () => {
			expect(() =>
				TextureValidation.validateCreateOptions({
					width: 64,
					height: 64,
					format: TextureFormat.RGBA8 as any,
					dimension: TextureDimension.TEXTURE_2D as any,
					usage: TextureUsage.STATIC as any,
					samples: 3,
				})
			).toThrow(/Invalid sample count/);
		});

		it('accepts valid sample counts', () => {
			for (const samples of [1, 2, 4, 8, 16]) {
				expect(() =>
					TextureValidation.validateCreateOptions({
						width: 64,
						height: 64,
						format: TextureFormat.RGBA8 as any,
						dimension: TextureDimension.TEXTURE_2D as any,
						usage: TextureUsage.STATIC as any,
						samples,
					})
				).not.toThrow();
			}
		});
	});

	describe('validateSamplerOptions', () => {
		const baseSamplerOptions = {
			minFilter: FilterMode.LINEAR as any,
			magFilter: FilterMode.LINEAR as any,
			wrapS: WrapMode.REPEAT as any,
			wrapT: WrapMode.REPEAT as any,
		};

		it('accepts valid sampler options', () => {
			expect(() => TextureValidation.validateSamplerOptions(baseSamplerOptions)).not.toThrow();
		});

		it('rejects maxAnisotropy < 1', () => {
			expect(() =>
				TextureValidation.validateSamplerOptions({ ...baseSamplerOptions, maxAnisotropy: 0 })
			).toThrow(/Max anisotropy must be at least 1/);
		});

		it('rejects minLod > maxLod', () => {
			expect(() =>
				TextureValidation.validateSamplerOptions({
					...baseSamplerOptions,
					minLod: 5,
					maxLod: 2,
				})
			).toThrow(/Min LOD cannot be greater than max LOD/);
		});

		it('accepts valid minLod <= maxLod', () => {
			expect(() =>
				TextureValidation.validateSamplerOptions({
					...baseSamplerOptions,
					minLod: 0,
					maxLod: 10,
				})
			).not.toThrow();
		});

		it('rejects border color components outside [0, 1]', () => {
			expect(() =>
				TextureValidation.validateSamplerOptions({
					...baseSamplerOptions,
					borderColor: new Vec4(1.5, 0, 0, 1),
				})
			).toThrow(/Border color components must be in range/);

			expect(() =>
				TextureValidation.validateSamplerOptions({
					...baseSamplerOptions,
					borderColor: new Vec4(0, -0.1, 0, 1),
				})
			).toThrow(/Border color components must be in range/);
		});

		it('accepts valid border color', () => {
			expect(() =>
				TextureValidation.validateSamplerOptions({
					...baseSamplerOptions,
					borderColor: new Vec4(0.5, 0.5, 0.5, 1),
				})
			).not.toThrow();
		});
	});
});
