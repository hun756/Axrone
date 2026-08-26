import type { ContextSource, IGLContext } from '../context';
import { resolveContext } from '../context';
import {
	EXPOSURE_HISTORY_FRAGMENT_SHADER_SOURCE,
	FULLSCREEN_VERTEX_SHADER_SOURCE,
	POST_PROCESS_FRAGMENT_SHADER_SOURCE,
	TONEMAP_FRAGMENT_SHADER_SOURCE,
} from '../internal/render-pass-shaders';
import { createValidationError, type WebGL2RenderPassLocale } from './errors';

interface WebGL2TonemapUniforms {
	readonly source: WebGLUniformLocation | null;
	readonly exposureHistory: WebGLUniformLocation | null;
	readonly mode: WebGLUniformLocation | null;
	readonly exposureScale: WebGLUniformLocation | null;
	readonly gamma: WebGLUniformLocation | null;
	readonly contrast: WebGLUniformLocation | null;
	readonly saturation: WebGLUniformLocation | null;
	readonly shoulderStrength: WebGLUniformLocation | null;
	readonly toeStrength: WebGLUniformLocation | null;
	readonly colorSpace: WebGLUniformLocation | null;
	readonly useExposureHistory: WebGLUniformLocation | null;
}

interface WebGL2TonemapResources {
	readonly program: WebGLProgram;
	readonly vertexArray: WebGLVertexArrayObject | null;
	readonly uniforms: WebGL2TonemapUniforms;
}

interface WebGL2ExposureUniforms {
	readonly source: WebGLUniformLocation | null;
	readonly previousExposure: WebGLUniformLocation | null;
	readonly keyValue: WebGLUniformLocation | null;
	readonly minExposure: WebGLUniformLocation | null;
	readonly maxExposure: WebGLUniformLocation | null;
	readonly adaptationRate: WebGLUniformLocation | null;
	readonly deltaTime: WebGLUniformLocation | null;
	readonly hasPreviousExposure: WebGLUniformLocation | null;
}

interface WebGL2ExposureResources {
	readonly program: WebGLProgram;
	readonly vertexArray: WebGLVertexArrayObject | null;
	readonly uniforms: WebGL2ExposureUniforms;
}

interface WebGL2PostProcessUniforms {
	readonly source: WebGLUniformLocation | null;
	readonly auxSource: WebGLUniformLocation | null;
	readonly effectMode: WebGLUniformLocation | null;
	readonly texelSize: WebGLUniformLocation | null;
	readonly primary: WebGLUniformLocation | null;
	readonly secondary: WebGLUniformLocation | null;
	readonly color: WebGLUniformLocation | null;
	readonly lift: WebGLUniformLocation | null;
	readonly gamma: WebGLUniformLocation | null;
	readonly gain: WebGLUniformLocation | null;
	readonly frameSeed: WebGLUniformLocation | null;
}

interface WebGL2PostProcessResources {
	readonly program: WebGLProgram;
	readonly vertexArray: WebGLVertexArrayObject | null;
	readonly uniforms: WebGL2PostProcessUniforms;
}

const resolveShaderCreateMessage = (
	locale: WebGL2RenderPassLocale,
	label: string
): string =>
	locale === 'tr'
		? `WebGL2 ${label} shader olusturulamadi`
		: `Failed to create WebGL2 ${label} shader`;

const resolveShaderCompileMessage = (
	locale: WebGL2RenderPassLocale,
	label: string,
	infoLog: string
): string =>
	locale === 'tr'
		? `WebGL2 ${label} shader derlenemedi: ${infoLog}`
		: `Failed to compile WebGL2 ${label} shader: ${infoLog}`;

const resolveProgramCreateMessage = (
	locale: WebGL2RenderPassLocale,
	label: string
): string =>
	locale === 'tr'
		? `WebGL2 ${label} programi olusturulamadi`
		: `Failed to create WebGL2 ${label} program`;

