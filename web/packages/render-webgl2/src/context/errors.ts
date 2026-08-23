export type GLContextErrorCode =
    | 'CONTEXT_LOST'
    | 'CONTEXT_ALREADY_DISPOSED'
    | 'CONTEXT_CREATION_FAILED'
    | 'INVALID_OPERATION'
    | 'INVALID_VALUE'
    | 'OUT_OF_MEMORY'
    | 'UNSUPPORTED_OPERATION'
    | 'EXTENSION_NOT_SUPPORTED'
    | 'CAPABILITY_QUERY_FAILED';

export type GLContextLocale = 'en' | 'tr';

export type GLContextErrorContext = Readonly<Record<string, unknown>> | undefined;

const ERROR_MESSAGES: Readonly<Record<GLContextLocale, Readonly<Record<GLContextErrorCode, string>>>> =
    Object.freeze({
        en: Object.freeze({
            CONTEXT_LOST: 'WebGL2 context is lost',
            CONTEXT_ALREADY_DISPOSED: 'WebGL2 context has been disposed',
            CONTEXT_CREATION_FAILED: 'Failed to create WebGL2 context',
            INVALID_OPERATION: 'Invalid WebGL2 operation',
            INVALID_VALUE: 'Invalid value provided to WebGL2 context',
            OUT_OF_MEMORY: 'WebGL2 context ran out of memory',
            UNSUPPORTED_OPERATION: 'Unsupported operation for WebGL2 context',
            EXTENSION_NOT_SUPPORTED: 'Requested WebGL extension is not supported',
            CAPABILITY_QUERY_FAILED: 'Failed to query WebGL2 capabilities',
        }),
        tr: Object.freeze({
            CONTEXT_LOST: 'WebGL2 baglami kayboldu',
            CONTEXT_ALREADY_DISPOSED: 'WebGL2 baglami dispose edildi',
            CONTEXT_CREATION_FAILED: 'WebGL2 baglami olusturulamadi',
            INVALID_OPERATION: 'Gecersiz WebGL2 islemi',
            INVALID_VALUE: 'WebGL2 baglamina gecersiz deger verildi',
            OUT_OF_MEMORY: 'WebGL2 baglami bellek tuketti',
            UNSUPPORTED_OPERATION: 'WebGL2 baglami icin desteklenmeyen islem',
            EXTENSION_NOT_SUPPORTED: 'Istenen WebGL eklentisi desteklenmiyor',
            CAPABILITY_QUERY_FAILED: 'WebGL2 yetenekleri sorgulanamadi',
        }),
    });

const serializeContext = (context: GLContextErrorContext): string => {
    if (!context || Object.keys(context).length === 0) return '';
    try {
        return ` ${JSON.stringify(context)}`;
    } catch { // best-effort
        return '';
    }
};

export class GLContextError extends Error {
    public readonly code: GLContextErrorCode;
    public readonly locale: GLContextLocale;
    public readonly context?: GLContextErrorContext;
    public override readonly cause?: unknown;

    constructor(
        code: GLContextErrorCode,
        locale: GLContextLocale = 'en',
        context?: GLContextErrorContext,
        cause?: unknown
    ) {
        const catalog = ERROR_MESSAGES[locale] ?? ERROR_MESSAGES.en;
        const base = catalog[code] ?? catalog.INVALID_OPERATION;
        const details = serializeContext(context);
        super(details ? `${base}${details}` : base);
        Object.setPrototypeOf(this, new.target.prototype);
        this.name = new.target.name;
        this.code = code;
        this.locale = locale;
        this.context = context ? Object.freeze({ ...context }) : undefined;
        this.cause = cause;
        (Error as unknown as { captureStackTrace?: (t: object, c: Function) => void }).captureStackTrace?.(
            this,
            new.target
        );
    }

    public toJSON(): Record<string, unknown> {
        return {
            name: this.name,
            code: this.code,
            message: this.message,
            locale: this.locale,
            context: this.context,
            cause: this.cause instanceof Error ? this.cause.message : this.cause,
        };
    }
}

export class GLContextLostError extends GLContextError {
    constructor(locale: GLContextLocale = 'en', context?: GLContextErrorContext, cause?: unknown) {
        super('CONTEXT_LOST', locale, context, cause);
        Object.setPrototypeOf(this, new.target.prototype);
        this.name = new.target.name;
    }
}

export class GLContextDisposedError extends GLContextError {
    constructor(locale: GLContextLocale = 'en', context?: GLContextErrorContext, cause?: unknown) {
        super('CONTEXT_ALREADY_DISPOSED', locale, context, cause);
        Object.setPrototypeOf(this, new.target.prototype);
        this.name = new.target.name;
    }
}

export const isGLContextError = (value: unknown): value is GLContextError =>
    value instanceof GLContextError;

export const isContextLostError = (value: unknown): value is GLContextLostError =>
    value instanceof GLContextLostError;

export const assertNotDisposed = (
    isDisposed: boolean,
    locale: GLContextLocale = 'en',
    context?: GLContextErrorContext
): void => {
    if (isDisposed) throw new GLContextDisposedError(locale, context);
};

export const assertNotLost = (
    isLost: boolean,
    locale: GLContextLocale = 'en',
    context?: GLContextErrorContext
): void => {
    if (isLost) throw new GLContextLostError(locale, context);
};

export const throwContextCreationFailed = (
    locale: GLContextLocale = 'en',
    context?: GLContextErrorContext,
    cause?: unknown
): never => {
    throw new GLContextError('CONTEXT_CREATION_FAILED', locale, context, cause);
};

export const throwExtensionNotSupported = (
    name: string,
    locale: GLContextLocale = 'en',
    cause?: unknown
): never => {
    throw new GLContextError('EXTENSION_NOT_SUPPORTED', locale, { extension: name }, cause);
};
