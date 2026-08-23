import { Vec2, Vec3, Vec4 } from '@axrone/numeric';
import { ByteBuffer } from '@axrone/memory';
import {
    ITexture,
    ITextureCreateOptions,
    ITextureSubresource,
    TextureDimension,
    TextureFormat,
    TextureUsage,
    ColorSpace,
    TextureDataSource,
    TextureError,
    TextureErrorCode,
} from './interfaces';
import { TextureFormatInfo, TextureUtils, TextureWebGLConstants, TextureValidation } from './utils';
import type { ContextSource, IGLContext } from '../context';
import { resolveContext } from '../context';
import { DisposalTracker } from '../internal/disposable';

export class WebGLTexture implements ITexture {
    public readonly id: string;
    public readonly nativeHandle: globalThis.WebGLTexture;
    public readonly dimension: TextureDimension;
    public readonly format: TextureFormat;
    public readonly width: number;
    public readonly height: number;
    public readonly depth: number;
    public readonly mipLevels: number;
    public readonly arrayLayers: number;
    public readonly samples: number;
    public readonly usage: TextureUsage;
    public readonly colorSpace: ColorSpace;
    public readonly label: string | null;

    public readonly isCompressed: boolean;
    public readonly bytesPerPixel: number;
    public readonly totalMemoryUsage: number;

    private readonly _disposal = new DisposalTracker();
    private _currentUnit = -1;
    private _generation = 0;

    private readonly _ctx: IGLContext;
    private readonly _gl: WebGL2RenderingContext;
    private readonly _target: number;

    constructor(
        source: ContextSource,
        options: ITextureCreateOptions,
        data?: TextureDataSource
    ) {
        const ctx = resolveContext(source);
        const gl = ctx.gl;
        this._ctx = ctx;
        this._gl = gl;

        TextureValidation.validateCreateOptions(options);

        this.id = TextureUtils.generateTextureId();
        this.dimension = options.dimension;
        this.format = options.format;
        this.width = options.width;
        this.height = options.height;
        this.depth = options.depth || 1;
        this.mipLevels = options.mipLevels || 1;
        this.arrayLayers = options.arrayLayers || 1;
        this.samples = options.samples || 1;
        this.usage = options.usage;
        this.colorSpace = options.colorSpace || ColorSpace.LINEAR;
        this.label = options.label || null;

        const formatInfo = TextureFormatInfo.getFormatInfo(this.format, this.colorSpace);
        this.isCompressed = formatInfo.compressed;
        this.bytesPerPixel = formatInfo.bytesPerPixel;
        this.totalMemoryUsage = TextureUtils.calculateMemoryUsage(
            this.width,
            this.height,
            this.depth,
            this.format,
            this.mipLevels
        );

        this._target = TextureWebGLConstants.getDimensionConstant(this.dimension);

        const handle = this._gl.createTexture();
        if (!handle) {
            throw new TextureError(
                'Failed to create WebGL texture',
                TextureErrorCode.CONTEXT_LOST,
                this.id
            );
        }
        this.nativeHandle = handle as globalThis.WebGLTexture;

        this._initializeStorage();

        if (data !== null && data !== undefined) {
            this.setData(data);
        }

        if (this.label) {
            const dbg = this._ctx.extensions.get('KHR_debug');
            this._ctx.labelObject(dbg?.TEXTURE ?? 0x1700, this.nativeHandle, this.label);
        }
    }

    public get isDisposed(): boolean {
        return this._disposal.isDisposed;
    }

    public setData(data: TextureDataSource, subresource?: ITextureSubresource): void {
        this._validateNotDisposed();

        const mipLevel = subresource?.mipLevel || 0;
        const arrayLayer = subresource?.arrayLayer || 0;

        const mipDims = TextureUtils.getMipDimensions(
            this.width,
            this.height,
            this.depth,
            mipLevel
        );
        const x = subresource?.x || 0;
        const y = subresource?.y || 0;
        const z = subresource?.z || 0;
        const width = subresource?.width || mipDims.width;
        const height = subresource?.height || mipDims.height;
        const depth = subresource?.depth || mipDims.depth;

        this.bind();

        try {
            this._uploadData(data, mipLevel, arrayLayer, x, y, z, width, height, depth);
            this._generation++;
        } finally {
            this.unbind();
        }
    }

