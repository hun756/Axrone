namespace Axrone.Scene.Prefab.Tests;

using System.Globalization;
using System.Text.Json;
using Axrone.Numeric;
using Axrone.Scene;
using Axrone.Scene.Prefab;
using FluentAssertions;
using Xunit;

public sealed class ScenePrefabTests
{
    [Fact]
    public void FactoryAndInstantiator_RoundTripHierarchyAndComponents()
    {
        SceneProfile profile = CreateProfile();
        SceneCodecRegistry codecs = CreateCodecs(profile);
        using SceneWorld source = profile.CreateWorld();

        SceneActor root = source.CreateActor("Root");
        SceneActor child = source.CreateActor("Child");
        source.GetTransform3D(root).SetPosition(new Vec3(1.0f, 2.0f, 3.0f));
        source.GetTransform3D(child).SetPosition(new Vec3(4.0f, 5.0f, 6.0f));
        source.AddData(root, new Health { Value = 10 });
        source.AddData(child, new Health { Value = 20 });
        source.Hierarchy.SetParent(child, root);
        source.UpdateTransforms();

        ScenePrefabDefinition prefab = ScenePrefabFactory.Create(source, codecs, "prefab/roundtrip");
        prefab.Actors.Should().HaveCount(2);
        prefab.Actors[0].NodeId.Should().Be(root.Slot.ToString(CultureInfo.InvariantCulture));
        prefab.Actors[1].ParentNodeId.Should().Be(root.Slot.ToString(CultureInfo.InvariantCulture));
        prefab.Actors[0].Components.Should().Contain(component => component.Type == "Health");
        prefab.Actors[0].Components.Should().Contain(component => component.Type == "Transform3D");

        using SceneWorld copy = profile.CreateWorld();
        ScenePrefabInstantiationResult result = ScenePrefabInstantiator.Instantiate(copy, codecs, prefab);
        SceneActor rootCopy = result.NodeMap[prefab.Actors[0].NodeId];
        SceneActor childCopy = result.NodeMap[prefab.Actors[1].NodeId];

        result.Actors.Should().HaveCount(2);
        copy.Hierarchy.ParentOf(childCopy).Should().Be(rootCopy);
        copy.GetTransform3D(rootCopy).Position.Should().Be(new Vec3(1.0f, 2.0f, 3.0f));
        copy.GetTransform3D(childCopy).Position.Should().Be(new Vec3(4.0f, 5.0f, 6.0f));
        copy.GetData<Health>(rootCopy).Value.Should().Be(10);
        copy.GetData<Health>(childCopy).Value.Should().Be(20);

        copy.UpdateTransforms();
        copy.GetWorldPosition3D(childCopy).Should().Be(new Vec3(5.0f, 7.0f, 9.0f));
    }

    [Fact]
    public void Json_RoundTripsPrefab()
    {
        SceneProfile profile = CreateProfile();
        SceneCodecRegistry codecs = CreateCodecs(profile);
        using SceneWorld source = profile.CreateWorld();
        SceneActor root = source.CreateActor("Root");
        source.GetTransform3D(root).SetPosition(new Vec3(2.0f, 0.0f, 0.0f));
        source.AddData(root, new Health { Value = 7 });

        ScenePrefabDefinition prefab = ScenePrefabFactory.Create(source, codecs, "prefab/json");
        byte[] json = ScenePrefabJson.Serialize(prefab);
        ScenePrefabDefinition read = ScenePrefabJson.Deserialize(json);

        read.Id.Should().Be("prefab/json");
        read.Actors.Should().ContainSingle();
        read.Actors[0].Name.Should().Be("Root");
        read.Actors[0].Components.Should().Contain(component => component.Type == "Health");

        using SceneWorld copy = profile.CreateWorld();
        ScenePrefabInstantiationResult result = ScenePrefabInstantiator.Instantiate(copy, codecs, read);
        result.Actors.Should().ContainSingle();
        copy.GetData<Health>(result.Actors[0]).Value.Should().Be(7);
    }

