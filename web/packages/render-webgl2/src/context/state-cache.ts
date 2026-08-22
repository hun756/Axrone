import type { GLStateSnapshot, IGLStateCache } from './types';

type TextureBinding = { target: number; texture: WebGLTexture | null };

export class GLStateCache implements IGLStateCache {
    readonly #gl: WebGL2RenderingContext;
    readonly #enabled: boolean;

    #boundArrayBuffer: WebGLBuffer | null = null;
    #boundElementArrayBuffer: WebGLBuffer | null = null;
    #boundFramebuffer: WebGLFramebuffer | null = null;
    #boundReadFramebuffer: WebGLFramebuffer | null = null;
    #boundDrawFramebuffer: WebGLFramebuffer | null = null;
    #boundRenderbuffer: WebGLRenderbuffer | null = null;
    #boundVertexArray: WebGLVertexArrayObject | null = null;
    #currentProgram: WebGLProgram | null = null;
    #activeTextureUnit = 0;
    #viewport: [number, number, number, number] = [0, 0, 0, 0];
    #scissor: [number, number, number, number] = [0, 0, 0, 0];
    #capabilities = new Map<number, boolean>();
    #textureBindings = new Map<number, TextureBinding>();
    #samplerBindings = new Map<number, WebGLSampler | null>();
    #invalidated = false;

    constructor(gl: WebGL2RenderingContext, enabled = true) {
        this.#gl = gl;
        this.#enabled = enabled;
    }