    public async getData(subresource?: ITextureSubresource): Promise<ArrayBufferView> {
        this._validateNotDisposed();

        if (this.isCompressed) {
            throw new TextureError(
                'Cannot read back compressed texture data via GPU readback',
                TextureErrorCode.INVALID_OPERATION,
                this.id
            );
        }

        const gl = this._gl;
        const formatInfo = TextureFormatInfo.getFormatInfo(this.format, this.colorSpace);

        const mipLevel = subresource?.mipLevel ?? 0;
        const mipDims = TextureUtils.getMipDimensions(
            this.width,
            this.height,
            this.depth,
            mipLevel
        );

        const x = subresource?.x ?? 0;
        const y = subresource?.y ?? 0;
        const readWidth = subresource?.width ?? mipDims.width;
        const readHeight = subresource?.height ?? mipDims.height;

        // Determine the GL texture target to use for FBO attachment
        const attachTarget = this._target;

        // Create a temporary framebuffer and attach this texture
        const fbo = gl.createFramebuffer();
        if (!fbo) {
            throw new TextureError(
                'Failed to create temporary framebuffer for texture readback',
                TextureErrorCode.CONTEXT_LOST,
                this.id
            );
        }

        // Save the currently bound framebuffer so we can restore it
        const previousFBO = this._ctx.state.snapshot.boundFramebuffer;

        try {
            this._ctx.state.bindFramebuffer(gl.FRAMEBUFFER, fbo);

            const attachmentPoint = formatInfo.depth
                ? gl.DEPTH_ATTACHMENT
                : gl.COLOR_ATTACHMENT0;

            if (attachTarget === gl.TEXTURE_CUBE_MAP) {
                const arrayLayer = subresource?.arrayLayer ?? 0;
                const faceTargets = [
                    gl.TEXTURE_CUBE_MAP_POSITIVE_X,
                    gl.TEXTURE_CUBE_MAP_NEGATIVE_X,
                    gl.TEXTURE_CUBE_MAP_POSITIVE_Y,
                    gl.TEXTURE_CUBE_MAP_NEGATIVE_Y,
                    gl.TEXTURE_CUBE_MAP_POSITIVE_Z,
                    gl.TEXTURE_CUBE_MAP_NEGATIVE_Z,
                ];
                gl.framebufferTexture2D(
                    gl.FRAMEBUFFER,
                    attachmentPoint,
                    faceTargets[arrayLayer],
                    this.nativeHandle,
                    mipLevel
                );
            } else {
                gl.framebufferTexture2D(
                    gl.FRAMEBUFFER,
                    attachmentPoint,
                    attachTarget,
                    this.nativeHandle,
                    mipLevel
                );
            }

            const status = gl.checkFramebufferStatus(gl.FRAMEBUFFER);
            if (status !== gl.FRAMEBUFFER_COMPLETE) {
                throw new TextureError(
                    `Framebuffer incomplete for readback (status: 0x${status.toString(16)})`,
                    TextureErrorCode.INVALID_OPERATION,
                    this.id
                );
            }

            // Determine buffer size and TypedArray constructor from format type
            const pixelCount = readWidth * readHeight;
            const totalBytes = pixelCount * formatInfo.bytesPerPixel;
            const resultBuffer = this._createTypedArrayForType(
                formatInfo.type,
                totalBytes
            );

            // Set appropriate read format/type for readPixels
            const readFormat = formatInfo.format;
            const readType = formatInfo.type;

            gl.readPixels(x, y, readWidth, readHeight, readFormat, readType, resultBuffer);

            return resultBuffer;
        } finally {
            // Restore previous framebuffer binding
            this._ctx.state.bindFramebuffer(gl.FRAMEBUFFER, previousFBO);
            gl.deleteFramebuffer(fbo);
        }
    }

