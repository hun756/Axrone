using System.Buffers.Binary;
using System.Text;
using Axrone.AssetGltf;
using Axrone.AssetGltf.Codecs;

static byte[] Glb(byte[] json, byte[] bin)
{
    int jsonPad = (4 - (json.Length % 4)) % 4;
    int binPad = (4 - (bin.Length % 4)) % 4;
    int total = 12 + 8 + json.Length + jsonPad + 8 + bin.Length + binPad;
    byte[] result = new byte[total];
    BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0, 4), 0x46546C67);
    BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), 2);
    BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8, 4), (uint)total);
    BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12, 4), (uint)(json.Length + jsonPad));
    BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16, 4), 0x4E4F534A);
    json.CopyTo(result, 20);
    for (int i = 0; i < jsonPad; i++) result[20 + json.Length + i] = 0x20;
    int offset = 20 + json.Length + jsonPad;
    BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset, 4), (uint)(bin.Length + binPad));
    BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset + 4, 4), 0x004E4942);
    bin.CopyTo(result, offset + 8);
    return result;
}

byte[] bin = new byte[44];
float[] positions = [0, 0, 0, 1, 0, 0, 0, 1, 0];
Buffer.BlockCopy(positions, 0, bin, 0, 36);
bin[36] = 0; bin[38] = 1; bin[40] = 2;
const string json = """
{"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[0]}],"nodes":[{"name":"N","mesh":0}],"meshes":[{"primitives":[{"attributes":{"POSITION":0},"indices":1}]}],"accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3"},{"bufferView":1,"componentType":5123,"count":3,"type":"SCALAR"}],"bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":36},{"buffer":0,"byteOffset":36,"byteLength":6}],"buffers":[{"byteLength":44}]}
""";
GltfImportResult result = await new GltfImporter().ImportAsync(Glb(Encoding.UTF8.GetBytes(json), bin)).ConfigureAwait(false);
Console.WriteLine($"aot-ok meshes={result.Meshes.Length} draco={NoDracoCompressionPolicy.IsSupported} meshopt={MeshoptCompressionPolicy.IsSupported} basis={BasisKtx2TranscoderPolicy.PolicyId}");
