namespace Axrone.Scene.Assets;

/// <summary>Plane orientation used by <see cref="SceneMeshFactory.CreateQuad"/>.</summary>
public enum SceneQuadOrientation
{
    /// <summary>Faces +Z in the XY plane.</summary>
    Xy = 0,

    /// <summary>Faces +Y in the XZ plane.</summary>
    Xz = 1,

    /// <summary>Faces +X in the YZ plane.</summary>
    Yz = 2
}

/// <summary>
/// Builds scene mesh definitions from the engine's procedural primitive
/// generators. Vertices are interleaved as position/normal/UV and bounds are
/// derived from the position attribute, so generated meshes are immediately
/// uploadable and cullable.
/// </summary>
public static class SceneMeshFactory
{
    private const int PositionOffset = 0;
    private const int NormalOffset = 12;
    private const int UvOffset = 24;

    /// <summary>Creates a subdivided plane facing +Z.</summary>
    public static SceneMeshDefinition CreatePlane(
        SceneAssetId<MeshAssetTag> id,
        float width = 1.0f,
        float height = 1.0f,
        int widthSegments = 1,
        int heightSegments = 1)
    {
        ValidateSize(width, nameof(width));
        ValidateSize(height, nameof(height));

        var sink = new MeshSink();
        var config = new PlaneConfig
        {
            Width = new Metric(width),
            Height = new Metric(height),
            WidthSegments = new SegmentResolution((uint)Math.Max(1, widthSegments)),
            HeightSegments = new SegmentResolution((uint)Math.Max(1, heightSegments))
        };

        ProceduralPrimitives.EmitPlane<MeshSink, VertexP3N3T2, uint>(ref sink, in config);
        return CreateDefinition(id, sink);
    }

    /// <summary>Creates an axis-aligned box centered on the origin.</summary>
    public static SceneMeshDefinition CreateBox(
        SceneAssetId<MeshAssetTag> id,
        float width = 1.0f,
        float height = 1.0f,
        float depth = 1.0f,
        int segments = 1)
    {
        ValidateSize(width, nameof(width));
        ValidateSize(height, nameof(height));
        ValidateSize(depth, nameof(depth));

        int divisions = Math.Max(1, segments);
        var sink = new MeshSink();
        var config = new BoxConfig
        {
            Width = new Metric(width),
            Height = new Metric(height),
            Depth = new Metric(depth),
            WidthSegments = new SegmentResolution((uint)divisions),
            HeightSegments = new SegmentResolution((uint)divisions),
            DepthSegments = new SegmentResolution((uint)divisions)
        };

        ProceduralPrimitives.EmitBox<MeshSink, VertexP3N3T2, uint>(ref sink, in config);
        return CreateDefinition(id, sink);
    }

    /// <summary>Creates a UV sphere centered on the origin.</summary>
    public static SceneMeshDefinition CreateSphere(
        SceneAssetId<MeshAssetTag> id,
        float radius = 0.5f,
        int segments = 24)
    {
        ValidateSize(radius, nameof(radius));

        int divisions = Math.Max(3, segments);
        var sink = new MeshSink();
        var config = new SphereConfig
        {
            Radius = new Metric(radius),
            WidthSegments = new SegmentResolution((uint)divisions),
            HeightSegments = new SegmentResolution((uint)Math.Max(2, divisions / 2))
        };

        ProceduralPrimitives.EmitSphere<MeshSink, VertexP3N3T2, uint>(ref sink, in config);
        return CreateDefinition(id, sink);
    }

    /// <summary>Creates a cylinder centered on the origin and aligned to +Y.</summary>
    public static SceneMeshDefinition CreateCylinder(
        SceneAssetId<MeshAssetTag> id,
        float radius = 0.5f,
        float height = 1.0f,
        int radialSegments = 24,
        bool openEnded = false)
    {
        ValidateSize(radius, nameof(radius));
        ValidateSize(height, nameof(height));

        var sink = new MeshSink();
        var config = new CylinderConfig
        {
            RadiusTop = new Metric(radius),
            RadiusBottom = new Metric(radius),
            Height = new Metric(height),
            RadialSegments = new SegmentResolution((uint)Math.Max(3, radialSegments)),
            OpenEnded = openEnded
        };

        ProceduralPrimitives.EmitCylinder<MeshSink, VertexP3N3T2, uint>(ref sink, in config);
        return CreateDefinition(id, sink);
    }

