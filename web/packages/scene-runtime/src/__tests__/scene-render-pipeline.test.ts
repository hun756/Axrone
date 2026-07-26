import { Mat4 } from '@axrone/numeric';
import { describe, expect, it, vi } from 'vitest';
import { SceneRenderFrameState } from '../rendering/render-frame-state';
import { SceneRenderPipeline } from '../rendering/scene-render-pipeline';

const createMockGL = (): WebGL2RenderingContext => {
    let nextTextureId = 1;
    let nextFramebufferId = 1;
    let nextShaderId = 1;
    let nextProgramId = 1;
    let nextVertexArrayId = 1;

    return {
        FRAMEBUFFER: 0x8d40,
        READ_FRAMEBUFFER: 0x8ca8,
        DRAW_FRAMEBUFFER: 0x8ca9,
        DEPTH_TEST: 0x0b71,
        CULL_FACE: 0x0b44,
        BLEND: 0x0be2,
        TEXTURE_2D: 0x0de1,
        TEXTURE0: 0x84c0,
        TEXTURE_MIN_FILTER: 0x2801,
        TEXTURE_MAG_FILTER: 0x2800,
        TEXTURE_WRAP_S: 0x2802,
        TEXTURE_WRAP_T: 0x2803,
        CLAMP_TO_EDGE: 0x812f,
        NEAREST: 0x2600,
        COLOR_ATTACHMENT0: 0x8ce0,
        DEPTH_ATTACHMENT: 0x8d00,
        COLOR_BUFFER_BIT: 0x4000,
        DEPTH_BUFFER_BIT: 0x0100,
        RGBA8: 0x8058,
        RGBA16F: 0x881a,
        RGBA: 0x1908,
        HALF_FLOAT: 0x140b,
        UNSIGNED_BYTE: 0x1401,
        FLOAT: 0x1406,
        TRIANGLES: 0x0004,
        VERTEX_SHADER: 0x8b31,
        FRAGMENT_SHADER: 0x8b30,
        COMPILE_STATUS: 0x8b81,
        LINK_STATUS: 0x8b82,
        DEPTH_COMPONENT24: 0x81a6,
        DEPTH_COMPONENT: 0x1902,
        UNSIGNED_INT: 0x1405,
        createShader: vi.fn((type: number) => ({ id: nextShaderId++, type }) as unknown as WebGLShader),
        shaderSource: vi.fn(),
        compileShader: vi.fn(),
        getShaderParameter: vi.fn(() => true),
        getShaderInfoLog: vi.fn(() => ''),
        deleteShader: vi.fn(),
        createProgram: vi.fn(() => ({ id: nextProgramId++ }) as unknown as WebGLProgram),
        attachShader: vi.fn(),
        linkProgram: vi.fn(),
        getProgramParameter: vi.fn(() => true),
        getProgramInfoLog: vi.fn(() => ''),
        deleteProgram: vi.fn(),
        getUniformLocation: vi.fn(
            (_program: WebGLProgram, name: string) => ({ name }) as unknown as WebGLUniformLocation
        ),
        useProgram: vi.fn(),
        createVertexArray: vi.fn(
            () => ({ id: nextVertexArrayId++ }) as unknown as WebGLVertexArrayObject
        ),
        bindVertexArray: vi.fn(),
        deleteVertexArray: vi.fn(),
        createTexture: vi.fn(() => ({ id: nextTextureId++ }) as unknown as WebGLTexture),
        deleteTexture: vi.fn(),
        bindTexture: vi.fn(),
        activeTexture: vi.fn(),
        texParameteri: vi.fn(),
        texStorage2D: vi.fn(),
        createFramebuffer: vi.fn(() => ({ id: nextFramebufferId++ }) as unknown as WebGLFramebuffer),
        deleteFramebuffer: vi.fn(),
        bindFramebuffer: vi.fn(),
        framebufferTexture2D: vi.fn(),
        drawBuffers: vi.fn(),
        viewport: vi.fn(),
        clearColor: vi.fn(),
        clearDepth: vi.fn(),
        clear: vi.fn(),
        clearStencil: vi.fn(),
        blitFramebuffer: vi.fn(),
        disable: vi.fn(),
        depthMask: vi.fn(),
        colorMask: vi.fn(),
        uniform2f: vi.fn(),
        uniform3f: vi.fn(),
        uniform4f: vi.fn(),
        uniform1i: vi.fn(),
        uniform1f: vi.fn(),
        drawArrays: vi.fn(),
    } as unknown as WebGL2RenderingContext;
};