const resolveProgramLinkMessage = (
	locale: WebGL2RenderPassLocale,
	label: string,
	infoLog: string
): string =>
	locale === 'tr'
		? `WebGL2 ${label} programi baglanamadi: ${infoLog}`
		: `Failed to link WebGL2 ${label} program: ${infoLog}`;

export class WebGL2FullscreenProgramCache {
	private _tonemapResources: WebGL2TonemapResources | null = null;
	private _exposureResources: WebGL2ExposureResources | null = null;
	private _postProcessResources: WebGL2PostProcessResources | null = null;
	private readonly _ctx: IGLContext;
	private readonly _gl: WebGL2RenderingContext;

    constructor(
		source: ContextSource,
		private readonly _locale: WebGL2RenderPassLocale
	) {
		const ctx = resolveContext(source);
		this._ctx = ctx;
		this._gl = ctx.gl;
	}

	get context(): IGLContext {
		return this._ctx;
	}

	get gl(): WebGL2RenderingContext {
		return this._gl;
	}

	getTonemapResources(): WebGL2TonemapResources {
		if (this._tonemapResources) {
			return this._tonemapResources;
		}

		const program = this._createFullscreenProgram(TONEMAP_FRAGMENT_SHADER_SOURCE, 'tonemap');
		const vertexArray = this._gl.createVertexArray?.() ?? null;
		this._tonemapResources = {
			program,
			vertexArray,
			uniforms: {
				source: this._gl.getUniformLocation(program, 'uSource'),
				exposureHistory: this._gl.getUniformLocation(program, 'uExposureHistory'),
				mode: this._gl.getUniformLocation(program, 'uMode'),
				exposureScale: this._gl.getUniformLocation(program, 'uExposureScale'),
				gamma: this._gl.getUniformLocation(program, 'uGamma'),
				contrast: this._gl.getUniformLocation(program, 'uContrast'),
				saturation: this._gl.getUniformLocation(program, 'uSaturation'),
				shoulderStrength: this._gl.getUniformLocation(program, 'uShoulderStrength'),
				toeStrength: this._gl.getUniformLocation(program, 'uToeStrength'),
				colorSpace: this._gl.getUniformLocation(program, 'uColorSpace'),
				useExposureHistory: this._gl.getUniformLocation(program, 'uUseExposureHistory'),
			},
		};

		return this._tonemapResources;
	}

	getExposureResources(): WebGL2ExposureResources {
		if (this._exposureResources) {
			return this._exposureResources;
		}

		const program = this._createFullscreenProgram(
			EXPOSURE_HISTORY_FRAGMENT_SHADER_SOURCE,
			'exposure-history'
		);
		const vertexArray = this._gl.createVertexArray?.() ?? null;
		this._exposureResources = {
			program,
			vertexArray,
			uniforms: {
				source: this._gl.getUniformLocation(program, 'uSource'),
				previousExposure: this._gl.getUniformLocation(program, 'uPreviousExposure'),
				keyValue: this._gl.getUniformLocation(program, 'uKeyValue'),
				minExposure: this._gl.getUniformLocation(program, 'uMinExposure'),
				maxExposure: this._gl.getUniformLocation(program, 'uMaxExposure'),
				adaptationRate: this._gl.getUniformLocation(program, 'uAdaptationRate'),
				deltaTime: this._gl.getUniformLocation(program, 'uDeltaTime'),
				hasPreviousExposure: this._gl.getUniformLocation(program, 'uHasPreviousExposure'),
			},
		};

		return this._exposureResources;
	}

