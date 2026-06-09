import {
    IShaderInstance,
    ICompiledShader,
    IShaderVariant,
    ShaderUniformValue,
    IUniformBlock,
    ShaderDataType,
} from './interfaces';
import {
    getWebGLType,
    getShaderDataTypeComponentCount,
    getShaderDataTypeSize,
} from './utils';
import { ByteBuffer } from '@axrone/memory';
import { Mat4, Vec2, Vec3, Vec4 } from '@axrone/numeric';
import {
    ShaderInstanceError,
    ShaderInstanceLifecycleError,
    ShaderInstanceValidationError,
    ShaderInstanceBackendError,
} from './errors';

type UniformValuePrimitive = number | boolean;

const FLOAT32_ARRAY_TAG = '[object Float32Array]';
const INT32_ARRAY_TAG = '[object Int32Array]';
const UINT32_ARRAY_TAG = '[object Uint32Array]';

const toObjectTag = (value: unknown): string =>
    Object.prototype.toString.call(value);

const isFloat32Array = (value: unknown): value is Float32Array =>
    toObjectTag(value) === FLOAT32_ARRAY_TAG;

const isInt32Array = (value: unknown): value is Int32Array =>
    toObjectTag(value) === INT32_ARRAY_TAG;

const isUint32Array = (value: unknown): value is Uint32Array =>
    toObjectTag(value) === UINT32_ARRAY_TAG;

const assertContext = (gl: WebGL2RenderingContext | null | undefined): WebGL2RenderingContext => {
    if (!gl) {
        throw new ShaderInstanceBackendError('BACKEND_UNAVAILABLE', 'en', {
            reason: 'no-context-bound',
        });
    }
    return gl;
};

class UniformUploader {
    private readonly gl: WebGL2RenderingContext;

    constructor(gl: WebGL2RenderingContext) {
        this.gl = gl;
    }

    uploadUniform(
        location: WebGLUniformLocation,
        type: ShaderDataType,
        value: ShaderUniformValue
    ): void {
        if (value === null || value === undefined) {
            return;
        }

        switch (type) {
            case ShaderDataType.FLOAT:
                this.gl.uniform1f(location, value as number);
                break;

            case ShaderDataType.VEC2:
                this.uploadVec2(location, value);
                break;

            case ShaderDataType.VEC3:
                this.uploadVec3(location, value);
                break;

            case ShaderDataType.VEC4:
                this.uploadVec4(location, value);
                break;

            case ShaderDataType.MAT2:
            case ShaderDataType.MAT3:
            case ShaderDataType.MAT4:
                this.uploadMatrix(location, type, value);
                break;

            case ShaderDataType.INT:
            case ShaderDataType.BOOL:
                this.gl.uniform1i(location, value as number);
                break;

            case ShaderDataType.UINT:
                this.gl.uniform1ui(location, value as number);
                break;

            case ShaderDataType.IVEC2:
                this.uploadIVec2(location, value);
                break;

            case ShaderDataType.IVEC3:
                this.uploadIVec3(location, value);
                break;

            case ShaderDataType.IVEC4:
                this.uploadIVec4(location, value);
                break;

            case ShaderDataType.UVEC2:
            case ShaderDataType.UVEC3:
            case ShaderDataType.UVEC4:
                this.uploadUVec(location, type, value);
                break;

            case ShaderDataType.BVEC2:
            case ShaderDataType.BVEC3:
            case ShaderDataType.BVEC4:
                this.uploadBVec(location, type, value);
                break;

            case ShaderDataType.SAMPLER_2D:
            case ShaderDataType.SAMPLER_CUBE:
            case ShaderDataType.SAMPLER_2D_ARRAY:
                this.gl.uniform1i(location, value as number);
                break;

            default:
                throw new ShaderInstanceValidationError('INVALID_VALUE_TYPE', 'en', {
                    type,
                });
        }
    }

