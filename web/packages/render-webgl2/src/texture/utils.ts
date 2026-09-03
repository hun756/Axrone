import { Vec2, Vec3, Vec4, Color } from '@axrone/numeric';
import { Djb2 } from '@axrone/hash';
import type { ContextSource, IGLContext } from '../context';
import { isGLContext, resolveContext } from '../context';

import {
    TextureDimension,
    TextureFormat,
    FilterMode,
    WrapMode,
    TextureUsage,
    ColorSpace,
    ITextureCreateOptions,
    ITextureSamplerOptions,
    TextureError,
    TextureErrorCode,
} from './interfaces';

// ── WebGL2 named constants ──────────────────────────────────────────

const GL_TYPE = {
    UNSIGNED_BYTE: 0x1401,
    UNSIGNED_SHORT: 0x1403,
    UNSIGNED_INT: 0x1405,
    FLOAT: 0x1406,
    HALF_FLOAT: 0x140b,
    UNSIGNED_INT_24_8: 0x84F9,
    FLOAT_32_UNSIGNED_INT_24_8_REV: 0x8dad,
} as const;

const GL_FORMAT = {
    DEPTH_COMPONENT: 0x1902,
    RED: 0x1903,
    RG: 0x8227,
    RGB: 0x1907,
    RGBA: 0x1908,
    DEPTH_STENCIL: 0x84f9,
} as const;

const GL_INTERNAL_FORMAT = {
    // Unsigned normalized
    R8: 0x8229,
    RG8: 0x822b,
    RGB8: 0x8051,
    RGBA8: 0x8058,
    // Float
    R16F: 0x822d,
    RG16F: 0x822f,
    RGB16F: 0x881b,
    RGBA16F: 0x881a,
    R32F: 0x822e,
    RG32F: 0x8230,
    RGB32F: 0x8815,
    RGBA32F: 0x8814,
    // Depth / stencil
    DEPTH_COMPONENT16: 0x81a5,
    DEPTH_COMPONENT24: 0x81a6,
    DEPTH_COMPONENT32F: 0x8cac,
    DEPTH24_STENCIL8: 0x88f0,
    DEPTH32F_STENCIL8: 0x8cad,
    // sRGB
    SRGB8: 0x8c41,
    SRGB8_ALPHA8: 0x8c43,
    // S3TC / BC
    COMPRESSED_RGB_S3TC_DXT1: 0x83f0,
    COMPRESSED_RGBA_S3TC_DXT1: 0x83f1,
    COMPRESSED_RGBA_S3TC_DXT3: 0x83f2,
    COMPRESSED_RGBA_S3TC_DXT5: 0x83f3,
    // RGTC
    COMPRESSED_RED_RGTC1: 0x8dbb,
    COMPRESSED_RG_RGTC2: 0x8dbd,
    // BPTC
    COMPRESSED_RGB_BPTC_UNSIGNED_FLOAT: 0x8e8f,
    COMPRESSED_RGB_BPTC_SIGNED_FLOAT: 0x8e8e,
    COMPRESSED_RGBA_BPTC_UNORM: 0x8e8c,
    // ASTC
    COMPRESSED_RGBA_ASTC_4x4: 0x93b0,
    COMPRESSED_RGBA_ASTC_5x4: 0x93b1,
    COMPRESSED_RGBA_ASTC_5x5: 0x93b2,
    COMPRESSED_RGBA_ASTC_6x5: 0x93b3,
    COMPRESSED_RGBA_ASTC_6x6: 0x93b4,
    COMPRESSED_RGBA_ASTC_8x5: 0x93b5,
    COMPRESSED_RGBA_ASTC_8x6: 0x93b6,
    COMPRESSED_RGBA_ASTC_8x8: 0x93b7,
    COMPRESSED_RGBA_ASTC_10x5: 0x93b8,
    COMPRESSED_RGBA_ASTC_10x6: 0x93b9,
    COMPRESSED_RGBA_ASTC_10x8: 0x93ba,
    COMPRESSED_RGBA_ASTC_10x10: 0x93bb,
    COMPRESSED_RGBA_ASTC_12x10: 0x93bc,
    COMPRESSED_RGBA_ASTC_12x12: 0x93bd,
} as const;

