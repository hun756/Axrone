using System.Text;
using Axrone.AssetGltf;
using BenchmarkDotNet.Attributes;

namespace Axrone.AssetGltf.Benchmarks;

/// <summary>
/// Isolates the JSON/schema parser from the canonical compile stage.
/// Comparing <c>IncludeNames</c> on/off quantifies the upper bound of a
/// string-arena optimization for this document shape.
/// </summary>
[MemoryDiagnoser]
public class GltfParserBenchmarks
{
    private const int NodeCount = 5_000;
    private const int MeshCount = 500;
    private const int PrimitivesPerMesh = 2;

    private byte[] _json = [];

    /// <summary>Whether entity names are emitted in the test document.</summary>
    [Params(true, false)]
    public bool IncludeNames { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _json = BuildJson(IncludeNames);
    }

    [Benchmark]
    public int Parse()
    {
        using GltfRawModel model = GltfJsonParser.Parse(_json);
        return model.NodeCount + model.MeshCount + model.AccessorCount;
    }

    private static byte[] BuildJson(bool includeNames)
    {
        var builder = new StringBuilder(1 << 20);
        builder.Append("""{"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[""");
        for (int i = 0; i < NodeCount; i++)
        {
            if (i > 0) builder.Append(',');
            builder.Append(i);
        }

        builder.Append("]}],\"nodes\":[");
        for (int i = 0; i < NodeCount; i++)
        {
            if (i > 0) builder.Append(',');
            builder.Append('{');
            if (includeNames)
            {
                builder.Append("\"name\":\"Node_").Append(i).Append("\",");
            }

            builder.Append("\"mesh\":").Append(i % MeshCount).Append('}');
        }

        builder.Append("],\"meshes\":[");
        int accessor = 0;
        for (int m = 0; m < MeshCount; m++)
        {
            if (m > 0) builder.Append(',');
            builder.Append('{');
            if (includeNames)
            {
                builder.Append("\"name\":\"Mesh_").Append(m).Append("\",");
            }

            builder.Append("\"primitives\":[");
            for (int p = 0; p < PrimitivesPerMesh; p++)
            {
                if (p > 0) builder.Append(',');
                builder.Append("{\"attributes\":{\"POSITION\":").Append(accessor)
                    .Append(",\"NORMAL\":").Append(accessor + 1)
                    .Append(",\"TEXCOORD_0\":").Append(accessor + 2)
                    .Append("},\"indices\":").Append(accessor + 3)
                    .Append(",\"material\":").Append(m).Append('}');
                accessor += 4;
            }

            builder.Append("]}");
        }

        builder.Append("],\"accessors\":[");
        for (int a = 0; a < accessor; a++)
        {
            if (a > 0) builder.Append(',');
            builder.Append(a % 4 == 3
                ? """{"bufferView":0,"componentType":5123,"count":3,"type":"SCALAR"}"""
                : """{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3"}""");
        }

        builder.Append("],\"materials\":[");
        for (int m = 0; m < MeshCount; m++)
        {
            if (m > 0) builder.Append(',');
            builder.Append('{');
            if (includeNames)
            {
                builder.Append("\"name\":\"Material_").Append(m).Append("\",");
            }

            builder.Append("\"pbrMetallicRoughness\":{\"baseColorFactor\":[1,0.5,0.25,1]}}");
        }

        builder.Append("""
            ],"bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":36}],
            "buffers":[{"byteLength":36}]}
            """);

        return Encoding.UTF8.GetBytes(builder.ToString());
    }
}
