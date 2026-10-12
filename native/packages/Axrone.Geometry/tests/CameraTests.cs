namespace Axrone.Geometry.Tests;

using System.Text.Json;
using Axrone.Geometry;
using Axrone.Numeric;

using FluentAssertions;
using Xunit;

public class CameraTests
{
    private static CameraPerspectiveProjection Perspective() => new(
        RadianFoV.FromDegrees(60f),
        new AspectRatio(16f / 9f),
        new DepthDistance(0.1f),
        new DepthDistance(100f));

    private static Camera3D<CameraPerspectiveProjection> LookAtOrigin() => new(
        new CameraId("main"),
        Perspective(),
        new Vec3(0, 0, 5),
        new Vec3(0, 0, 0));

    [Fact]
    public void Nominals_GuardTheirDomains()
    {
        Action emptyId = () => _ = new CameraId(string.Empty);
        Action blankId = () => _ = new CameraId("  ");
        Action zeroFov = () => _ = new RadianFoV(0f);
        Action wideFov = () => _ = new RadianFoV(MathF.PI);
        Action badAspect = () => _ = new AspectRatio(0f);
        Action badNear = () => _ = new DepthDistance(float.PositiveInfinity);

        emptyId.Should().Throw<ArgumentException>();
        blankId.Should().Throw<ArgumentException>();
        zeroFov.Should().Throw<ArgumentOutOfRangeException>();
        wideFov.Should().Throw<ArgumentOutOfRangeException>();
        badAspect.Should().Throw<ArgumentOutOfRangeException>();
        badNear.Should().Throw<ArgumentOutOfRangeException>();

        new CameraId("main").Value.Should().Be("main");
        ((string)new CameraId("main")).Should().Be("main");
        ((CameraId)"main").Value.Should().Be("main");
        RadianFoV.FromDegrees(90f).Value.Should().BeApproximately(MathF.PI / 2f, 1e-5f);
    }

    [Fact]
    public void Projections_GuardAndMatchEngineMatrices()
    {
        Action nearFar = () => _ = new CameraPerspectiveProjection(
            RadianFoV.FromDegrees(60f), new AspectRatio(1f), new DepthDistance(10f), new DepthDistance(1f));
        Action flatBox = () => _ = new CameraOrthographicProjection(
            1f, 1f, -1f, 1f, new DepthDistance(0.1f), new DepthDistance(10f));

        nearFar.Should().Throw<ArgumentException>();
        flatBox.Should().Throw<ArgumentException>();

        var perspective = Perspective();
        Mat4 direct = Mat4.CreatePerspectiveFieldOfView(
            perspective.VerticalFieldOfView.Value, perspective.Ratio.Value, 0.1f, 100f);
        Mat4 built = perspective.ComputeProjectionMatrix();
        built.M11.Should().BeApproximately(direct.M11, 1e-6f);
        built.M22.Should().BeApproximately(direct.M22, 1e-6f);
        built.M33.Should().BeApproximately(direct.M33, 1e-6f);

        var centered = new CameraOrthographicProjection(-2f, 2f, -1f, 1f, new DepthDistance(0.1f), new DepthDistance(100f));
        Mat4 centeredDirect = Mat4.CreateOrthographic(4f, 2f, 0.1f, 100f);
        Mat4 centeredBuilt = centered.ComputeProjectionMatrix();
        centeredBuilt.M11.Should().BeApproximately(centeredDirect.M11, 1e-6f);
        centeredBuilt.M22.Should().BeApproximately(centeredDirect.M22, 1e-6f);
        centeredBuilt.M41.Should().BeApproximately(0f, 1e-6f);

        var offCenter = new CameraOrthographicProjection(1f, 5f, -1f, 3f, new DepthDistance(0.1f), new DepthDistance(10f));
        Mat4 offBuilt = offCenter.ComputeProjectionMatrix();
        offBuilt.M11.Should().BeApproximately(0.5f, 1e-6f);
        offBuilt.M41.Should().BeApproximately(-1.5f, 1e-6f);
    }

    [Fact]
    public void Frustum_ClassifiesBoxesSpheresAndPoints()
    {
        CameraFrustum frustum = LookAtOrigin().Frustum;

        frustum.ClassifyAabb(new Aabb3D(new Vec3(-0.5f), new Vec3(0.5f))).Should().Be(ContainmentType.Contains);
        frustum.ClassifyAabb(new Aabb3D(new Vec3(0, 0, 49), new Vec3(1, 1, 50))).Should().Be(ContainmentType.Disjoint);
        frustum.ClassifyAabb(new Aabb3D(new Vec3(-50f), new Vec3(50f))).Should().Be(ContainmentType.Intersects);

        frustum.ClassifySphere(new BoundingSphere3(new Vec3(0, 0, 0), 0.5f)).Should().Be(ContainmentType.Contains);
        frustum.ClassifySphere(new BoundingSphere3(new Vec3(0, 0, 50), 1f)).Should().Be(ContainmentType.Disjoint);

        frustum.ContainsPoint(new Vec3(0, 0, 0)).Should().BeTrue();
        frustum.ContainsPoint(new Vec3(0, 0, 50)).Should().BeFalse();

        frustum.IntersectsAabb(new Aabb3D(new Vec3(-0.5f), new Vec3(0.5f))).Should().BeTrue();
        frustum.IntersectsSphere(new BoundingSphere3(new Vec3(0, 0, 50), 1f)).Should().BeFalse();
    }