    public copyTo(
        destination: ITexture,
        sourceRegion?: ITextureSubresource,
        destRegion?: ITextureSubresource
    ): void {
        this._validateNotDisposed();

        if (destination.isDisposed) {
            throw new TextureError(
                'Cannot copy to disposed texture',
                TextureErrorCode.ALREADY_DISPOSED,
                destination.id
            );
        }

        if (this.isCompressed || destination.isCompressed) {
            throw new TextureError(
                'Cannot copy compressed textures via GPU copy',
                TextureErrorCode.INVALID_OPERATION,
                this.id
            );
        }

        // Validate format compatibility
        const srcFormatInfo = TextureFormatInfo.getFormatInfo(this.format, this.colorSpace);
        const dstFormatInfo = TextureFormatInfo.getFormatInfo(
            destination.format,
            destination.colorSpace
        );

        if (srcFormatInfo.format !== dstFormatInfo.format || srcFormatInfo.type !== dstFormatInfo.type) {
            throw new TextureError(
                `Cannot copy between incompatible formats: ${this.format} -> ${destination.format}`,
                TextureErrorCode.INVALID_OPERATION,
                this.id
            );
        }

        const gl = this._gl;

        // Source region defaults
        const srcMipLevel = sourceRegion?.mipLevel ?? 0;
        const srcMipDims = TextureUtils.getMipDimensions(
            this.width,
            this.height,
            this.depth,
            srcMipLevel
        );
        const srcX = sourceRegion?.x ?? 0;
        const srcY = sourceRegion?.y ?? 0;
        const copyWidth = sourceRegion?.width ?? srcMipDims.width;
        const copyHeight = sourceRegion?.height ?? srcMipDims.height;

        // Destination region defaults
        const dstMipLevel = destRegion?.mipLevel ?? 0;
        const dstX = destRegion?.x ?? 0;
        const dstY = destRegion?.y ?? 0;

        // Validate that copy dimensions fit within destination mip level
        const dstMipDims = TextureUtils.getMipDimensions(
            destination.width,
            destination.height,
            destination.depth,
            dstMipLevel
        );
        if (dstX + copyWidth > dstMipDims.width || dstY + copyHeight > dstMipDims.height) {
            throw new TextureError(
                'Copy region exceeds destination texture bounds',
                TextureErrorCode.INVALID_DIMENSIONS,
                this.id
            );
        }

        // Determine the destination target (must be TEXTURE_2D for copyTexSubImage2D)
        const destTarget = TextureWebGLConstants.getDimensionConstant(destination.dimension);

        // Create a temporary framebuffer and attach the SOURCE texture for reading
        const fbo = gl.createFramebuffer();
        if (!fbo) {
            throw new TextureError(
                'Failed to create temporary framebuffer for texture copy',
                TextureErrorCode.CONTEXT_LOST,
                this.id
            );
        }

        const previousFBO = this._ctx.state.snapshot.boundFramebuffer;

        try {
            this._ctx.state.bindFramebuffer(gl.FRAMEBUFFER, fbo);

            const attachmentPoint = srcFormatInfo.depth
                ? gl.DEPTH_ATTACHMENT
                : gl.COLOR_ATTACHMENT0;

            const srcTarget = this._target;

            if (srcTarget === gl.TEXTURE_CUBE_MAP) {
                const arrayLayer = sourceRegion?.arrayLayer ?? 0;
                const faceTargets = [
                    gl.TEXTURE_CUBE_MAP_POSITIVE_X,
                    gl.TEXTURE_CUBE_MAP_NEGATIVE_X,
                    gl.TEXTURE_CUBE_MAP_POSITIVE_Y,
                    gl.TEXTURE_CUBE_MAP_NEGATIVE_Y,
                    gl.TEXTURE_CUBE_MAP_POSITIVE_Z,
                    gl.TEXTURE_CUBE_MAP_NEGATIVE_Z,
                ];
                gl.framebufferTexture2D(
                    gl.FRAMEBUFFER,
                    attachmentPoint,
                    faceTargets[arrayLayer],
                    this.nativeHandle,
                    srcMipLevel
                );
            } else {
                gl.framebufferTexture2D(
                    gl.FRAMEBUFFER,
                    attachmentPoint,
                    srcTarget,
                    this.nativeHandle,
                    srcMipLevel
                );
            }

            const status = gl.checkFramebufferStatus(gl.FRAMEBUFFER);
            if (status !== gl.FRAMEBUFFER_COMPLETE) {
                throw new TextureError(
                    `Framebuffer incomplete for copy (status: 0x${status.toString(16)})`,
                    TextureErrorCode.INVALID_OPERATION,
                    this.id
                );
            }

            // Bind the destination texture
            const destWebGL = destination as WebGLTexture;
            destWebGL.bind();

            // Perform the copy
            gl.copyTexSubImage2D(
                destTarget,
                dstMipLevel,
                dstX,
                dstY,
                srcX,
                srcY,
                copyWidth,
                copyHeight
            );

            destWebGL.unbind();
        } finally {
            this._ctx.state.bindFramebuffer(gl.FRAMEBUFFER, previousFBO);
            gl.deleteFramebuffer(fbo);
        }
    }

