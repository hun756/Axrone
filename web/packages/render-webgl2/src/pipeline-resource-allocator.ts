import type {
    RenderResourceAllocator,
    RenderTextureDescriptor,
} from '@axrone/render-core/types';
import type {
    WebGL2DefaultFramebufferHandle,
    WebGL2RenderResourceHandle,
    WebGL2RenderTextureNativeHandle,
} from './pipeline-contracts';

interface WebGL2FormatInfo {
    readonly internalFormat: number;
    readonly format: number;
    readonly type: number;
}

const DEFAULT_FRAMEBUFFER_HANDLE: WebGL2DefaultFramebufferHandle = Object.freeze({
    kind: 'default-framebuffer',
});

const resolveFormatInfo = (
    gl: WebGL2RenderingContext,
    format: RenderTextureDescriptor['format']
): WebGL2FormatInfo => {
    switch (format) {
        case 'r11g11b10f':
            return {
                internalFormat: gl.R11F_G11F_B10F,
                format: gl.RGB,
                type: gl.UNSIGNED_INT_10F_11F_11F_REV,
            };
        case 'rgba8':
            return {
                internalFormat: gl.RGBA8,
                format: gl.RGBA,
                type: gl.UNSIGNED_BYTE,
            };
        case 'rgba16f':
            return {
                internalFormat: gl.RGBA16F,
                format: gl.RGBA,
                type: gl.HALF_FLOAT,
            };
        case 'rgba32f':
            return {
                internalFormat: gl.RGBA32F,
                format: gl.RGBA,
                type: gl.FLOAT,
            };
        case 'rg16f':
            return {
                internalFormat: gl.RG16F,
                format: gl.RG,
                type: gl.HALF_FLOAT,
            };
        case 'rg32f':
            return {
                internalFormat: gl.RG32F,
                format: gl.RG,
                type: gl.FLOAT,
            };
        case 'r16f':
            return {
                internalFormat: gl.R16F,
                format: gl.RED,
                type: gl.HALF_FLOAT,
            };
        case 'r32f':
            return {
                internalFormat: gl.R32F,
                format: gl.RED,
                type: gl.FLOAT,
            };
        case 'depth24':
            return {
                internalFormat: gl.DEPTH_COMPONENT24,
                format: gl.DEPTH_COMPONENT,
                type: gl.UNSIGNED_INT,
            };
        case 'depth32f':
            return {
                internalFormat: gl.DEPTH_COMPONENT32F,
                format: gl.DEPTH_COMPONENT,
                type: gl.FLOAT,
            };
        case 'depth24-stencil8':
            return {
                internalFormat: gl.DEPTH24_STENCIL8,
                format: gl.DEPTH_STENCIL,
                type: gl.UNSIGNED_INT_24_8,
            };
    }
};

const createTextureTarget = (
    gl: WebGL2RenderingContext,
    descriptor: Readonly<RenderTextureDescriptor>
): number => {
    if (descriptor.cube) {
        return gl.TEXTURE_CUBE_MAP;
    }

    if ((descriptor.arrayLayers ?? 1) > 1) {
        return gl.TEXTURE_2D_ARRAY;
    }

    if ((descriptor.depth ?? 1) > 1) {
        return gl.TEXTURE_3D;
    }

    return gl.TEXTURE_2D;
};

const createTextureStorage = (
    gl: WebGL2RenderingContext,
    texture: WebGLTexture,
    descriptor: Readonly<RenderTextureDescriptor>
): WebGL2RenderTextureNativeHandle => {
    const target = createTextureTarget(gl, descriptor);
    const formatInfo = resolveFormatInfo(gl, descriptor.format);
    const mipLevels = Math.max(1, descriptor.mipLevels ?? 1);

    gl.bindTexture(target, texture);
    gl.texParameteri(target, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(target, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    gl.texParameteri(target, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(target, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

    if (target === gl.TEXTURE_3D || target === gl.TEXTURE_2D_ARRAY) {
        gl.texParameteri(target, gl.TEXTURE_WRAP_R, gl.CLAMP_TO_EDGE);
    }

    if (target === gl.TEXTURE_2D || target === gl.TEXTURE_CUBE_MAP) {
        gl.texStorage2D(target, mipLevels, formatInfo.internalFormat, descriptor.width, descriptor.height);
    } else {
        gl.texStorage3D(
            target,
            mipLevels,
            formatInfo.internalFormat,
            descriptor.width,
            descriptor.height,
            descriptor.depth ?? descriptor.arrayLayers ?? 1
        );
    }

    gl.bindTexture(target, null);

    return {
        kind: 'texture',
        texture,
        target,
    };
};

export const createWebGL2RenderResourceAllocator = (
    gl: WebGL2RenderingContext
): RenderResourceAllocator<WebGL2RenderResourceHandle> => ({
    createTexture(descriptor, previous) {
        if (descriptor.usage.includes('present')) {
            if (previous?.kind === 'texture') {
                gl.deleteTexture(previous.texture);
            }
            return DEFAULT_FRAMEBUFFER_HANDLE;
        }

        if ((descriptor.samples ?? 1) !== 1) {
            throw new Error('WebGL2 render resource allocator does not support multisampled textures yet');
        }

        if (previous?.kind === 'texture') {
            gl.deleteTexture(previous.texture);
        }

        const texture = gl.createTexture();
        if (!texture) {
            throw new Error('Failed to allocate WebGL2 render texture');
        }

        return createTextureStorage(gl, texture, descriptor);
    },
    destroyTexture(native) {
        if (native.kind === 'texture') {
            gl.deleteTexture(native.texture);
        }
    },
});