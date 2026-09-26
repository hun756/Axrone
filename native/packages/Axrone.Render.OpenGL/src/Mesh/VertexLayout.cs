
namespace Axrone.Render.OpenGL.Mesh;

/// <summary>
/// Immutable vertex layout descriptor. Defines the attribute structure of a vertex.
/// Used to configure <see cref="GLVertexArray"/> layouts and validate mesh data.
/// </summary>
public sealed class VertexLayout : IEquatable<VertexLayout>
{
    private readonly VertexAttribute[] _attributes;

    /// <summary>
    /// Gets the number of attributes in this layout.
    /// </summary>
    public int AttributeCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _attributes.Length;
    }

    /// <summary>
    /// Gets the total vertex stride in bytes.
    /// </summary>
    public int VertexStride { get; }

    /// <summary>
    /// Gets the attributes in this layout.
    /// </summary>
    public ReadOnlySpan<VertexAttribute> Attributes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _attributes;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VertexLayout"/> class.
    /// </summary>
    /// <param name="attributes">The vertex attributes defining this layout.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when attributes have duplicate locations, invalid component counts,
    /// inconsistent strides, or zero stride.
    /// </exception>
    public VertexLayout(ReadOnlySpan<VertexAttribute> attributes)
    {
        if (attributes.Length == 0)
            ThrowHelper.ThrowInvalidArgument("Vertex layout must contain at least one attribute");

        _attributes = new VertexAttribute[attributes.Length];
        attributes.CopyTo(_attributes);

        // Validate and compute stride
        int stride = 0;
        Span<int> seenLocations = stackalloc int[attributes.Length];
        int seenCount = 0;

        for (int i = 0; i < attributes.Length; i++)
        {
            ref readonly var attr = ref attributes[i];

            // Validate component count (1-4)
            if (attr.ComponentCount < 1 || attr.ComponentCount > 4)
            {
                ThrowHelper.ThrowInvalidArgument(
                    $"Attribute at index {i} has invalid component count {attr.ComponentCount}. Must be 1-4.");
            }

            // Validate stride > 0
            if (attr.Stride <= 0)
            {
                ThrowHelper.ThrowInvalidArgument(
                    $"Attribute at index {i} has invalid stride {attr.Stride}. Must be positive.");
            }

            // Validate unique locations
            for (int j = 0; j < seenCount; j++)
            {
                if (seenLocations[j] == attr.Location)
                {
                    ThrowHelper.ThrowInvalidArgument(
                        $"Duplicate attribute location {attr.Location} at index {i}.");
                }
            }

            seenLocations[seenCount++] = attr.Location;

            // All attributes must share the same stride (interleaved layout)
            if (stride == 0)
            {
                stride = attr.Stride;
            }
            else if (attr.Stride != stride)
            {
                ThrowHelper.ThrowInvalidArgument(
                    $"Attribute at index {i} has stride {attr.Stride} but layout stride is {stride}. " +
                    "All attributes in an interleaved layout must share the same stride.");
            }
        }

        VertexStride = stride;
    }

    /// <summary>
    /// Gets the attribute at the specified location.
    /// </summary>
    /// <param name="location">The attribute location to look up.</param>
    /// <returns>The attribute if found; otherwise, <c>null</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VertexAttribute? GetAttribute(int location)
    {
        for (int i = 0; i < _attributes.Length; i++)
        {
            if (_attributes[i].Location == location)
                return _attributes[i];
        }

        return null;
    }

    /// <summary>
    /// Determines whether this layout has an attribute at the specified location.
    /// </summary>
    /// <param name="location">The attribute location to check.</param>
    /// <returns><c>true</c> if the layout contains an attribute at the location; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAttribute(int location)
    {
        for (int i = 0; i < _attributes.Length; i++)
        {
            if (_attributes[i].Location == location)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the byte offset of the attribute at the specified location.
    /// </summary>
    /// <param name="location">The attribute location.</param>
    /// <returns>The byte offset of the attribute.</returns>
    /// <exception cref="ArgumentException">Thrown when no attribute exists at the location.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetOffset(int location)
    {
        for (int i = 0; i < _attributes.Length; i++)
        {
            if (_attributes[i].Location == location)
                return _attributes[i].Offset;
        }

        ThrowHelper.ThrowInvalidArgument($"No attribute found at location {location}.");
        return 0; // Unreachable
    }

    /// <summary>
    /// Builds a <see cref="GLVertexArray"/> configured with this layout for non-indexed drawing.
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="vertexBuffer">The vertex buffer to bind.</param>
    /// <param name="vertexCount">The number of vertices to draw.</param>
    /// <returns>A configured <see cref="GLVertexArray"/>.</returns>
    public GLVertexArray BuildVertexArray(GLContext context, GLBuffer vertexBuffer, int vertexCount)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(vertexBuffer);

        var vao = new GLVertexArray(context, "mesh_vao");
        vao.ConfigureLayout(vertexBuffer, _attributes, vertexCount);
        return vao;
    }

    /// <summary>
    /// Builds a <see cref="GLVertexArray"/> configured with this layout for indexed drawing.
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="vertexBuffer">The vertex buffer to bind.</param>
    /// <param name="indexBuffer">The index buffer to bind.</param>
    /// <param name="vertexCount">The number of vertices.</param>
    /// <param name="indexCount">The number of indices to draw.</param>
    /// <returns>A configured <see cref="GLVertexArray"/>.</returns>
    public GLVertexArray BuildVertexArray(
        GLContext context,
        GLBuffer vertexBuffer,
        GLBuffer indexBuffer,
        int vertexCount,
        int indexCount)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(vertexBuffer);
        ArgumentNullException.ThrowIfNull(indexBuffer);

        var vao = new GLVertexArray(context, "mesh_vao");
        vao.ConfigureLayout(vertexBuffer, indexBuffer, _attributes, vertexCount, indexCount);
        return vao;
    }

    /// <inheritdoc/>
    public bool Equals(VertexLayout? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (VertexStride != other.VertexStride || _attributes.Length != other._attributes.Length)
            return false;

        for (int i = 0; i < _attributes.Length; i++)
        {
            ref readonly var a = ref _attributes[i];
            ref readonly var b = ref other._attributes[i];

            if (a.Location != b.Location ||
                a.ComponentCount != b.ComponentCount ||
                a.Type != b.Type ||
                a.Normalized != b.Normalized ||
                a.Stride != b.Stride ||
                a.Offset != b.Offset ||
                a.Divisor != b.Divisor ||
                a.IsInteger != b.IsInteger)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is VertexLayout other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(VertexStride);

        for (int i = 0; i < _attributes.Length; i++)
        {
            ref readonly var attr = ref _attributes[i];
            hash.Add(attr.Location);
            hash.Add(attr.ComponentCount);
            hash.Add(attr.Type);
            hash.Add(attr.Offset);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(VertexLayout? left, VertexLayout? right) =>
        Equals(left, right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(VertexLayout? left, VertexLayout? right) =>
        !Equals(left, right);

    /// <inheritdoc/>
    public override string ToString() =>
        $"VertexLayout: Stride={VertexStride}, Attributes={_attributes.Length}";
}