    [Fact]
    public void Frustum_AgreesWithHouseFrustum()
    {
        var camera = LookAtOrigin();
        Mat4 viewProjection = camera.ViewProjectionMatrix;

        Frustum3 house = Frustum3.CreateFromMatrix(in viewProjection);
        CameraFrustum fast = CameraFrustum.CreateFromMatrix(in viewProjection);
        var box = new Aabb3D(new Vec3(-0.5f), new Vec3(0.5f));
        var far = new Aabb3D(new Vec3(0, 0, 49), new Vec3(1, 1, 50));

        fast.ClassifyAabb(box).Should().Be(house.Contains(in box));
        fast.ClassifyAabb(far).Should().Be(house.Contains(in far));
        fast.Planes.Length.Should().Be(6);
        fast[0].Should().Be(fast.Left);
        fast[5].Should().Be(fast.Far);
    }

    [Fact]
    public void Volumes_DispatchThroughContracts()
    {
        CameraFrustum frustum = LookAtOrigin().Frustum;
        var box = new Aabb3D(new Vec3(-0.5f), new Vec3(0.5f));
        var sphere = new BoundingSphere3(new Vec3(0, 0, 0), 0.5f);

        BoundingVolume boxVolume = box;
        BoundingVolume sphereVolume = sphere;

        boxVolume.Kind.Should().Be(BoundingVolumeKind.Aabb);
        sphereVolume.Kind.Should().Be(BoundingVolumeKind.Sphere);
        boxVolume.Classify(in frustum).Should().Be(ContainmentType.Contains);
        sphereVolume.Classify(in frustum).Should().Be(ContainmentType.Contains);
        frustum.Classify(box).Should().Be(ContainmentType.Contains);
        frustum.Intersects(sphere).Should().BeTrue();
        boxVolume.Should().Be(new BoundingVolume(box));
    }

    [Fact]
    public void Camera_Pipeline_UpdatesOnPoseAndProjection()
    {
        var camera = LookAtOrigin();

        camera.Id.Value.Should().Be("main");
        camera.Near.Value.Should().BeApproximately(0.1f, 1e-6f);
        camera.Classify(new Aabb3D(new Vec3(-0.5f), new Vec3(0.5f))).Should().Be(ContainmentType.Contains);

        camera.SetPose(new Vec3(0, 0, -5), new Vec3(0, 0, 0));
        camera.Classify(new Aabb3D(new Vec3(-0.5f), new Vec3(0.5f))).Should().Be(ContainmentType.Contains);
        camera.Position.Should().Be(new Vec3(0, 0, -5));

        camera.SetProjection(new CameraPerspectiveProjection(
            RadianFoV.FromDegrees(90f), new AspectRatio(1f), new DepthDistance(0.5f), new DepthDistance(50f)));
        camera.Near.Value.Should().BeApproximately(0.5f, 1e-6f);
        camera.Far.Value.Should().BeApproximately(50f, 1e-6f);
    }

    [Fact]
    public void Builder_BuildsConfiguredCamera()
    {
        Camera3D<CameraPerspectiveProjection> camera = CameraBuilder<CameraUnconfigured, CameraPerspectiveProjection>
            .Create(new CameraId("builder"))
            .WithProjection(Perspective())
            .WithPose(new Vec3(0, 0, 5), new Vec3(0, 0, 0))
            .Build();

        camera.Id.Value.Should().Be("builder");
        camera.Classify(new Aabb3D(new Vec3(-0.5f), new Vec3(0.5f))).Should().Be(ContainmentType.Contains);

        Camera3D<CameraOrthographicProjection> ortho = CameraBuilder<CameraUnconfigured, CameraPerspectiveProjection>
            .Create(new CameraId("ortho"))
            .WithProjection(new CameraOrthographicProjection(-4f, 4f, -3f, 3f, new DepthDistance(0.1f), new DepthDistance(50f)))
            .WithPose(new CameraPose(new Vec3(0, 0, 5), new Vec3(0, 0, 0)))
            .Build();

        ortho.Projection.Left.Should().BeApproximately(-4f, 1e-6f);
    }