const GL_TEX_PARAM = {
    TEXTURE_1D: 0x0de0,
    TEXTURE_2D: 0x0de1,
    TEXTURE_3D: 0x806f,
    TEXTURE_CUBE_MAP: 0x8513,
    TEXTURE_2D_ARRAY: 0x8c1a,
    TEXTURE_CUBE_MAP_ARRAY: 0x9009,
    NEAREST: 0x2600,
    LINEAR: 0x2601,
    NEAREST_MIPMAP_NEAREST: 0x2700,
    LINEAR_MIPMAP_NEAREST: 0x2701,
    NEAREST_MIPMAP_LINEAR: 0x2702,
    LINEAR_MIPMAP_LINEAR: 0x2703,
    REPEAT: 0x2901,
    CLAMP_TO_EDGE: 0x812f,
    CLAMP_TO_BORDER: 0x812d,
    MIRRORED_REPEAT: 0x8370,
} as const;

interface FormatInfo {
    readonly internalFormat: number;
    readonly format: number;
    readonly type: number;
    readonly bytesPerPixel: number;
    readonly channels: number;
    readonly compressed: boolean;
    readonly blockSize?: number;
    readonly blockWidth?: number;
    readonly blockHeight?: number;
    readonly extensionNames?: readonly string[];
    readonly floatingPoint: boolean;
    readonly integer: boolean;
    readonly depth: boolean;
    readonly stencil: boolean;
    readonly srgb: boolean;
}

const WEBGL_S3TC_EXTENSIONS = [
    'WEBGL_compressed_texture_s3tc',
    'WEBKIT_WEBGL_compressed_texture_s3tc',
] as const;
const WEBGL_RGTC_EXTENSIONS = ['EXT_texture_compression_rgtc'] as const;
const WEBGL_BPTC_EXTENSIONS = ['EXT_texture_compression_bptc'] as const;
const WEBGL_ASTC_EXTENSIONS = ['WEBGL_compressed_texture_astc'] as const;

const createCompressedFormatInfo = (
    internalFormat: number,
    channels: number,
    blockSize: number,
    blockWidth: number,
    blockHeight: number,
    extensionNames: readonly string[],
    options: {
        readonly floatingPoint?: boolean;
        readonly srgb?: boolean;
    } = {}
): FormatInfo => ({
    internalFormat,
    format: 0,
    type: 0,
    bytesPerPixel: 0,
    channels,
    compressed: true,
    blockSize,
    blockWidth,
    blockHeight,
    extensionNames,
    floatingPoint: options.floatingPoint ?? false,
    integer: false,
    depth: false,
    stencil: false,
    srgb: options.srgb ?? false,
});