    private uploadVec2(location: WebGLUniformLocation, value: ShaderUniformValue): void {
        if (value instanceof Vec2) {
            this.gl.uniform2f(location, value.x, value.y);
        } else if (isFloat32Array(value)) {
            this.gl.uniform2fv(location, value);
        } else if (Array.isArray(value)) {
            this.gl.uniform2f(location, value[0] as number, value[1] as number);
        } else {
            throw new ShaderInstanceValidationError('INVALID_VALUE_TYPE', 'en', {
                expected: 'vec2-compatible',
            });
        }
    }

    private uploadVec3(location: WebGLUniformLocation, value: ShaderUniformValue): void {
        if (value instanceof Vec3) {
            this.gl.uniform3f(location, value.x, value.y, value.z);
        } else if (isFloat32Array(value)) {
            this.gl.uniform3fv(location, value);
        } else if (Array.isArray(value)) {
            this.gl.uniform3f(location, value[0] as number, value[1] as number, value[2] as number);
        } else {
            throw new ShaderInstanceValidationError('INVALID_VALUE_TYPE', 'en', {
                expected: 'vec3-compatible',
            });
        }
    }

    private uploadVec4(location: WebGLUniformLocation, value: ShaderUniformValue): void {
        if (value instanceof Vec4) {
            this.gl.uniform4f(location, value.x, value.y, value.z, value.w);
        } else if (isFloat32Array(value)) {
            this.gl.uniform4fv(location, value);
        } else if (Array.isArray(value)) {
            this.gl.uniform4f(
                location,
                value[0] as number,
                value[1] as number,
                value[2] as number,
                value[3] as number
            );
        } else {
            throw new ShaderInstanceValidationError('INVALID_VALUE_TYPE', 'en', {
                expected: 'vec4-compatible',
            });
        }
    }

    private uploadMatrix(
        location: WebGLUniformLocation,
        type: ShaderDataType,
        value: ShaderUniformValue
    ): void {
        const fn =
            type === ShaderDataType.MAT2
                ? this.gl.uniformMatrix2fv
                : type === ShaderDataType.MAT3
                ? this.gl.uniformMatrix3fv
                : this.gl.uniformMatrix4fv;

        if (value instanceof Mat4) {
            fn.call(this.gl, location, false, value.data);
        } else if (isFloat32Array(value)) {
            fn.call(this.gl, location, false, value);
        } else {
            throw new ShaderInstanceValidationError('INVALID_VALUE_TYPE', 'en', {
                expected: `${type}-compatible`,
            });
        }
    }

    private uploadIVec2(location: WebGLUniformLocation, value: ShaderUniformValue): void {
        if (isInt32Array(value)) {
            this.gl.uniform2iv(location, value);
        } else if (Array.isArray(value)) {
            this.gl.uniform2i(location, value[0] as number, value[1] as number);
        } else {
            throw new ShaderInstanceValidationError('INVALID_VALUE_TYPE', 'en', {
                expected: 'ivec2-compatible',
            });
        }
    }

    private uploadIVec3(location: WebGLUniformLocation, value: ShaderUniformValue): void {
        if (isInt32Array(value)) {
            this.gl.uniform3iv(location, value);
        } else if (Array.isArray(value)) {
            this.gl.uniform3i(
                location,
                value[0] as number,
                value[1] as number,
                value[2] as number
            );
        } else {
            throw new ShaderInstanceValidationError('INVALID_VALUE_TYPE', 'en', {
                expected: 'ivec3-compatible',
            });
        }
    }

    private uploadIVec4(location: WebGLUniformLocation, value: ShaderUniformValue): void {
        if (isInt32Array(value)) {
            this.gl.uniform4iv(location, value);
        } else if (Array.isArray(value)) {
            this.gl.uniform4i(
                location,
                value[0] as number,
                value[1] as number,
                value[2] as number,
                value[3] as number
            );
        } else {
            throw new ShaderInstanceValidationError('INVALID_VALUE_TYPE', 'en', {
                expected: 'ivec4-compatible',
            });
        }
    }