    /// <summary>Creates a single-segment quad in the requested plane.</summary>
    public static SceneMeshDefinition CreateQuad(
        SceneAssetId<MeshAssetTag> id,
        float width = 1.0f,
        float height = 1.0f,
        SceneQuadOrientation orientation = SceneQuadOrientation.Xy)
    {
        ValidateSize(width, nameof(width));
        ValidateSize(height, nameof(height));

        var sink = new MeshSink();
        var config = new PlaneConfig
        {
            Width = new Metric(width),
            Height = new Metric(height)
        };

        ProceduralPrimitives.EmitPlane<MeshSink, VertexP3N3T2, uint>(ref sink, in config);
        Orient(sink, orientation);
        return CreateDefinition(id, sink);
    }

    private static void Orient(MeshSink sink, SceneQuadOrientation orientation)
    {
        if (orientation == SceneQuadOrientation.Xy)
        {
            return;
        }

        List<VertexP3N3T2> vertices = sink.Vertices;
        for (int i = 0; i < vertices.Count; i++)
        {
            VertexP3N3T2 vertex = vertices[i];
            float x = vertex.Position.X;
            float y = vertex.Position.Y;

            vertices[i] = orientation == SceneQuadOrientation.Xz
                ? new VertexP3N3T2(
                    new Position3D(x, 0.0f, -y),
                    Normal3D.UnitY,
                    vertex.UV)
                : new VertexP3N3T2(
                    new Position3D(0.0f, y, -x),
                    Normal3D.UnitX,
                    vertex.UV);
        }
    }

    private static SceneMeshDefinition CreateDefinition(SceneAssetId<MeshAssetTag> id, MeshSink sink)
    {
        byte[] vertexBytes = MemoryMarshal.AsBytes(
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(sink.Vertices)).ToArray();

        SceneIndexFormat indexFormat;
        byte[] indexBytes;

        if (sink.MaxIndex <= ushort.MaxValue)
        {
            indexFormat = SceneIndexFormat.UInt16;
            var indices = new ushort[sink.Indices.Count];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = (ushort)sink.Indices[i];
            }

            indexBytes = MemoryMarshal.AsBytes(indices.AsSpan()).ToArray();
        }
        else
        {
            indexFormat = SceneIndexFormat.UInt32;
            indexBytes = MemoryMarshal.AsBytes(
                System.Runtime.InteropServices.CollectionsMarshal.AsSpan(sink.Indices)).ToArray();
        }

        SceneVertexAttribute[] attributes =
        [
            new SceneVertexAttribute(SceneMeshSemantic.Position, 3, PositionOffset, VertexP3N3T2.ByteStride),
            new SceneVertexAttribute(SceneMeshSemantic.Normal, 3, NormalOffset, VertexP3N3T2.ByteStride),
            new SceneVertexAttribute(SceneMeshSemantic.Uv0, 2, UvOffset, VertexP3N3T2.ByteStride)
        ];

        var definition = new SceneMeshDefinition(
            id,
            vertexBytes,
            attributes,
            sink.Vertices.Count,
            indexBytes,
            indexFormat,
            sink.Indices.Count,
            SceneMeshTopology.Triangles);

        return SceneMeshBounds.TryCompute(definition, out BoundingSphere3 bounds)
            ? new SceneMeshDefinition(
                id,
                vertexBytes,
                attributes,
                sink.Vertices.Count,
                indexBytes,
                indexFormat,
                sink.Indices.Count,
                SceneMeshTopology.Triangles,
                bounds)
            : definition;
    }

    private static void ValidateSize(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0.0f)
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                $"Procedural mesh '{name}' must be finite and positive; received {value}.");
        }
    }

    private sealed class MeshSink : IMeshSink<VertexP3N3T2, uint>
    {
        public List<VertexP3N3T2> Vertices { get; } = [];

        public List<uint> Indices { get; } = [];

        public uint MaxIndex { get; private set; }

        public uint CurrentVertexCount => (uint)Vertices.Count;

        public void AppendVertex(in VertexP3N3T2 vertex) => Vertices.Add(vertex);

        public void AppendTriangle(uint i0, uint i1, uint i2)
        {
            Indices.Add(i0);
            Indices.Add(i1);
            Indices.Add(i2);

            MaxIndex = Math.Max(MaxIndex, Math.Max(i0, Math.Max(i1, i2)));
        }
    }
}