const COMPRESSED_FORMAT_DATABASE: readonly [TextureFormat, FormatInfo][] = [
    [
        TextureFormat.BC1_RGB,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGB_S3TC_DXT1, 3, 8, 4, 4, WEBGL_S3TC_EXTENSIONS),
    ],
    [
        TextureFormat.BC1_RGBA,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_S3TC_DXT1, 4, 8, 4, 4, WEBGL_S3TC_EXTENSIONS),
    ],
    [
        TextureFormat.BC2_RGBA,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_S3TC_DXT3, 4, 16, 4, 4, WEBGL_S3TC_EXTENSIONS),
    ],
    [
        TextureFormat.BC3_RGBA,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_S3TC_DXT5, 4, 16, 4, 4, WEBGL_S3TC_EXTENSIONS),
    ],
    [
        TextureFormat.BC4_R,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RED_RGTC1, 1, 8, 4, 4, WEBGL_RGTC_EXTENSIONS),
    ],
    [
        TextureFormat.BC5_RG,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RG_RGTC2, 2, 16, 4, 4, WEBGL_RGTC_EXTENSIONS),
    ],
    [
        TextureFormat.BC6H_RGB_UF16,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGB_BPTC_UNSIGNED_FLOAT, 3, 16, 4, 4, WEBGL_BPTC_EXTENSIONS, {
            floatingPoint: true,
        }),
    ],
    [
        TextureFormat.BC6H_RGB_SF16,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGB_BPTC_SIGNED_FLOAT, 3, 16, 4, 4, WEBGL_BPTC_EXTENSIONS, {
            floatingPoint: true,
        }),
    ],
    [
        TextureFormat.BC7_RGBA,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_BPTC_UNORM, 4, 16, 4, 4, WEBGL_BPTC_EXTENSIONS),
    ],
    [
        TextureFormat.ASTC_4x4,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_ASTC_4x4, 4, 16, 4, 4, WEBGL_ASTC_EXTENSIONS),
    ],
    [
        TextureFormat.ASTC_5x4,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_ASTC_5x4, 4, 16, 5, 4, WEBGL_ASTC_EXTENSIONS),
    ],
    [
        TextureFormat.ASTC_5x5,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_ASTC_5x5, 4, 16, 5, 5, WEBGL_ASTC_EXTENSIONS),
    ],
    [
        TextureFormat.ASTC_6x5,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_ASTC_6x5, 4, 16, 6, 5, WEBGL_ASTC_EXTENSIONS),
    ],
    [
        TextureFormat.ASTC_6x6,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_ASTC_6x6, 4, 16, 6, 6, WEBGL_ASTC_EXTENSIONS),
    ],
    [
        TextureFormat.ASTC_8x5,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_ASTC_8x5, 4, 16, 8, 5, WEBGL_ASTC_EXTENSIONS),
    ],
    [
        TextureFormat.ASTC_8x6,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_ASTC_8x6, 4, 16, 8, 6, WEBGL_ASTC_EXTENSIONS),
    ],
    [
        TextureFormat.ASTC_8x8,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_ASTC_8x8, 4, 16, 8, 8, WEBGL_ASTC_EXTENSIONS),
    ],
    [
        TextureFormat.ASTC_10x5,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_ASTC_10x5, 4, 16, 10, 5, WEBGL_ASTC_EXTENSIONS),
    ],
    [
        TextureFormat.ASTC_10x6,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_ASTC_10x6, 4, 16, 10, 6, WEBGL_ASTC_EXTENSIONS),
    ],
    [
        TextureFormat.ASTC_10x8,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_ASTC_10x8, 4, 16, 10, 8, WEBGL_ASTC_EXTENSIONS),
    ],
    [
        TextureFormat.ASTC_10x10,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_ASTC_10x10, 4, 16, 10, 10, WEBGL_ASTC_EXTENSIONS),
    ],
    [
        TextureFormat.ASTC_12x10,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_ASTC_12x10, 4, 16, 12, 10, WEBGL_ASTC_EXTENSIONS),
    ],
    [
        TextureFormat.ASTC_12x12,
        createCompressedFormatInfo(GL_INTERNAL_FORMAT.COMPRESSED_RGBA_ASTC_12x12, 4, 16, 12, 12, WEBGL_ASTC_EXTENSIONS),
    ],
];