    private uploadUVec(
        location: WebGLUniformLocation,
        type: ShaderDataType,
        value: ShaderUniformValue
    ): void {
        if (isUint32Array(value)) {
            if (type === ShaderDataType.UVEC2) this.gl.uniform2uiv(location, value);
            else if (type === ShaderDataType.UVEC3) this.gl.uniform3uiv(location, value);
            else this.gl.uniform4uiv(location, value);
        } else if (Array.isArray(value)) {
            const v0 = value[0] as number;
            const v1 = value[1] as number;
            const v2 = value[2] as number;
            const v3 = value[3] as number;
            if (type === ShaderDataType.UVEC2) this.gl.uniform2ui(location, v0, v1);
            else if (type === ShaderDataType.UVEC3) this.gl.uniform3ui(location, v0, v1, v2);
            else this.gl.uniform4ui(location, v0, v1, v2, v3);
        } else {
            throw new ShaderInstanceValidationError('INVALID_VALUE_TYPE', 'en', {
                expected: `${type}-compatible`,
            });
        }
    }

    private uploadBVec(
        location: WebGLUniformLocation,
        type: ShaderDataType,
        value: ShaderUniformValue
    ): void {
        const toI = (v: UniformValuePrimitive): number => (v ? 1 : 0);
        if (isInt32Array(value) || Array.isArray(value)) {
            const data = isInt32Array(value) ? value : (value as number[]);
            if (type === ShaderDataType.BVEC2)
                this.gl.uniform2i(location, toI(Boolean(data[0])), toI(Boolean(data[1])));
            else if (type === ShaderDataType.BVEC3)
                this.gl.uniform3i(
                    location,
                    toI(Boolean(data[0])),
                    toI(Boolean(data[1])),
                    toI(Boolean(data[2]))
                );
            else
                this.gl.uniform4i(
                    location,
                    toI(Boolean(data[0])),
                    toI(Boolean(data[1])),
                    toI(Boolean(data[2])),
                    toI(Boolean(data[3]))
                );
            return;
        }
        throw new ShaderInstanceValidationError('INVALID_VALUE_TYPE', 'en', {
            expected: `${type}-compatible`,
        });
    }
}

interface UniformDescriptor {
    readonly name: string;
    readonly type: ShaderDataType;
    readonly arraySize: number;
    readonly location: WebGLUniformLocation | null;
    readonly category: 'material' | 'frame' | 'camera' | 'object' | 'lighting' | 'system';
}

interface AttributeDescriptor {
    readonly name: string;
    readonly type: ShaderDataType;
    readonly location: number;
    readonly size: number;
    readonly binding: number;
    readonly divisor: number;
}

interface UniformBatchEntry {
    readonly descriptor: UniformDescriptor;
    readonly value: ShaderUniformValue;
}

interface ShaderInstanceStats {
    readonly uniformUpdateCount: number;
    readonly dirtyUniforms: number;
    readonly dirtyBuffers: number;
    readonly boundTextures: number;
    readonly programBinds: number;
    readonly stateChanges: number;
    readonly uploadSkipped: number;
}

const EPSILON = 1e-6;

const areFloatsEqual = (a: number, b: number): boolean => Math.abs(a - b) <= EPSILON;