    [Fact]
    public void Instantiation_UnknownComponent_RollsBack()
    {
        SceneProfile profile = CreateProfile();
        SceneCodecRegistry codecs = CreateCodecs(profile);
        using JsonDocument document = JsonDocument.Parse("{}");
        var component = new SceneComponentSnapshot("Missing", document.RootElement);
        var actor = new SceneActorSnapshot(
            "0",
            "Actor",
            0,
            "Default",
            active: true,
            persistent: false,
            pooled: false,
            [component]);
        var prefab = new ScenePrefabDefinition("prefab/missing", [actor]);
        using SceneWorld world = profile.CreateWorld();

        Action instantiate = () => ScenePrefabInstantiator.Instantiate(world, codecs, prefab);

        instantiate.Should().Throw<ScenePrefabException>()
            .Which.Code.Should().Be(ScenePrefabErrorCodes.UnknownComponent);
        world.ActorCount.Should().Be(0);
    }

    [Fact]
    public void Prefab_RejectsDuplicateNodeIds()
    {
        var first = CreateEmptyActor("0", "First");
        var second = CreateEmptyActor("0", "Second");

        Action create = () => { _ = new ScenePrefabDefinition("prefab/duplicate", [first, second]); };

        create.Should().Throw<ScenePrefabException>()
            .Which.Code.Should().Be(ScenePrefabErrorCodes.DuplicateNode);
    }

    [Fact]
    public void Prefab_RejectsMissingParent()
    {
        var child = new SceneActorSnapshot(
            "1",
            "Child",
            0,
            "Default",
            active: true,
            persistent: false,
            pooled: false,
            components: [],
            parentNodeId: "99");

        Action create = () => { _ = new ScenePrefabDefinition("prefab/missing-parent", [child]); };

        create.Should().Throw<ScenePrefabException>()
            .Which.Code.Should().Be(ScenePrefabErrorCodes.MissingParent);
    }

    [Fact]
    public void Instantiation_AppliesNamePrefix()
    {
        SceneProfile profile = CreateProfile();
        SceneCodecRegistry codecs = CreateCodecs(profile);
        using SceneWorld source = profile.CreateWorld();
        _ = source.CreateActor("Root");
        ScenePrefabDefinition prefab = ScenePrefabFactory.Create(source, codecs, "prefab/prefix");
        using SceneWorld copy = profile.CreateWorld();

        ScenePrefabInstantiationResult result = ScenePrefabInstantiator.Instantiate(
            copy,
            codecs,
            prefab,
            new ScenePrefabInstantiateOptions { NamePrefix = "copy_" });

        copy.GetName(result.Actors[0]).Should().Be("copy_Root");
    }

    private static SceneProfile CreateProfile()
    {
        var builder = new SceneProfileBuilder(
            new SceneProfileId(SceneStringId.FromName("test/prefab")));
        return builder
            .AddTransform3D()
            .AddData<Health>("Health", SceneComponentTraits.Serializable)
            .Build();
    }

    private static SceneCodecRegistry CreateCodecs(SceneProfile profile)
    {
        var codecs = new SceneCodecRegistry(profile.Registry);
        ScenePrefabCodecs.RegisterCore(codecs, profile.Registry);
        codecs.Register(new HealthCodec());
        return codecs;
    }

    private static SceneActorSnapshot CreateEmptyActor(string nodeId, string name) =>
        new(
            nodeId,
            name,
            0,
            "Default",
            active: true,
            persistent: false,
            pooled: false,
            components: []);

    private struct Health : ISceneDataComponent
    {
        public int Value;
    }

    private sealed class HealthCodec : SceneDataCodec<Health>
    {
        public override void Write(Utf8JsonWriter writer, in Health value) =>
            writer.WriteNumber("value", value.Value);

        public override Health Read(JsonElement data) =>
            new() { Value = data.GetProperty("value").GetInt32() };
    }
}
