namespace Axrone.Scene.Prefab.Tests;

using System.Text.Json;
using Axrone.Scene;
using Axrone.Scene.Prefab;
using FluentAssertions;
using Xunit;

public sealed class ScenePrefabOverrideTests
{
    private static readonly int[] ChangedPosition = [9, 9, 9];

    [Fact]
    public void SetActorField_RebuildsActorMetadata()
    {
        ScenePrefabDefinition definition = CreateDefinition(
            Actor("root"),
            Actor("child", parentNodeId: "root"));

        ScenePrefabDefinition result = ScenePrefabOverrides.Apply(
            definition,
            [
                new ScenePrefabSetActorFieldOperation(
                    "child",
                    ScenePrefabActorField.Name,
                    ScenePrefabActorFieldValue.FromString("Renamed")),
                new ScenePrefabSetActorFieldOperation(
                    "child",
                    ScenePrefabActorField.Layer,
                    ScenePrefabActorFieldValue.FromInt32(7)),
                new ScenePrefabSetActorFieldOperation(
                    "child",
                    ScenePrefabActorField.Tag,
                    ScenePrefabActorFieldValue.FromString("Player")),
                new ScenePrefabSetActorFieldOperation(
                    "child",
                    ScenePrefabActorField.Active,
                    ScenePrefabActorFieldValue.FromBoolean(false)),
                new ScenePrefabSetActorFieldOperation(
                    "child",
                    ScenePrefabActorField.Persistent,
                    ScenePrefabActorFieldValue.FromBoolean(true)),
                new ScenePrefabSetActorFieldOperation(
                    "child",
                    ScenePrefabActorField.Pooled,
                    ScenePrefabActorFieldValue.FromBoolean(true))
            ]);

        SceneActorSnapshot child = result.Actors[1];
        child.Name.Should().Be("Renamed");
        child.Layer.Should().Be(7);
        child.Tag.Should().Be("Player");
        child.Active.Should().BeFalse();
        child.Persistent.Should().BeTrue();
        child.Pooled.Should().BeTrue();
        child.ParentNodeId.Should().Be("root");
    }

    [Fact]
    public void ReparentActor_ChangesParent()
    {
        ScenePrefabDefinition definition = CreateDefinition(
            Actor("root"),
            Actor("other"),
            Actor("child", parentNodeId: "root"));

        ScenePrefabDefinition result = ScenePrefabOverrides.Apply(
            definition,
            [new ScenePrefabReparentActorOperation("child", "other")]);

        result.Actors[2].ParentNodeId.Should().Be("other");

        ScenePrefabDefinition detached = ScenePrefabOverrides.Apply(
            result,
            [new ScenePrefabReparentActorOperation("child", null)]);
        detached.Actors[2].ParentNodeId.Should().BeNull();
    }

    [Fact]
    public void RemoveActor_RemovesSubtree()
    {
        ScenePrefabDefinition definition = CreateDefinition(
            Actor("root"),
            Actor("branch", parentNodeId: "root"),
            Actor("leaf", parentNodeId: "branch"),
            Actor("sibling", parentNodeId: "root"));

        ScenePrefabDefinition result = ScenePrefabOverrides.Apply(
            definition,
            [new ScenePrefabRemoveActorOperation("branch")]);

        result.Actors.Should().HaveCount(2);
        result.Actors.Should().OnlyContain(actor => actor.NodeId != "branch" && actor.NodeId != "leaf");
        result.Actors.Should().Contain(actor => actor.NodeId == "sibling");
    }

    [Fact]
    public void AddActor_InsertsAfterAnchor()
    {
        ScenePrefabDefinition definition = CreateDefinition(
            Actor("root"),
            Actor("tail", parentNodeId: "root"));

        ScenePrefabDefinition result = ScenePrefabOverrides.Apply(
            definition,
            [new ScenePrefabAddActorOperation(Actor("inserted", parentNodeId: "root"), "root")]);

        result.Actors.Select(actor => actor.NodeId)
            .Should().ContainInOrder("root", "inserted", "tail");
    }

