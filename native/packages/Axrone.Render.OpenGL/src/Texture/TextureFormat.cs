namespace Axrone.Render.OpenGL.Texture;

/// <summary>
/// Texture format enumeration.
/// </summary>
public enum TextureFormat : ushort
{
    // 8-bit formats
    R8,
    R8Snorm,
    Rg8,
    Rg8Snorm,
    Rgb8,
    Rgb8Snorm,
    Rgba8,
    Rgba8Snorm,

    // 16-bit formats
    R16,
    R16Snorm,
    Rg16,
    Rg16Snorm,
    Rgb16,
    Rgba16,

    // Float formats
    R16f,
    Rg16f,
    Rgba16f,
    R32f,
    Rg32f,
    Rgba32f,

    // Depth formats
    Depth16,
    Depth24,
    Depth32f,
    Depth24Stencil8,
    Depth32fStencil8,

    // Packed formats
    Rgb565,
    Rgb5A1,
    Rgba4,
    Rgb10A2,
    R11fG11fB10f,
    Rgb9E5,

    // sRGB formats
    Srgb8,
    Srgb8Alpha8,

    // Integer formats
    R8i,
    R8ui,
    R16i,
    R16ui,
    R32i,
    R32ui,
    Rg8i,
    Rg8ui,
    Rg16i,
    Rg16ui,
    Rg32i,
    Rg32ui,
    Rgba8i,
    Rgba8ui,
    Rgba16i,
    Rgba16ui,
    Rgba32i,
    Rgba32ui,

    // Compressed formats (BC/DXT)
    Bc1Rgb,
    Bc1Rgba,
    Bc2Rgba,
    Bc3Rgba,
    Bc4R,
    Bc4RSigned,
    Bc5Rg,
    Bc5RgSigned,
    Bc6hRgbUf16,
    Bc6hRgbSf16,
    Bc7Rgba,
    Bc7Srgb,

    // Compressed formats (ETC/EAC)
    Etc2Rgb8,
    Etc2Srgb8,
    Etc2Rgb8A1,
    Etc2Srgb8A1,
    Etc2Rgba8,
    Etc2Srgb8Alpha8,
    EacR11,
    EacR11Signed,
    EacRg11,
    EacRg11Signed,

    // Compressed formats (ASTC)
    Astc4x4,
    Astc5x4,
    Astc5x5,
    Astc6x5,
    Astc6x6,
    Astc8x5,
    Astc8x6,
    Astc8x8,
    Astc10x5,
    Astc10x6,
    Astc10x8,
    Astc10x10,
    Astc12x10,
    Astc12x12,
}

/// <summary>
/// Texture format flags.
/// </summary>
[Flags]
public enum TextureFormatFlags : byte
{
    None = 0,
    Compressed = 1 << 0,
    Depth = 1 << 1,
    Stencil = 1 << 2,
    Integer = 1 << 3,
    Srgb = 1 << 4,
    Float = 1 << 5,
    Filterable = 1 << 6,
    RenderTarget = 1 << 7,
}