const isUniformValueEqual = (a: ShaderUniformValue, b: ShaderUniformValue): boolean => {
    if (a === b) return true;
    if (a === null || b === null) return false;
    const ta = typeof a;
    const tb = typeof b;
    if (ta !== tb) return false;

    if (isFloat32Array(a) && isFloat32Array(b)) {
        if (a.length !== b.length) return false;
        for (let i = 0; i < a.length; i++) {
            if (!areFloatsEqual(a[i] as number, b[i] as number)) return false;
        }
        return true;
    }
    if (isInt32Array(a) && isInt32Array(b)) {
        if (a.length !== b.length) return false;
        for (let i = 0; i < a.length; i++) {
            if (a[i] !== b[i]) return false;
        }
        return true;
    }
    if (isUint32Array(a) && isUint32Array(b)) {
        if (a.length !== b.length) return false;
        for (let i = 0; i < a.length; i++) {
            if (a[i] !== b[i]) return false;
        }
        return true;
    }
    if (a instanceof Vec2 && b instanceof Vec2) return a.equals(b);
    if (a instanceof Vec3 && b instanceof Vec3) return a.equals(b);
    if (a instanceof Vec4 && b instanceof Vec4) return a.equals(b);
    if (a instanceof Mat4 && b instanceof Mat4) return a.equals(b);

    if (Array.isArray(a) && Array.isArray(b)) {
        if (a.length !== b.length) return false;
        for (let i = 0; i < a.length; i++) {
            const av = a[i];
            const bv = b[i];
            if (typeof av === 'number' && typeof bv === 'number') {
                if (!areFloatsEqual(av, bv)) return false;
            } else if (av !== bv) {
                return false;
            }
        }
        return true;
    }

    return false;
};

const valueToString = (value: ShaderUniformValue): string => {
    if (value === null) return 'null';
    if (typeof value === 'number' || typeof value === 'boolean') return String(value);
    if (isFloat32Array(value) || isInt32Array(value) || isUint32Array(value)) {
        return Array.from(value).join(',');
    }
    if (value instanceof Vec2) return `Vec2(${value.x},${value.y})`;
    if (value instanceof Vec3) return `Vec3(${value.x},${value.y},${value.z})`;
    if (value instanceof Vec4) return `Vec4(${value.x},${value.y},${value.z},${value.w})`;
    if (value instanceof Mat4) return 'Mat4';
    if (value instanceof WebGLTexture) return 'WebGLTexture';
    if (Array.isArray(value)) return `[${(value as unknown as { length: number }).length}]`;
    return typeof value;
};

const categoryOrder: Readonly<Record<UniformDescriptor['category'], number>> = {
    system: 0,
    camera: 1,
    frame: 2,
    lighting: 3,
    object: 4,
    material: 5,
};

const DEFAULT_BATCH_CAPACITY = 64;

export class ShaderInstance implements IShaderInstance {
    public readonly shader: ICompiledShader;
    public readonly variant: IShaderVariant;
    public readonly uniforms = new Map<string, ShaderUniformValue>();
    public readonly textures = new Map<string, WebGLTexture>();
    public readonly uniformBuffers = new Map<string, ByteBuffer>();

    private readonly gl: WebGL2RenderingContext | null;
    private readonly uniformUploader: UniformUploader;
    private readonly uniformDescriptors: Map<string, UniformDescriptor> = new Map();
    private readonly attributeDescriptors: Map<string, AttributeDescriptor> = new Map();
    private readonly textureSlots: Map<string, number> = new Map();
    private readonly uniformBlockBindings: Map<string, number> = new Map();

    private readonly dirtyUniforms: Set<string> = new Set();
    private readonly dirtyBuffers: Set<string> = new Set();
    private readonly boundTextureUnits: Set<number> = new Set();

    private uniformUpdateCount = 0;
    private programBindCount = 0;
    private stateChangeCount = 0;
    private uploadSkipped = 0;
    private lastBoundProgram: WebGLProgram | null = null;
    private disposed = false;

    private batchActive = false;
    private readonly batchBuffer: UniformBatchEntry[] = [];
    private readonly batchCapacity: number;

    constructor(
        shader: ICompiledShader,
        variant: IShaderVariant,
        gl?: WebGL2RenderingContext | null,
        options?: { batchCapacity?: number }
    ) {
        this.shader = shader;
        this.variant = variant;
        this.gl = gl ?? null;
        this.batchCapacity = Math.max(1, options?.batchCapacity ?? DEFAULT_BATCH_CAPACITY);
        this.uniformUploader = this.gl ? new UniformUploader(this.gl) : new UniformUploader(assertContext(null));
        this.buildDescriptors();
        this.initializeDefaultValues();
    }

