
namespace Axrone.Render.OpenGL.Mesh;

/// <summary>
/// Static factory for common primitive mesh generation.
/// Generates vertex data arrays suitable for uploading to GPU.
/// Vertex format: Position (3 float) + Normal (3 float) + UV (2 float) = 32 bytes.
/// </summary>
public static class MeshGenerators
{
    /// <summary>
    /// Size of a single vertex in bytes (Position 12 + Normal 12 + UV 8 = 32).
    /// </summary>
    private const int VertexSize = 32;

    /// <summary>
    /// Gets the default vertex layout: Position (location 0, 3 float) +
    /// Normal (location 1, 3 float) + UV (location 2, 2 float).
    /// </summary>
    public static VertexLayout DefaultLayout { get; } = CreateDefaultLayout();

    private static VertexLayout CreateDefaultLayout()
    {
        ReadOnlySpan<VertexAttribute> attrs =
        [
            VertexAttribute.Float(0, 3, VertexSize, 0),   // Position
            VertexAttribute.Float(1, 3, VertexSize, 12),  // Normal
            VertexAttribute.Float(2, 2, VertexSize, 24),  // UV
        ];
        return new VertexLayout(attrs);
    }

    /// <summary>
    /// Creates a plane mesh on the XZ plane facing +Y.
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="width">The plane width along the X axis.</param>
    /// <param name="height">The plane height along the Z axis.</param>
    /// <param name="label">The debug label.</param>
    /// <returns>A <see cref="GLMesh"/> representing the plane.</returns>
    public static GLMesh CreatePlane(GLContext context, float width, float height, string label = "plane")
    {
        float hw = width * 0.5f;
        float hh = height * 0.5f;

        // 4 vertices: corners of the plane on XZ, normal = +Y
        float[] vertices =
        [
            // Position          // Normal       // UV
            -hw, 0, -hh,        0, 1, 0,       0, 0,
             hw, 0, -hh,        0, 1, 0,       1, 0,
             hw, 0,  hh,        0, 1, 0,       1, 1,
            -hw, 0,  hh,        0, 1, 0,       0, 1,
        ];

        // CCW winding: 0-1-2, 0-2-3
        uint[] indices =
        [
            0, 1, 2,
            0, 2, 3,
        ];

        byte[] vertexBytes = MemoryMarshal.AsBytes(vertices.AsSpan()).ToArray();
        byte[] indexBytes = MemoryMarshal.AsBytes(indices.AsSpan()).ToArray();
        Bounds3D bounds = ComputeBoundsFromInterleaved(vertexBytes, DefaultLayout, 0);

        return new GLMesh(context, DefaultLayout, vertexBytes, indexBytes, bounds, GLConst.Triangles, label);
    }

