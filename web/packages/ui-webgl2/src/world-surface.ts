/**
 * Offscreen render surface for world-space UI.
 *
 * A UI frame is rendered into this framebuffer's colour texture, which is then
 * sampled by the world-space quad pass ({@link ./world-quad}). Keeping the
 * surface separate from the renderer lets a single renderer instance serve both
 * the screen overlay and any number of world-space hosts.
 */
export interface UIWorldSurface extends Disposable {
    readonly texture: WebGLTexture;
    readonly framebuffer: WebGLFramebuffer;
    readonly width: number;
    readonly height: number;
    /** Reallocates the colour attachment; a no-op when the size is unchanged. */
    resize(width: number, height: number): void;
    dispose(): void;
}

const MIN_SURFACE_SIZE = 1;
const MAX_SURFACE_SIZE = 4096;

const clampSurfaceSize = (value: number): number => {
    if (!Number.isFinite(value)) {
        return MIN_SURFACE_SIZE;
    }
    return Math.max(MIN_SURFACE_SIZE, Math.min(MAX_SURFACE_SIZE, Math.floor(value)));
};

export function createUIWorldSurface(
    gl: WebGL2RenderingContext,
    width: number,
    height: number
): UIWorldSurface {
    const texture = gl.createTexture();
    const framebuffer = gl.createFramebuffer();
    if (!texture || !framebuffer) {
        throw new Error('Failed to allocate the world-space UI surface.');
    }

    let currentWidth = 0;
    let currentHeight = 0;
    let disposed = false;

    const previousTexture = gl.getParameter(gl.TEXTURE_BINDING_2D) as WebGLTexture | null;
    const previousFramebuffer = gl.getParameter(
        gl.FRAMEBUFFER_BINDING
    ) as WebGLFramebuffer | null;

    const allocate = (nextWidth: number, nextHeight: number): void => {
        const clampedWidth = clampSurfaceSize(nextWidth);
        const clampedHeight = clampSurfaceSize(nextHeight);
        if (clampedWidth === currentWidth && clampedHeight === currentHeight) {
            return;
        }
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.texImage2D(
            gl.TEXTURE_2D,
            0,
            gl.RGBA,
            clampedWidth,
            clampedHeight,
            0,
            gl.RGBA,
            gl.UNSIGNED_BYTE,
            null
        );
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.bindFramebuffer(gl.FRAMEBUFFER, framebuffer);
        gl.framebufferTexture2D(
            gl.FRAMEBUFFER,
            gl.COLOR_ATTACHMENT0,
            gl.TEXTURE_2D,
            texture,
            0
        );
        currentWidth = clampedWidth;
        currentHeight = clampedHeight;
    };

    allocate(width, height);

    // Leave the context exactly as it was found.
    gl.bindFramebuffer(gl.FRAMEBUFFER, previousFramebuffer);
    gl.bindTexture(gl.TEXTURE_2D, previousTexture);

    const surface: UIWorldSurface = {
        texture,
        framebuffer,
        get width() {
            return currentWidth;
        },
        get height() {
            return currentHeight;
        },
        resize(nextWidth: number, nextHeight: number): void {
            if (disposed) {
                return;
            }
            const restoreTexture = gl.getParameter(gl.TEXTURE_BINDING_2D) as WebGLTexture | null;
            const restoreFramebuffer = gl.getParameter(
                gl.FRAMEBUFFER_BINDING
            ) as WebGLFramebuffer | null;
            allocate(nextWidth, nextHeight);
            gl.bindFramebuffer(gl.FRAMEBUFFER, restoreFramebuffer);
            gl.bindTexture(gl.TEXTURE_2D, restoreTexture);
        },
        dispose(): void {
            if (disposed) {
                return;
            }
            disposed = true;
            gl.deleteFramebuffer(framebuffer);
            gl.deleteTexture(texture);
        },
        [Symbol.dispose]() {
            this.dispose();
        },
    };

    return surface;
}