    [Fact]
    public void AddComponent_ClampsIndexAndRejectsDuplicates()
    {
        ScenePrefabDefinition definition = CreateDefinition(
            Actor("root", components: Component("Transform", """{"position":[0,0,0]}""")));

        ScenePrefabDefinition result = ScenePrefabOverrides.Apply(
            definition,
            [
                new ScenePrefabAddComponentOperation(
                    "root",
                    Component("Camera", """{"fov":60}"""),
                    index: 16)
            ]);

        result.Actors[0].Components.Should().HaveCount(2);
        result.Actors[0].Components[1].Type.Should().Be("Camera");

        Action duplicateType = () => ScenePrefabOverrides.Apply(
            result,
            [
                new ScenePrefabAddComponentOperation(
                    "root",
                    Component("Camera", """{"fov":75}"""))
            ]);
        duplicateType.Should().Throw<ScenePrefabException>()
            .Where(exception => exception.Code == ScenePrefabErrorCodes.DuplicateComponent);

        Action duplicateId = () => ScenePrefabOverrides.Apply(
            result,
            [
                new ScenePrefabAddComponentOperation(
                    "root",
                    Component("Light", """{"intensity":1}""", id: "light"),
                    0)
            ]);
        duplicateId.Should().NotThrow();
    }

    [Fact]
    public void RemoveComponent_ResolvesByType()
    {
        ScenePrefabDefinition definition = CreateDefinition(
            Actor(
                "root",
                components:
                [
                    Component("Transform", """{"position":[0,0,0]}"""),
                    Component("Camera", """{"fov":60}""")
                ]));

        ScenePrefabDefinition result = ScenePrefabOverrides.Apply(
            definition,
            [
                new ScenePrefabRemoveComponentOperation(
                    "root",
                    ScenePrefabComponentSelector.FromType("Transform"))
            ]);

        result.Actors[0].Components.Should().ContainSingle()
            .Which.Type.Should().Be("Camera");
    }

    [Fact]
    public void ReplaceComponent_SwapsPayload()
    {
        ScenePrefabDefinition definition = CreateDefinition(
            Actor("root", components: Component("Camera", """{"fov":60}""", id: "main-camera")));

        ScenePrefabDefinition result = ScenePrefabOverrides.Apply(
            definition,
            [
                new ScenePrefabReplaceComponentOperation(
                    "root",
                    ScenePrefabComponentSelector.FromId("main-camera"),
                    Component("Camera", """{"fov":90}""", id: "main-camera"))
            ]);

        SceneComponentSnapshot component = result.Actors[0].Components[0];
        component.Data.GetProperty("fov").GetInt32().Should().Be(90);
    }

    [Fact]
    public void SetComponentProperty_WritesNestedValues()
    {
        ScenePrefabDefinition definition = CreateDefinition(
            Actor("root", components: Component("Transform", """{"position":[1,2,3]}""")));

        ScenePrefabDefinition result = ScenePrefabOverrides.Apply(
            definition,
            [
                new ScenePrefabSetComponentPropertyOperation(
                    "root",
                    ScenePrefabComponentSelector.FromType("Transform"),
                    new ScenePrefabPropertyPath(
                        ScenePrefabPropertyPathSegment.ForName("position"),
                        ScenePrefabPropertyPathSegment.ForIndex(1)),
                    JsonSerializer.SerializeToElement(5)),
                new ScenePrefabSetComponentPropertyOperation(
                    "root",
                    ScenePrefabComponentSelector.FromType("Transform"),
                    new ScenePrefabPropertyPath(
                        ScenePrefabPropertyPathSegment.ForName("meta"),
                        ScenePrefabPropertyPathSegment.ForName("author")),
                    JsonSerializer.SerializeToElement("vex"))
            ]);

        JsonElement data = result.Actors[0].Components[0].Data;
        data.GetProperty("position")[1].GetInt32().Should().Be(5);
        data.GetProperty("meta").GetProperty("author").GetString().Should().Be("vex");
    }

