using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Mesh;
using Axrone.Render.OpenGL.Native;
using Axrone.Render.OpenGL.Resources;

namespace Axrone.Render.OpenGL.Tests;

/// <summary>
/// Tests for the mesh system: <see cref="VertexLayout"/>, <see cref="Bounds3D"/>,
/// <see cref="MeshGenerators"/>, and <see cref="GLMesh"/>.
/// </summary>
public sealed class MeshSystemTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public MeshSystemTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    // ========================================================================
    // VertexLayout tests
    // ========================================================================

    [Fact]
    public void VertexLayout_DuplicateLocations_Throws()
    {
        VertexAttribute[] attrs =
        [
            VertexAttribute.Float(0, 3, 32, 0),
            VertexAttribute.Float(0, 2, 32, 12), // Duplicate location 0
        ];

        var action = () => new VertexLayout(attrs);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void VertexLayout_ComputesCorrectStride()
    {
        ReadOnlySpan<VertexAttribute> attrs =
        [
            VertexAttribute.Float(0, 3, 32, 0),   // Position
            VertexAttribute.Float(1, 3, 32, 12),  // Normal
            VertexAttribute.Float(2, 2, 32, 24),  // UV
        ];

        var layout = new VertexLayout(attrs);

        layout.VertexStride.Should().Be(32);
        layout.AttributeCount.Should().Be(3);
    }

    [Fact]
    public void VertexLayout_InconsistentStride_Throws()
    {
        VertexAttribute[] attrs =
        [
            VertexAttribute.Float(0, 3, 32, 0),
            VertexAttribute.Float(1, 3, 48, 12), // Different stride
        ];

        var action = () => new VertexLayout(attrs);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void VertexLayout_EmptyAttributes_Throws()
    {
        VertexAttribute[] attrs = [];

        var action = () => new VertexLayout(attrs);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void VertexLayout_HasAttribute_ReturnsCorrectly()
    {
        ReadOnlySpan<VertexAttribute> attrs =
        [
            VertexAttribute.Float(0, 3, 32, 0),
            VertexAttribute.Float(1, 3, 32, 12),
        ];

        var layout = new VertexLayout(attrs);

        layout.HasAttribute(0).Should().BeTrue();
        layout.HasAttribute(1).Should().BeTrue();
        layout.HasAttribute(2).Should().BeFalse();
    }

    // ========================================================================
    // Bounds3D tests
    // ========================================================================

    [Fact]
    public void Bounds3D_Empty_HasCorrectSentinelValues()
    {
        var empty = Bounds3D.Empty;

        empty.Min.X.Should().Be(float.MaxValue);
        empty.Min.Y.Should().Be(float.MaxValue);
        empty.Min.Z.Should().Be(float.MaxValue);
        empty.Max.X.Should().Be(float.MinValue);
        empty.Max.Y.Should().Be(float.MinValue);
        empty.Max.Z.Should().Be(float.MinValue);
    }

    [Fact]
    public void Bounds3D_FromPoints_ComputesCorrectBounds()
    {
        Vector3[] points =
        [
            new(-1, -2, -3),
            new(4, 5, 6),
            new(0, 0, 0),
        ];

        var bounds = Bounds3D.FromPoints(points);

        bounds.Min.Should().Be(new Vector3(-1, -2, -3));
        bounds.Max.Should().Be(new Vector3(4, 5, 6));
    }

    [Fact]
    public void Bounds3D_FromPoints_EmptySpan_ReturnsEmpty()
    {
        var bounds = Bounds3D.FromPoints(ReadOnlySpan<Vector3>.Empty);

        bounds.Min.X.Should().Be(float.MaxValue);
        bounds.Max.X.Should().Be(float.MinValue);
    }

    [Fact]
    public void Bounds3D_Contains_PointInside_ReturnsTrue()
    {
        var bounds = new Bounds3D(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));

        bounds.Contains(Vector3.Zero).Should().BeTrue();
    }

    [Fact]
    public void Bounds3D_Contains_PointOutside_ReturnsFalse()
    {
        var bounds = new Bounds3D(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));

        bounds.Contains(new Vector3(2, 0, 0)).Should().BeFalse();
    }

    [Fact]
    public void Bounds3D_Contains_PointOnBoundary_ReturnsTrue()
    {
        var bounds = new Bounds3D(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));

        bounds.Contains(new Vector3(1, 1, 1)).Should().BeTrue();
    }

    [Fact]
    public void Bounds3D_Intersects_Overlapping_ReturnsTrue()
    {
        var a = new Bounds3D(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));
        var b = new Bounds3D(new Vector3(0, 0, 0), new Vector3(2, 2, 2));

        a.Intersects(b).Should().BeTrue();
    }

    [Fact]
    public void Bounds3D_Intersects_NonOverlapping_ReturnsFalse()
    {
        var a = new Bounds3D(new Vector3(-1, -1, -1), new Vector3(0, 0, 0));
        var b = new Bounds3D(new Vector3(1, 1, 1), new Vector3(2, 2, 2));

        a.Intersects(b).Should().BeFalse();
    }

    [Fact]
    public void Bounds3D_Intersects_Touching_ReturnsTrue()
    {
        var a = new Bounds3D(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
        var b = new Bounds3D(new Vector3(1, 1, 1), new Vector3(2, 2, 2));

        a.Intersects(b).Should().BeTrue();
    }

    // ========================================================================
    // MeshGenerators tests
    // ========================================================================

    [Fact]
    public void MeshGenerators_CreatePlane_GeneratesCorrectVertexCount()
    {
        var mesh = MeshGenerators.CreatePlane(_context, 10, 10);

        mesh.VertexCount.Should().Be(4);
        mesh.IndexCount.Should().Be(6);
        mesh.IsIndexed.Should().BeTrue();
    }

    [Fact]
    public void MeshGenerators_CreateCube_Generates24Vertices36Indices()
    {
        var mesh = MeshGenerators.CreateCube(_context, 2);

        mesh.VertexCount.Should().Be(24);
        mesh.IndexCount.Should().Be(36);
        mesh.IsIndexed.Should().BeTrue();
    }

    [Fact]
    public void MeshGenerators_CreateSphere_GeneratesCorrectTopology()
    {
        int segments = 8;
        int rings = 4;
        int expectedVertices = (segments + 1) * (rings + 1); // 9 * 5 = 45
        int expectedIndices = segments * rings * 6; // 8 * 4 * 6 = 192

        var mesh = MeshGenerators.CreateSphere(_context, 1.0f, segments, rings);

        mesh.VertexCount.Should().Be(expectedVertices);
        mesh.IndexCount.Should().Be(expectedIndices);
    }

    [Fact]
    public void MeshGenerators_CreateSphere_InvalidSegments_Throws()
    {
        var action = () => MeshGenerators.CreateSphere(_context, 1.0f, segments: 2);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MeshGenerators_CreateSphere_InvalidRings_Throws()
    {
        var action = () => MeshGenerators.CreateSphere(_context, 1.0f, rings: 1);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MeshGenerators_CreateFullscreenQuad_Generates3Vertices()
    {
        var mesh = MeshGenerators.CreateFullscreenQuad(_context);

        mesh.VertexCount.Should().Be(3);
        mesh.IndexCount.Should().Be(0);
        mesh.IsIndexed.Should().BeFalse();
    }

    [Fact]
    public void MeshGenerators_DefaultLayout_HasCorrectStride()
    {
        var layout = MeshGenerators.DefaultLayout;

        layout.VertexStride.Should().Be(32); // 3 float pos + 3 float normal + 2 float UV = 32 bytes
        layout.AttributeCount.Should().Be(3);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