	getPostProcessResources(): WebGL2PostProcessResources {
		if (this._postProcessResources) {
			return this._postProcessResources;
		}

		const program = this._createFullscreenProgram(POST_PROCESS_FRAGMENT_SHADER_SOURCE, 'post-process');
		const vertexArray = this._gl.createVertexArray?.() ?? null;
		this._postProcessResources = {
			program,
			vertexArray,
			uniforms: {
				source: this._gl.getUniformLocation(program, 'uSource'),
				auxSource: this._gl.getUniformLocation(program, 'uAuxSource'),
				effectMode: this._gl.getUniformLocation(program, 'uEffectMode'),
				texelSize: this._gl.getUniformLocation(program, 'uTexelSize'),
				primary: this._gl.getUniformLocation(program, 'uPrimary'),
				secondary: this._gl.getUniformLocation(program, 'uSecondary'),
				color: this._gl.getUniformLocation(program, 'uColor'),
				lift: this._gl.getUniformLocation(program, 'uLift'),
				gamma: this._gl.getUniformLocation(program, 'uGammaVec'),
				gain: this._gl.getUniformLocation(program, 'uGain'),
				frameSeed: this._gl.getUniformLocation(program, 'uFrameSeed'),
			},
		};

		return this._postProcessResources;
	}

	dispose(): void {
		if (this._tonemapResources?.vertexArray) {
			this._gl.deleteVertexArray?.(this._tonemapResources.vertexArray);
		}
		if (this._tonemapResources?.program) {
			this._gl.deleteProgram?.(this._tonemapResources.program);
		}
		if (this._exposureResources?.vertexArray) {
			this._gl.deleteVertexArray?.(this._exposureResources.vertexArray);
		}
		if (this._exposureResources?.program) {
			this._gl.deleteProgram?.(this._exposureResources.program);
		}
		if (this._postProcessResources?.vertexArray) {
			this._gl.deleteVertexArray?.(this._postProcessResources.vertexArray);
		}
		if (this._postProcessResources?.program) {
			this._gl.deleteProgram?.(this._postProcessResources.program);
		}
		this._tonemapResources = null;
		this._exposureResources = null;
		this._postProcessResources = null;
	}

	/**
	 * Drops cached fullscreen program/VAO handles without GL delete calls so
	 * they are recompiled lazily after a restored context.
	 */
	invalidateContextResources(): void {
		this._tonemapResources = null;
		this._exposureResources = null;
		this._postProcessResources = null;
	}

	private _compileShader(type: number, source: string, label: string): WebGLShader {
		const shader = this._gl.createShader(type);
		if (!shader) {
			throw createValidationError(
				this._locale,
				'SHADER_CREATE_FAILED',
				resolveShaderCreateMessage(this._locale, label)
			);
		}

		this._gl.shaderSource(shader, source);
		this._gl.compileShader(shader);

		if (!this._gl.getShaderParameter(shader, this._gl.COMPILE_STATUS)) {
			const infoLog = this._gl.getShaderInfoLog(shader) ?? 'unknown compile error';
			this._gl.deleteShader(shader);
			throw createValidationError(
				this._locale,
				'SHADER_COMPILE_FAILED',
				resolveShaderCompileMessage(this._locale, label, infoLog)
			);
		}

		return shader;
	}

	private _createFullscreenProgram(fragmentSource: string, label: string): WebGLProgram {
		const program = this._gl.createProgram();
		if (!program) {
			throw createValidationError(
				this._locale,
				'PROGRAM_CREATE_FAILED',
				resolveProgramCreateMessage(this._locale, label)
			);
		}

		const vertexShader = this._compileShader(
			this._gl.VERTEX_SHADER,
			FULLSCREEN_VERTEX_SHADER_SOURCE,
			`${label} vertex`
		);
		const fragmentShader = this._compileShader(
			this._gl.FRAGMENT_SHADER,
			fragmentSource,
			`${label} fragment`
		);

		try {
			this._gl.attachShader(program, vertexShader);
			this._gl.attachShader(program, fragmentShader);
			this._gl.linkProgram(program);

			if (!this._gl.getProgramParameter(program, this._gl.LINK_STATUS)) {
				const infoLog = this._gl.getProgramInfoLog(program) ?? 'unknown link error';
				throw createValidationError(
					this._locale,
					'PROGRAM_LINK_FAILED',
					resolveProgramLinkMessage(this._locale, label, infoLog)
				);
			}
		} catch (error) {
			this._gl.deleteProgram(program);
			throw error;
		} finally {
			this._gl.deleteShader(vertexShader);
			this._gl.deleteShader(fragmentShader);
		}

		return program;
	}
}
