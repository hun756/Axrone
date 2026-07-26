export type ShaderInstanceErrorCode =
    | 'SHADER_COMPILE_FAILED'
    | 'SHADER_LINK_FAILED'
    | 'UNIFORM_NOT_FOUND'
    | 'ATTRIBUTE_NOT_FOUND'
    | 'INVALID_VALUE_TYPE'
    | 'PROGRAM_DISPOSED'
    | 'PROGRAM_NOT_LINKED'
    | 'BACKEND_UNAVAILABLE'
    | 'CACHE_EVICTION_FAILED'
    | 'INVALID_ARGUMENT'
    | 'OUT_OF_MEMORY'
    | 'BATCH_OVERFLOW'
    | 'BATCH_NOT_STARTED'
    | 'BATCH_ALREADY_STARTED';

export type ShaderInstanceLocale = 'en' | 'tr';

export type ShaderInstanceErrorContext = Readonly<Record<string, unknown>> | undefined;

const ERROR_MESSAGES: Readonly<
    Record<ShaderInstanceLocale, Readonly<Record<ShaderInstanceErrorCode, string>>>
> = Object.freeze({
    en: Object.freeze({
        SHADER_COMPILE_FAILED: 'shader compilation failed',
        SHADER_LINK_FAILED: 'shader program linking failed',
        UNIFORM_NOT_FOUND: 'requested uniform was not found in the active program',
        ATTRIBUTE_NOT_FOUND: 'requested attribute was not found in the active program',
        INVALID_VALUE_TYPE: 'uniform value type does not match the declared glsl type',
        PROGRAM_DISPOSED: 'shader program has been disposed',
        PROGRAM_NOT_LINKED: 'shader program has not been linked',
        BACKEND_UNAVAILABLE: 'requested graphics backend is not available',
        CACHE_EVICTION_FAILED: 'shader cache eviction failed',
        INVALID_ARGUMENT: 'invalid argument provided to shader instance',
        OUT_OF_MEMORY: 'shader instance ran out of memory',
        BATCH_OVERFLOW: 'uniform batch capacity exceeded',
        BATCH_NOT_STARTED: 'uniform batch has not been started',
        BATCH_ALREADY_STARTED: 'uniform batch is already active',
    }),
    tr: Object.freeze({
        SHADER_COMPILE_FAILED: 'shader derlemesi basarisiz oldu',
        SHADER_LINK_FAILED: 'shader programi linklenemedi',
        UNIFORM_NOT_FOUND: 'aktif programda istenen uniform bulunamadi',
        ATTRIBUTE_NOT_FOUND: 'aktif programda istenen attribute bulunamadi',
        INVALID_VALUE_TYPE: 'uniform deger turu beyan edilen glsl turuyle eslesmiyor',
        PROGRAM_DISPOSED: 'shader programi dispose edildi',
        PROGRAM_NOT_LINKED: 'shader programi henuz linklenmedi',
        BACKEND_UNAVAILABLE: 'istenen grafik backend mevcut degil',
        CACHE_EVICTION_FAILED: 'shader onbellegi bosaltma basarisiz oldu',
        INVALID_ARGUMENT: 'shader instance icin gecersiz arguman verildi',
        OUT_OF_MEMORY: 'shader instance yetersiz bellek hatasi verdi',
        BATCH_OVERFLOW: 'uniform batch kapasitesi asildi',
        BATCH_NOT_STARTED: 'uniform batch baslatilmamis',
        BATCH_ALREADY_STARTED: 'uniform batch zaten aktif',
    }),
});

const serializeContext = (context: ShaderInstanceErrorContext): string => {
    if (!context || Object.keys(context).length === 0) {
        return '';
    }
    try {
        return ` ${JSON.stringify(context)}`;
    } catch {
        return '';
    }
};

export class ShaderInstanceError extends Error {
    readonly code: ShaderInstanceErrorCode;
    readonly locale: ShaderInstanceLocale;
    readonly context?: ShaderInstanceErrorContext;

    constructor(
        code: ShaderInstanceErrorCode,
        locale: ShaderInstanceLocale = 'en',
        context?: ShaderInstanceErrorContext,
        cause?: Error
    ) {
        const catalog = ERROR_MESSAGES[locale] ?? ERROR_MESSAGES.en;
        const message = `[ShaderInstance:${code}] ${catalog[code]}${serializeContext(context)}`;
        super(message);
        this.name = 'ShaderInstanceError';
        this.code = code;
        this.locale = locale;
        this.context = context;
        if (cause) {
            this.cause = cause;
        }
        Object.setPrototypeOf(this, new.target.prototype);
    }
}

export class ShaderInstanceValidationError extends ShaderInstanceError {
    constructor(
        code: Extract<
            ShaderInstanceErrorCode,
            | 'INVALID_VALUE_TYPE'
            | 'UNIFORM_NOT_FOUND'
            | 'ATTRIBUTE_NOT_FOUND'
            | 'INVALID_ARGUMENT'
        >,
        locale: ShaderInstanceLocale = 'en',
        context?: ShaderInstanceErrorContext
    ) {
        super(code, locale, context);
        this.name = 'ShaderInstanceValidationError';
    }
}

export class ShaderInstanceLifecycleError extends ShaderInstanceError {
    constructor(
        code: Extract<
            ShaderInstanceErrorCode,
            'PROGRAM_DISPOSED' | 'PROGRAM_NOT_LINKED' | 'OUT_OF_MEMORY' | 'BATCH_OVERFLOW'
        >,
        locale: ShaderInstanceLocale = 'en',
        context?: ShaderInstanceErrorContext
    ) {
        super(code, locale, context);
        this.name = 'ShaderInstanceLifecycleError';
    }
}

export class ShaderInstanceBackendError extends ShaderInstanceError {
    constructor(
        code: Extract<
            ShaderInstanceErrorCode,
            'SHADER_COMPILE_FAILED' | 'SHADER_LINK_FAILED' | 'BACKEND_UNAVAILABLE'
        >,
        locale: ShaderInstanceLocale = 'en',
        context?: ShaderInstanceErrorContext,
        cause?: Error
    ) {
        super(code, locale, context, cause);
        this.name = 'ShaderInstanceBackendError';
    }
}

export const isShaderInstanceError = (value: unknown): value is ShaderInstanceError =>
    value instanceof ShaderInstanceError;
