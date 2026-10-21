# Axrone.AssetGltf.Codecs

Optional decoder package for `Axrone.AssetGltf`. The core importer stays free of
native codec dependencies; this package supplies real implementations for:

- `EXT_meshopt_compression` via `Alimer.Bindings.MeshOptimizer` (native bindings
  with Windows, Linux, macOS, and Android runtime assets).

## Usage

```csharp
using Axrone.AssetGltf;
using Axrone.AssetGltf.Codecs;

var importer = new GltfImporter();
GltfImportResult result = await importer
    .ImportAsync<NoDracoCompressionPolicy, MeshoptCompressionPolicy, PassthroughTranscoderPolicy>(
        glbBytes,
        cancellationToken);
```

The generic `ImportAsync` overload is available on the core `GltfImporter`
without referencing this package. The default overload keeps the no-codec
policies and rejects required Draco payloads with a coded
`gltf.extension.unsupported` error.

## Decoder characteristics

### Meshopt

- Supports `ATTRIBUTES`, `TRIANGLES`, and `INDICES` modes.
- Applies `OCTAHEDRAL`, `QUATERNION`, and `EXPONENTIAL` filters.
- Uses the platform native `meshoptimizer` library shipped by the NuGet
  runtime assets.
- Verified with Native AOT on `win-x64`; native assets are resolved by RID.

## Licensing

- `Alimer.Bindings.MeshOptimizer` — MIT bindings; meshoptimizer is MIT.
- See the respective NuGet package licenses and `THIRD-PARTY-NOTICES.md`
  requirements before redistribution.

## Not yet included

KTX2/Basis transcoding and browser/WebAssembly native assets are outside this
package. `KHR_texture_basisu` payloads are still parsed by the core importer and
can be transcoded through a host-provided `ITextureTranscoderPolicy`.