    /// <summary>
    /// Creates a cube mesh centered at the origin.
    /// 24 vertices (4 per face for correct normals), 36 indices.
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="size">The full side length of the cube.</param>
    /// <param name="label">The debug label.</param>
    /// <returns>A <see cref="GLMesh"/> representing the cube.</returns>
    public static GLMesh CreateCube(GLContext context, float size, string label = "cube")
    {
        float h = size * 0.5f;

        // 6 faces x 4 vertices = 24 vertices
        // Each face has unique normals for correct lighting
        float[] vertices =
        [
            // Front face (+Z)
            -h, -h,  h,    0,  0,  1,    0, 0,
             h, -h,  h,    0,  0,  1,    1, 0,
             h,  h,  h,    0,  0,  1,    1, 1,
            -h,  h,  h,    0,  0,  1,    0, 1,

            // Back face (-Z)
             h, -h, -h,    0,  0, -1,    0, 0,
            -h, -h, -h,    0,  0, -1,    1, 0,
            -h,  h, -h,    0,  0, -1,    1, 1,
             h,  h, -h,    0,  0, -1,    0, 1,

            // Top face (+Y)
            -h,  h,  h,    0,  1,  0,    0, 0,
             h,  h,  h,    0,  1,  0,    1, 0,
             h,  h, -h,    0,  1,  0,    1, 1,
            -h,  h, -h,    0,  1,  0,    0, 1,

            // Bottom face (-Y)
            -h, -h, -h,    0, -1,  0,    0, 0,
             h, -h, -h,    0, -1,  0,    1, 0,
             h, -h,  h,    0, -1,  0,    1, 1,
            -h, -h,  h,    0, -1,  0,    0, 1,

            // Right face (+X)
             h, -h,  h,    1,  0,  0,    0, 0,
             h, -h, -h,    1,  0,  0,    1, 0,
             h,  h, -h,    1,  0,  0,    1, 1,
             h,  h,  h,    1,  0,  0,    0, 1,

            // Left face (-X)
            -h, -h, -h,   -1,  0,  0,    0, 0,
            -h, -h,  h,   -1,  0,  0,    1, 0,
            -h,  h,  h,   -1,  0,  0,    1, 1,
            -h,  h, -h,   -1,  0,  0,    0, 1,
        ];

        // 6 faces x 2 triangles x 3 indices = 36 indices (CCW winding)
        uint[] indices =
        [
            0,  1,  2,   0,  2,  3,   // Front
            4,  5,  6,   4,  6,  7,   // Back
            8,  9,  10,  8,  10, 11,  // Top
            12, 13, 14,  12, 14, 15,  // Bottom
            16, 17, 18,  16, 18, 19,  // Right
            20, 21, 22,  20, 22, 23,  // Left
        ];

        byte[] vertexBytes = MemoryMarshal.AsBytes(vertices.AsSpan()).ToArray();
        byte[] indexBytes = MemoryMarshal.AsBytes(indices.AsSpan()).ToArray();
        Bounds3D bounds = new(new Vector3(-h, -h, -h), new Vector3(h, h, h));

        return new GLMesh(context, DefaultLayout, vertexBytes, indexBytes, bounds, GLConst.Triangles, label);
    }

