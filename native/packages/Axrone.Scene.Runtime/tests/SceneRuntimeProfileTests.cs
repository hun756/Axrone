namespace Axrone.Scene.Runtime.Tests;

using Axrone.Scene;
using Axrone.Scene.Rendering;
using Axrone.Scene.Runtime;
using FluentAssertions;
using Xunit;

public sealed class SceneRuntimeProfileTests
{
    [Fact]
    public void BuiltInProfiles_ExposeStableIdsAndKinds()
    {
        SceneRuntimeProfiles.Core.Id.Should().Be(SceneRuntimeProfileIds.Core);
        SceneRuntimeProfiles.TwoD.Id.Should().Be(SceneRuntimeProfileIds.TwoD);
        SceneRuntimeProfiles.ThreeD.Id.Should().Be(SceneRuntimeProfileIds.ThreeD);
        SceneRuntimeProfiles.Mixed.Id.Should().Be(SceneRuntimeProfileIds.Mixed);
        SceneRuntimeProfiles.Full.Id.Should().Be(SceneRuntimeProfileIds.Full);

        SceneRuntimeProfiles.Core.Kind.Should().Be(SceneRuntimeProfileKind.Core);
        SceneRuntimeProfiles.TwoD.Kind.Should().Be(SceneRuntimeProfileKind.TwoD);
        SceneRuntimeProfiles.ThreeD.Kind.Should().Be(SceneRuntimeProfileKind.ThreeD);
        SceneRuntimeProfiles.Mixed.Kind.Should().Be(SceneRuntimeProfileKind.Mixed);
        SceneRuntimeProfiles.Full.Kind.Should().Be(SceneRuntimeProfileKind.Full);
    }

    [Theory]
    [InlineData(SceneRuntimeProfileIds.Core, SceneRuntimeProfileKind.Core)]
    [InlineData(SceneRuntimeProfileIds.TwoD, SceneRuntimeProfileKind.TwoD)]
    [InlineData(SceneRuntimeProfileIds.ThreeD, SceneRuntimeProfileKind.ThreeD)]
    [InlineData(SceneRuntimeProfileIds.Mixed, SceneRuntimeProfileKind.Mixed)]
    [InlineData(SceneRuntimeProfileIds.Full, SceneRuntimeProfileKind.Full)]
    public void TryResolve_FindsBuiltIns(string id, SceneRuntimeProfileKind expected)
    {
        SceneRuntimeProfiles.TryResolve(id, out SceneRuntimeProfile? profile).Should().BeTrue();
        profile!.Kind.Should().Be(expected);
    }

    [Fact]
    public void TryResolve_RejectsUnknownIds()
    {
        SceneRuntimeProfiles.TryResolve("scene/unknown", out SceneRuntimeProfile? profile).Should().BeFalse();
        profile.Should().BeNull();
    }

    [Fact]
    public void Get_RejectsCustomFamily()
    {
        Action act = () => SceneRuntimeProfiles.Get(SceneRuntimeProfileKind.Custom);

        act.Should().Throw<SceneConfigurationException>();
    }

    [Fact]
    public void CoreProfile_RegistersTransformsOnly()
    {
        SceneProfile profile = SceneRuntimeProfiles.Core.Profile;

        profile.HasTransform3D.Should().BeTrue();
        profile.HasTransform2D.Should().BeFalse();
        profile.Registry.TryGetDataByType(typeof(SceneCamera), out _).Should().BeFalse();
        profile.Registry.TryGetDataByType(typeof(SceneSpriteRenderer), out _).Should().BeFalse();
        profile.Registry.TryGetDataByType(typeof(SceneMeshRenderer), out _).Should().BeFalse();
    }

    [Fact]
    public void TwoDProfile_RegistersCameraAndSprites()
    {
        SceneProfile profile = SceneRuntimeProfiles.TwoD.Profile;

        profile.HasTransform2D.Should().BeTrue();
        profile.HasTransform3D.Should().BeFalse();
        profile.Registry.TryGetDataByType(typeof(SceneCamera), out _).Should().BeTrue();
        profile.Registry.TryGetDataByType(typeof(SceneCamera2D), out _).Should().BeTrue();
        profile.Registry.TryGetDataByType(typeof(SceneSpriteRenderer), out _).Should().BeTrue();
        profile.Registry.TryGetDataByType(typeof(SceneMeshRenderer), out _).Should().BeFalse();
        profile.Registry.TryGetDataByType(typeof(SceneFog), out _).Should().BeFalse();
    }

    [Fact]
    public void ThreeDProfile_RegistersCameraMeshesLightsAndFog()
    {
        SceneProfile profile = SceneRuntimeProfiles.ThreeD.Profile;

        profile.HasTransform3D.Should().BeTrue();
        profile.HasTransform2D.Should().BeFalse();
        profile.Registry.TryGetDataByType(typeof(SceneCamera), out _).Should().BeTrue();
        profile.Registry.TryGetDataByType(typeof(SceneMeshRenderer), out _).Should().BeTrue();
        profile.Registry.TryGetDataByType(typeof(SceneDirectionalLight), out _).Should().BeTrue();
        profile.Registry.TryGetDataByType(typeof(SceneFog), out _).Should().BeTrue();
        profile.Registry.TryGetDataByType(typeof(SceneSpriteRenderer), out _).Should().BeFalse();
    }

