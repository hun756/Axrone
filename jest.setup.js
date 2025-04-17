import 'jest-canvas-mock';

const mockWebGLRenderingContext = {
    canvas: document.createElement('canvas'),
    drawingBufferWidth: 800,
    drawingBufferHeight: 600,

    enable: jest.fn(),
    disable: jest.fn(),
    blendFunc: jest.fn(),
    clearColor: jest.fn(),
    clear: jest.fn(),
    viewport: jest.fn(),

    createShader: jest.fn(() => ({})),
    shaderSource: jest.fn(),
    compileShader: jest.fn(),
    getShaderParameter: jest.fn(() => true),
    createProgram: jest.fn(() => ({})),
    attachShader: jest.fn(),
    linkProgram: jest.fn(),
    getProgramParameter: jest.fn(() => true),
    useProgram: jest.fn(),

    createBuffer: jest.fn(() => ({})),
    bindBuffer: jest.fn(),
    bufferData: jest.fn(),
    getAttribLocation: jest.fn(() => 0),
    getUniformLocation: jest.fn(() => ({})),
    enableVertexAttribArray: jest.fn(),
    vertexAttribPointer: jest.fn(),

    drawArrays: jest.fn(),
    drawElements: jest.fn(),

    DEPTH_TEST: 2929,
    VERTEX_SHADER: 35633,
    FRAGMENT_SHADER: 35632,
    ARRAY_BUFFER: 34962,
    STATIC_DRAW: 35044,
    FLOAT: 5126,
    TRIANGLES: 4,
    COLOR_BUFFER_BIT: 16384,
    DEPTH_BUFFER_BIT: 256
};

HTMLCanvasElement.prototype.getContext = jest.fn((contextType) => {
    if (contextType === 'webgl' || contextType === 'experimental-webgl') {
        return mockWebGLRenderingContext;
    }
    return null;
});

beforeEach(() => {
    jest.clearAllMocks();
});