    public generateMipmaps(): void {
        this._validateNotDisposed();

        if (this.mipLevels <= 1) {
            return;
        }

        if (this.isCompressed) {
            throw new TextureError(
                'Cannot generate mipmaps for compressed textures',
                TextureErrorCode.INVALID_OPERATION,
                this.id
            );
        }

        this.bind();
        this._gl.generateMipmap(this._target);
        this.unbind();

        this._generation++;
    }

    public hasMipmaps(): boolean {
        return this.mipLevels > 1;
    }

    public resize(width: number, height: number, depth?: number): void {
        this._validateNotDisposed();

        const newDepth = depth !== undefined ? depth : this.depth;

        TextureUtils.validateDimensions(width, height, newDepth, this.dimension);

        const newOptions: ITextureCreateOptions = {
            width,
            height,
            depth: newDepth,
            format: this.format,
            dimension: this.dimension,
            mipLevels: this.mipLevels,
            arrayLayers: this.arrayLayers,
            samples: this.samples,
            usage: this.usage,
            colorSpace: this.colorSpace,
            label: this.label || undefined,
        };

        this._initializeStorageWithOptions(newOptions);
        this._generation++;
    }

    public clone(): ITexture {
        this._validateNotDisposed();

        const cloneOptions: ITextureCreateOptions = {
            width: this.width,
            height: this.height,
            depth: this.depth,
            format: this.format,
            dimension: this.dimension,
            mipLevels: this.mipLevels,
            arrayLayers: this.arrayLayers,
            samples: this.samples,
            usage: this.usage,
            colorSpace: this.colorSpace,
            label: this.label ? `${this.label}_clone` : undefined,
        };

        const clone = new WebGLTexture(this._ctx, cloneOptions);

        return clone;
    }

    public bind(unit?: number): void {
        this._validateNotDisposed();

        let targetUnit: number;
        if (unit !== undefined) {
            this._currentUnit = unit;
            targetUnit = unit;
        } else {
            targetUnit = this._currentUnit >= 0 ? this._currentUnit : 0;
        }
        this._ctx.state.activeTexture(targetUnit);
        this._ctx.state.bindTexture(this._target, this.nativeHandle);
    }

    public unbind(): void {
        if (this._currentUnit >= 0) {
            this._ctx.state.activeTexture(this._currentUnit);
        }
        this._ctx.state.bindTexture(this._target, null);
        this._currentUnit = -1;
    }

    public dispose(): void {
        if (!this._disposal.markDisposed()) {
            return;
        }

        this._gl.deleteTexture(this.nativeHandle);
        this._currentUnit = -1;
    }

    private _createTypedArrayForType(glType: number, byteLength: number): ArrayBufferView {
        const gl = this._gl;
        switch (glType) {
            case gl.UNSIGNED_BYTE: // 0x1401
                return new Uint8Array(byteLength);
            case gl.BYTE: // 0x1400
                return new Int8Array(byteLength);
            case gl.UNSIGNED_SHORT: // 0x1403
            case gl.HALF_FLOAT: // 0x140b
                return new Uint16Array(byteLength / 2);
            case gl.SHORT: // 0x1402
                return new Int16Array(byteLength / 2);
            case gl.UNSIGNED_INT: // 0x1405
            case gl.UNSIGNED_INT_24_8: // 0x84FA
                return new Uint32Array(byteLength / 4);
            case gl.INT: // 0x1404
                return new Int32Array(byteLength / 4);
            case gl.FLOAT: // 0x1406
                return new Float32Array(byteLength / 4);
            case gl.FLOAT_32_UNSIGNED_INT_24_8_REV: // 0x8DAD
                return new Float32Array(byteLength / 4);
            default:
                return new Uint8Array(byteLength);
        }
    }

    private _validateNotDisposed(): void {
        if (this._disposal.isDisposed) {
            throw new TextureError(
                'Texture has been disposed',
                TextureErrorCode.ALREADY_DISPOSED,
                this.id
            );
        }
    }