    [Fact]
    public void Serialization_RoundTrips()
    {
        var camera = LookAtOrigin();
        CameraSerialized dto = camera.ToSerialized();

        dto.Kind.Should().Be("perspective");
        dto.VerticalFieldOfView.Should().HaveValue();

        string json = JsonSerializer.Serialize(dto, CameraJsonContext.Default.CameraSerialized);
        object rebuilt = CameraSerializationFactory.FromJson(json);
        var perspective = rebuilt.Should().BeOfType<Camera3D<CameraPerspectiveProjection>>().Subject;

        perspective.Id.Value.Should().Be("main");
        perspective.Position.Should().Be(new Vec3(0, 0, 5));
        perspective.Near.Value.Should().BeApproximately(0.1f, 1e-5f);
    }

    [Fact]
    public void Serialization_RejectsUnknownKind()
    {
        var dto = new CameraSerialized { Id = "x", Kind = "fisheye", Near = 0.1f, Far = 10f };

        Action act = () => CameraSerializationFactory.FromDto(dto);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Culler_CullsSynchronously_WithStats()
    {
        using var culler = FrustumCullerFactory.CreateForAabbs<Aabb3D>(static (in Aabb3D item) => item);
        CameraFrustum frustum = LookAtOrigin().Frustum;

        var items = new Aabb3D[]
        {
            new(new Vec3(-0.5f), new Vec3(0.5f)),
            new(new Vec3(0, 0, 49), new Vec3(1, 1, 50)),
            new(new Vec3(-50f), new Vec3(50f)),
        };
        culler.Cull(items, in frustum);

        culler.VisibleCount.Should().Be(2);
        culler.Stats.TotalCount.Should().Be(3);
        culler.Stats.OutsideCount.Should().Be(1);
        (culler.Stats.InsideCount + culler.Stats.IntersectCount).Should().Be(2);
        culler.VisibleView.Length.Should().Be(2);
    }

    [Fact]
    public async Task Culler_CullsAsynchronously_InBatches()
    {
        using var culler = FrustumCullerFactory.CreateForSpheres<BoundingSphere3>(static (in BoundingSphere3 item) => item);
        CameraFrustum frustum = LookAtOrigin().Frustum;

        var items = new BoundingSphere3[20];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = new BoundingSphere3(new Vec3(0, 0, -i), 0.4f);
        }

        await culler.CullAsync(new ReadOnlyMemory<BoundingSphere3>(items), frustum, new CullingExecutionOptions(BatchSize: 6));

        culler.VisibleCount.Should().Be(20);
        culler.Stats.TotalCount.Should().Be(20);
    }

    [Fact]
    public async Task Culler_Cancellation_Aborts()
    {
        using var culler = FrustumCullerFactory.CreateForAabbs<Aabb3D>(static (in Aabb3D item) => item);
        CameraFrustum frustum = LookAtOrigin().Frustum;

        var items = new Aabb3D[32];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = new Aabb3D(new Vec3(-0.5f), new Vec3(0.5f));
        }

        var options = new CullingExecutionOptions(BatchSize: 4, CancellationToken: new CancellationToken(true));
        Func<Task> act = () => culler.CullAsync(new ReadOnlyMemory<Aabb3D>(items), frustum, options).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Culler_GrowsBuffer_And_DisposesCleanly()
    {
        using var culler = FrustumCullerFactory.CreateForAabbs<Aabb3D>(static (in Aabb3D item) => item, 16);
        CameraFrustum frustum = LookAtOrigin().Frustum;

        var items = new Aabb3D[100];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = new Aabb3D(new Vec3(-0.5f), new Vec3(0.5f));
        }

        culler.Cull(items, in frustum);
        culler.VisibleCount.Should().Be(100);

        Action dispose = () => culler.Dispose();
        dispose.Should().NotThrow();
    }

    [Fact]
    public void Culler_AfterDispose_Throws()
    {
        var culler = FrustumCullerFactory.CreateForAabbs<Aabb3D>(static (in Aabb3D item) => item);
        CameraFrustum frustum = LookAtOrigin().Frustum;
        culler.Dispose();

        var items = new Aabb3D[] { new(new Vec3(-0.5f), new Vec3(0.5f)) };
        Action cull = () => culler.Cull(items, in frustum);
        cull.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task CullerAsync_AfterDispose_Throws()
    {
        var culler = FrustumCullerFactory.CreateForAabbs<Aabb3D>(static (in Aabb3D item) => item);
        CameraFrustum frustum = LookAtOrigin().Frustum;
        culler.Dispose();

        var items = new Aabb3D[] { new(new Vec3(-0.5f), new Vec3(0.5f)) };
        Func<Task> act = () => culler.CullAsync(new ReadOnlyMemory<Aabb3D>(items), frustum).AsTask();

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
