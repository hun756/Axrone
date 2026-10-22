# Axrone.AssetGltf.Codecs

Optional decoder package for `Axrone.AssetGltf`. The core importer stays free of
native codec dependencies; this package supplies real implementations for:

- `KHR_draco_mesh_compression` via `NativeDracoMeshPolicy`: a thin C ABI
  bridge over the official google/draco C++ sources, built per RID from
  `native/extern/draco-bridge` (no third-party managed port).
- `EXT_meshopt_compression` via `Alimer.Bindings.MeshOptimizer` (native bindings
  with Windows, Linux, macOS, and Android runtime assets).
- `KHR_texture_basisu` via `BasisKtx2TranscoderPolicy`: Basis Universal
  transcoding of KTX2 textures to the block-compressed format requested by the
  host, with per-mip-level output.

## Usage

```csharp
using Axrone.AssetGltf;
using Axrone.AssetGltf.Codecs;

var importer = new GltfImporter();
GltfImportResult result = await importer
    .ImportAsync<NativeDracoMeshPolicy, MeshoptCompressionPolicy, PassthroughTranscoderPolicy>(
        glbBytes,
        cancellationToken);
```

The generic `ImportAsync` overload is available on the core `GltfImporter`
without referencing this package. The default overload keeps the no-codec
policies and rejects required Draco payloads with a coded
`gltf.extension.unsupported` error.

## Decoder characteristics

### Draco (native)

- Decodes attributes and triangle indices in one pass through
  `axr_draco_*`; glTF attributes are mapped through the Draco `unique_id`,
  like the glTF extension requires.
- Attribute values are converted to floats per point; layouts the bridge
  cannot represent are rejected instead of being silently misread.
- Normalized integer accessors keep Draco's raw integer storage. The bridge
  reports each attribute's storage type and the importer normalizes those
  values before re-quantizing them, so colors and weights are not corrupted.
- The decoded attribute and index sizes are measured before allocation and
  checked against `GltfImportLimits.MaxDecodedBytes`, so oversized payloads
  fail with `GltfLimitExceededError` instead of allocating first.
- `IsSupported` reports whether the native library for the current RID is
  actually present, so a required extension is never accepted only to fail
  at decode time. CI ships `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`
  and `android-arm64`; other RIDs get the coded
  `gltf.extension.unsupported` error.
- Build the natives with `native/extern/draco-bridge`; CI packs them from
  the `draco-native` job artifacts. The `AXRONE_REQUIRE_NATIVE_DRACO=1`
  environment variable turns a missing library into a test failure instead
  of a skip.

### Meshopt

- Supports `ATTRIBUTES`, `TRIANGLES`, and `INDICES` modes.
- Applies `OCTAHEDRAL`, `QUATERNION`, and `EXPONENTIAL` filters.
- Uses the platform native `meshoptimizer` library shipped by the NuGet
  runtime assets.
- Verified with Native AOT on `win-x64`; native assets are resolved by RID.

## Licensing

- google/draco sources (built from `native/extern/draco-bridge`, never vendored)
  - Apache-2.0, including its Section 3 patent grant.
- `Alimer.Bindings.MeshOptimizer` — MIT bindings; meshoptimizer is MIT.
- See the respective NuGet package licenses and `THIRD-PARTY-NOTICES.md`
  requirements before redistribution.

### Basis / KTX2

- Transcodes `KHR_texture_basisu` payloads through Basis Universal with
  per-mip-level output (`GltfTextureMipLevel`).
- Target formats are mapped by `TryMapTarget`; unsupported targets fail with a
  reason instead of producing mislabeled bytes.
- The core importer still parses KTX2 payloads without this package; a
  host-provided `ITextureTranscoderPolicy` can supply an alternative backend.

## Not yet included

Browser/WebAssembly native assets are outside this package.