    [Fact]
    public void UnsetComponentProperty_RemovesValues()
    {
        ScenePrefabDefinition definition = CreateDefinition(
            Actor(
                "root",
                components: Component(
                    "Transform",
                    """{"position":[1,2,3],"meta":{"author":"vex","revision":2}}""")));

        ScenePrefabDefinition result = ScenePrefabOverrides.Apply(
            definition,
            [
                new ScenePrefabUnsetComponentPropertyOperation(
                    "root",
                    ScenePrefabComponentSelector.FromType("Transform"),
                    new ScenePrefabPropertyPath(
                        ScenePrefabPropertyPathSegment.ForName("meta"),
                        ScenePrefabPropertyPathSegment.ForName("author"))),
                new ScenePrefabUnsetComponentPropertyOperation(
                    "root",
                    ScenePrefabComponentSelector.FromType("Transform"),
                    new ScenePrefabPropertyPath(
                        ScenePrefabPropertyPathSegment.ForName("position"),
                        ScenePrefabPropertyPathSegment.ForIndex(1)))
            ]);

        JsonElement data = result.Actors[0].Components[0].Data;
        data.GetProperty("meta").TryGetProperty("author", out _).Should().BeFalse();
        data.GetProperty("position").GetArrayLength().Should().Be(2);
        data.GetProperty("position")[1].GetInt32().Should().Be(3);
    }

    [Fact]
    public void Apply_LeavesBaseDefinitionUntouched()
    {
        ScenePrefabDefinition definition = CreateDefinition(
            Actor("root", components: Component("Transform", """{"position":[0,0,0]}""")));

        _ = ScenePrefabOverrides.Apply(
            definition,
            [
                new ScenePrefabSetActorFieldOperation(
                    "root",
                    ScenePrefabActorField.Name,
                    ScenePrefabActorFieldValue.FromString("Changed")),
                new ScenePrefabSetComponentPropertyOperation(
                    "root",
                    ScenePrefabComponentSelector.FromType("Transform"),
                    new ScenePrefabPropertyPath(ScenePrefabPropertyPathSegment.ForName("position")),
                    JsonSerializer.SerializeToElement(ChangedPosition))
            ]);

        definition.Actors[0].Name.Should().Be("root");
        definition.Actors[0].Components[0].Data
            .GetProperty("position")[0].GetInt32().Should().Be(0);
    }

    [Fact]
    public void UnknownNode_Throws()
    {
        ScenePrefabDefinition definition = CreateDefinition(Actor("root"));

        Action act = () => ScenePrefabOverrides.Apply(
            definition,
            [new ScenePrefabRemoveActorOperation("missing")]);

        act.Should().Throw<ScenePrefabException>()
            .Where(exception => exception.Code == ScenePrefabErrorCodes.UnknownNode)
            .Where(exception => exception.NodeId == "missing");
    }

    [Fact]
    public void UnknownComponent_Throws()
    {
        ScenePrefabDefinition definition = CreateDefinition(Actor("root"));

        Action act = () => ScenePrefabOverrides.Apply(
            definition,
            [
                new ScenePrefabRemoveComponentOperation(
                    "root",
                    ScenePrefabComponentSelector.FromType("Camera"))
            ]);

        act.Should().Throw<ScenePrefabException>()
            .Where(exception => exception.Code == ScenePrefabErrorCodes.UnknownComponent);
    }

    [Fact]
    public void DuplicateComponentType_IsRejectedByTheDefinition()
    {
        Action act = () => CreateDefinition(
            Actor(
                "root",
                components:
                [
                    Component("Camera", """{"fov":60}"""),
                    Component("Camera", """{"fov":90}""")
                ]));

        act.Should().Throw<ScenePrefabException>()
            .Where(exception => exception.Code == ScenePrefabErrorCodes.DuplicateComponent);
    }

    private static ScenePrefabDefinition CreateDefinition(params SceneActorSnapshot[] actors) =>
        new("prefab/test", actors);

    private static SceneActorSnapshot Actor(
        string nodeId,
        string? parentNodeId = null,
        params SceneComponentSnapshot[] components) =>
        new(
            nodeId,
            nodeId,
            0,
            "Untagged",
            active: true,
            persistent: false,
            pooled: false,
            components,
            parentNodeId);

    private static SceneComponentSnapshot Component(string type, string json, string? id = null)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return new SceneComponentSnapshot(type, document.RootElement.Clone(), id);
    }
}
