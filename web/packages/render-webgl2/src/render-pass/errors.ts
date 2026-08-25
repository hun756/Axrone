import type { RenderPassKind, ResolvedRenderPass } from '@axrone/render-core/types';

export type WebGL2RuntimeRenderPassErrorCode =
	| 'EXECUTOR_NOT_FOUND'
	| 'FRAMEBUFFER_UNSUPPORTED_ATTACHMENT'
	| 'FRAMEBUFFER_CREATE_FAILED'
	| 'SOURCE_TEXTURE_INVALID'
	| 'PROGRAM_CREATE_FAILED'
	| 'SHADER_CREATE_FAILED'
	| 'SHADER_COMPILE_FAILED'
	| 'PROGRAM_LINK_FAILED'
	| 'PASS_EXECUTION_FAILED';

export type WebGL2RenderPassLocale = 'en' | 'tr';

export interface WebGL2RenderPassErrorOptions {
	readonly code: WebGL2RuntimeRenderPassErrorCode;
	readonly message: string;
	readonly locale: WebGL2RenderPassLocale;
	readonly cause?: unknown;
	readonly pass?: ResolvedRenderPass;
}

const resolveExecutionFailureMessage = (
	locale: WebGL2RenderPassLocale,
	pass: ResolvedRenderPass
): string =>
	locale === 'tr'
		? `'${pass.name}' render pass yurutulemedi`
		: `Failed to execute WebGL2 render pass '${pass.name}'`;

export class WebGL2RenderPassError extends Error {
	readonly code: WebGL2RuntimeRenderPassErrorCode;
	readonly locale: WebGL2RenderPassLocale;
	readonly passKind: RenderPassKind | null;
	readonly passName: string | null;
	readonly cause: unknown;

	constructor(options: WebGL2RenderPassErrorOptions) {
		super(options.message);
		Object.setPrototypeOf(this, new.target.prototype);
		this.name = new.target.name;
		this.code = options.code;
		this.locale = options.locale;
		this.passKind = options.pass?.kind ?? null;
		this.passName = options.pass ? String(options.pass.name) : null;
		this.cause = options.cause;
	}
}

export class WebGL2RenderPassValidationError extends WebGL2RenderPassError {}

export class WebGL2RenderPassExecutionError extends WebGL2RenderPassError {}

export const createValidationError = (
	locale: WebGL2RenderPassLocale,
	code: Exclude<WebGL2RuntimeRenderPassErrorCode, 'PASS_EXECUTION_FAILED'>,
	message: string,
	pass?: ResolvedRenderPass,
	cause?: unknown
): WebGL2RenderPassValidationError =>
	new WebGL2RenderPassValidationError({
		code,
		message,
		locale,
		pass,
		cause,
	});

export const createExecutionError = (
	locale: WebGL2RenderPassLocale,
	pass: ResolvedRenderPass,
	cause: unknown
): WebGL2RenderPassExecutionError =>
	new WebGL2RenderPassExecutionError({
		code: 'PASS_EXECUTION_FAILED',
		message: resolveExecutionFailureMessage(locale, pass),
		locale,
		pass,
		cause,
	});