    public get snapshot(): GLStateSnapshot {
        return Object.freeze({
            boundArrayBuffer: this.#boundArrayBuffer,
            boundElementArrayBuffer: this.#boundElementArrayBuffer,
            boundFramebuffer: this.#boundFramebuffer,
            boundReadFramebuffer: this.#boundReadFramebuffer,
            boundDrawFramebuffer: this.#boundDrawFramebuffer,
            boundRenderbuffer: this.#boundRenderbuffer,
            boundVertexArray: this.#boundVertexArray,
            currentProgram: this.#currentProgram,
            activeTextureUnit: this.#activeTextureUnit,
            viewport: Object.freeze([...this.#viewport]) as readonly [number, number, number, number],
            scissor: Object.freeze([...this.#scissor]) as readonly [number, number, number, number],
            blendEnabled: this.#capabilities.get(this.#gl.BLEND) ?? false,
            cullFaceEnabled: this.#capabilities.get(this.#gl.CULL_FACE) ?? false,
            depthTestEnabled: this.#capabilities.get(this.#gl.DEPTH_TEST) ?? false,
            scissorTestEnabled: this.#capabilities.get(this.#gl.SCISSOR_TEST) ?? false,
            stencilTestEnabled: this.#capabilities.get(this.#gl.STENCIL_TEST) ?? false,
        });
    }

    public bindBuffer(target: number, buffer: WebGLBuffer | null): void {
        if (!this.#enabled || this.#invalidated) {
            this.#gl.bindBuffer(target, buffer);
            this.#trackBuffer(target, buffer);
            return;
        }
        if (target === this.#gl.ARRAY_BUFFER) {
            if (this.#boundArrayBuffer === buffer) return;
            this.#boundArrayBuffer = buffer;
        } else if (target === this.#gl.ELEMENT_ARRAY_BUFFER) {
            if (this.#boundElementArrayBuffer === buffer) return;
            this.#boundElementArrayBuffer = buffer;
        } else if (target === this.#gl.COPY_READ_BUFFER || target === this.#gl.COPY_WRITE_BUFFER) {
            this.#gl.bindBuffer(target, buffer);
            return;
        } else if (target === this.#gl.PIXEL_PACK_BUFFER || target === this.#gl.PIXEL_UNPACK_BUFFER) {
            this.#gl.bindBuffer(target, buffer);
            return;
        }
        this.#gl.bindBuffer(target, buffer);
    }

    public bindFramebuffer(target: number, framebuffer: WebGLFramebuffer | null): void {
        if (!this.#enabled || this.#invalidated) {
            this.#gl.bindFramebuffer(target, framebuffer);
            this.#trackFramebuffer(target, framebuffer);
            return;
        }
        if (target === this.#gl.FRAMEBUFFER) {
            if (this.#boundFramebuffer === framebuffer && this.#boundReadFramebuffer === framebuffer && this.#boundDrawFramebuffer === framebuffer) {
                return;
            }
            this.#boundFramebuffer = framebuffer;
            this.#boundReadFramebuffer = framebuffer;
            this.#boundDrawFramebuffer = framebuffer;
        } else if (target === this.#gl.READ_FRAMEBUFFER) {
            if (this.#boundReadFramebuffer === framebuffer) return;
            this.#boundReadFramebuffer = framebuffer;
            if (this.#boundFramebuffer !== null && framebuffer !== this.#boundFramebuffer) {
                this.#boundFramebuffer = null;
            }
        } else if (target === this.#gl.DRAW_FRAMEBUFFER) {
            if (this.#boundDrawFramebuffer === framebuffer) return;
            this.#boundDrawFramebuffer = framebuffer;
            if (this.#boundFramebuffer !== null && framebuffer !== this.#boundFramebuffer) {
                this.#boundFramebuffer = null;
            }
        }
        this.#gl.bindFramebuffer(target, framebuffer);
    }

    public bindRenderbuffer(target: number, renderbuffer: WebGLRenderbuffer | null): void {
        if (!this.#enabled || this.#invalidated) {
            this.#gl.bindRenderbuffer(target, renderbuffer);
            this.#boundRenderbuffer = renderbuffer;
            return;
        }
        if (this.#boundRenderbuffer === renderbuffer) return;
        this.#boundRenderbuffer = renderbuffer;
        this.#gl.bindRenderbuffer(target, renderbuffer);
    }

    public bindVertexArray(vao: WebGLVertexArrayObject | null): void {
        if (!this.#enabled || this.#invalidated) {
            this.#gl.bindVertexArray(vao);
            this.#boundVertexArray = vao;
            return;
        }
        if (this.#boundVertexArray === vao) return;
        this.#boundVertexArray = vao;
        this.#gl.bindVertexArray(vao);
    }

    public useProgram(program: WebGLProgram | null): void {
        if (!this.#enabled || this.#invalidated) {
            this.#gl.useProgram(program);
            this.#currentProgram = program;
            return;
        }
        if (this.#currentProgram === program) return;
        this.#currentProgram = program;
        this.#gl.useProgram(program);
    }

    public activeTexture(unit: number): void {
        if (!this.#enabled || this.#invalidated) {
            this.#gl.activeTexture(this.#gl.TEXTURE0 + unit);
            this.#activeTextureUnit = unit;
            return;
        }
        if (this.#activeTextureUnit === unit) return;
        this.#activeTextureUnit = unit;
        this.#gl.activeTexture(this.#gl.TEXTURE0 + unit);
    }

    public bindTexture(target: number, texture: WebGLTexture | null): void {
        const unit = this.#activeTextureUnit;
        const key = unit;
        const existing = this.#textureBindings.get(key);
        if (this.#enabled && !this.#invalidated && existing && existing.target === target && existing.texture === texture) return;
        this.#textureBindings.set(key, { target, texture });
        this.#gl.bindTexture(target, texture);
    }

    public bindSampler(unit: number, sampler: WebGLSampler | null): void {
        if (this.#enabled && !this.#invalidated) {
            const existing = this.#samplerBindings.get(unit);
            if (existing === sampler) return;
        }
        this.#samplerBindings.set(unit, sampler);
        this.#gl.bindSampler(unit, sampler);
    }

    public enable(cap: number): void {
        if (this.#enabled && !this.#invalidated) {
            if (this.#capabilities.get(cap) === true) return;
            this.#capabilities.set(cap, true);
        } else {
            this.#capabilities.set(cap, true);
        }
        this.#gl.enable(cap);
    }

    public disable(cap: number): void {
        if (this.#enabled && !this.#invalidated) {
            if (this.#capabilities.get(cap) === false) return;
            this.#capabilities.set(cap, false);
        } else {
            this.#capabilities.set(cap, false);
        }
        this.#gl.disable(cap);
    }

    public viewport(x: number, y: number, width: number, height: number): void {
        if (this.#enabled && !this.#invalidated) {
            if (
                this.#viewport[0] === x &&
                this.#viewport[1] === y &&
                this.#viewport[2] === width &&
                this.#viewport[3] === height
            )
                return;
            this.#viewport[0] = x;
            this.#viewport[1] = y;
            this.#viewport[2] = width;
            this.#viewport[3] = height;
        } else {
            this.#viewport[0] = x;
            this.#viewport[1] = y;
            this.#viewport[2] = width;
            this.#viewport[3] = height;
        }
        this.#gl.viewport(x, y, width, height);
    }

    public scissor(x: number, y: number, width: number, height: number): void {
        if (this.#enabled && !this.#invalidated) {
            if (
                this.#scissor[0] === x &&
                this.#scissor[1] === y &&
                this.#scissor[2] === width &&
                this.#scissor[3] === height
            )
                return;
            this.#scissor[0] = x;
            this.#scissor[1] = y;
            this.#scissor[2] = width;
            this.#scissor[3] = height;
        } else {
            this.#scissor[0] = x;
            this.#scissor[1] = y;
            this.#scissor[2] = width;
            this.#scissor[3] = height;
        }
        this.#gl.scissor(x, y, width, height);
    }

    public reset(): void {
        this.#boundArrayBuffer = null;
        this.#boundElementArrayBuffer = null;
        this.#boundFramebuffer = null;
        this.#boundReadFramebuffer = null;
        this.#boundDrawFramebuffer = null;
        this.#boundRenderbuffer = null;
        this.#boundVertexArray = null;
        this.#currentProgram = null;
        this.#activeTextureUnit = 0;
        this.#viewport = [0, 0, 0, 0];
        this.#scissor = [0, 0, 0, 0];
        this.#capabilities.clear();
        this.#textureBindings.clear();
        this.#samplerBindings.clear();
        this.#invalidated = false;
    }

    public invalidate(): void {
        this.#invalidated = true;
        this.#boundArrayBuffer = null;
        this.#boundElementArrayBuffer = null;
        this.#boundFramebuffer = null;
        this.#boundReadFramebuffer = null;
        this.#boundDrawFramebuffer = null;
        this.#boundRenderbuffer = null;
        this.#boundVertexArray = null;
        this.#currentProgram = null;
        this.#textureBindings.clear();
        this.#samplerBindings.clear();
        this.#capabilities.clear();
    }

    #trackBuffer = (target: number, buffer: WebGLBuffer | null): void => {
        if (target === this.#gl.ARRAY_BUFFER) this.#boundArrayBuffer = buffer;
        else if (target === this.#gl.ELEMENT_ARRAY_BUFFER) this.#boundElementArrayBuffer = buffer;
    };

    #trackFramebuffer = (target: number, framebuffer: WebGLFramebuffer | null): void => {
        if (target === this.#gl.FRAMEBUFFER) {
            this.#boundFramebuffer = framebuffer;
            this.#boundReadFramebuffer = framebuffer;
            this.#boundDrawFramebuffer = framebuffer;
        } else if (target === this.#gl.READ_FRAMEBUFFER) this.#boundReadFramebuffer = framebuffer;
        else if (target === this.#gl.DRAW_FRAMEBUFFER) this.#boundDrawFramebuffer = framebuffer;
    };
}

export const createStateCache = (gl: WebGL2RenderingContext, enabled = true): IGLStateCache =>
    new GLStateCache(gl, enabled);