export class TextureFormatInfo {
    private static readonly formatDatabase = new Map<TextureFormat, FormatInfo>([
        [
            TextureFormat.R8,
            {
                internalFormat: GL_INTERNAL_FORMAT.R8,
                format: GL_FORMAT.RED,
                type: GL_TYPE.UNSIGNED_BYTE,
                bytesPerPixel: 1,
                channels: 1,
                compressed: false,
                floatingPoint: false,
                integer: false,
                depth: false,
                stencil: false,
                srgb: false,
            },
        ],
        [
            TextureFormat.RG8,
            {
                internalFormat: GL_INTERNAL_FORMAT.RG8,
                format: GL_FORMAT.RG,
                type: GL_TYPE.UNSIGNED_BYTE,
                bytesPerPixel: 2,
                channels: 2,
                compressed: false,
                floatingPoint: false,
                integer: false,
                depth: false,
                stencil: false,
                srgb: false,
            },
        ],
        [
            TextureFormat.RGB8,
            {
                internalFormat: GL_INTERNAL_FORMAT.RGB8,
                format: GL_FORMAT.RGB,
                type: GL_TYPE.UNSIGNED_BYTE,
                bytesPerPixel: 3,
                channels: 3,
                compressed: false,
                floatingPoint: false,
                integer: false,
                depth: false,
                stencil: false,
                srgb: false,
            },
        ],
        [
            TextureFormat.RGBA8,
            {
                internalFormat: GL_INTERNAL_FORMAT.RGBA8,
                format: GL_FORMAT.RGBA,
                type: GL_TYPE.UNSIGNED_BYTE,
                bytesPerPixel: 4,
                channels: 4,
                compressed: false,
                floatingPoint: false,
                integer: false,
                depth: false,
                stencil: false,
                srgb: false,
            },
        ],

        [
            TextureFormat.R16F,
            {
                internalFormat: GL_INTERNAL_FORMAT.R16F,
                format: GL_FORMAT.RED,
                type: GL_TYPE.HALF_FLOAT,
                bytesPerPixel: 2,
                channels: 1,
                compressed: false,
                floatingPoint: true,
                integer: false,
                depth: false,
                stencil: false,
                srgb: false,
            },
        ],
        [
            TextureFormat.RG16F,
            {
                internalFormat: GL_INTERNAL_FORMAT.RG16F,
                format: GL_FORMAT.RG,
                type: GL_TYPE.HALF_FLOAT,
                bytesPerPixel: 4,
                channels: 2,
                compressed: false,
                floatingPoint: true,
                integer: false,
                depth: false,
                stencil: false,
                srgb: false,
            },
        ],
        [
            TextureFormat.RGB16F,
            {
                internalFormat: GL_INTERNAL_FORMAT.RGB16F,
                format: GL_FORMAT.RGB,
                type: GL_TYPE.HALF_FLOAT,
                bytesPerPixel: 6,
                channels: 3,
                compressed: false,
                floatingPoint: true,
                integer: false,
                depth: false,
                stencil: false,
                srgb: false,
            },
        ],
        [
            TextureFormat.RGBA16F,
            {
                internalFormat: GL_INTERNAL_FORMAT.RGBA16F,
                format: GL_FORMAT.RGBA,
                type: GL_TYPE.HALF_FLOAT,
                bytesPerPixel: 8,
                channels: 4,
                compressed: false,
                floatingPoint: true,
                integer: false,
                depth: false,
                stencil: false,
                srgb: false,
            },
        ],

        [
            TextureFormat.R32F,
            {
                internalFormat: GL_INTERNAL_FORMAT.R32F,
                format: GL_FORMAT.RED,
                type: GL_TYPE.FLOAT,
                bytesPerPixel: 4,
                channels: 1,
                compressed: false,
                floatingPoint: true,
                integer: false,
                depth: false,
                stencil: false,
                srgb: false,
            },
        ],
        [
            TextureFormat.RG32F,
            {
                internalFormat: GL_INTERNAL_FORMAT.RG32F,
                format: GL_FORMAT.RG,
                type: GL_TYPE.FLOAT,
                bytesPerPixel: 8,
                channels: 2,
                compressed: false,
                floatingPoint: true,
                integer: false,
                depth: false,
                stencil: false,
                srgb: false,
            },
        ],
        [
            TextureFormat.RGB32F,
            {
                internalFormat: GL_INTERNAL_FORMAT.RGB32F,
                format: GL_FORMAT.RGB,
                type: GL_TYPE.FLOAT,
                bytesPerPixel: 12,
                channels: 3,
                compressed: false,
                floatingPoint: true,
                integer: false,
                depth: false,
                stencil: false,
                srgb: false,
            },
        ],
        [
            TextureFormat.RGBA32F,
            {
                internalFormat: GL_INTERNAL_FORMAT.RGBA32F,
                format: GL_FORMAT.RGBA,
                type: GL_TYPE.FLOAT,
                bytesPerPixel: 16,
                channels: 4,
                compressed: false,
                floatingPoint: true,
                integer: false,
                depth: false,
                stencil: false,
                srgb: false,
            },
        ],

        [
            TextureFormat.DEPTH_COMPONENT16,
            {
                internalFormat: GL_INTERNAL_FORMAT.DEPTH_COMPONENT16,
                format: GL_FORMAT.DEPTH_COMPONENT,
                type: GL_TYPE.UNSIGNED_SHORT,
                bytesPerPixel: 2,
                channels: 1,
                compressed: false,
                floatingPoint: false,
                integer: false,
                depth: true,
                stencil: false,
                srgb: false,
            },
        ],
        [
            TextureFormat.DEPTH_COMPONENT24,
            {
                internalFormat: GL_INTERNAL_FORMAT.DEPTH_COMPONENT24,
                format: GL_FORMAT.DEPTH_COMPONENT,
                type: GL_TYPE.UNSIGNED_INT,
                bytesPerPixel: 4,
                channels: 1,
                compressed: false,
                floatingPoint: false,
                integer: false,
                depth: true,
                stencil: false,
                srgb: false,
            },
        ],
        [
            TextureFormat.DEPTH_COMPONENT32F,
            {
                internalFormat: GL_INTERNAL_FORMAT.DEPTH_COMPONENT32F,
                format: GL_FORMAT.DEPTH_COMPONENT,
                type: GL_TYPE.FLOAT,
                bytesPerPixel: 4,
                channels: 1,
                compressed: false,
                floatingPoint: true,
                integer: false,
                depth: true,
                stencil: false,
                srgb: false,
            },
        ],
        [
            TextureFormat.DEPTH24_STENCIL8,
            {
                internalFormat: GL_INTERNAL_FORMAT.DEPTH24_STENCIL8,
                format: GL_FORMAT.DEPTH_STENCIL,
                type: GL_TYPE.UNSIGNED_INT_24_8,
                bytesPerPixel: 4,
                channels: 2,
                compressed: false,
                floatingPoint: false,
                integer: false,
                depth: true,
                stencil: true,
                srgb: false,
            },
        ],
        [
            TextureFormat.DEPTH32F_STENCIL8,
            {
                internalFormat: GL_INTERNAL_FORMAT.DEPTH32F_STENCIL8,
                format: GL_FORMAT.DEPTH_STENCIL,
                type: GL_TYPE.FLOAT_32_UNSIGNED_INT_24_8_REV,
                bytesPerPixel: 8,
                channels: 2,
                compressed: false,
                floatingPoint: true,
                integer: false,
                depth: true,
                stencil: true,
                srgb: false,
            },
        ],
        ...COMPRESSED_FORMAT_DATABASE,
    ]);

