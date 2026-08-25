import { GLError as BufferGLError } from './buffer';
import { FramebufferError } from './framebuffer';
import { GLContextError } from './context/errors';

export type ErrorCode = BufferGLError['code'] | FramebufferError['code'] | GLContextError['code'] | 'DISPOSED' | 'INVALID_STATE';

const formatMessage = (code: string, detail?: string): string => detail ? `[${code}] ${detail}` : `[${code}]`;

export const createDisposedError = (resource: string, locale: string = 'en'): Error => {
    const msg = locale === 'tr' ? `${resource} dispose edildi` : `${resource} has been disposed`;
    return new BufferGLError(msg, 'BUFFER_ALREADY_DISPOSED');
};

export const createInvalidStateError = (msg: string, code: ErrorCode = 'INVALID_STATE'): Error =>
    new Error(formatMessage(code, msg));

export const createContextLostError = (locale: string = 'en'): GLContextError =>
    new GLContextError('CONTEXT_LOST', locale as never);

export const Errors = Object.freeze({
    createDisposedError,
    createInvalidStateError,
    createContextLostError,
});