    private _initializeStorage(): void {
        const options: ITextureCreateOptions = {
            width: this.width,
            height: this.height,
            depth: this.depth,
            format: this.format,
            dimension: this.dimension,
            mipLevels: this.mipLevels,
            arrayLayers: this.arrayLayers,
            samples: this.samples,
            usage: this.usage,
            colorSpace: this.colorSpace,
        };

        this._initializeStorageWithOptions(options);
    }

    private _initializeStorageWithOptions(options: ITextureCreateOptions): void {
        this.bind();

        const formatInfo = TextureFormatInfo.getFormatInfo(
            options.format,
            options.colorSpace ?? ColorSpace.LINEAR
        );

        try {
            switch (options.dimension) {
                case TextureDimension.TEXTURE_2D:
                    this._initializeTexture2D(options, formatInfo);
                    break;

                case TextureDimension.TEXTURE_3D:
                    this._initializeTexture3D(options, formatInfo);
                    break;

                case TextureDimension.TEXTURE_CUBE:
                    this._initializeTextureCube(options, formatInfo);
                    break;

                case TextureDimension.TEXTURE_2D_ARRAY:
                    this._initializeTexture2DArray(options, formatInfo);
                    break;

                default:
                    throw new TextureError(
                        `Unsupported texture dimension: ${options.dimension}`,
                        TextureErrorCode.UNSUPPORTED_FORMAT,
                        this.id
                    );
            }
            // Ensure mipmap completeness: clamp MAX_LEVEL to the allocated mip chain.
            // Without this, TEXTURE_MAX_LEVEL defaults to 1000 and a mipmapped min filter
            // treats the texture as incomplete (black) until all 1000 levels are defined.
            // This must apply even for single-mip textures (mipLevels === 1) where MAX_LEVEL
            // would otherwise be 1000 and the texture would be incomplete with a mipmap minFilter.
            if (!formatInfo.compressed) {
                const maxLevel = Math.max(0, (options.mipLevels ?? 1) - 1);
                this._gl.texParameteri(this._target, this._gl.TEXTURE_MAX_LEVEL, maxLevel);
                this._gl.texParameteri(this._target, this._gl.TEXTURE_BASE_LEVEL, 0);
            }
        } finally {
            this.unbind();
        }
    }

    private _initializeTexture2D(options: ITextureCreateOptions, formatInfo: any): void {
        if (formatInfo.compressed) {
            return;
        }

        for (let mip = 0; mip < options.mipLevels!; mip++) {
            const mipDims = TextureUtils.getMipDimensions(options.width, options.height, 1, mip);

            this._gl.texImage2D(
                this._target,
                mip,
                formatInfo.internalFormat,
                mipDims.width,
                mipDims.height,
                0,
                formatInfo.format,
                formatInfo.type,
                null
            );
        }
    }

    private _initializeTexture3D(options: ITextureCreateOptions, formatInfo: any): void {
        if (formatInfo.compressed) {
            return;
        }

        for (let mip = 0; mip < options.mipLevels!; mip++) {
            const mipDims = TextureUtils.getMipDimensions(
                options.width,
                options.height,
                options.depth!,
                mip
            );

            this._gl.texImage3D(
                this._target,
                mip,
                formatInfo.internalFormat,
                mipDims.width,
                mipDims.height,
                mipDims.depth,
                0,
                formatInfo.format,
                formatInfo.type,
                null
            );
        }
    }

    private _initializeTextureCube(options: ITextureCreateOptions, formatInfo: any): void {
        if (formatInfo.compressed) {
            return;
        }

        const faces = [
            this._gl.TEXTURE_CUBE_MAP_POSITIVE_X,
            this._gl.TEXTURE_CUBE_MAP_NEGATIVE_X,
            this._gl.TEXTURE_CUBE_MAP_POSITIVE_Y,
            this._gl.TEXTURE_CUBE_MAP_NEGATIVE_Y,
            this._gl.TEXTURE_CUBE_MAP_POSITIVE_Z,
            this._gl.TEXTURE_CUBE_MAP_NEGATIVE_Z,
        ];

        for (let face = 0; face < 6; face++) {
            for (let mip = 0; mip < options.mipLevels!; mip++) {
                const mipDims = TextureUtils.getMipDimensions(
                    options.width,
                    options.height,
                    1,
                    mip
                );

                this._gl.texImage2D(
                    faces[face],
                    mip,
                    formatInfo.internalFormat,
                    mipDims.width,
                    mipDims.height,
                    0,
                    formatInfo.format,
                    formatInfo.type,
                    null
                );
            }
        }
    }