    private static readonly SRGB_INTERNAL_FORMAT_OVERRIDES = new Map<TextureFormat, number>([
        [TextureFormat.RGB8, GL_INTERNAL_FORMAT.SRGB8],
        [TextureFormat.RGBA8, GL_INTERNAL_FORMAT.SRGB8_ALPHA8],
    ]);

    public static getFormatInfo(
        format: TextureFormat,
        colorSpace: ColorSpace = ColorSpace.LINEAR
    ): FormatInfo {
        const info = this.formatDatabase.get(format);
        if (!info) {
            throw new TextureError(
                `Unsupported texture format: ${format}`,
                TextureErrorCode.UNSUPPORTED_FORMAT
            );
        }

        if (colorSpace !== ColorSpace.SRGB || info.compressed || info.depth || info.integer) {
            return info;
        }

        const srgbInternalFormat = this.SRGB_INTERNAL_FORMAT_OVERRIDES.get(format);
        if (srgbInternalFormat === undefined) {
            return info;
        }

        return {
            ...info,
            internalFormat: srgbInternalFormat,
            srgb: true,
        };
    }

    public static getBytesPerPixel(format: TextureFormat): number {
        return this.getFormatInfo(format).bytesPerPixel;
    }

    public static getChannelCount(format: TextureFormat): number {
        return this.getFormatInfo(format).channels;
    }

    public static isCompressed(format: TextureFormat): boolean {
        return this.getFormatInfo(format).compressed;
    }

    public static isFloatingPoint(format: TextureFormat): boolean {
        return this.getFormatInfo(format).floatingPoint;
    }

    public static isInteger(format: TextureFormat): boolean {
        return this.getFormatInfo(format).integer;
    }

    public static isDepth(format: TextureFormat): boolean {
        return this.getFormatInfo(format).depth;
    }

    public static hasStencil(format: TextureFormat): boolean {
        return this.getFormatInfo(format).stencil;
    }

    public static isSRGB(format: TextureFormat): boolean {
        return this.getFormatInfo(format).srgb;
    }

    public static getCompressedFormats(): readonly TextureFormat[] {
        return this.getSupportedFormats().filter((format) => this.isCompressed(format));
    }

    public static getContextSupportedCompressedFormats(
        gl: WebGL2RenderingContext,
        formats: readonly TextureFormat[] = this.getCompressedFormats()
    ): readonly TextureFormat[] {
        const supported: TextureFormat[] = [];

        for (const format of formats) {
            if (this.isCompressed(format) === false) {
                continue;
            }

            try {
                TextureWebGLConstants.getCompressedInternalFormat(gl, format);
                supported.push(format);
            } catch { // best-effort
                continue;
            }
        }

        return Object.freeze(supported);
    }

    public static getSupportedFormats(): readonly TextureFormat[] {
        return Array.from(this.formatDatabase.keys());
    }
}