describe('SceneRenderPipeline', () => {
    it('routes HDR scene frames through tonemap and present passes', () => {
        const gl = createMockGL();
        const execute = vi.fn();
        const pipeline = new SceneRenderPipeline({
            gl,
            drawExecutor: {
                execute,
            },
            pipeline: {
                hdr: true,
            },
        });
        const frameState = new SceneRenderFrameState().begin(1);

        const stats = pipeline.render({
            actors: [],
            frame: 1,
            deltaSeconds: 1 / 60,
            viewportWidth: 640,
            viewportHeight: 360,
            cameraFrame: {
                camera: {
                    id: 'camera:main',
                    clearFlags: ['color', 'depth'],
                    clearColor: [0.1, 0.2, 0.3, 1],
                    clearDepth: 1,
                    near: 0.1,
                    far: 100,
                },
                viewMatrix: new Mat4(),
                projectionMatrix: new Mat4(),
                viewProjectionMatrix: new Mat4(),
                camera3D: {
                    frustum: {},
                },
                position: [0, 0, 5],
            } as any,
            lighting: {
                stats: {
                    selectedDirectionalCount: 0,
                    selectedPointCount: 0,
                    selectedSpotCount: 0,
                },
            } as any,
            renderPass: {
                id: 'main',
                clearFlags: ['color', 'depth'],
                clearColor: null,
                clearDepth: null,
            } as any,
            drawContext: {} as any,
            frameState,
            renderItems: [
                {
                    renderer: {
                        id: 'renderer:cube',
                        meshId: 'mesh:cube',
                        renderOrder: 0,
                        visible: true,
                        receiveLighting: true,
                    },
                    transform: {
                        worldMatrix: new Mat4(),
                    },
                },
            ] as any,
            resolveBounds: () => null,
            resolveMaterial: () => ({
                materialId: 'material:cube',
                shadingModel: 'pbr',
                alphaMode: 'opaque',
                transparent: false,
            }),
        });

        expect(execute).toHaveBeenCalledTimes(1);
        expect(stats).toEqual(
            expect.objectContaining({
                passCount: 3,
                opaqueCount: 1,
                transparentCount: 0,
            })
        );
        expect(gl.drawArrays).toHaveBeenCalledWith(gl.TRIANGLES, 0, 3);
        expect(gl.blitFramebuffer).toHaveBeenCalledTimes(1);

        pipeline.dispose();
    });

    it('routes configured post-process effects through the fullscreen backend path', () => {
        const gl = createMockGL();
        const execute = vi.fn();
        const pipeline = new SceneRenderPipeline({
            gl,
            drawExecutor: {
                execute,
            },
            pipeline: {
                postProcess: [{ category: 'builtin', name: 'vignette' }],
            },
        });
        const frameState = new SceneRenderFrameState().begin(1);

        const stats = pipeline.render({
            actors: [],
            frame: 1,
            deltaSeconds: 1 / 60,
            viewportWidth: 640,
            viewportHeight: 360,
            cameraFrame: {
                camera: {
                    id: 'camera:main',
                    clearFlags: ['color', 'depth'],
                    clearColor: [0.1, 0.2, 0.3, 1],
                    clearDepth: 1,
                    near: 0.1,
                    far: 100,
                },
                viewMatrix: new Mat4(),
                projectionMatrix: new Mat4(),
                viewProjectionMatrix: new Mat4(),
                camera3D: {
                    frustum: {},
                },
                position: [0, 0, 5],
            } as any,
            lighting: {
                stats: {
                    selectedDirectionalCount: 0,
                    selectedPointCount: 0,
                    selectedSpotCount: 0,
                },
            } as any,
            renderPass: {
                id: 'main',
                clearFlags: ['color', 'depth'],
                clearColor: null,
                clearDepth: null,
            } as any,
            drawContext: {} as any,
            frameState,
            renderItems: [
                {
                    renderer: {
                        id: 'renderer:cube',
                        meshId: 'mesh:cube',
                        renderOrder: 0,
                        visible: true,
                        receiveLighting: true,
                    },
                    transform: {
                        worldMatrix: new Mat4(),
                    },
                },
            ] as any,
            resolveBounds: () => null,
            resolveMaterial: () => ({
                materialId: 'material:cube',
                shadingModel: 'pbr',
                alphaMode: 'opaque',
                transparent: false,
            }),
        });

        expect(execute).toHaveBeenCalledTimes(1);
        expect(stats).toEqual(
            expect.objectContaining({
                passCount: 3,
                opaqueCount: 1,
                transparentCount: 0,
            })
        );
        expect(gl.drawArrays).toHaveBeenCalledWith(gl.TRIANGLES, 0, 3);
        expect(gl.blitFramebuffer).toHaveBeenCalledTimes(1);

        pipeline.dispose();
    });

    it('owns sprite batching and transparent budget aggregation for the active render pass', () => {
        const gl = createMockGL();
        const execute = vi.fn();
        const spriteRender = vi.fn(() => ({
            drawnSpriteCount: 2,
            spriteBatchCount: 1,
            skippedSpriteCount: 1,
            warnings: ['sprite budget warning'],
        }));
        const spriteClear = vi.fn();
        const pipeline = new SceneRenderPipeline({
            gl,
            drawExecutor: {
                execute,
            },
            planning: {
                maxTransparentPrimitives: 5,
            },
            spriteBatchRuntime: {
                render: spriteRender,
                clear: spriteClear,
            },
        });
        const frameState = new SceneRenderFrameState().begin(1);

        const stats = pipeline.render({
            actors: [{} as any],
            frame: 1,
            deltaSeconds: 1 / 60,
            viewportWidth: 640,
            viewportHeight: 360,
            cameraFrame: {
                camera: {
                    id: 'camera:main',
                    clearFlags: ['color', 'depth'],
                    clearColor: [0.1, 0.2, 0.3, 1],
                    clearDepth: 1,
                    near: 0.1,
                    far: 100,
                },
                viewMatrix: new Mat4(),
                projectionMatrix: new Mat4(),
                viewProjectionMatrix: new Mat4(),
                camera3D: {
                    frustum: {},
                },
                position: [0, 0, 5],
            } as any,
            lighting: {
                stats: {
                    selectedDirectionalCount: 0,
                    selectedPointCount: 0,
                    selectedSpotCount: 0,
                },
            } as any,
            renderPass: {
                id: 'main',
                clearFlags: ['color', 'depth'],
                clearColor: null,
                clearDepth: null,
            } as any,
            drawContext: {} as any,
            frameState,
            renderItems: [
                {
                    renderer: {
                        id: 'renderer:glass',
                        meshId: 'mesh:glass',
                        renderOrder: 0,
                        visible: true,
                        receiveLighting: true,
                    },
                    transform: {
                        worldMatrix: new Mat4(),
                    },
                },
            ] as any,
            resolveBounds: () => null,
            resolveMaterial: () => ({
                materialId: 'material:glass',
                shadingModel: 'pbr',
                alphaMode: 'blend',
                transparent: true,
            }),
        });

        expect(execute).toHaveBeenCalledTimes(1);
        expect(spriteRender).toHaveBeenCalledWith(
            expect.objectContaining({
                transparentBudget: {
                    total: 5,
                    remaining: 4,
                },
            })
        );
        expect(stats).toEqual(
            expect.objectContaining({
                transparentCount: 3,
                meshTransparentCount: 1,
                spriteTransparentCount: 2,
                spriteBatchCount: 1,
                skippedSpriteCount: 1,
                warnings: ['sprite budget warning'],
            })
        );

        pipeline.reset();
        expect(spriteClear).toHaveBeenCalledTimes(1);
        pipeline.dispose();
        expect(spriteClear).toHaveBeenCalledTimes(2);
    });
});