/// <summary>
/// Texture format information.
/// </summary>
public readonly struct TextureFormatInfo : IEquatable<TextureFormatInfo>
{
    /// <summary>Gets the internal format.</summary>
    public uint InternalFormat { get; }

    /// <summary>Gets the pixel format.</summary>
    public uint Format { get; }

    /// <summary>Gets the pixel type.</summary>
    public uint Type { get; }

    /// <summary>Gets the bytes per pixel.</summary>
    public int BytesPerPixel { get; }

    /// <summary>Gets the block width (for compressed formats).</summary>
    public int BlockWidth { get; }

    /// <summary>Gets the block height (for compressed formats).</summary>
    public int BlockHeight { get; }

    /// <summary>Gets the block size (for compressed formats).</summary>
    public int BlockSize { get; }

    /// <summary>Gets the format flags.</summary>
    public TextureFormatFlags Flags { get; }

    /// <summary>Gets a value indicating whether this is a compressed format.</summary>
    public bool IsCompressed => (Flags & TextureFormatFlags.Compressed) != 0;

    /// <summary>Gets a value indicating whether this is a depth format.</summary>
    public bool IsDepth => (Flags & TextureFormatFlags.Depth) != 0;

    /// <summary>Gets a value indicating whether this is a stencil format.</summary>
    public bool IsStencil => (Flags & TextureFormatFlags.Stencil) != 0;

    /// <summary>Gets a value indicating whether this is an sRGB format.</summary>
    public bool IsSrgb => (Flags & TextureFormatFlags.Srgb) != 0;

    /// <summary>Gets a value indicating whether this is a float format.</summary>
    public bool IsFloat => (Flags & TextureFormatFlags.Float) != 0;

    /// <summary>Gets a value indicating whether this is an integer format.</summary>
    public bool IsInteger => (Flags & TextureFormatFlags.Integer) != 0;

    /// <summary>Gets a value indicating whether this format is filterable.</summary>
    public bool IsFilterable => (Flags & TextureFormatFlags.Filterable) != 0;

    /// <summary>Gets a value indicating whether this format can be used as a render target.</summary>
    public bool IsRenderTarget => (Flags & TextureFormatFlags.RenderTarget) != 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextureFormatInfo"/> struct.
    /// </summary>
    public TextureFormatInfo(
        uint internalFormat,
        uint format,
        uint type,
        int bytesPerPixel,
        TextureFormatFlags flags)
    {
        InternalFormat = internalFormat;
        Format = format;
        Type = type;
        BytesPerPixel = bytesPerPixel;
        BlockWidth = 1;
        BlockHeight = 1;
        BlockSize = bytesPerPixel;
        Flags = flags;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TextureFormatInfo"/> struct for compressed formats.
    /// </summary>
    public TextureFormatInfo(
        uint internalFormat,
        uint format,
        uint type,
        int blockWidth,
        int blockHeight,
        int blockSize,
        TextureFormatFlags flags)
    {
        InternalFormat = internalFormat;
        Format = format;
        Type = type;
        BytesPerPixel = 0;
        BlockWidth = blockWidth;
        BlockHeight = blockHeight;
        BlockSize = blockSize;
        Flags = flags;
    }

    /// <summary>
    /// Computes the byte size for a mip level.
    /// </summary>
    public long ComputeMipByteSize(int width, int height, int depth = 1)
    {
        if (IsCompressed)
        {
            long blocksX = (width + BlockWidth - 1) / BlockWidth;
            long blocksY = (height + BlockHeight - 1) / BlockHeight;
            return blocksX * blocksY * depth * BlockSize;
        }

        return (long)width * height * depth * BytesPerPixel;
    }

    /// <inheritdoc/>
    public bool Equals(TextureFormatInfo other) =>
        InternalFormat == other.InternalFormat &&
        Format == other.Format &&
        Type == other.Type &&
        BytesPerPixel == other.BytesPerPixel &&
        BlockWidth == other.BlockWidth &&
        BlockHeight == other.BlockHeight &&
        BlockSize == other.BlockSize &&
        Flags == other.Flags;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is TextureFormatInfo other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(InternalFormat, Format, Type, BytesPerPixel, BlockWidth, BlockHeight, BlockSize, (byte)Flags);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(TextureFormatInfo left, TextureFormatInfo right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(TextureFormatInfo left, TextureFormatInfo right) => !left.Equals(right);
}

/// <summary>
/// Texture format database.
/// </summary>
public static class TextureFormats
{
    private const TextureFormatFlags F = TextureFormatFlags.Filterable;
    private const TextureFormatFlags FR = TextureFormatFlags.Filterable | TextureFormatFlags.RenderTarget;
    private const TextureFormatFlags C = TextureFormatFlags.Compressed;
    private const TextureFormatFlags CF = TextureFormatFlags.Compressed | TextureFormatFlags.Filterable;
    private const TextureFormatFlags DF = TextureFormatFlags.Depth | TextureFormatFlags.Filterable;
    private const TextureFormatFlags DS = TextureFormatFlags.Depth | TextureFormatFlags.Stencil | TextureFormatFlags.Filterable;
    private const TextureFormatFlags I = TextureFormatFlags.Integer;
    private const TextureFormatFlags IR = TextureFormatFlags.Integer | TextureFormatFlags.RenderTarget;
    private const TextureFormatFlags FF = TextureFormatFlags.Float | TextureFormatFlags.Filterable | TextureFormatFlags.RenderTarget;
    private const TextureFormatFlags S = TextureFormatFlags.Srgb;

    private static readonly Dictionary<TextureFormat, TextureFormatInfo> _table = Build();

    private static Dictionary<TextureFormat, TextureFormatInfo> Build()
    {
        var table = new Dictionary<TextureFormat, TextureFormatInfo>(128)
        {
            // 8-bit
            [TextureFormat.R8] = new(Native.GLConst.R8, Native.GLConst.Red, Native.GLConst.UnsignedByte, 1, FR),
            [TextureFormat.R8Snorm] = new(0x8F94, Native.GLConst.Red, Native.GLConst.Byte, 1, F),
            [TextureFormat.Rg8] = new(Native.GLConst.Rg8, Native.GLConst.Rg, Native.GLConst.UnsignedByte, 2, FR),
            [TextureFormat.Rg8Snorm] = new(0x8F95, Native.GLConst.Rg, Native.GLConst.Byte, 2, F),
            [TextureFormat.Rgb8] = new(Native.GLConst.Rgb8, Native.GLConst.Rgb, Native.GLConst.UnsignedByte, 3, FR),
            [TextureFormat.Rgb8Snorm] = new(0x8F96, Native.GLConst.Rgb, Native.GLConst.Byte, 3, F),
            [TextureFormat.Rgba8] = new(Native.GLConst.Rgba8, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 4, FR),
            [TextureFormat.Rgba8Snorm] = new(0x8F97, Native.GLConst.Rgba, Native.GLConst.Byte, 4, F),

            // 16-bit
            [TextureFormat.R16] = new(0x822A, Native.GLConst.Red, Native.GLConst.UnsignedShort, 2, FR),
            [TextureFormat.R16Snorm] = new(0x8F98, Native.GLConst.Red, Native.GLConst.Short, 2, F),
            [TextureFormat.Rg16] = new(0x822C, Native.GLConst.Rg, Native.GLConst.UnsignedShort, 4, FR),
            [TextureFormat.Rg16Snorm] = new(0x8F99, Native.GLConst.Rg, Native.GLConst.Short, 4, F),
            [TextureFormat.Rgb16] = new(0x8054, Native.GLConst.Rgb, Native.GLConst.UnsignedShort, 6, FR),
            [TextureFormat.Rgba16] = new(0x805B, Native.GLConst.Rgba, Native.GLConst.UnsignedShort, 8, FR),

            // Float
            [TextureFormat.R16f] = new(Native.GLConst.R16f, Native.GLConst.Red, Native.GLConst.HalfFloat, 2, FF),
            [TextureFormat.Rg16f] = new(Native.GLConst.Rg16f, Native.GLConst.Rg, Native.GLConst.HalfFloat, 4, FF),
            [TextureFormat.Rgba16f] = new(Native.GLConst.Rgba16f, Native.GLConst.Rgba, Native.GLConst.HalfFloat, 8, FF),
            [TextureFormat.R32f] = new(Native.GLConst.R32f, Native.GLConst.Red, Native.GLConst.Float, 4, FF),
            [TextureFormat.Rg32f] = new(Native.GLConst.Rg32f, Native.GLConst.Rg, Native.GLConst.Float, 8, FF),
            [TextureFormat.Rgba32f] = new(Native.GLConst.Rgba32f, Native.GLConst.Rgba, Native.GLConst.Float, 16, TextureFormatFlags.Float | TextureFormatFlags.RenderTarget),

            // Depth
            [TextureFormat.Depth16] = new(Native.GLConst.DepthComponent16, Native.GLConst.DepthComponent, Native.GLConst.UnsignedShort, 2, DF),
            [TextureFormat.Depth24] = new(Native.GLConst.DepthComponent24, Native.GLConst.DepthComponent, Native.GLConst.UnsignedInt, 4, DF),
            [TextureFormat.Depth32f] = new(Native.GLConst.DepthComponent32f, Native.GLConst.DepthComponent, Native.GLConst.Float, 4, DF),
            [TextureFormat.Depth24Stencil8] = new(Native.GLConst.Depth24Stencil8, Native.GLConst.DepthStencil, Native.GLConst.UnsignedInt248, 4, DS),
            [TextureFormat.Depth32fStencil8] = new(0x8CAD, Native.GLConst.DepthStencil, 0x8DAD, 8, DS),

            // Packed
            [TextureFormat.Rgb565] = new(0x8D62, Native.GLConst.Rgb, 0x8363, 2, F),
            [TextureFormat.Rgb5A1] = new(0x8057, Native.GLConst.Rgba, Native.GLConst.UnsignedShort, 2, F),
            [TextureFormat.Rgba4] = new(0x8056, Native.GLConst.Rgba, Native.GLConst.UnsignedShort, 2, F),
            [TextureFormat.Rgb10A2] = new(0x8059, Native.GLConst.Rgba, 0x8036, 4, FR),
            [TextureFormat.R11fG11fB10f] = new(0x8C3A, Native.GLConst.Rgb, 0x8C3B, 4, FR | TextureFormatFlags.Float),
            [TextureFormat.Rgb9E5] = new(0x8C3D, Native.GLConst.Rgb, 0x8C3E, 4, F),

            // sRGB
            [TextureFormat.Srgb8] = new(0x8C41, Native.GLConst.Rgb, Native.GLConst.UnsignedByte, 3, FR | S),
            [TextureFormat.Srgb8Alpha8] = new(0x8C43, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 4, FR | S),

            // Integer formats (abbreviated for brevity)
            [TextureFormat.R8i] = new(0x8231, Native.GLConst.Red, Native.GLConst.Byte, 1, IR),
            [TextureFormat.R8ui] = new(0x8232, Native.GLConst.Red, Native.GLConst.UnsignedByte, 1, IR),
            [TextureFormat.R16i] = new(0x8233, Native.GLConst.Red, Native.GLConst.Short, 2, IR),
            [TextureFormat.R16ui] = new(0x8234, Native.GLConst.Red, Native.GLConst.UnsignedShort, 2, IR),
            [TextureFormat.R32i] = new(0x8235, Native.GLConst.Red, Native.GLConst.Int, 4, IR),
            [TextureFormat.R32ui] = new(0x8236, Native.GLConst.Red, Native.GLConst.UnsignedInt, 4, IR),

            // S3TC / RGTC / BPTC block-compressed formats (4x4 blocks)
            [TextureFormat.Bc1Rgb] = new(Native.GLConst.CompressedRgbS3tcDxt1, Native.GLConst.Rgb, Native.GLConst.UnsignedByte, 4, 4, 8, C),
            [TextureFormat.Bc1Rgba] = new(Native.GLConst.CompressedRgbaS3tcDxt1, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 4, 4, 8, C),
            [TextureFormat.Bc2Rgba] = new(Native.GLConst.CompressedRgbaS3tcDxt3, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 4, 4, 16, C),
            [TextureFormat.Bc3Rgba] = new(Native.GLConst.CompressedRgbaS3tcDxt5, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 4, 4, 16, C),
            [TextureFormat.Bc4R] = new(Native.GLConst.CompressedRedRgtc1, Native.GLConst.Red, Native.GLConst.UnsignedByte, 4, 4, 8, C),
            [TextureFormat.Bc4RSigned] = new(Native.GLConst.CompressedSignedRedRgtc1, Native.GLConst.Red, Native.GLConst.Byte, 4, 4, 8, C | I),
            [TextureFormat.Bc5Rg] = new(Native.GLConst.CompressedRgRgtc2, Native.GLConst.Rg, Native.GLConst.UnsignedByte, 4, 4, 16, C),
            [TextureFormat.Bc5RgSigned] = new(Native.GLConst.CompressedSignedRgRgtc2, Native.GLConst.Rg, Native.GLConst.Byte, 4, 4, 16, C | I),
            [TextureFormat.Bc6hRgbUf16] = new(Native.GLConst.CompressedRgbBptcUnsignedFloat, Native.GLConst.Rgb, Native.GLConst.UnsignedByte, 4, 4, 16, C | FF),
            [TextureFormat.Bc6hRgbSf16] = new(Native.GLConst.CompressedRgbBptcSignedFloat, Native.GLConst.Rgb, Native.GLConst.Byte, 4, 4, 16, C | FF),
            [TextureFormat.Bc7Rgba] = new(Native.GLConst.CompressedRgbaBptcUnorm, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 4, 4, 16, CF),
            [TextureFormat.Bc7Srgb] = new(Native.GLConst.CompressedSrgbAlphaBptcUnorm, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 4, 4, 16, CF | S),

            // ETC2 / EAC formats (4x4 blocks)
            [TextureFormat.Etc2Rgb8] = new(Native.GLConst.CompressedRgb8Etc2, Native.GLConst.Rgb, Native.GLConst.UnsignedByte, 4, 4, 8, C),
            [TextureFormat.Etc2Srgb8] = new(Native.GLConst.CompressedSrgb8Etc2, Native.GLConst.Rgb, Native.GLConst.UnsignedByte, 4, 4, 8, C | S),
            [TextureFormat.Etc2Rgb8A1] = new(Native.GLConst.CompressedRgb8PunchthroughAlpha1Etc2, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 4, 4, 8, C),
            [TextureFormat.Etc2Srgb8A1] = new(Native.GLConst.CompressedSrgb8PunchthroughAlpha1Etc2, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 4, 4, 8, C | S),
            [TextureFormat.Etc2Rgba8] = new(Native.GLConst.CompressedRgba8Etc2Eac, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 4, 4, 16, C),
            [TextureFormat.Etc2Srgb8Alpha8] = new(Native.GLConst.CompressedSrgb8Alpha8Etc2Eac, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 4, 4, 16, C | S),
            [TextureFormat.EacR11] = new(Native.GLConst.CompressedR11Eac, Native.GLConst.Red, Native.GLConst.UnsignedByte, 4, 4, 8, C),
            [TextureFormat.EacR11Signed] = new(Native.GLConst.CompressedSignedR11Eac, Native.GLConst.Red, Native.GLConst.Byte, 4, 4, 8, C | I),
            [TextureFormat.EacRg11] = new(Native.GLConst.CompressedRg11Eac, Native.GLConst.Rg, Native.GLConst.UnsignedByte, 4, 4, 16, C),
            [TextureFormat.EacRg11Signed] = new(Native.GLConst.CompressedSignedRg11Eac, Native.GLConst.Rg, Native.GLConst.Byte, 4, 4, 16, C | I),

            // ASTC 2D LDR formats (16 bytes per block)
            [TextureFormat.Astc4x4] = new(Native.GLConst.CompressedRgbaAstc4x4, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 4, 4, 16, CF),
            [TextureFormat.Astc5x4] = new(Native.GLConst.CompressedRgbaAstc5x4, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 5, 4, 16, CF),
            [TextureFormat.Astc5x5] = new(Native.GLConst.CompressedRgbaAstc5x5, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 5, 5, 16, CF),
            [TextureFormat.Astc6x5] = new(Native.GLConst.CompressedRgbaAstc6x5, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 6, 5, 16, CF),
            [TextureFormat.Astc6x6] = new(Native.GLConst.CompressedRgbaAstc6x6, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 6, 6, 16, CF),
            [TextureFormat.Astc8x5] = new(Native.GLConst.CompressedRgbaAstc8x5, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 8, 5, 16, CF),
            [TextureFormat.Astc8x6] = new(Native.GLConst.CompressedRgbaAstc8x6, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 8, 6, 16, CF),
            [TextureFormat.Astc8x8] = new(Native.GLConst.CompressedRgbaAstc8x8, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 8, 8, 16, CF),
            [TextureFormat.Astc10x5] = new(Native.GLConst.CompressedRgbaAstc10x5, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 10, 5, 16, CF),
            [TextureFormat.Astc10x6] = new(Native.GLConst.CompressedRgbaAstc10x6, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 10, 6, 16, CF),
            [TextureFormat.Astc10x8] = new(Native.GLConst.CompressedRgbaAstc10x8, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 10, 8, 16, CF),
            [TextureFormat.Astc10x10] = new(Native.GLConst.CompressedRgbaAstc10x10, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 10, 10, 16, CF),
            [TextureFormat.Astc12x10] = new(Native.GLConst.CompressedRgbaAstc12x10, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 12, 10, 16, CF),
            [TextureFormat.Astc12x12] = new(Native.GLConst.CompressedRgbaAstc12x12, Native.GLConst.Rgba, Native.GLConst.UnsignedByte, 12, 12, 16, CF),
        };

        return table;
    }

    /// <summary>
    /// Gets the format info for a texture format.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TextureFormatInfo Get(TextureFormat format)
    {
        if (!_table.TryGetValue(format, out var info))
        {
            throw new ArgumentException($"Unknown texture format: {format}", nameof(format));
        }

        return info;
    }

    /// <summary>
    /// Computes the maximum number of mip levels for a given size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MaxMipLevels(int width, int height, int depth = 1)
    {
        int maxDim = Math.Max(width, Math.Max(height, depth));
        return maxDim <= 0 ? 1 : (int)Math.Floor(Math.Log2(maxDim)) + 1;
    }
}