    private _initializeTexture2DArray(options: ITextureCreateOptions, formatInfo: any): void {
        if (formatInfo.compressed) {
            return;
        }

        for (let mip = 0; mip < options.mipLevels!; mip++) {
            const mipDims = TextureUtils.getMipDimensions(options.width, options.height, 1, mip);

            this._gl.texImage3D(
                this._target,
                mip,
                formatInfo.internalFormat,
                mipDims.width,
                mipDims.height,
                options.arrayLayers!,
                0,
                formatInfo.format,
                formatInfo.type,
                null
            );
        }
    }

    private _uploadData(
        data: TextureDataSource,
        mipLevel: number,
        arrayLayer: number,
        x: number,
        y: number,
        z: number,
        width: number,
        height: number,
        depth: number
    ): void {
        const formatInfo = TextureFormatInfo.getFormatInfo(this.format, this.colorSpace);

        if (data === null) {
            return;
        }

        if (data instanceof ByteBuffer) {
            this._uploadBufferData(
                data,
                formatInfo,
                mipLevel,
                arrayLayer,
                x,
                y,
                z,
                width,
                height,
                depth
            );
        } else if (
            data instanceof HTMLImageElement ||
            data instanceof HTMLCanvasElement ||
            data instanceof HTMLVideoElement ||
            data instanceof ImageBitmap
        ) {
            this._uploadImageData(data, formatInfo, mipLevel, arrayLayer, x, y);
        } else if (data instanceof ImageData) {
            this._uploadImageData(data, formatInfo, mipLevel, arrayLayer, x, y);
        } else if (ArrayBuffer.isView(data)) {
            this._uploadTypedArrayData(
                data,
                formatInfo,
                mipLevel,
                arrayLayer,
                x,
                y,
                z,
                width,
                height,
                depth
            );
        } else {
            throw new TextureError(
                'Unsupported data source type',
                TextureErrorCode.INVALID_DATA,
                this.id
            );
        }
    }

    private _uploadBufferData(
        buffer: ByteBuffer,
        formatInfo: any,
        mipLevel: number,
        arrayLayer: number,
        x: number,
        y: number,
        z: number,
        width: number,
        height: number,
        depth: number
    ): void {
        const typedView = buffer.asTypedView('uint8');
        const values = typedView.getValues(0, typedView.capacity);
        const data = new Uint8Array(values);
        this._uploadTypedArrayData(
            data,
            formatInfo,
            mipLevel,
            arrayLayer,
            x,
            y,
            z,
            width,
            height,
            depth
        );
    }

    private _uploadImageData(
        image: TexImageSource,
        formatInfo: any,
        mipLevel: number,
        arrayLayer: number,
        x: number,
        y: number
    ): void {
        if (this.isCompressed) {
            throw new TextureError(
                'Image upload is not supported for compressed textures',
                TextureErrorCode.INVALID_OPERATION,
                this.id
            );
        }

        switch (this.dimension) {
            case TextureDimension.TEXTURE_2D:
                this._gl.texSubImage2D(
                    this._target,
                    mipLevel,
                    x,
                    y,
                    formatInfo.format,
                    formatInfo.type,
                    image
                );
                break;

            case TextureDimension.TEXTURE_CUBE:
                const faces = [
                    this._gl.TEXTURE_CUBE_MAP_POSITIVE_X,
                    this._gl.TEXTURE_CUBE_MAP_NEGATIVE_X,
                    this._gl.TEXTURE_CUBE_MAP_POSITIVE_Y,
                    this._gl.TEXTURE_CUBE_MAP_NEGATIVE_Y,
                    this._gl.TEXTURE_CUBE_MAP_POSITIVE_Z,
                    this._gl.TEXTURE_CUBE_MAP_NEGATIVE_Z,
                ];

                this._gl.texSubImage2D(
                    faces[arrayLayer],
                    mipLevel,
                    x,
                    y,
                    formatInfo.format,
                    formatInfo.type,
                    image
                );
                break;

            default:
                throw new TextureError(
                    `Image upload not supported for dimension: ${this.dimension}`,
                    TextureErrorCode.INVALID_OPERATION,
                    this.id
                );
        }
    }

