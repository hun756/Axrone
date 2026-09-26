namespace Axrone.Render.OpenGL.Native;

/// <summary>
/// OpenGL constant definitions.
/// </summary>
public static class GLConst
{
    // Buffer targets
    public const uint ArrayBuffer = 0x8892;
    public const uint ElementArrayBuffer = 0x8893;
    public const uint UniformBuffer = 0x8A11;
    public const uint CopyReadBuffer = 0x8F36;
    public const uint CopyWriteBuffer = 0x8F37;
    public const uint TransformFeedbackBuffer = 0x8C8E;
    public const uint PixelPackBuffer = 0x88EB;
    public const uint PixelUnpackBuffer = 0x88EC;

    // Buffer usage
    public const uint StaticDraw = 0x88E4;
    public const uint DynamicDraw = 0x88E8;
    public const uint StreamDraw = 0x88E0;

    // Texture targets
    public const uint Texture2D = 0x0DE1;
    public const uint Texture3D = 0x806F;
    public const uint TextureCubeMap = 0x8513;
    public const uint Texture2DArray = 0x8C1A;

    // Texture parameters
    public const uint TextureMinFilter = 0x2801;
    public const uint TextureMagFilter = 0x2800;
    public const uint TextureWrapS = 0x2802;
    public const uint TextureWrapT = 0x2803;
    public const uint TextureWrapR = 0x8072;
    public const uint TextureMaxLevel = 0x813D;
    public const uint TextureBaseLevel = 0x813C;

    // Texture filters
    public const uint Nearest = 0x2600;
    public const uint Linear = 0x2601;
    public const uint NearestMipmapNearest = 0x2700;
    public const uint LinearMipmapNearest = 0x2701;
    public const uint NearestMipmapLinear = 0x2702;
    public const uint LinearMipmapLinear = 0x2703;

    // Texture wrap modes
    public const uint ClampToEdge = 0x812F;
    public const uint Repeat = 0x2901;
    public const uint MirroredRepeat = 0x8370;

    // Pixel formats
    public const uint Red = 0x1903;
    public const uint Rg = 0x8227;
    public const uint Rgb = 0x1907;
    public const uint Rgba = 0x1908;
    public const uint DepthComponent = 0x1902;
    public const uint DepthStencil = 0x84F9;

    // Pixel types
    public const uint UnsignedByte = 0x1401;
    public const uint Byte = 0x1400;
    public const uint UnsignedShort = 0x1403;
    public const uint Short = 0x1402;
    public const uint UnsignedInt = 0x1405;
    public const uint Int = 0x1404;
    public const uint Float = 0x1406;
    public const uint HalfFloat = 0x140B;
    public const uint UnsignedInt248 = 0x84FA;

    // Internal formats
    public const uint R8 = 0x8229;
    public const uint Rg8 = 0x822B;
    public const uint Rgb8 = 0x8051;
    public const uint Rgba8 = 0x8058;
    public const uint R16f = 0x822D;
    public const uint Rg16f = 0x822F;
    public const uint Rgba16f = 0x881A;
    public const uint R32f = 0x822E;
    public const uint Rg32f = 0x8230;
    public const uint Rgba32f = 0x8814;
    public const uint DepthComponent16 = 0x81A5;
    public const uint DepthComponent24 = 0x81A6;
    public const uint DepthComponent32f = 0x8CAC;
    public const uint Depth24Stencil8 = 0x88F0;

    // Framebuffer
    public const uint Framebuffer = 0x8D40;
    public const uint ReadFramebuffer = 0x8CA8;
    public const uint DrawFramebuffer = 0x8CA9;
    public const uint ColorAttachment0 = 0x8CE0;
    public const uint DepthAttachment = 0x8D00;
    public const uint StencilAttachment = 0x8D20;
    public const uint DepthStencilAttachment = 0x821A;
    public const uint FramebufferComplete = 0x8CD5;

    // Renderbuffer
    public const uint Renderbuffer = 0x8D41;

    // Clear bits
    public const uint ColorBufferBit = 0x00004000;
    public const uint DepthBufferBit = 0x00000100;
    public const uint StencilBufferBit = 0x00000400;

    // Draw modes
    public const uint Triangles = 0x0004;
    public const uint TriangleStrip = 0x0005;
    public const uint Lines = 0x0001;
    public const uint LineStrip = 0x0003;
    public const uint Points = 0x0000;

    // Blending
    public const uint Blend = 0x0BE2;
    public const uint SrcAlpha = 0x0302;
    public const uint OneMinusSrcAlpha = 0x0303;
    public const uint One = 1;
    public const uint Zero = 0;
    public const uint FuncAdd = 0x8006;

    // Depth test
    public const uint DepthTest = 0x0B71;
    public const uint Less = 0x0201;
    public const uint Lequal = 0x0203;
    public const uint Greater = 0x0204;
    public const uint Gequal = 0x0206;
    public const uint Equal = 0x0202;
    public const uint Always = 0x0207;

