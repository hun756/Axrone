import type {
	ReadonlyRenderResourceRegistry,
	RenderClearState,
	RenderExecutionContext,
	RenderResourceName,
	ResolvedRenderPass,
} from '@axrone/render-core/types';
import type {
	WebGL2RenderResourceHandle,
	WebGL2RenderTextureNativeHandle,
	WebGL2ResolvedFramebufferBinding,
} from '../pipeline-contracts';
import type { ContextSource, IGLContext } from '../context';
import { resolveContext } from '../context';
import {
	toNativeFramebufferHandle,
} from '../internal/native-framebuffer-handle';
import { createValidationError, type WebGL2RenderPassLocale } from './errors';

const isFrameResourceName = (value: RenderResourceName | null): boolean =>
	value !== null && value.startsWith('frame:');

const resolveAttachmentValidationMessage = (locale: WebGL2RenderPassLocale): string =>
	locale === 'tr'
		? 'Yonetilen WebGL2 render pass kutuphanesi yalnizca 2D framebuffer attachment tiplerini destekler'
		: 'Managed WebGL2 render pass library supports 2D framebuffer attachments only';

const resolveFramebufferCreationMessage = (locale: WebGL2RenderPassLocale): string =>
	locale === 'tr'
		? 'WebGL2 render pass kutuphanesi icin framebuffer olusturulamadi'
		: 'Failed to create WebGL2 framebuffer for render pass library';

const resolveFramebufferTargets = (
	pass: ResolvedRenderPass
): {
	readonly colorTarget: RenderResourceName | null;
	readonly colorTargets?: readonly RenderResourceName[] | null;
	readonly depthTarget: RenderResourceName | null;
} => {
	const meta = pass.metadata as unknown as { colorAttachments?: readonly RenderResourceName[]; gBuffer?: readonly RenderResourceName[] };
	if (meta.colorAttachments && Array.isArray(meta.colorAttachments) && meta.colorAttachments.length > 0) {
		return {
			colorTarget: meta.colorAttachments[0] ?? null,
			colorTargets: meta.colorAttachments,
			depthTarget: (pass.metadata as unknown as { depth?: RenderResourceName | null }).depth ?? null,
		};
	}
	if (meta.gBuffer && Array.isArray(meta.gBuffer) && meta.gBuffer.length > 0) {
		return {
			colorTarget: meta.gBuffer[0] ?? null,
			colorTargets: meta.gBuffer,
			depthTarget: (pass.metadata as unknown as { depth?: RenderResourceName | null }).depth ?? null,
		};
	}
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

export const clearFramebuffer = (
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

const createFramebufferCacheKey = (
	colorTarget: RenderResourceName | null,
	depthTarget: RenderResourceName | null
): string => `${colorTarget ?? 'none'}|${depthTarget ?? 'none'}`;

export class WebGL2FramebufferResolver {
	private readonly _cache = new Map<string, WebGLFramebuffer>();
	private readonly _defaultFramebuffer: WebGLFramebuffer | null;
	private readonly _ctx: IGLContext;
	private readonly _gl: WebGL2RenderingContext;

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
		context: RenderExecutionContext<WebGL2RenderResourceHandle>,
		colorTargets?: readonly RenderResourceName[] | null
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

		const effectiveColorKey = colorTargets && colorTargets.length > 0 ? colorTargets.join('+') : (colorTarget ?? 'none');
		const cacheKey = `${effectiveColorKey}|${depthTarget ?? 'none'}`;
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

		this._ctx.state.bindFramebuffer(this._gl.FRAMEBUFFER, framebuffer);

		const colorHandles: WebGL2RenderTextureNativeHandle[] = [];
		if (colorTargets && colorTargets.length > 0) {
			for (const ct of colorTargets) {
				const h = this.getTextureHandle(context.graph, ct);
				if (h) colorHandles.push(h);
			}
		} else if (colorHandle) colorHandles.push(colorHandle);

		if (colorHandles.length > 0) {
			const attachments: number[] = [];
			for (let i = 0; i < colorHandles.length; i++) {
				const h = colorHandles[i]!;
				this._gl.framebufferTexture2D(
					this._gl.FRAMEBUFFER,
					(this._gl.COLOR_ATTACHMENT0 as number) + i,
					this._gl.TEXTURE_2D,
					h.texture,
					0
				);
				attachments.push((this._gl.COLOR_ATTACHMENT0 as number) + i);
			}
			this._gl.drawBuffers(attachments);
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
		return this.resolveBinding(targets.colorTarget, targets.depthTarget, context, (targets as unknown as { colorTargets?: readonly RenderResourceName[] }).colorTargets ?? null);
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