    setUniform(name: string, value: ShaderUniformValue): void {
        if (this.disposed) {
            throw new ShaderInstanceLifecycleError('PROGRAM_DISPOSED', 'en', { name });
        }

        const descriptor = this.uniformDescriptors.get(name);
        if (!descriptor) {
            throw new ShaderInstanceValidationError('UNIFORM_NOT_FOUND', 'en', {
                name,
                shader: this.shader.name,
            });
        }

        if (descriptor.location === null) {
            this.uploadSkipped++;
            return;
        }

        if (this.batchActive) {
            if (this.batchBuffer.length >= this.batchCapacity) {
                throw new ShaderInstanceLifecycleError('BATCH_OVERFLOW', 'en', {
                    capacity: this.batchCapacity,
                });
            }
            this.batchBuffer.push({ descriptor, value });
            this.uniforms.set(name, value);
            this.dirtyUniforms.add(name);
            return;
        }

        const current = this.uniforms.get(name);
        if (current !== undefined && isUniformValueEqual(current, value)) {
            this.uploadSkipped++;
            return;
        }

        this.uniforms.set(name, value);
        this.dirtyUniforms.add(name);
    }

    setUniformIfChanged(name: string, value: ShaderUniformValue): boolean {
        if (!this.uniformDescriptors.has(name)) return false;
        const current = this.uniforms.get(name);
        if (current !== undefined && isUniformValueEqual(current, value)) return false;
        this.setUniform(name, value);
        return true;
    }

    setUniformBlock(name: string, values: Readonly<Record<string, ShaderUniformValue>>): void {
        if (this.disposed) {
            throw new ShaderInstanceLifecycleError('PROGRAM_DISPOSED', 'en', { name });
        }
        const binding = this.uniformBlockBindings.get(name);
        if (binding === undefined) {
            throw new ShaderInstanceValidationError('UNIFORM_NOT_FOUND', 'en', {
                block: name,
            });
        }
        const block = this.shader.uniformBlocks.get(name);
        if (!block) return;
        const buffer = ByteBuffer.alloc(block.size);
        for (const variable of block.variables) {
            const v = values[variable.name];
            if (v === undefined) continue;
            this.writeBlockMember(buffer, variable, v);
        }
        this.uniformBuffers.set(name, buffer);
        this.dirtyBuffers.add(name);
    }

    setTexture(name: string, texture: WebGLTexture): void {
        if (this.disposed) {
            throw new ShaderInstanceLifecycleError('PROGRAM_DISPOSED', 'en', { name });
        }
        if (!this.textureSlots.has(name)) {
            throw new ShaderInstanceValidationError('UNIFORM_NOT_FOUND', 'en', {
                name,
                kind: 'texture',
            });
        }
        this.textures.set(name, texture);
    }

    setUniformBuffer(name: string, buffer: ByteBuffer): void {
        if (this.disposed) {
            throw new ShaderInstanceLifecycleError('PROGRAM_DISPOSED', 'en', { name });
        }
        if (!this.uniformBlockBindings.has(name)) {
            throw new ShaderInstanceValidationError('UNIFORM_NOT_FOUND', 'en', {
                name,
                kind: 'uniform-buffer',
            });
        }
        this.uniformBuffers.set(name, buffer);
        this.dirtyBuffers.add(name);
    }

    beginBatch(): void {
        if (this.batchActive) {
            throw new ShaderInstanceError('BATCH_ALREADY_STARTED', 'en');
        }
        this.batchActive = true;
        this.batchBuffer.length = 0;
    }

    commitBatch(): void {
        if (!this.batchActive) {
            throw new ShaderInstanceError('BATCH_NOT_STARTED', 'en');
        }
        this.batchActive = false;
        this.batchBuffer.length = 0;
    }

    bind(gl: WebGL2RenderingContext): void {
        if (this.disposed) {
            throw new ShaderInstanceLifecycleError('PROGRAM_DISPOSED', 'en', {
                shader: this.shader.name,
            });
        }
        const program = this.variant.shader.program;
        if (this.lastBoundProgram !== program) {
            gl.useProgram(program);
            this.lastBoundProgram = program;
            this.programBindCount++;
        }

        this.flushDirtyUniforms(gl);
        this.bindTextures(gl);
        this.flushUniformBuffers(gl);
    }

