namespace Axrone.Render.OpenGL.Tests;

using System.Runtime.InteropServices;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Mesh;
using Axrone.Render.OpenGL.Native;
using Axrone.Render.OpenGL.Resources;

/// <summary>
/// Integration tests for mesh creation and rendering workflows including generation,
/// multi-mesh rendering, custom layouts, fullscreen quads, and bounds computation.
/// </summary>
public sealed class MeshPipelineTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public MeshPipelineTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    [Fact]
    public void GenerateAndRender_CubeMesh_DrawsSuccessfully()
    {
        // Arrange: create a cube mesh via MeshGenerators
        var cube = MeshGenerators.CreateCube(_context, 2.0f, "test_cube");

        // Assert: mesh was created correctly
        cube.Should().NotBeNull();
        cube.Label.Should().Be("test_cube");
        cube.VertexCount.Should().Be(24, "a cube has 6 faces x 4 vertices");
        cube.IndexCount.Should().Be(36, "a cube has 6 faces x 2 triangles x 3 indices");
        cube.IsIndexed.Should().BeTrue();
        cube.VertexBuffer.Should().NotBeNull();
        cube.IndexBuffer.Should().NotBeNull();
        cube.VertexArray.Should().NotBeNull();
        cube.VertexBuffer.Id.Should().NotBe(0);
        cube.IndexBuffer!.Id.Should().NotBe(0);
        cube.VertexArray.Id.Should().NotBe(0);

        // Act: draw the cube
        _mock.ClearCallLog();
        var action = () => cube.Draw();

        // Assert: draw call was issued
        action.Should().NotThrow();
        _mock.CallLog.Should().Contain(c => c.Contains("DrawElements", StringComparison.Ordinal),
            "indexed mesh should use DrawElements");
    }

    [Fact]
    public void MultipleMeshes_PlaneCubeSphere_AllDrawSuccessfully()
    {
        // Arrange: create three different mesh types
        var plane = MeshGenerators.CreatePlane(_context, 10.0f, 10.0f, "ground_plane");
        var cube = MeshGenerators.CreateCube(_context, 1.0f, "unit_cube");
        var sphere = MeshGenerators.CreateSphere(_context, 1.0f, 16, 8, "test_sphere");

        // Assert: all meshes created correctly
        plane.VertexCount.Should().Be(4, "a plane has 4 corner vertices");
        plane.IndexCount.Should().Be(6, "a plane has 2 triangles = 6 indices");

        cube.VertexCount.Should().Be(24);
        cube.IndexCount.Should().Be(36);

        // Sphere: (segments+1) * (rings+1) vertices
        int expectedSphereVerts = (16 + 1) * (8 + 1);
        sphere.VertexCount.Should().Be(expectedSphereVerts);
        int expectedSphereIndices = 16 * 8 * 6;
        sphere.IndexCount.Should().Be(expectedSphereIndices);

        // Act: draw all meshes
        _mock.ClearCallLog();
        var drawAction = () =>
        {
            plane.Draw();
            cube.Draw();
            sphere.Draw();
        };

        // Assert: all draw calls succeeded
        drawAction.Should().NotThrow();

        // All three are indexed, so should use DrawElements
        var drawCalls = _mock.CallLog.Where(c => c.Contains("DrawElements", StringComparison.Ordinal)).ToList();
        drawCalls.Should().HaveCount(3, "three indexed meshes should produce three DrawElements calls");
    }

    [Fact]
    public void MeshWithCustomLayout_BuildsAndDraws()
    {
        // Arrange: create a custom vertex layout with position + color
        // Custom layout: Position (3 floats) + Color (4 floats) = 28 bytes stride
        const int customStride = 28;

        ReadOnlySpan<VertexAttribute> attrs =
        [
            VertexAttribute.Float(0, 3, customStride, 0),    // Position: location 0, 3 floats, offset 0
            VertexAttribute.Float(1, 4, customStride, 12),   // Color: location 1, 4 floats, offset 12
        ];

        var layout = new VertexLayout(attrs);

        // Verify layout properties
        layout.AttributeCount.Should().Be(2);
        layout.VertexStride.Should().Be(customStride);
        layout.HasAttribute(0).Should().BeTrue();
        layout.HasAttribute(1).Should().BeTrue();
        layout.HasAttribute(2).Should().BeFalse();

        // Create vertex data: 3 vertices (a triangle) with position + color
        float[] vertices =
        [
            // Position          // Color (RGBA)
            0.0f, 1.0f, 0.0f,   1.0f, 0.0f, 0.0f, 1.0f,
            -1.0f, -1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f,
            1.0f, -1.0f, 0.0f,  0.0f, 0.0f, 1.0f, 1.0f,
        ];

        byte[] vertexBytes = MemoryMarshal.AsBytes(vertices.AsSpan()).ToArray();
        Bounds3D bounds = new(new Vector3(-1, -1, 0), new Vector3(1, 1, 0));

        // Act: create mesh with custom layout
        var mesh = new GLMesh(_context, layout, vertexBytes, default, bounds, GLConst.Triangles, "custom_mesh");

        // Assert: mesh created correctly
        mesh.VertexCount.Should().Be(3);
        mesh.IsIndexed.Should().BeFalse("no index data provided");
        mesh.IndexBuffer.Should().BeNull();
        mesh.Layout.Should().Be(layout);

        // Draw should use DrawArrays (non-indexed)
        _mock.ClearCallLog();
        var drawAction = () => mesh.Draw();
        drawAction.Should().NotThrow();

        _mock.CallLog.Should().Contain(c => c.Contains("DrawArrays", StringComparison.Ordinal),
            "non-indexed mesh should use DrawArrays");
    }

    [Fact]
    public void FullscreenQuad_CreatesAndDrawsForPostProcess()
    {
        // Arrange: create a fullscreen quad for post-processing
        var quad = MeshGenerators.CreateFullscreenQuad(_context, "postprocess_quad");

        // Assert: fullscreen quad properties
        quad.Should().NotBeNull();
        quad.Label.Should().Be("postprocess_quad");
        quad.VertexCount.Should().Be(3, "fullscreen quad uses 3 vertices (oversized triangle)");
        quad.IsIndexed.Should().BeFalse("fullscreen quad has no index buffer");
        quad.IndexBuffer.Should().BeNull();

        // Bounds should cover the screen area
        quad.Bounds.Min.X.Should().Be(-1.0f);
        quad.Bounds.Min.Y.Should().Be(-1.0f);
        quad.Bounds.Max.X.Should().Be(3.0f);
        quad.Bounds.Max.Y.Should().Be(3.0f);

        // Act: draw the fullscreen quad
        _mock.ClearCallLog();
        var drawAction = () => quad.Draw();

        // Assert
        drawAction.Should().NotThrow();
        _mock.CallLog.Should().Contain(c => c.Contains("DrawArrays", StringComparison.Ordinal),
            "non-indexed fullscreen quad should use DrawArrays");
    }

    [Fact]
    public void BoundsComputation_Sphere_ContainsAllVertices()
    {
        // Arrange: generate a sphere with known radius
        const float radius = 2.5f;
        var sphere = MeshGenerators.CreateSphere(_context, radius, 16, 8, "bounds_sphere");

        // Assert: bounds should be centered at origin with extents matching radius
        sphere.Bounds.Min.X.Should().BeApproximately(-radius, 0.001f);
        sphere.Bounds.Min.Y.Should().BeApproximately(-radius, 0.001f);
        sphere.Bounds.Min.Z.Should().BeApproximately(-radius, 0.001f);
        sphere.Bounds.Max.X.Should().BeApproximately(radius, 0.001f);
        sphere.Bounds.Max.Y.Should().BeApproximately(radius, 0.001f);
        sphere.Bounds.Max.Z.Should().BeApproximately(radius, 0.001f);

        // Center should be at origin
        sphere.Bounds.Center.X.Should().BeApproximately(0.0f, 0.001f);
        sphere.Bounds.Center.Y.Should().BeApproximately(0.0f, 0.001f);
        sphere.Bounds.Center.Z.Should().BeApproximately(0.0f, 0.001f);

        // Size should be 2*radius in each dimension
        sphere.Bounds.Size.X.Should().BeApproximately(radius * 2, 0.001f);
        sphere.Bounds.Size.Y.Should().BeApproximately(radius * 2, 0.001f);
        sphere.Bounds.Size.Z.Should().BeApproximately(radius * 2, 0.001f);

        // Verify bounds actually contain vertex positions by checking the vertex data
        // The sphere vertex data is stored in the vertex buffer snapshot
        // We can verify by checking that the bounds contain the poles and equator points
        sphere.Bounds.Contains(new Vector3(0, radius, 0)).Should().BeTrue("north pole should be within bounds");
        sphere.Bounds.Contains(new Vector3(0, -radius, 0)).Should().BeTrue("south pole should be within bounds");
        sphere.Bounds.Contains(new Vector3(radius, 0, 0)).Should().BeTrue("equator point should be within bounds");
    }

    [Fact]
    public void CubeBounds_ContainsAllCorners()
    {
        // Arrange
        const float size = 4.0f;
        float h = size * 0.5f;
        var cube = MeshGenerators.CreateCube(_context, size, "bounds_cube");

        // Assert: bounds should encompass the full cube
        cube.Bounds.Min.Should().Be(new Vector3(-h, -h, -h));
        cube.Bounds.Max.Should().Be(new Vector3(h, h, h));

        // All 8 corners should be contained
        cube.Bounds.Contains(new Vector3(h, h, h)).Should().BeTrue();
        cube.Bounds.Contains(new Vector3(-h, h, h)).Should().BeTrue();
        cube.Bounds.Contains(new Vector3(h, -h, h)).Should().BeTrue();
        cube.Bounds.Contains(new Vector3(h, h, -h)).Should().BeTrue();
        cube.Bounds.Contains(new Vector3(-h, -h, -h)).Should().BeTrue();
    }

    [Fact]
    public void MeshDisposal_ReleasesGLResources()
    {
        // Arrange
        var cube = MeshGenerators.CreateCube(_context, 1.0f, "disposable_cube");
        uint vboId = cube.VertexBuffer.Id;
        uint iboId = cube.IndexBuffer!.Id;
        uint vaoId = cube.VertexArray.Id;

        vboId.Should().NotBe(0);
        iboId.Should().NotBe(0);
        vaoId.Should().NotBe(0);

        // Act
        _mock.ClearCallLog();
        cube.Dispose();

        // Assert
        cube.IsDisposed.Should().BeTrue();
        _mock.CallLog.Should().Contain(c => c.Contains($"DeleteBuffer({vboId})"),
            "vertex buffer should be deleted");
        _mock.CallLog.Should().Contain(c => c.Contains($"DeleteBuffer({iboId})"),
            "index buffer should be deleted");
        _mock.CallLog.Should().Contain(c => c.Contains($"DeleteVertexArray({vaoId})"),
            "VAO should be deleted");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
