import {
	BUILTIN_POST_PROCESS_EFFECTS as CORE_BUILTIN_POST_PROCESS_EFFECTS,
	type ReadonlyRenderResourceRegistry,
	type RenderClearState,
	type RenderExecutionContext,
	type RenderFrameResult,
	type RenderPassKind,
	type RenderResourceName,
	type ResolvedRenderPass,
} from '@axrone/render-core/types';
import {
	defineWebGL2RenderPassExecutor,
	isWebGL2RenderPassExecutorDescriptor,
	type ManagedWebGL2RenderPassLibrary,
	type ManagedWebGL2RenderPipelineBackend,
	type ManagedWebGL2RenderPipelineBackendOptions,
	type WebGL2AnyRenderPassExecutorDescriptor,
	type WebGL2AnyRenderPassExecutorRegistration,
	type WebGL2RenderBackendProfilerSnapshot,
	type WebGL2RenderFrameCapture,
	type WebGL2RenderPassCapture,
	type WebGL2RenderPassExecutionContext,
	type WebGL2RenderPassExecutionResult,
	type WebGL2RenderPassExecutorDescriptor,
	type WebGL2RenderPassLibraryOptions,
	type WebGL2RenderPassOf,
	type WebGL2RenderResourceHandle,
	type WebGL2RenderTextureNativeHandle,
	type WebGL2ResolvedFramebufferBinding,
} from './pipeline-contracts';
import type { ContextSource, IGLContext } from './context';
import { resolveContext } from './context';

import {
	EXPOSURE_HISTORY_FRAGMENT_SHADER_SOURCE,
	FULLSCREEN_VERTEX_SHADER_SOURCE,
	POST_PROCESS_FRAGMENT_SHADER_SOURCE,
	TONEMAP_FRAGMENT_SHADER_SOURCE,
} from './internal/render-pass-shaders';
import {
	resolveNativeFramebuffer,
	toNativeFramebufferHandle,
} from './internal/native-framebuffer-handle';

type WebGL2RuntimeRenderPassErrorCode =
	| 'EXECUTOR_NOT_FOUND'
	| 'FRAMEBUFFER_UNSUPPORTED_ATTACHMENT'
	| 'FRAMEBUFFER_CREATE_FAILED'
	| 'SOURCE_TEXTURE_INVALID'
	| 'PROGRAM_CREATE_FAILED'
	| 'SHADER_CREATE_FAILED'
	| 'SHADER_COMPILE_FAILED'
	| 'PROGRAM_LINK_FAILED'
	| 'PASS_EXECUTION_FAILED';

type WebGL2RenderPassLocale = 'en' | 'tr';

interface WebGL2RenderPassErrorOptions {
	readonly code: WebGL2RuntimeRenderPassErrorCode;
	readonly message: string;
	readonly locale: WebGL2RenderPassLocale;
	readonly cause?: unknown;
	readonly pass?: ResolvedRenderPass;
}

interface RegisteredExecutor {
	readonly descriptor: WebGL2AnyRenderPassExecutorDescriptor;
	readonly priority: number;
	readonly sequence: number;
}

interface MutableProfilerState {
	frame: number;
	passCount: number;
	drawCalls: number;
	clears: number;
	presents: number;
	cpuTimeMs: number;
}

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

interface BuiltinExecutorDependencies {
	readonly ctx: IGLContext;
	readonly gl: WebGL2RenderingContext;
	readonly locale: WebGL2RenderPassLocale;
	readonly framebuffers: WebGL2FramebufferResolver;
	readonly programs: WebGL2FullscreenProgramCache;
}

const REQUIRED_EXECUTOR_KINDS = [
	'depth-prepass',
	'shadow',
	'opaque',
	'reflection-probe',
	'global-illumination',
	'volumetric',
	'skybox',
	'transparent',
	'post-process',
	'tonemap',
	'light-bake',
] as const satisfies readonly RenderPassKind[];

const BUILTIN_EXECUTOR_PRIORITY = -1024;
const BUILTIN_POST_PROCESS_EFFECT_SET = new Set<string>(CORE_BUILTIN_POST_PROCESS_EFFECTS);
const REQUIRED_EXECUTOR_KIND_SET = new Set<RenderPassKind>(REQUIRED_EXECUTOR_KINDS);
const EMPTY_NOTES = Object.freeze([]) as readonly string[];
const EMPTY_EXECUTORS = Object.freeze([]) as readonly WebGL2AnyRenderPassExecutorDescriptor[];
const TONEMAP_PASS_NOTES = Object.freeze(['builtin-tonemap']) as readonly string[];
const AUTOMATIC_EXPOSURE_TONEMAP_PASS_NOTES = Object.freeze([
	'builtin-tonemap',
	'builtin-auto-exposure',
]) as readonly string[];
const PRESENT_PASS_NOTES = Object.freeze(['builtin-present']) as readonly string[];
const DIRECT_PRESENT_PASS_NOTES = Object.freeze(['builtin-present:direct']) as readonly string[];
const POST_PROCESS_NOTES = new Map<string, readonly string[]>();

const getNow = (): number =>
	typeof performance !== 'undefined' && typeof performance.now === 'function'
		? performance.now()
		: Date.now();

const isPromiseLike = <T>(value: T | PromiseLike<T>): value is PromiseLike<T> =>
	typeof value === 'object' && value !== null && 'then' in value;

const isFrameResourceName = (value: RenderResourceName | null): boolean =>
	value !== null && value.startsWith('frame:');

const normalizeLocale = (locale?: string): WebGL2RenderPassLocale =>
	locale?.toLowerCase().startsWith('tr') ? 'tr' : 'en';

const normalizeDrawCalls = (value: number | null | undefined): number => {
	if (!Number.isFinite(value)) {
		return 0;
	}

	return Math.max(0, Math.floor(value ?? 0));
};

