import type { WebGL2NativeFramebufferHandle } from '../pipeline-contracts';

/**
 * Internal bridge between the opaque `WebGL2NativeFramebufferHandle`
 * published on `WebGL2ResolvedFramebufferBinding` and the raw
 * `WebGLFramebuffer` the backend binds against. Raw framebuffer access
 * must never escape the render-webgl2 package (refactor plan phase 2.3).
 */
export const toNativeFramebufferHandle = (
    framebuffer: WebGLFramebuffer | null
): WebGL2NativeFramebufferHandle | null =>
    framebuffer as unknown as WebGL2NativeFramebufferHandle | null;

export const resolveNativeFramebuffer = (
    handle: WebGL2NativeFramebufferHandle | null
): WebGLFramebuffer | null => handle as unknown as WebGLFramebuffer | null;