export class TextureWebGLConstants {
    public static readonly DIMENSION_MAP = new Map<TextureDimension, number>([
        [TextureDimension.TEXTURE_1D, GL_TEX_PARAM.TEXTURE_1D],
        [TextureDimension.TEXTURE_2D, GL_TEX_PARAM.TEXTURE_2D],
        [TextureDimension.TEXTURE_3D, GL_TEX_PARAM.TEXTURE_3D],
        [TextureDimension.TEXTURE_CUBE, GL_TEX_PARAM.TEXTURE_CUBE_MAP],
        [TextureDimension.TEXTURE_2D_ARRAY, GL_TEX_PARAM.TEXTURE_2D_ARRAY],
        [TextureDimension.TEXTURE_CUBE_ARRAY, GL_TEX_PARAM.TEXTURE_CUBE_MAP_ARRAY],
    ]);

    public static readonly FILTER_MAP = new Map<FilterMode, number>([
        [FilterMode.NEAREST, GL_TEX_PARAM.NEAREST],
        [FilterMode.LINEAR, GL_TEX_PARAM.LINEAR],
        [FilterMode.NEAREST_MIPMAP_NEAREST, GL_TEX_PARAM.NEAREST_MIPMAP_NEAREST],
        [FilterMode.LINEAR_MIPMAP_NEAREST, GL_TEX_PARAM.LINEAR_MIPMAP_NEAREST],
        [FilterMode.NEAREST_MIPMAP_LINEAR, GL_TEX_PARAM.NEAREST_MIPMAP_LINEAR],
        [FilterMode.LINEAR_MIPMAP_LINEAR, GL_TEX_PARAM.LINEAR_MIPMAP_LINEAR],
    ]);

    public static readonly WRAP_MAP = new Map<WrapMode, number>([
        [WrapMode.REPEAT, GL_TEX_PARAM.REPEAT],
        [WrapMode.CLAMP_TO_EDGE, GL_TEX_PARAM.CLAMP_TO_EDGE],
        [WrapMode.CLAMP_TO_BORDER, GL_TEX_PARAM.CLAMP_TO_BORDER],
        [WrapMode.MIRRORED_REPEAT, GL_TEX_PARAM.MIRRORED_REPEAT],
    ]);

    public static getDimensionConstant(dimension: TextureDimension): number {
        const constant = this.DIMENSION_MAP.get(dimension);
        if (constant === undefined) {
            throw new TextureError(
                `Unsupported texture dimension: ${dimension}`,
                TextureErrorCode.UNSUPPORTED_FORMAT
            );
        }
        return constant;
    }

    public static getFilterConstant(filter: FilterMode): number {
        const constant = this.FILTER_MAP.get(filter);
        if (constant === undefined) {
            throw new TextureError(
                `Unsupported filter mode: ${filter}`,
                TextureErrorCode.INVALID_OPERATION
            );
        }
        return constant;
    }

    public static getWrapConstant(wrap: WrapMode): number {
        const constant = this.WRAP_MAP.get(wrap);
        if (constant === undefined) {
            throw new TextureError(
                `Unsupported wrap mode: ${wrap}`,
                TextureErrorCode.INVALID_OPERATION
            );
        }
        return constant;
    }

    public static getCompressedInternalFormat(
        gl: WebGL2RenderingContext,
        format: TextureFormat
    ): number {
        const info = TextureFormatInfo.getFormatInfo(format);
        if (info.compressed === false) {
            return info.internalFormat;
        }

        const extensionNames = info.extensionNames ?? [];
        if (extensionNames.length > 0) {
            const supported = extensionNames.some((name) => gl.getExtension(name) !== null);
            if (!supported) {
                throw new TextureError(
                    `Compressed texture format ${format} is not supported by this WebGL context`,
                    TextureErrorCode.UNSUPPORTED_FORMAT
                );
            }
        }

        return info.internalFormat;
    }
}