const freezeNotes = (notes: readonly string[] | null | undefined): readonly string[] => {
	if (!notes || notes.length === 0) {
		return EMPTY_NOTES;
	}

	return Object.freeze([...notes]);
};

const getPostProcessNotes = (effectName: string): readonly string[] => {
	const existing = POST_PROCESS_NOTES.get(effectName);
	if (existing) {
		return existing;
	}

	const created = Object.freeze([`builtin-post-process:${effectName}`]) as readonly string[];
	POST_PROCESS_NOTES.set(effectName, created);
	return created;
};

const createFramebufferCacheKey = (
	colorTarget: RenderResourceName | null,
	depthTarget: RenderResourceName | null
): string => `${colorTarget ?? 'none'}|${depthTarget ?? 'none'}`;

const resolveMissingExecutorMessage = (
	locale: WebGL2RenderPassLocale,
	kind: RenderPassKind
): string =>
	locale === 'tr'
		? `'${kind}' gecisleri icin WebGL2 render pass executor kaydi bulunamadi`
		: `No WebGL2 render pass executor is registered for '${kind}' passes`;

const resolveAttachmentValidationMessage = (locale: WebGL2RenderPassLocale): string =>
	locale === 'tr'
		? 'Yonetilen WebGL2 render pass kutuphanesi yalnizca 2D framebuffer attachment tiplerini destekler'
		: 'Managed WebGL2 render pass library supports 2D framebuffer attachments only';

const resolveFramebufferCreationMessage = (locale: WebGL2RenderPassLocale): string =>
	locale === 'tr'
		? 'WebGL2 render pass kutuphanesi icin framebuffer olusturulamadi'
		: 'Failed to create WebGL2 framebuffer for render pass library';

const resolveSourceTextureMessage = (
	locale: WebGL2RenderPassLocale,
	kind: 'tonemap' | 'post-process'
): string =>
	locale === 'tr'
		? `Yerlesik WebGL2 ${kind} gecisleri 2D doku kaynagi gerektirir`
		: `Built-in WebGL2 ${kind} passes require a 2D texture source`;

const resolveTonemapExposureHistoryMessage = (
	locale: WebGL2RenderPassLocale,
	kind: 'source' | 'target'
): string =>
	locale === 'tr'
		? `Yerlesik WebGL2 automatic exposure yolu ${kind === 'source' ? 'kaynak' : 'hedef'} 2D history dokusu gerektirir`
		: `Built-in WebGL2 automatic exposure requires a 2D history ${kind} texture`;

const resolveAuxiliaryTextureMessage = (
	locale: WebGL2RenderPassLocale,
	effectName: string
): string =>
	locale === 'tr'
		? `Yerlesik WebGL2 ${effectName} gecisi yardimci 2D doku girdisi gerektirir`
		: `Built-in WebGL2 ${effectName} pass requires an auxiliary 2D texture input`;

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