    unbind(gl: WebGL2RenderingContext): void {
        for (const unit of this.boundTextureUnits) {
            gl.activeTexture(gl.TEXTURE0 + unit);
            gl.bindTexture(gl.TEXTURE_2D, null);
        }
        this.boundTextureUnits.clear();
    }

    getUniform(name: string): ShaderUniformValue {
        return this.uniforms.get(name) ?? null;
    }

    hasUniform(name: string): boolean {
        return this.uniformDescriptors.has(name);
    }

    hasAttribute(name: string): boolean {
        return this.attributeDescriptors.has(name);
    }

    getAttributeLocation(name: string): number {
        return this.attributeDescriptors.get(name)?.location ?? -1;
    }

    getUniformNames(): string[] {
        return Array.from(this.uniformDescriptors.keys());
    }

    getAttributeNames(): string[] {
        return Array.from(this.attributeDescriptors.keys());
    }

    getRenderState() {
        return this.shader.renderState;
    }

    getStats(): Readonly<ShaderInstanceStats> {
        return {
            uniformUpdateCount: this.uniformUpdateCount,
            dirtyUniforms: this.dirtyUniforms.size,
            dirtyBuffers: this.dirtyBuffers.size,
            boundTextures: this.textures.size,
            programBinds: this.programBindCount,
            stateChanges: this.stateChangeCount,
            uploadSkipped: this.uploadSkipped,
        };
    }

    isDisposed(): boolean {
        return this.disposed;
    }

    dispose(): void {
        if (this.disposed) return;
        this.disposed = true;
        this.uniforms.clear();
        this.textures.clear();
        this.uniformBuffers.clear();
        this.dirtyUniforms.clear();
        this.dirtyBuffers.clear();
        this.boundTextureUnits.clear();
        this.batchBuffer.length = 0;
        this.lastBoundProgram = null;
    }

    private buildDescriptors(): void {
        for (const uniform of this.shader.configuration.uniforms) {
            const location = this.shader.uniformLocations.get(uniform.name) ?? null;
            this.uniformDescriptors.set(uniform.name, {
                name: uniform.name,
                type: uniform.type,
                arraySize: uniform.arraySize ?? 1,
                location,
                category: uniform.category ?? 'material',
            });
        }

        for (const attribute of this.shader.configuration.attributes) {
            const location = this.shader.attributeLocations.get(attribute.name) ?? -1;
            this.attributeDescriptors.set(attribute.name, {
                name: attribute.name,
                type: attribute.type,
                location,
                size: getShaderDataTypeComponentCount(attribute.type),
                binding: attribute.binding,
                divisor: attribute.divisor ?? 0,
            });
        }

        for (const texture of this.shader.configuration.textures) {
            this.textureSlots.set(texture.name, texture.slot);
        }

        for (const [name, block] of this.shader.uniformBlocks) {
            this.uniformBlockBindings.set(name, block.binding);
        }
    }

    private initializeDefaultValues(): void {
        for (const descriptor of this.uniformDescriptors.values()) {
            const source = this.shader.configuration.uniforms.find(
                (u) => u.name === descriptor.name
            );
            if (source?.defaultValue !== undefined) {
                this.uniforms.set(descriptor.name, source.defaultValue);
            }
        }
        for (const [name, block] of this.shader.uniformBlocks) {
            if (block.buffer) {
                this.uniformBuffers.set(name, block.buffer);
            }
        }
    }