    private _uploadTypedArrayData(
        data: ArrayBufferView,
        formatInfo: any,
        mipLevel: number,
        arrayLayer: number,
        x: number,
        y: number,
        z: number,
        width: number,
        height: number,
        depth: number
    ): void {
        if (this.isCompressed) {
            this._uploadCompressedTypedArrayData(
                data,
                mipLevel,
                arrayLayer,
                x,
                y,
                z,
                width,
                height,
                depth
            );
            return;
        }

        switch (this.dimension) {
            case TextureDimension.TEXTURE_2D:
                this._gl.texSubImage2D(
                    this._target,
                    mipLevel,
                    x,
                    y,
                    width,
                    height,
                    formatInfo.format,
                    formatInfo.type,
                    data
                );
                break;

            case TextureDimension.TEXTURE_3D:
                this._gl.texSubImage3D(
                    this._target,
                    mipLevel,
                    x,
                    y,
                    z,
                    width,
                    height,
                    depth,
                    formatInfo.format,
                    formatInfo.type,
                    data
                );
                break;

            case TextureDimension.TEXTURE_2D_ARRAY:
                this._gl.texSubImage3D(
                    this._target,
                    mipLevel,
                    x,
                    y,
                    arrayLayer,
                    width,
                    height,
                    1,
                    formatInfo.format,
                    formatInfo.type,
                    data
                );
                break;

            case TextureDimension.TEXTURE_CUBE:
                const faces = [
                    this._gl.TEXTURE_CUBE_MAP_POSITIVE_X,
                    this._gl.TEXTURE_CUBE_MAP_NEGATIVE_X,
                    this._gl.TEXTURE_CUBE_MAP_POSITIVE_Y,
                    this._gl.TEXTURE_CUBE_MAP_NEGATIVE_Y,
                    this._gl.TEXTURE_CUBE_MAP_POSITIVE_Z,
                    this._gl.TEXTURE_CUBE_MAP_NEGATIVE_Z,
                ];

                this._gl.texSubImage2D(
                    faces[arrayLayer],
                    mipLevel,
                    x,
                    y,
                    width,
                    height,
                    formatInfo.format,
                    formatInfo.type,
                    data
                );
                break;

            default:
                throw new TextureError(
                    `Data upload not supported for dimension: ${this.dimension}`,
                    TextureErrorCode.INVALID_OPERATION,
                    this.id
                );
        }
    }

    private _uploadCompressedTypedArrayData(
        data: ArrayBufferView,
        mipLevel: number,
        arrayLayer: number,
        x: number,
        y: number,
        z: number,
        width: number,
        height: number,
        depth: number
    ): void {
        if (x !== 0 || y !== 0 || z !== 0) {
            throw new TextureError(
                'Compressed texture uploads must cover the full mip level',
                TextureErrorCode.INVALID_OPERATION,
                this.id
            );
        }

        if (depth !== 1) {
            throw new TextureError(
                'Compressed texture uploads only support 2D surfaces',
                TextureErrorCode.INVALID_OPERATION,
                this.id
            );
        }

        const internalFormat = TextureWebGLConstants.getCompressedInternalFormat(
            this._gl,
            this.format
        );

        switch (this.dimension) {
            case TextureDimension.TEXTURE_2D:
                this._gl.compressedTexImage2D(
                    this._target,
                    mipLevel,
                    internalFormat,
                    width,
                    height,
                    0,
                    data
                );
                break;

            case TextureDimension.TEXTURE_CUBE:
                const faces = [
                    this._gl.TEXTURE_CUBE_MAP_POSITIVE_X,
                    this._gl.TEXTURE_CUBE_MAP_NEGATIVE_X,
                    this._gl.TEXTURE_CUBE_MAP_POSITIVE_Y,
                    this._gl.TEXTURE_CUBE_MAP_NEGATIVE_Y,
                    this._gl.TEXTURE_CUBE_MAP_POSITIVE_Z,
                    this._gl.TEXTURE_CUBE_MAP_NEGATIVE_Z,
                ];

                this._gl.compressedTexImage2D(
                    faces[arrayLayer],
                    mipLevel,
                    internalFormat,
                    width,
                    height,
                    0,
                    data
                );
                break;

            default:
                throw new TextureError(
                    `Compressed texture uploads are not supported for dimension: ${this.dimension}`,
                    TextureErrorCode.INVALID_OPERATION,
                    this.id
                );
        }
    }
}