const createValidationError = (
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

const createExecutionError = (
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

const normalizeExecutorRegistration = (
	registration: WebGL2AnyRenderPassExecutorRegistration
): WebGL2AnyRenderPassExecutorDescriptor => {
	const descriptor = isWebGL2RenderPassExecutorDescriptor(registration)
		? registration
		: defineWebGL2RenderPassExecutor(registration);

	return Object.freeze({
		...descriptor,
		priority: Number.isFinite(descriptor.priority) ? descriptor.priority : 0,
	});
};

class WebGL2RenderPassExecutorRegistry {
	private readonly _byKind = new Map<RenderPassKind, readonly WebGL2AnyRenderPassExecutorDescriptor[]>();
	private readonly _all: readonly WebGL2AnyRenderPassExecutorDescriptor[];

	constructor(registrations: readonly WebGL2AnyRenderPassExecutorRegistration[]) {
		const grouped = new Map<RenderPassKind, RegisteredExecutor[]>();
		const all: WebGL2AnyRenderPassExecutorDescriptor[] = [];

		for (let index = 0; index < registrations.length; index += 1) {
			const descriptor = normalizeExecutorRegistration(registrations[index]!);
			const current = grouped.get(descriptor.kind) ?? [];
			current.push({
				descriptor,
				priority: descriptor.priority,
				sequence: index,
			});
			grouped.set(descriptor.kind, current);
			all.push(descriptor);
		}

		for (const [kind, entries] of grouped.entries()) {
			entries.sort((left, right) =>
				right.priority === left.priority
					? left.sequence - right.sequence
					: right.priority - left.priority
			);
			this._byKind.set(
				kind,
				Object.freeze(entries.map((entry) => entry.descriptor))
			);
		}

		this._all = Object.freeze(all);
	}

	has(kind: RenderPassKind): boolean {
		return (this._byKind.get(kind)?.length ?? 0) > 0;
	}

	list(kind?: RenderPassKind): readonly WebGL2AnyRenderPassExecutorDescriptor[] {
		return kind ? this._byKind.get(kind) ?? EMPTY_EXECUTORS : this._all;
	}

	resolve<K extends RenderPassKind>(
		pass: WebGL2RenderPassOf<K>,
		context: RenderExecutionContext<WebGL2RenderResourceHandle>,
		execution: WebGL2RenderPassExecutionContext
	): WebGL2RenderPassExecutorDescriptor<K> | null {
		const descriptors = this._byKind.get(pass.kind);
		if (!descriptors) {
			return null;
		}

		for (let index = 0; index < descriptors.length; index += 1) {
			const descriptor = descriptors[index]!;
			if (descriptor.kind !== pass.kind) {
				continue;
			}

			// The `_byKind` bucket plus the kind check above guarantee the
			// descriptor targets this pass kind; the distributed union cannot
			// express that relationship for a generic K, so narrow explicitly.
			const typedDescriptor =
				descriptor as unknown as WebGL2RenderPassExecutorDescriptor<K>;
			if (!typedDescriptor.matches || typedDescriptor.matches(pass, context, execution)) {
				return typedDescriptor;
			}
		}

		return null;
	}
}

const resolveFramebufferTargets = (
	pass: ResolvedRenderPass
): {
	readonly colorTarget: RenderResourceName | null;
	readonly depthTarget: RenderResourceName | null;
} => {
	switch (pass.kind) {
		case 'depth-prepass':
			return {
				colorTarget: null,
				depthTarget: pass.metadata.depth,
			};
		case 'shadow':
			return {
				colorTarget: null,
				depthTarget: pass.metadata.atlas,
			};
		case 'opaque':
		case 'skybox':
		case 'transparent':
			return {
				colorTarget: pass.metadata.color,
				depthTarget: pass.metadata.depth,
			};
		case 'post-process':
		case 'tonemap':
		case 'reflection-probe':
		case 'global-illumination':
			return {
				colorTarget: pass.metadata.target,
				depthTarget: null,
			};
		case 'volumetric':
			return {
				colorTarget: pass.metadata.froxelGrid,
				depthTarget: null,
			};
		case 'present':
		case 'light-bake':
			return {
				colorTarget: null,
				depthTarget: null,
			};
	}
};

const clearFramebuffer = (
	gl: WebGL2RenderingContext,
	clearState: RenderClearState | null | undefined
): number => {
	if (!clearState) {
		return 0;
	}

	let bits = 0;

	if (clearState.color !== undefined && clearState.color !== null) {
		const color = clearState.color;
		// RenderVector4Like is `Vec4 | readonly [number x4]`; only the tuple
		// shape is indexable, so branch on the runtime representation.
		if (Array.isArray(color)) {
			gl.clearColor(color[0] ?? 0, color[1] ?? 0, color[2] ?? 0, color[3] ?? 1);
		} else {
			const vector = color as { x: number; y: number; z: number; w: number };
			gl.clearColor(vector.x ?? 0, vector.y ?? 0, vector.z ?? 0, vector.w ?? 1);
		}
		bits |= gl.COLOR_BUFFER_BIT;
	}

	if (clearState.depth !== undefined && clearState.depth !== null) {
		gl.clearDepth(clearState.depth);
		bits |= gl.DEPTH_BUFFER_BIT;
	}

	if (clearState.stencil !== undefined && clearState.stencil !== null) {
		gl.clearStencil(clearState.stencil);
		bits |= gl.STENCIL_BUFFER_BIT;
	}

	if (bits !== 0) {
		gl.clear(bits);
	}

	return bits;
};

class WebGL2FramebufferResolver {
	private readonly _cache = new Map<string, WebGLFramebuffer>();
	private readonly _defaultFramebuffer: WebGLFramebuffer | null;
	private readonly _ctx!: IGLContext;
	private readonly _gl!: WebGL2RenderingContext;

	constructor(
		source: ContextSource,
		defaultFramebuffer: WebGLFramebuffer | null | undefined,
		private readonly _directFrameOutput: boolean,
		private readonly _locale: WebGL2RenderPassLocale
	) {
		const ctx = resolveContext(source);
		this._ctx = ctx;
		this._gl = ctx.gl;
		this._defaultFramebuffer = defaultFramebuffer ?? null;
	}

	get context(): IGLContext {
		return this._ctx;
	}

	get gl(): WebGL2RenderingContext {
		return this._gl;
	}

	get defaultFramebuffer(): WebGLFramebuffer | null {
		return this._defaultFramebuffer;
	}

	isDirectFrameTarget(resourceName: RenderResourceName | null): boolean {
		return this._directFrameOutput && isFrameResourceName(resourceName);
	}

	getTextureSnapshot(
		graph: ReadonlyRenderResourceRegistry<WebGL2RenderResourceHandle>,
		resourceName: RenderResourceName | null
	) {
		return resourceName ? graph.getTexture(resourceName) : null;
	}

	getTextureHandle(
		graph: ReadonlyRenderResourceRegistry<WebGL2RenderResourceHandle>,
		resourceName: RenderResourceName | null
	): WebGL2RenderTextureNativeHandle | null {
		const snapshot = this.getTextureSnapshot(graph, resourceName);
		if (!snapshot || !snapshot.native || snapshot.native.kind !== 'texture') {
			return null;
		}

		return snapshot.native;
	}

	resolveBinding(
		colorTarget: RenderResourceName | null,
		depthTarget: RenderResourceName | null,
		context: RenderExecutionContext<WebGL2RenderResourceHandle>
	): WebGL2ResolvedFramebufferBinding {
		if (this._directFrameOutput && (isFrameResourceName(colorTarget) || isFrameResourceName(depthTarget))) {
			return {
				framebuffer: toNativeFramebufferHandle(this._defaultFramebuffer),
				colorTarget,
				depthTarget,
				width: context.viewport.width,
				height: context.viewport.height,
				defaultFramebuffer: true,
			};
		}

		const colorHandle = this.getTextureHandle(context.graph, colorTarget);
		const depthHandle = this.getTextureHandle(context.graph, depthTarget);

		if (!colorHandle && !depthHandle) {
			return {
				framebuffer: toNativeFramebufferHandle(this._defaultFramebuffer),
				colorTarget,
				depthTarget,
				width: context.viewport.width,
				height: context.viewport.height,
				defaultFramebuffer: true,
			};
		}

		if (
			(colorHandle && colorHandle.target !== this._gl.TEXTURE_2D) ||
			(depthHandle && depthHandle.target !== this._gl.TEXTURE_2D)
		) {
			throw createValidationError(
				this._locale,
				'FRAMEBUFFER_UNSUPPORTED_ATTACHMENT',
				resolveAttachmentValidationMessage(this._locale)
			);
		}

		const cacheKey = createFramebufferCacheKey(colorTarget, depthTarget);
		let framebuffer = this._cache.get(cacheKey);
		if (!framebuffer) {
			framebuffer = this._gl.createFramebuffer();
			if (!framebuffer) {
				throw createValidationError(
					this._locale,
					'FRAMEBUFFER_CREATE_FAILED',
					resolveFramebufferCreationMessage(this._locale)
				);
			}
			this._cache.set(cacheKey, framebuffer);
		}

		this._gl.bindFramebuffer(this._gl.FRAMEBUFFER, framebuffer);

		if (colorHandle) {
			this._gl.framebufferTexture2D(
				this._gl.FRAMEBUFFER,
				this._gl.COLOR_ATTACHMENT0,
				this._gl.TEXTURE_2D,
				colorHandle.texture,
				0
			);
			this._gl.drawBuffers([this._gl.COLOR_ATTACHMENT0]);
		} else {
			this._gl.drawBuffers([]);
		}

		if (depthHandle) {
			const depthDescriptor = this.getTextureSnapshot(context.graph, depthTarget)?.descriptor;
			const attachment =
				depthDescriptor?.format === 'depth24-stencil8'
					? this._gl.DEPTH_STENCIL_ATTACHMENT
					: this._gl.DEPTH_ATTACHMENT;

			this._gl.framebufferTexture2D(
				this._gl.FRAMEBUFFER,
				attachment,
				this._gl.TEXTURE_2D,
				depthHandle.texture,
				0
			);
		}

		const sizeSnapshot = this.getTextureSnapshot(context.graph, colorTarget ?? depthTarget);

		return {
			framebuffer: toNativeFramebufferHandle(framebuffer),
			colorTarget,
			depthTarget,
			width: sizeSnapshot?.descriptor.width ?? context.viewport.width,
			height: sizeSnapshot?.descriptor.height ?? context.viewport.height,
			defaultFramebuffer: false,
		};
	}

	resolveForPass(
		pass: ResolvedRenderPass,
		context: RenderExecutionContext<WebGL2RenderResourceHandle>
	): WebGL2ResolvedFramebufferBinding {
		if (pass.kind === 'present') {
			return {
				framebuffer: toNativeFramebufferHandle(this._defaultFramebuffer),
				colorTarget: null,
				depthTarget: null,
				width: context.viewport.width,
				height: context.viewport.height,
				defaultFramebuffer: true,
			};
		}

		const targets = resolveFramebufferTargets(pass);
		return this.resolveBinding(targets.colorTarget, targets.depthTarget, context);
	}

	dispose(): void {
		for (const framebuffer of this._cache.values()) {
			this._gl.deleteFramebuffer(framebuffer);
		}
		this._cache.clear();
	}

	/**
	 * Drops cached framebuffer handles without issuing GL delete calls.
	 * Intended for `webglcontextlost` recovery: the underlying objects are
	 * already invalid, so they only need to be forgotten and lazily rebuilt.
	 */
	invalidateContextResources(): void {
		this._cache.clear();
	}
}

class WebGL2FullscreenProgramCache {
	private _tonemapResources: WebGL2TonemapResources | null = null;
	private _exposureResources: WebGL2ExposureResources | null = null;
	private _postProcessResources: WebGL2PostProcessResources | null = null;
	private readonly _ctx!: IGLContext;
	private readonly _gl!: WebGL2RenderingContext;

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

const resolveTonemapModeId = (
	mode: WebGL2RenderPassOf<'tonemap'>['metadata']['mode']
): number => {
	switch (mode) {
		case 'reinhard':
			return 1;
		case 'aces':
			return 2;
		case 'aces-fitted':
			return 3;
		case 'filmic':
			return 4;
		case 'agx':
			return 5;
		case 'neutral':
			return 6;
		default:
			return 0;
	}
};

const resolveTonemapColorSpaceId = (
	colorSpace: WebGL2RenderPassOf<'tonemap'>['metadata']['colorSpace']
): number => {
	switch (colorSpace) {
		case 'display-p3':
			return 1;
		case 'rec2020':
			return 2;
		default:
			return 0;
	}
};

const resolveTonemapExposureScale = (
	exposure: WebGL2RenderPassOf<'tonemap'>['metadata']['exposure']
): number => {
	if (!exposure) {
		return 1;
	}

	if (exposure.mode === 'manual') {
		return Math.pow(2, exposure.exposure);
	}

	return Math.max(0, exposure.keyValue ?? 0.18) / 0.18;
};

const resolveAutomaticExposureSettings = (
	exposure: WebGL2RenderPassOf<'tonemap'>['metadata']['exposure']
): {
	readonly keyValue: number;
	readonly minExposure: number;
	readonly maxExposure: number;
	readonly adaptationRate: number;
} | null => {
	if (!exposure || exposure.mode !== 'automatic') {
		return null;
	}

	return {
		keyValue: exposure.keyValue ?? 0.18,
		minExposure: exposure.minExposure ?? -6,
		maxExposure: exposure.maxExposure ?? 6,
		adaptationRate: exposure.adaptationRate ?? 1.5,
	};
};

const resolvePostProcessEffectId = (effectName: string): number => {
	switch (effectName) {
		case 'fxaa':
			return 1;
		case 'vignette':
			return 2;
		case 'film-grain':
			return 3;
		case 'chromatic-aberration':
			return 4;
		case 'color-grading':
			return 5;
		case 'bloom':
			return 6;
		case 'ssao':
			return 7;
		case 'depth-of-field':
			return 8;
		case 'taa':
			return 9;
		default:
			return 0;
	}
};

const requiresAuxiliaryPostProcessTexture = (effectName: string): boolean =>
	effectName === 'ssao' || effectName === 'depth-of-field' || effectName === 'taa';

const createBuiltinExecutors = (
	dependencies: BuiltinExecutorDependencies
): readonly WebGL2AnyRenderPassExecutorDescriptor[] => {
	const { gl, locale, framebuffers, programs } = dependencies;

	return Object.freeze([
		defineWebGL2RenderPassExecutor({
			kind: 'present',
			name: 'builtin-present',
			priority: BUILTIN_EXECUTOR_PRIORITY,
			execute(pass, context) {
				if (framebuffers.isDirectFrameTarget(pass.metadata.source)) {
					return {
						drawCalls: 1,
						notes: DIRECT_PRESENT_PASS_NOTES,
					};
				}

				const sourceBinding = framebuffers.resolveBinding(pass.metadata.source, null, context);
				gl.bindFramebuffer(gl.READ_FRAMEBUFFER, resolveNativeFramebuffer(sourceBinding.framebuffer));
				gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, framebuffers.defaultFramebuffer);
				gl.blitFramebuffer(
					0,
					0,
					sourceBinding.width,
					sourceBinding.height,
					0,
					0,
					context.viewport.width,
					context.viewport.height,
					gl.COLOR_BUFFER_BIT,
					gl.NEAREST
				);

				return {
					drawCalls: 1,
					notes: PRESENT_PASS_NOTES,
				};
			},
		}),
		defineWebGL2RenderPassExecutor({
			kind: 'tonemap',
			name: 'builtin-tonemap',
			priority: BUILTIN_EXECUTOR_PRIORITY,
			execute(pass, context, execution) {
				const sourceTexture = framebuffers.getTextureHandle(context.graph, pass.metadata.source);
				const automaticExposure = resolveAutomaticExposureSettings(pass.metadata.exposure);
				const exposureHistorySourceSnapshot = pass.metadata.exposureHistorySource
					? framebuffers.getTextureSnapshot(context.graph, pass.metadata.exposureHistorySource)
					: null;
				const exposureHistorySourceTexture = pass.metadata.exposureHistorySource
					? framebuffers.getTextureHandle(context.graph, pass.metadata.exposureHistorySource)
					: null;
				const exposureHistoryTargetTexture = pass.metadata.exposureHistoryTarget
					? framebuffers.getTextureHandle(context.graph, pass.metadata.exposureHistoryTarget)
					: null;

				if (!sourceTexture || sourceTexture.target !== gl.TEXTURE_2D) {
					throw createValidationError(
						locale,
						'SOURCE_TEXTURE_INVALID',
						resolveSourceTextureMessage(locale, 'tonemap'),
						pass
					);
				}

				const resources = programs.getTonemapResources();
				let resolvedExposureTexture: WebGL2RenderTextureNativeHandle | null = null;
				let drawCalls = 1;

				if (automaticExposure && pass.metadata.exposureHistoryTarget) {
					if (
						!exposureHistoryTargetTexture ||
						exposureHistoryTargetTexture.target !== gl.TEXTURE_2D
					) {
						throw createValidationError(
							locale,
							'SOURCE_TEXTURE_INVALID',
							resolveTonemapExposureHistoryMessage(locale, 'target'),
							pass
						);
					}

					const hasPreviousExposure = exposureHistorySourceSnapshot?.reused === true;
					if (
						hasPreviousExposure &&
						(!exposureHistorySourceTexture || exposureHistorySourceTexture.target !== gl.TEXTURE_2D)
					) {
						throw createValidationError(
							locale,
							'SOURCE_TEXTURE_INVALID',
							resolveTonemapExposureHistoryMessage(locale, 'source'),
							pass
						);
					}

					const exposureBinding = framebuffers.resolveBinding(
						pass.metadata.exposureHistoryTarget,
						null,
						context
					);
					const exposureResources = programs.getExposureResources();

					gl.bindFramebuffer(gl.FRAMEBUFFER, resolveNativeFramebuffer(exposureBinding.framebuffer));
					gl.viewport(0, 0, exposureBinding.width, exposureBinding.height);
					gl.disable(gl.DEPTH_TEST);
					gl.disable(gl.CULL_FACE);
					gl.disable(gl.BLEND);
					gl.depthMask?.(false);
					gl.colorMask?.(true, true, true, true);
					gl.useProgram(exposureResources.program);
					gl.bindVertexArray?.(exposureResources.vertexArray);
					gl.activeTexture?.(gl.TEXTURE0);
					gl.bindTexture(gl.TEXTURE_2D, sourceTexture.texture);
					gl.uniform1i?.(exposureResources.uniforms.source, 0);
					gl.activeTexture?.(gl.TEXTURE0 + 1);
					gl.bindTexture(
						gl.TEXTURE_2D,
						hasPreviousExposure ? exposureHistorySourceTexture?.texture ?? null : null
					);
					gl.uniform1i?.(exposureResources.uniforms.previousExposure, 1);
					gl.uniform1f?.(exposureResources.uniforms.keyValue, automaticExposure.keyValue);
					gl.uniform1f?.(exposureResources.uniforms.minExposure, automaticExposure.minExposure);
					gl.uniform1f?.(exposureResources.uniforms.maxExposure, automaticExposure.maxExposure);
					gl.uniform1f?.(
						exposureResources.uniforms.adaptationRate,
						automaticExposure.adaptationRate
					);
					gl.uniform1f?.(
						exposureResources.uniforms.deltaTime,
						context.statistics.deltaTime
					);
					gl.uniform1i?.(
						exposureResources.uniforms.hasPreviousExposure,
						hasPreviousExposure ? 1 : 0
					);
					gl.drawArrays(gl.TRIANGLES, 0, 3);
					gl.bindVertexArray?.(null);
					gl.activeTexture?.(gl.TEXTURE0 + 1);
					gl.bindTexture(gl.TEXTURE_2D, null);
					gl.activeTexture?.(gl.TEXTURE0);
					gl.bindTexture(gl.TEXTURE_2D, null);

					resolvedExposureTexture = exposureHistoryTargetTexture;
					drawCalls += 1;

					gl.bindFramebuffer(gl.FRAMEBUFFER, resolveNativeFramebuffer(execution.binding.framebuffer));
					gl.viewport(0, 0, execution.binding.width, execution.binding.height);
				}

				gl.disable(gl.DEPTH_TEST);
				gl.disable(gl.CULL_FACE);
				gl.disable(gl.BLEND);
				gl.depthMask?.(false);
				gl.colorMask?.(true, true, true, true);
				gl.useProgram(resources.program);
				gl.bindVertexArray?.(resources.vertexArray);
				gl.activeTexture?.(gl.TEXTURE0);
				gl.bindTexture(gl.TEXTURE_2D, sourceTexture.texture);
				gl.uniform1i?.(resources.uniforms.source, 0);
				gl.activeTexture?.(gl.TEXTURE0 + 1);
				gl.bindTexture(gl.TEXTURE_2D, resolvedExposureTexture?.texture ?? null);
				gl.uniform1i?.(resources.uniforms.exposureHistory, 1);
				gl.uniform1i?.(resources.uniforms.mode, resolveTonemapModeId(pass.metadata.mode));
				gl.uniform1i?.(
					resources.uniforms.useExposureHistory,
					resolvedExposureTexture ? 1 : 0
				);
				gl.uniform1f?.(
					resources.uniforms.exposureScale,
					resolveTonemapExposureScale(pass.metadata.exposure)
				);
				gl.uniform1f?.(resources.uniforms.gamma, pass.metadata.gamma);
				gl.uniform1f?.(resources.uniforms.contrast, pass.metadata.contrast);
				gl.uniform1f?.(resources.uniforms.saturation, pass.metadata.saturation);
				gl.uniform1f?.(
					resources.uniforms.shoulderStrength,
					pass.metadata.shoulderStrength
				);
				gl.uniform1f?.(resources.uniforms.toeStrength, pass.metadata.toeStrength);
				gl.uniform1i?.(
					resources.uniforms.colorSpace,
					resolveTonemapColorSpaceId(pass.metadata.colorSpace)
				);
				gl.drawArrays(gl.TRIANGLES, 0, 3);
				gl.bindVertexArray?.(null);
				gl.activeTexture?.(gl.TEXTURE0 + 1);
				gl.bindTexture(gl.TEXTURE_2D, null);
				gl.activeTexture?.(gl.TEXTURE0);
				gl.bindTexture(gl.TEXTURE_2D, null);
				gl.useProgram(null);
				gl.depthMask?.(true);

				return {
					drawCalls,
					notes: resolvedExposureTexture
						? AUTOMATIC_EXPOSURE_TONEMAP_PASS_NOTES
						: TONEMAP_PASS_NOTES,
				};
			},
		}),
		defineWebGL2RenderPassExecutor({
			kind: 'post-process',
			name: 'builtin-post-process',
			priority: BUILTIN_EXECUTOR_PRIORITY,
			matches(pass) {
				return (
					pass.metadata.effect.category === 'builtin' &&
					BUILTIN_POST_PROCESS_EFFECT_SET.has(pass.metadata.effect.name)
				);
			},
			execute(pass, context) {
				const effect = pass.metadata.effect;
				if (effect.category !== 'builtin') {
					// matches() guarantees only builtin effects reach this executor;
					// this guard narrows the union for the settings switch below and
					// stays defensive should a custom effect ever slip through.
					return { drawCalls: 0, notes: EMPTY_NOTES };
				}
				const sourceSnapshot = framebuffers.getTextureSnapshot(
					context.graph,
					pass.metadata.source
				);
				const sourceTexture = framebuffers.getTextureHandle(context.graph, pass.metadata.source);
				const auxiliaryInput = pass.inputs.length > 1 ? pass.inputs[1] ?? null : null;
				const auxiliaryTexture = auxiliaryInput
					? framebuffers.getTextureHandle(context.graph, auxiliaryInput)
					: null;

				if (!sourceTexture || sourceTexture.target !== gl.TEXTURE_2D) {
					throw createValidationError(
						locale,
						'SOURCE_TEXTURE_INVALID',
						resolveSourceTextureMessage(locale, 'post-process'),
						pass
					);
				}

				if (
					requiresAuxiliaryPostProcessTexture(effect.name) &&
					(!auxiliaryTexture || auxiliaryTexture.target !== gl.TEXTURE_2D)
				) {
					throw createValidationError(
						locale,
						'SOURCE_TEXTURE_INVALID',
						resolveAuxiliaryTextureMessage(locale, effect.name),
						pass
					);
				}

				const width = sourceSnapshot?.descriptor.width ?? context.viewport.width;
				const height = sourceSnapshot?.descriptor.height ?? context.viewport.height;
				const resources = programs.getPostProcessResources();

				let primary: readonly [number, number, number, number] = [0, 0, 0, 0];
				let secondary: readonly [number, number, number, number] = [0, 0, 0, 0];
				let color: readonly [number, number, number] = [0, 0, 0];
				let lift: readonly [number, number, number] = [1, 1, 1];
				let gamma: readonly [number, number, number] = [1, 1, 1];
				let gain: readonly [number, number, number] = [1, 1, 1];

				switch (effect.name) {
					case 'bloom':
						primary = [
							effect.settings.threshold ?? 1,
							effect.settings.knee ?? 0.5,
							effect.settings.intensity ?? 0.65,
							effect.settings.radius ?? 0.9,
						];
						break;
					case 'fxaa':
						primary = [
							effect.settings.subpixel ?? 0.75,
							effect.settings.edgeThreshold ?? 0.166,
							effect.settings.edgeThresholdMin ?? 0.0833,
							0,
						];
						break;
					case 'vignette':
						primary = [
							effect.settings.intensity ?? 0.2,
							effect.settings.smoothness ?? 0.55,
							effect.settings.roundness ?? 1,
							0,
						];
						color = effect.settings.color ?? [0, 0, 0];
						break;
					case 'film-grain':
						primary = [
							effect.settings.intensity ?? 0.12,
							effect.settings.response ?? 0.85,
							0,
							0,
						];
						break;
					case 'chromatic-aberration':
						primary = [effect.settings.intensity ?? 0.03, 0, 0, 0];
						break;
					case 'color-grading':
						primary = [
							effect.settings.contrast ?? 1,
							effect.settings.saturation ?? 1,
							effect.settings.temperature ?? 0,
							effect.settings.tint ?? 0,
						];
						lift = effect.settings.lift ?? [1, 1, 1];
						gamma = effect.settings.gamma ?? [1, 1, 1];
						gain = effect.settings.gain ?? [1, 1, 1];
						break;
					case 'ssao':
						primary = [
							effect.settings.radius ?? 0.35,
							effect.settings.intensity ?? 1,
							effect.settings.bias ?? 0.025,
							effect.settings.sampleCount ?? 16,
						];
						secondary = [context.camera.near, context.camera.far, 0, 0];
						break;
					case 'depth-of-field':
						primary = [
							effect.settings.focusDistance ?? 10,
							effect.settings.aperture ?? 5.6,
							effect.settings.focalLength ?? 50,
							effect.settings.maxCoC ?? 12,
						];
						secondary = [context.camera.near, context.camera.far, 0, 0];
						break;
					case 'taa':
						primary = [
							effect.settings.blendFactor ?? 0.92,
							effect.settings.sharpen ?? 0.1,
							effect.settings.jitterScale ?? 1,
							0,
						];
						secondary = [
							context.camera.jitter?.[0] ?? 0,
							context.camera.jitter?.[1] ?? 0,
							auxiliaryTexture ? 1 : 0,
							0,
						];
						break;
				}

				gl.disable(gl.DEPTH_TEST);
				gl.disable(gl.CULL_FACE);
				gl.disable(gl.BLEND);
				gl.depthMask?.(false);
				gl.colorMask?.(true, true, true, true);
				gl.useProgram(resources.program);
				gl.bindVertexArray?.(resources.vertexArray);
				gl.activeTexture?.(gl.TEXTURE0);
				gl.bindTexture(gl.TEXTURE_2D, sourceTexture.texture);
				gl.uniform1i?.(resources.uniforms.source, 0);
				gl.activeTexture?.(gl.TEXTURE0 + 1);
				gl.bindTexture(gl.TEXTURE_2D, auxiliaryTexture?.texture ?? null);
				gl.uniform1i?.(resources.uniforms.auxSource, 1);
				gl.uniform1i?.(
					resources.uniforms.effectMode,
					resolvePostProcessEffectId(effect.name)
				);
				gl.uniform2f?.(
					resources.uniforms.texelSize,
					1 / Math.max(1, width),
					1 / Math.max(1, height)
				);
				gl.uniform4f?.(
					resources.uniforms.primary,
					primary[0],
					primary[1],
					primary[2],
					primary[3]
				);
				gl.uniform4f?.(
					resources.uniforms.secondary,
					secondary[0],
					secondary[1],
					secondary[2],
					secondary[3]
				);
				gl.uniform3f?.(resources.uniforms.color, color[0], color[1], color[2]);
				gl.uniform3f?.(resources.uniforms.lift, lift[0], lift[1], lift[2]);
				gl.uniform3f?.(resources.uniforms.gamma, gamma[0], gamma[1], gamma[2]);
				gl.uniform3f?.(resources.uniforms.gain, gain[0], gain[1], gain[2]);
				gl.uniform1f?.(resources.uniforms.frameSeed, context.frame);
				gl.drawArrays(gl.TRIANGLES, 0, 3);
				gl.bindVertexArray?.(null);
				gl.activeTexture?.(gl.TEXTURE0 + 1);
				gl.bindTexture(gl.TEXTURE_2D, null);
				gl.activeTexture?.(gl.TEXTURE0);
				gl.bindTexture(gl.TEXTURE_2D, null);
				gl.useProgram(null);
				gl.depthMask?.(true);

				return {
					drawCalls: 1,
					notes: getPostProcessNotes(effect.name),
				};
			},
		}),
	]);
};

class ManagedWebGL2RenderPassLibraryImpl implements ManagedWebGL2RenderPassLibrary {
	private readonly _ctx!: IGLContext;
	private readonly _gl!: WebGL2RenderingContext;
	private readonly _locale: WebGL2RenderPassLocale;
	private readonly _strictUnsupportedPasses: boolean;
	private readonly _registry: WebGL2RenderPassExecutorRegistry;
	private readonly _framebuffers: WebGL2FramebufferResolver;
	private readonly _programs: WebGL2FullscreenProgramCache;
	private readonly _profiler: MutableProfilerState = {
		frame: 0,
		passCount: 0,
		drawCalls: 0,
		clears: 0,
		presents: 0,
		cpuTimeMs: 0,
	};
	private readonly _frameCaptures: WebGL2RenderPassCapture[] = [];
	private _lastFrameCapture: WebGL2RenderFrameCapture | null = null;
	private _frameStartTime = 0;

	constructor(private readonly _options: WebGL2RenderPassLibraryOptions) {
		const resolvedCtx = _options.context ?? resolveContext(_options.gl as ContextSource);
		this._ctx = resolvedCtx;
		this._gl = resolvedCtx.gl;
		this._locale = normalizeLocale(_options.locale);
		this._strictUnsupportedPasses = _options.strictUnsupportedPasses ?? true;
		this._framebuffers = new WebGL2FramebufferResolver(
			resolvedCtx,
			_options.defaultFramebuffer,
			_options.directFrameOutput ?? false,
			this._locale
		);
		this._programs = new WebGL2FullscreenProgramCache(resolvedCtx, this._locale);
		this._registry = new WebGL2RenderPassExecutorRegistry([
			...(_options.executors ?? []),
			...createBuiltinExecutors({
				ctx: this._ctx,
				gl: this._gl,
				locale: this._locale,
				framebuffers: this._framebuffers,
				programs: this._programs,
			}),
		]);
		this._ctx.onLost(() => this.invalidateContextResources());
	}

	beginFrame(context: RenderExecutionContext<WebGL2RenderResourceHandle>): void {
		this._profiler.frame = context.frame;
		this._profiler.passCount = 0;
		this._profiler.drawCalls = 0;
		this._profiler.clears = 0;
		this._profiler.presents = 0;
		this._profiler.cpuTimeMs = 0;
		this._frameCaptures.length = 0;
		this._frameStartTime = getNow();
	}

	executePass(
		pass: ResolvedRenderPass,
		context: RenderExecutionContext<WebGL2RenderResourceHandle>
	): void | Promise<void> {
		const binding = this._framebuffers.resolveForPass(pass, context);
		const gl = this._gl;
		const state = this._ctx.state;

		state.bindFramebuffer(gl.FRAMEBUFFER, resolveNativeFramebuffer(binding.framebuffer));
		state.viewport(0, 0, binding.width, binding.height);

		if (clearFramebuffer(gl, pass.clearState) !== 0) {
			this._profiler.clears += 1;
		}

		const execution: WebGL2RenderPassExecutionContext = {
			binding,
			pass,
			frame: context.frame,
		};

		const executor = this._registry.resolve(pass, context, execution);
		if (!executor) {
			if (this._strictUnsupportedPasses && REQUIRED_EXECUTOR_KIND_SET.has(pass.kind)) {
				throw createValidationError(
					this._locale,
					'EXECUTOR_NOT_FOUND',
					resolveMissingExecutorMessage(this._locale, pass.kind),
					pass
				);
			}

			this._finalizePass(pass, binding, null, undefined);
			return;
		}

		try {
			const result = executor.execute(pass, context, execution);
			if (isPromiseLike(result)) {
				return result
					.then((resolvedResult) => {
						this._finalizePass(pass, binding, executor, resolvedResult ?? undefined);
					})
					.catch((error) => {
						throw createExecutionError(this._locale, pass, error);
					});
			}

			this._finalizePass(pass, binding, executor, result ?? undefined);
			return;
		} catch (error) {
			throw createExecutionError(this._locale, pass, error);
		}
	}

	endFrame(
		result: RenderFrameResult<WebGL2RenderResourceHandle>,
		_context: RenderExecutionContext<WebGL2RenderResourceHandle>
	): void {
		const gl = this._gl;
		void this._ctx;
		gl.bindFramebuffer(gl.FRAMEBUFFER, this._framebuffers.defaultFramebuffer);
		gl.bindFramebuffer(gl.READ_FRAMEBUFFER, null);
		gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, null);

		this._profiler.cpuTimeMs = getNow() - this._frameStartTime;
		this._lastFrameCapture = Object.freeze({
			frame: result.frame,
			passes: Object.freeze([...this._frameCaptures]),
		});
	}

	getLastFrameCapture(): WebGL2RenderFrameCapture | null {
		return this._lastFrameCapture;
	}

	getProfilerSnapshot(): WebGL2RenderBackendProfilerSnapshot {
		return Object.freeze({
			frame: this._profiler.frame,
			passCount: this._profiler.passCount,
			drawCalls: this._profiler.drawCalls,
			clears: this._profiler.clears,
			presents: this._profiler.presents,
			cpuTimeMs: this._profiler.cpuTimeMs,
		});
	}

	get context(): IGLContext {
		return this._ctx;
	}

	get gl(): WebGL2RenderingContext {
		return this._gl;
	}

	hasExecutor(kind: RenderPassKind): boolean {
		return this._registry.has(kind);
	}

	listExecutors(kind?: RenderPassKind): readonly WebGL2AnyRenderPassExecutorDescriptor[] {
		return this._registry.list(kind);
	}

	dispose(): void {
		this._framebuffers.dispose();
		this._programs.dispose();
		this._frameCaptures.length = 0;
		this._lastFrameCapture = null;
	}

	invalidateContextResources(): void {
		this._framebuffers.invalidateContextResources();
		this._programs.invalidateContextResources();
	}

	[Symbol.dispose](): void {
		this.dispose();
	}

	private _finalizePass(
		pass: ResolvedRenderPass,
		binding: WebGL2ResolvedFramebufferBinding,
		executor: WebGL2RenderPassExecutorDescriptor | null,
		result: WebGL2RenderPassExecutionResult | undefined
	): void {
		this._profiler.passCount += 1;
		this._profiler.drawCalls += normalizeDrawCalls(result?.drawCalls);
		if (pass.kind === 'present') {
			this._profiler.presents += 1;
		}

		this._frameCaptures.push(
			Object.freeze({
				name: String(pass.name),
				kind: pass.kind,
				target: pass.target,
				queue: pass.queue,
				itemCount: pass.items?.length ?? 0,
				lightCount: pass.lights?.length ?? 0,
				probeCount: pass.probes?.length ?? 0,
				defaultFramebuffer: binding.defaultFramebuffer,
				executorId: executor?.id ?? null,
				notes: freezeNotes(result?.notes),
			})
		);
	}
}

export const createWebGL2RenderPassLibrary = (
	options: WebGL2RenderPassLibraryOptions
): ManagedWebGL2RenderPassLibrary => new ManagedWebGL2RenderPassLibraryImpl(options);

export const createManagedWebGL2RenderPipelineBackend = (
	options: ManagedWebGL2RenderPipelineBackendOptions
): ManagedWebGL2RenderPipelineBackend => createWebGL2RenderPassLibrary(options);