export class TextureUtils {
    public static calculateMemoryUsage(
        width: number,
        height: number,
        depth: number,
        format: TextureFormat,
        mipLevels: number = 1
    ): number {
        const formatInfo = TextureFormatInfo.getFormatInfo(format);
        let totalBytes = 0;

        for (let mip = 0; mip < mipLevels; mip++) {
            const mipWidth = Math.max(1, width >> mip);
            const mipHeight = Math.max(1, height >> mip);
            const mipDepth = Math.max(1, depth >> mip);

            if (formatInfo.compressed) {
                const blockWidth = formatInfo.blockWidth ?? 4;
                const blockHeight = formatInfo.blockHeight ?? 4;
                const blockSize = formatInfo.blockSize ?? 0;
                const blocksWide = Math.max(1, Math.ceil(mipWidth / blockWidth));
                const blocksHigh = Math.max(1, Math.ceil(mipHeight / blockHeight));
                totalBytes += blocksWide * blocksHigh * mipDepth * blockSize;
                continue;
            }

            totalBytes += mipWidth * mipHeight * mipDepth * formatInfo.bytesPerPixel;
        }

        return totalBytes;
    }

    public static calculateMaxMipLevels(width: number, height: number, depth: number = 1): number {
        const maxDimension = Math.max(width, height, depth);
        return Math.floor(Math.log2(maxDimension)) + 1;
    }

    public static getMipDimensions(
        width: number,
        height: number,
        depth: number,
        mipLevel: number
    ): { width: number; height: number; depth: number } {
        return {
            width: Math.max(1, width >> mipLevel),
            height: Math.max(1, height >> mipLevel),
            depth: Math.max(1, depth >> mipLevel),
        };
    }

    public static validateDimensions(
        width: number,
        height: number,
        depth: number,
        dimension: TextureDimension
    ): void {
        if (width <= 0 || height <= 0 || depth <= 0) {
            throw new TextureError(
                'Texture dimensions must be positive',
                TextureErrorCode.INVALID_DIMENSIONS
            );
        }

        const isPowerOfTwo = (n: number) => (n & (n - 1)) === 0;

        switch (dimension) {
            case TextureDimension.TEXTURE_CUBE:
                if (width !== height) {
                    throw new TextureError(
                        'Cube textures must have equal width and height',
                        TextureErrorCode.INVALID_DIMENSIONS
                    );
                }
                break;

            case TextureDimension.TEXTURE_1D:
                if (height !== 1 || depth !== 1) {
                    throw new TextureError(
                        '1D textures must have height and depth of 1',
                        TextureErrorCode.INVALID_DIMENSIONS
                    );
                }
                break;

            case TextureDimension.TEXTURE_2D:
            case TextureDimension.TEXTURE_2D_ARRAY:
                if (depth < 1 && dimension === TextureDimension.TEXTURE_2D) {
                    throw new TextureError(
                        '2D textures must have depth of at least 1',
                        TextureErrorCode.INVALID_DIMENSIONS
                    );
                }
                break;
        }
    }

    public static isFormatCompatible(format: TextureFormat, dimension: TextureDimension): boolean {
        if (dimension === TextureDimension.TEXTURE_3D && TextureFormatInfo.isDepth(format)) {
            return false;
        }

        if (TextureFormatInfo.isInteger(format)) {
        }

        return true;
    }

    public static generateTextureId(): string {
        return `tex_${Date.now()}_${Math.random().toString(36).slice(2, 11)}`;
    }

    public static calculateTextureHash(options: ITextureCreateOptions): string {
        const hashData = [
            options.width,
            options.height,
            options.depth || 1,
            options.format,
            options.dimension,
            options.mipLevels || 1,
            options.arrayLayers || 1,
            options.usage,
            options.colorSpace || ColorSpace.LINEAR,
        ].join('|');

        return this.simpleHash(hashData);
    }

    private static simpleHash(str: string): string {
        const h = new Djb2();
        h.updateString(str);
        return (h.digest() as unknown as number).toString(36);
    }

    public static getDefaultSamplerOptions(usage: TextureUsage): ITextureSamplerOptions {
        switch (usage) {
            case TextureUsage.STATIC:
                return {
                    minFilter: FilterMode.LINEAR_MIPMAP_LINEAR,
                    magFilter: FilterMode.LINEAR,
                    wrapS: WrapMode.REPEAT,
                    wrapT: WrapMode.REPEAT,
                    maxAnisotropy: 16,
                };

            case TextureUsage.RENDER_TARGET:
                return {
                    minFilter: FilterMode.LINEAR,
                    magFilter: FilterMode.LINEAR,
                    wrapS: WrapMode.CLAMP_TO_EDGE,
                    wrapT: WrapMode.CLAMP_TO_EDGE,
                };

            case TextureUsage.DEPTH_BUFFER:
                return {
                    minFilter: FilterMode.NEAREST,
                    magFilter: FilterMode.NEAREST,
                    wrapS: WrapMode.CLAMP_TO_EDGE,
                    wrapT: WrapMode.CLAMP_TO_EDGE,
                    compareMode: 'COMPARE_REF_TO_TEXTURE',
                    compareFunc: 'LEQUAL',
                };

            default:
                return {
                    minFilter: FilterMode.LINEAR,
                    magFilter: FilterMode.LINEAR,
                    wrapS: WrapMode.REPEAT,
                    wrapT: WrapMode.REPEAT,
                };
        }
    }