    // Culling
    public const uint CullFace = 0x0B44;
    public const uint Front = 0x0404;
    public const uint Back = 0x0405;
    public const uint FrontAndBack = 0x0408;
    public const uint Ccw = 0x0901;
    public const uint Cw = 0x0900;

    // Stencil
    public const uint StencilTest = 0x0B90;

    // Transform feedback
    public const uint TransformFeedback = 0x8E22;
    public const uint TransformFeedbackBufferBinding = 0x8E25;

    // Object labels
    public const uint Texture = 0x1702;
    public const uint VertexArray = 0x8074;
    public const uint Buffer = 0x82E0;
    public const uint Program = 0x82E2;
    public const uint Shader = 0x82E1;

    // Blit filter
    public const uint NearestFilter = 0x2600;
    public const uint LinearFilter = 0x2601;

    // Compute shader
    public const uint ComputeShader = 0x91B9;
    public const uint ShaderStorageBuffer = 0x90D2;
    public const uint AllShaderStorageBits = 0xFFFFFFFF;

    // Sync
    public const uint SyncGpuCommandsComplete = 0x9117;
    public const uint SyncStatus = 0x9114;
    public const uint SyncFlushCommandsBit = 0x00000001;
    public const ulong TimeoutIgnored = 0xFFFFFFFFFFFFFFFF;
    public const uint Sync = 0x9117;

    // Sampler
    public const uint Sampler = 0x80E6;
    public const uint TextureCompareMode = 0x884C;
    public const uint TextureCompareFunc = 0x884D;
    public const uint TextureLodBias = 0x8501;
    public const uint TextureMinLod = 0x813A;
    public const uint TextureMaxLod = 0x813B;
    public const uint CompareRefToTexture = 0x884E;

    // Query
    public const uint Query = 0x82E3;
    public const uint QueryResult = 0x8866;
    public const uint QueryResultAvailable = 0x8867;

    // None
    public const uint None = 0;

    // Shader stages and compile/link status
    public const uint FragmentShader = 0x8B30;
    public const uint VertexShader = 0x8B31;
    public const uint CompileStatus = 0x8B81;
    public const uint LinkStatus = 0x8B82;

    // Program reflection
    public const uint ActiveUniforms = 0x8B86;
    public const uint ActiveUniformMaxLength = 0x8B87;
    public const uint ActiveAttributes = 0x8B89;
    public const uint ActiveAttributeMaxLength = 0x8B8A;

    // S3TC / RGTC / BPTC compressed formats
    public const uint CompressedRgbS3tcDxt1 = 0x83F0;
    public const uint CompressedRgbaS3tcDxt1 = 0x83F1;
    public const uint CompressedRgbaS3tcDxt3 = 0x83F2;
    public const uint CompressedRgbaS3tcDxt5 = 0x83F3;
    public const uint CompressedRedRgtc1 = 0x8DBB;
    public const uint CompressedSignedRedRgtc1 = 0x8DBC;
    public const uint CompressedRgRgtc2 = 0x8DBD;
    public const uint CompressedSignedRgRgtc2 = 0x8DBE;
    public const uint CompressedRgbBptcUnsignedFloat = 0x8E8F;
    public const uint CompressedRgbBptcSignedFloat = 0x8E8E;
    public const uint CompressedRgbaBptcUnorm = 0x8E8C;
    public const uint CompressedSrgbAlphaBptcUnorm = 0x8E8D;

    // ETC2 / EAC compressed formats
    public const uint CompressedR11Eac = 0x9270;
    public const uint CompressedSignedR11Eac = 0x9271;
    public const uint CompressedRg11Eac = 0x9272;
    public const uint CompressedSignedRg11Eac = 0x9273;
    public const uint CompressedRgb8Etc2 = 0x9274;
    public const uint CompressedSrgb8Etc2 = 0x9275;
    public const uint CompressedRgb8PunchthroughAlpha1Etc2 = 0x9276;
    public const uint CompressedSrgb8PunchthroughAlpha1Etc2 = 0x9277;
    public const uint CompressedRgba8Etc2Eac = 0x9278;
    public const uint CompressedSrgb8Alpha8Etc2Eac = 0x9279;

    // ASTC 2D LDR formats
    public const uint CompressedRgbaAstc4x4 = 0x93B0;
    public const uint CompressedRgbaAstc5x4 = 0x93B1;
    public const uint CompressedRgbaAstc5x5 = 0x93B2;
    public const uint CompressedRgbaAstc6x5 = 0x93B3;
    public const uint CompressedRgbaAstc6x6 = 0x93B4;
    public const uint CompressedRgbaAstc8x5 = 0x93B5;
    public const uint CompressedRgbaAstc8x6 = 0x93B6;
    public const uint CompressedRgbaAstc8x8 = 0x93B7;
    public const uint CompressedRgbaAstc10x5 = 0x93B8;
    public const uint CompressedRgbaAstc10x6 = 0x93B9;
    public const uint CompressedRgbaAstc10x8 = 0x93BA;
    public const uint CompressedRgbaAstc10x10 = 0x93BB;
    public const uint CompressedRgbaAstc12x10 = 0x93BC;
    public const uint CompressedRgbaAstc12x12 = 0x93BD;
}