    private flushDirtyUniforms(gl: WebGL2RenderingContext): void {
        if (this.batchActive && this.batchBuffer.length > 0) {
            this.batchActive = false;
            const drained = this.batchBuffer.slice();
            this.batchBuffer.length = 0;
            for (const entry of drained) {
                if (entry.descriptor.location) {
                    this.uniformUploader.uploadUniform(
                        entry.descriptor.location,
                        entry.descriptor.type,
                        entry.value
                    );
                    this.uniformUpdateCount++;
                }
            }
            this.dirtyUniforms.clear();
            return;
        }

        if (this.dirtyUniforms.size === 0) return;

        const ordered = Array.from(this.dirtyUniforms)
            .map((name) => this.uniformDescriptors.get(name))
            .filter((d): d is UniformDescriptor => d !== undefined)
            .sort(
                (a, b) =>
                    categoryOrder[a.category] - categoryOrder[b.category] ||
                    a.name.localeCompare(b.name)
            );

        for (const descriptor of ordered) {
            const value = this.uniforms.get(descriptor.name);
            if (value === undefined || descriptor.location === null) continue;
            this.uniformUploader.uploadUniform(
                descriptor.location,
                descriptor.type,
                value
            );
            this.uniformUpdateCount++;
        }

        this.dirtyUniforms.clear();
    }

    private bindTextures(gl: WebGL2RenderingContext): void {
        for (const [textureName, texture] of this.textures) {
            const slot = this.textureSlots.get(textureName);
            const location = this.shader.uniformLocations.get(textureName);
            if (slot === undefined || !location) continue;
            gl.activeTexture(gl.TEXTURE0 + slot);
            gl.bindTexture(gl.TEXTURE_2D, texture);
            gl.uniform1i(location, slot);
            this.boundTextureUnits.add(slot);
        }
    }

    private flushUniformBuffers(gl: WebGL2RenderingContext): void {
        if (this.dirtyBuffers.size === 0) return;
        for (const bufferName of this.dirtyBuffers) {
            const binding = this.uniformBlockBindings.get(bufferName);
            const buffer = this.uniformBuffers.get(bufferName);
            if (binding === undefined || !buffer) continue;
            const glBuffer = gl.createBuffer();
            if (!glBuffer) {
                throw new ShaderInstanceLifecycleError('OUT_OF_MEMORY', 'en', {
                    reason: 'uniform-buffer-creation-failed',
                });
            }
            gl.bindBuffer(gl.UNIFORM_BUFFER, glBuffer);
            const source = buffer as unknown as { buffer?: ArrayBuffer; byteOffset?: number };
            gl.bufferData(
                gl.UNIFORM_BUFFER,
                source.buffer ?? (buffer as unknown as ArrayBuffer),
                gl.DYNAMIC_DRAW
            );
            gl.bindBufferBase(gl.UNIFORM_BUFFER, binding, glBuffer);
        }
        this.dirtyBuffers.clear();
    }

    private writeBlockMember(
        buffer: ByteBuffer,
        variable: IUniformBlock['variables'][number],
        value: ShaderUniformValue
    ): void {
        const size = getShaderDataTypeSize(variable.type);
        if (typeof value === 'number') {
            buffer.putFloat32(value);
            const pad = size - 4;
            for (let i = 0; i < pad; i += 4) buffer.putFloat32(0);
        }
    }

    static isShaderInstance(value: unknown): value is ShaderInstance {
        return value instanceof ShaderInstance;
    }

    toString(): string {
        return `ShaderInstance<${this.shader.name}#${this.variant.hash}>`;
    }

    [Symbol.for('nodejs.util.inspect.custom')](): string {
        return this.toString();
    }

    describe(): Readonly<{
        shader: string;
        variant: string;
        uniforms: ReadonlyArray<{
            name: string;
            type: ShaderDataType;
            category: UniformDescriptor['category'];
            value: string;
        }>;
    }> {
        const uniforms = Array.from(this.uniformDescriptors.values()).map((d) => ({
            name: d.name,
            type: d.type,
            category: d.category,
            value: valueToString(this.uniforms.get(d.name) ?? null),
        }));
        return Object.freeze({
            shader: this.shader.name,
            variant: this.variant.hash,
            uniforms: Object.freeze(uniforms),
        });
    }
}
