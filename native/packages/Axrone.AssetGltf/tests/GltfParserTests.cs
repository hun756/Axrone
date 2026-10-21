namespace Axrone.AssetGltf.Tests;

public sealed class GltfParserTests
{
    [Fact]
    public void Parse_MinimalDocument_ReadsAllSections()
    {
        const string json = """
            {
              "asset": {"version": "2.0", "generator": "test"},
              "scene": 0,
              "scenes": [{"name": "S", "nodes": [0]}],
              "nodes": [{"name": "N", "mesh": 0, "translation": [1, 2, 3]}],
              "meshes": [{"name": "M", "primitives": [{"attributes": {"POSITION": 0}, "indices": 1, "mode": 4}]}],
              "accessors": [
                {"bufferView": 0, "byteOffset": 0, "componentType": 5126, "count": 3, "type": "VEC3"},
                {"bufferView": 1, "byteOffset": 0, "componentType": 5123, "count": 3, "type": "SCALAR"}
              ],
              "bufferViews": [
                {"buffer": 0, "byteOffset": 0, "byteLength": 36},
                {"buffer": 0, "byteOffset": 36, "byteLength": 6}
              ],
              "buffers": [{"byteLength": 42}],
              "materials": [{"name": "mat", "alphaMode": "MASK", "pbrMetallicRoughness": {"baseColorFactor": [1, 0, 0, 1]}}],
              "animations": [{"name": "A", "samplers": [{"input": 0, "interpolation": "LINEAR", "output": 0}], "channels": [{"sampler": 0, "target": {"node": 0, "path": "translation"}}]}],
              "extensionsUsed": ["KHR_materials_unlit"],
              "extensionsRequired": []
            }
            """;

        var model = GltfJsonParser.Parse(System.Text.Encoding.UTF8.GetBytes(json));

        model.Version.Should().Be("2.0");
        model.DefaultScene.Should().Be(0);
        model.Scenes.Should().HaveCount(1);
        model.Scenes[0].Nodes.Should().Equal(0);
        model.Nodes.Should().HaveCount(1);
        model.Nodes[0].Translation.Should().Equal(1f, 2f, 3f);
        model.Meshes.Should().HaveCount(1);
        model.Meshes[0].Primitives.Should().HaveCount(1);
        model.Meshes[0].Primitives[0].Attributes["POSITION"].Should().Be(0);
        model.Accessors.Should().HaveCount(2);
        model.Accessors[0].Type.Should().Be("VEC3");
        model.BufferViews.Should().HaveCount(2);
        model.Buffers.Should().HaveCount(1);
        model.Materials.Should().HaveCount(1);
        model.Materials[0].AlphaMode.Should().Be("MASK");
        model.Materials[0].Pbr.BaseColorFactor.Should().Equal(1f, 0f, 0f, 1f);
        model.Animations.Should().HaveCount(1);
        model.Animations[0].Channels[0].TargetPath.Should().Be("translation");
        model.ExtensionsUsed.Should().Contain("KHR_materials_unlit");
    }

    [Fact]
    public void Parse_NodeLightExtensions_PreservesSiblingNodes()
    {
        const string json = """
            {
              "asset": {"version": "2.0"},
              "extensionsUsed": ["KHR_lights_punctual"],
              "extensions": {
                "KHR_lights_punctual": {
                  "lights": [
                    {"type": "point", "intensity": 1, "range": 5},
                    {"type": "point", "intensity": 1, "range": 5},
                    {"type": "point", "intensity": 1, "range": 5}
                  ]
                }
              },
              "scene": 0,
              "scenes": [{"nodes": [0, 1, 2]}],
              "nodes": [
                {"name": "L0", "extensions": {"KHR_lights_punctual": {"light": 0}}},
                {"name": "L1", "extensions": {"KHR_lights_punctual": {"light": 1}}},
                {"name": "L2", "extensions": {"KHR_lights_punctual": {"light": 2}}}
              ]
            }
            """;

        var model = GltfJsonParser.Parse(System.Text.Encoding.UTF8.GetBytes(json));

        model.Lights.Should().HaveCount(3);
        model.Nodes.Should().HaveCount(3);
        model.Scenes[0].Nodes.Should().Equal(0, 1, 2);
        model.Nodes.Select(static node => node.Light).Should().Equal(0, 1, 2);
    }

    [Fact]
    public void Parse_MissingAsset_Throws()
    {
        Action act = () => GltfJsonParser.Parse("{}"u8.ToArray());
        act.Should().Throw<GltfSchemaError>();
    }

    [Fact]
    public void Parse_InvalidRoot_Throws()
    {
        Action act = () => GltfJsonParser.Parse("[]"u8.ToArray());
        act.Should().Throw<GltfContainerError>();
    }
}