    public static colorToVec4(color: string): Vec4 {
        try {
            const c = Color.fromHex(color);
            return new Vec4(c.r, c.g, c.b, c.a);
        } catch {
            return new Vec4(1, 1, 1, 1);
        }
    }

    public static isExtensionAvailableForContext(ctx: IGLContext, name: string): boolean {
        return ctx.extensions.has(name as any);
    }

    public static isExtensionAvailable(gl: WebGL2RenderingContext | IGLContext, name: string): boolean {
        const source = gl as unknown as ContextSource;
        if (isGLContext(source as any)) return (source as IGLContext).extensions.has(name as any);
        return (gl as WebGL2RenderingContext).getExtension(name) !== null;
    }

    public static getOptimalFormat(
        gl: WebGL2RenderingContext,
        usage: TextureUsage,
        hasAlpha: boolean = false,
        preferFloat: boolean = false
    ): TextureFormat {
        if (usage === TextureUsage.DEPTH_BUFFER) {
            return TextureFormat.DEPTH_COMPONENT24;
        }

        if (preferFloat) {
            return hasAlpha ? TextureFormat.RGBA16F : TextureFormat.RGB16F;
        }

        return hasAlpha ? TextureFormat.RGBA8 : TextureFormat.RGB8;
    }
}

export class TextureValidation {
    public static validateCreateOptions(options: ITextureCreateOptions): void {
        TextureUtils.validateDimensions(
            options.width,
            options.height,
            options.depth || 1,
            options.dimension
        );

        if (!TextureUtils.isFormatCompatible(options.format, options.dimension)) {
            throw new TextureError(
                `Format ${options.format} is not compatible with dimension ${options.dimension}`,
                TextureErrorCode.UNSUPPORTED_FORMAT
            );
        }

        if (options.mipLevels !== undefined) {
            const maxMips = TextureUtils.calculateMaxMipLevels(
                options.width,
                options.height,
                options.depth || 1
            );
            if (options.mipLevels > maxMips) {
                throw new TextureError(
                    `Too many mip levels: ${options.mipLevels}, maximum is ${maxMips}`,
                    TextureErrorCode.INVALID_DIMENSIONS
                );
            }
        }

        if (options.arrayLayers !== undefined && options.arrayLayers < 1) {
            throw new TextureError(
                'Array layers must be at least 1',
                TextureErrorCode.INVALID_DIMENSIONS
            );
        }

        if (options.samples !== undefined) {
            const validSamples = [1, 2, 4, 8, 16];
            if (!validSamples.includes(options.samples)) {
                throw new TextureError(
                    `Invalid sample count: ${options.samples}`,
                    TextureErrorCode.INVALID_DIMENSIONS
                );
            }
        }
    }

    public static validateSamplerOptions(options: ITextureSamplerOptions): void {
        if (options.maxAnisotropy !== undefined && options.maxAnisotropy < 1) {
            throw new TextureError(
                'Max anisotropy must be at least 1',
                TextureErrorCode.INVALID_OPERATION
            );
        }

        if (options.minLod !== undefined && options.maxLod !== undefined) {
            if (options.minLod > options.maxLod) {
                throw new TextureError(
                    'Min LOD cannot be greater than max LOD',
                    TextureErrorCode.INVALID_OPERATION
                );
            }
        }

        if (options.borderColor) {
            const color = options.borderColor;
            if (
                color.x < 0 ||
                color.x > 1 ||
                color.y < 0 ||
                color.y > 1 ||
                color.z < 0 ||
                color.z > 1 ||
                color.w < 0 ||
                color.w > 1
            ) {
                throw new TextureError(
                    'Border color components must be in range [0, 1]',
                    TextureErrorCode.INVALID_OPERATION
                );
            }
        }
    }
}