    [Fact]
    public void MixedProfile_RegistersBothPaths()
    {
        SceneProfile profile = SceneRuntimeProfiles.Mixed.Profile;

        profile.HasTransform3D.Should().BeTrue();
        profile.HasTransform2D.Should().BeTrue();
        profile.Registry.TryGetDataByType(typeof(SceneCamera), out _).Should().BeTrue();
        profile.Registry.TryGetDataByType(typeof(SceneCamera2D), out _).Should().BeTrue();
        profile.Registry.TryGetDataByType(typeof(SceneSpriteRenderer), out _).Should().BeTrue();
        profile.Registry.TryGetDataByType(typeof(SceneMeshRenderer), out _).Should().BeTrue();
        profile.Registry.TryGetDataByType(typeof(SceneDirectionalLight), out _).Should().BeTrue();
    }

    [Fact]
    public void FullProfile_MatchesThreeDComposition()
    {
        SceneProfile profile = SceneRuntimeProfiles.Full.Profile;

        profile.HasTransform3D.Should().BeTrue();
        profile.HasTransform2D.Should().BeFalse();
        profile.Registry.TryGetDataByType(typeof(SceneMeshRenderer), out _).Should().BeTrue();
        profile.Registry.TryGetDataByType(typeof(SceneSpriteRenderer), out _).Should().BeFalse();
    }

    [Fact]
    public void PhysicsBridgeSelection_FollowsProfileFamily()
    {
        SceneRuntimeProfiles.Core.PhysicsBridges.Should().Be(new ScenePhysicsBridgeSelection(true, false));
        SceneRuntimeProfiles.ThreeD.PhysicsBridges.Should().Be(new ScenePhysicsBridgeSelection(true, false));
        SceneRuntimeProfiles.Full.PhysicsBridges.Should().Be(new ScenePhysicsBridgeSelection(true, false));
        SceneRuntimeProfiles.TwoD.PhysicsBridges.Should().Be(new ScenePhysicsBridgeSelection(false, true));
        SceneRuntimeProfiles.Mixed.PhysicsBridges.Should().Be(new ScenePhysicsBridgeSelection(true, true));

        SceneRuntimeProfiles.ResolvePhysicsBridges("scene/unknown")
            .Should().Be(new ScenePhysicsBridgeSelection(true, false));
        SceneRuntimeProfiles.ResolvePhysicsBridges(null)
            .Should().Be(new ScenePhysicsBridgeSelection(true, false));
    }

    [Fact]
    public void Runtime_DefaultsToFullProfile()
    {
        using var runtime = new SceneRuntime(new SceneRuntimeOptions());

        runtime.RuntimeProfile.Should().BeSameAs(SceneRuntimeProfiles.Full);
        runtime.Profile.HasTransform3D.Should().BeTrue();
        runtime.Profile.HasTransform2D.Should().BeFalse();
        runtime.PhysicsBridges.Bridge3D.Should().BeTrue();
    }

    [Fact]
    public void Runtime_UsesExplicitRuntimeProfile()
    {
        using var runtime = new SceneRuntime(new SceneRuntimeOptions
        {
            RuntimeProfile = SceneRuntimeProfiles.TwoD
        });

        runtime.RuntimeProfile.Id.Should().Be(SceneRuntimeProfileIds.TwoD);
        runtime.World.HasTransform2D.Should().BeTrue();
        runtime.World.HasTransform3D.Should().BeFalse();
        runtime.PhysicsBridges.Should().Be(new ScenePhysicsBridgeSelection(false, true));
    }

    [Fact]
    public void Runtime_PrefersRuntimeProfileOverCustomComposition()
    {
        SceneProfile custom = new SceneProfileBuilder(
                new SceneProfileId(SceneStringId.FromName("test/custom")))
            .AddTransform2D()
            .Build();

        using var runtime = new SceneRuntime(new SceneRuntimeOptions
        {
            RuntimeProfile = SceneRuntimeProfiles.ThreeD,
            Profile = custom
        });

        runtime.RuntimeProfile.Kind.Should().Be(SceneRuntimeProfileKind.ThreeD);
        runtime.Profile.HasTransform3D.Should().BeTrue();
    }

    [Fact]
    public void Runtime_WrapsCustomComposition()
    {
        SceneProfile custom = new SceneProfileBuilder(
                new SceneProfileId(SceneStringId.FromName("test/custom")))
            .AddTransform2D()
            .Build();

        using var runtime = new SceneRuntime(new SceneRuntimeOptions { Profile = custom });

        runtime.RuntimeProfile.Kind.Should().Be(SceneRuntimeProfileKind.Custom);
        runtime.RuntimeProfile.Id.Should().Be(SceneRuntimeProfileIds.Custom);
        runtime.Profile.HasTransform2D.Should().BeTrue();
        runtime.PhysicsBridges.Bridge3D.Should().BeTrue();
    }
}