    /// <summary>
    /// Creates a UV sphere mesh.
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="radius">The sphere radius.</param>
    /// <param name="segments">The number of horizontal segments (longitude).</param>
    /// <param name="rings">The number of vertical rings (latitude).</param>
    /// <param name="label">The debug label.</param>
    /// <returns>A <see cref="GLMesh"/> representing the sphere.</returns>
    public static GLMesh CreateSphere(
        GLContext context,
        float radius,
        int segments = 32,
        int rings = 16,
        string label = "sphere")
    {
        if (segments < 3)
            ThrowHelper.ThrowInvalidArgument("Segments must be at least 3");

        if (rings < 2)
            ThrowHelper.ThrowInvalidArgument("Rings must be at least 2");

        int vertexCount = (segments + 1) * (rings + 1);
        float[] vertices = new float[vertexCount * 8]; // 8 floats per vertex
        int vi = 0;

        for (int ring = 0; ring <= rings; ring++)
        {
            float phi = MathF.PI * ring / rings;
            float sinPhi = MathF.Sin(phi);
            float cosPhi = MathF.Cos(phi);

            for (int seg = 0; seg <= segments; seg++)
            {
                float theta = 2.0f * MathF.PI * seg / segments;
                float sinTheta = MathF.Sin(theta);
                float cosTheta = MathF.Cos(theta);

                // Normal
                float nx = sinPhi * cosTheta;
                float ny = cosPhi;
                float nz = sinPhi * sinTheta;

                // Position
                float px = radius * nx;
                float py = radius * ny;
                float pz = radius * nz;

                // UV
                float u = (float)seg / segments;
                float v = (float)ring / rings;

                vertices[vi++] = px;
                vertices[vi++] = py;
                vertices[vi++] = pz;
                vertices[vi++] = nx;
                vertices[vi++] = ny;
                vertices[vi++] = nz;
                vertices[vi++] = u;
                vertices[vi++] = v;
            }
        }

        // Indices: 2 triangles per quad, segments * rings quads
        int indexCount = segments * rings * 6;
        uint[] indices = new uint[indexCount];
        int ii = 0;

        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                int current = ring * (segments + 1) + seg;
                int next = current + segments + 1;

                // CCW winding
                indices[ii++] = (uint)current;
                indices[ii++] = (uint)next;
                indices[ii++] = (uint)(current + 1);

                indices[ii++] = (uint)(current + 1);
                indices[ii++] = (uint)next;
                indices[ii++] = (uint)(next + 1);
            }
        }

        byte[] vertexBytes = MemoryMarshal.AsBytes(vertices.AsSpan()).ToArray();
        byte[] indexBytes = MemoryMarshal.AsBytes(indices.AsSpan()).ToArray();
        Bounds3D bounds = new(
            new Vector3(-radius, -radius, -radius),
            new Vector3(radius, radius, radius));

        return new GLMesh(context, DefaultLayout, vertexBytes, indexBytes, bounds, GLConst.Triangles, label);
    }

    /// <summary>
    /// Creates a screen-filling triangle for fullscreen rendering (e.g., post-processing).
    /// Uses 3 vertices with no index buffer. Positions range from (-1,-1) to (3,-1) to (-1,3).
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="label">The debug label.</param>
    /// <returns>A <see cref="GLMesh"/> representing the fullscreen quad.</returns>
    public static GLMesh CreateFullscreenQuad(GLContext context, string label = "fullscreen_quad")
    {
        // Oversized triangle that covers the entire screen
        // Positions in clip space: (-1,-1), (3,-1), (-1,3)
        // Normals: (0,0,1) — facing camera
        // UVs: (0,0), (2,0), (0,2) — covers [0,1] range across the visible portion
        float[] vertices =
        [
            // Position       // Normal       // UV
            -1, -1, 0,       0, 0, 1,       0, 0,
             3, -1, 0,       0, 0, 1,       2, 0,
            -1,  3, 0,       0, 0, 1,       0, 2,
        ];

        byte[] vertexBytes = MemoryMarshal.AsBytes(vertices.AsSpan()).ToArray();
        Bounds3D bounds = new(new Vector3(-1, -1, 0), new Vector3(3, 3, 0));

        return new GLMesh(context, DefaultLayout, vertexBytes, default, bounds, GLConst.Triangles, label);
    }

    /// <summary>
    /// Computes a <see cref="Bounds3D"/> from interleaved vertex data using the position attribute
    /// from <see cref="DefaultLayout"/> (location 0, offset 0, 3 floats).
    /// </summary>
    /// <param name="vertexData">The raw interleaved vertex data.</param>
    /// <param name="layout">The vertex layout.</param>
    /// <param name="positionOffset">The byte offset of position data within each vertex.</param>
    /// <returns>The computed bounding box.</returns>
    private static Bounds3D ComputeBoundsFromInterleaved(
        ReadOnlySpan<byte> vertexData,
        VertexLayout layout,
        int positionOffset)
    {
        int vertexCount = vertexData.Length / layout.VertexStride;
        if (vertexCount == 0)
            return Bounds3D.Empty;

        var bounds = Bounds3D.Empty;

        ReadOnlySpan<float> floats = MemoryMarshal.Cast<byte, float>(vertexData);
        int floatsPerVertex = layout.VertexStride / sizeof(float);
        int positionFloatOffset = positionOffset / sizeof(float);

        for (int i = 0; i < vertexCount; i++)
        {
            int baseIndex = i * floatsPerVertex + positionFloatOffset;
            var pos = new Vector3(floats[baseIndex], floats[baseIndex + 1], floats[baseIndex + 2]);
            bounds = bounds.Expand(pos);
        }

        return bounds;
    }
}
