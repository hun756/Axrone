import type {
	RenderExecutionContext,
	RenderFrameResult,
	RenderPassKind,
	ResolvedRenderPass,
} from '@axrone/render-core/types';
import {
	type ManagedWebGL2RenderPassLibrary,
	type ManagedWebGL2RenderPipelineBackend,
	type ManagedWebGL2RenderPipelineBackendOptions,
	type WebGL2AnyRenderPassExecutorDescriptor,
	type WebGL2RenderBackendProfilerSnapshot,
	type WebGL2RenderFrameCapture,
	type WebGL2RenderPassCapture,
	type WebGL2RenderPassExecutionContext,
	type WebGL2RenderPassExecutionResult,
	type WebGL2RenderPassExecutorDescriptor,
	type WebGL2RenderPassLibraryOptions,
	type WebGL2RenderResourceHandle,
	type WebGL2ResolvedFramebufferBinding,
} from '../pipeline-contracts';
import type { ContextSource, IGLContext } from '../context';
import { resolveContext } from '../context';
import { resolveNativeFramebuffer } from '../internal/native-framebuffer-handle';
import { WebGL2RenderPassExecutorRegistry } from './registry';
import {
	createValidationError,
	createExecutionError,
	type WebGL2RenderPassLocale,
} from './errors';
import { WebGL2FramebufferResolver, clearFramebuffer } from './framebuffer-resolver';
import { WebGL2FullscreenProgramCache } from './fullscreen-cache';
import { createBuiltinExecutors, REQUIRED_EXECUTOR_KIND_SET } from './builtin-executors';

interface MutableProfilerState {
	frame: number;
	passCount: number;
	drawCalls: number;
	clears: number;
	presents: number;
	cpuTimeMs: number;
}

const getNow = (): number =>
	typeof performance !== 'undefined' && typeof performance.now === 'function'
		? performance.now()
		: Date.now();

const isPromiseLike = <T>(value: T | PromiseLike<T>): value is PromiseLike<T> =>
	typeof value === 'object' && value !== null && 'then' in value;

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
		return Object.freeze([]) as readonly string[];
	}

	return Object.freeze([...notes]);
};

const resolveMissingExecutorMessage = (
	locale: WebGL2RenderPassLocale,
	kind: RenderPassKind
): string =>
	locale === 'tr'
		? `'${kind}' gecisleri icin WebGL2 render pass executor kaydi bulunamadi`
		: `No WebGL2 render pass executor is registered for '${kind}' passes`;

class ManagedWebGL2RenderPassLibraryImpl implements ManagedWebGL2RenderPassLibrary {
	private readonly _ctx: IGLContext;
	private readonly _gl: WebGL2RenderingContext;
	private readonly _unsubscribeLost: () => void;
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
		this._unsubscribeLost = this._ctx.onLost(() => this.invalidateContextResources());
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
		this._ctx.state.bindFramebuffer(gl.FRAMEBUFFER, this._framebuffers.defaultFramebuffer);
		this._ctx.state.bindFramebuffer(gl.READ_FRAMEBUFFER, null);
		this._ctx.state.bindFramebuffer(gl.DRAW_FRAMEBUFFER, null);

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
		try {
			this._unsubscribeLost();
		} catch {
			// best-effort
		}
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
