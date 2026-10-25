# Axrone.AssetGltf

Static glTF 2.0 importer for the Axrone engine. Parses `.gltf` and `.glb`
documents into canonical, engine-ready assets (scenes, meshes, materials,
textures, skins, animations) with no native dependencies and no reflection.

## Highlights

- AOT-safe manual `Utf8JsonReader` parser; trimming and AOT analyzers enabled.
- Zero-copy read paths over the GLB BIN chunk; interleaved vertex output.
- Sparse accessors, morph targets, skins, cameras, `KHR_lights_punctual`,
  material variants, `KHR_texture_transform`, and `KHR_animation_pointer`.
- Modern material extensions (specular, sheen, transmission, volume, IOR,
  iridescence, anisotropy, dispersion, emissive strength, unlit).
- `KHR_materials_variants` mappings and `KHR_draco_mesh_compression` through
  pluggable monomorphized policies.
- Application-specific vertex attributes (`_NAME`) preserved in the
  interleaved buffer and described by `GltfMeshDefinition.CustomAttributes`.
- Configurable import limits and cancellation for untrusted input.

## Usage

```csharp
using Axrone.AssetGltf;

var importer = new GltfImporter(new GltfImporterOptions { Id = "scene.glb" });
GltfImportResult result = await importer.ImportAsync(glbBytes, cancellationToken);
```

The default importer has no codec dependencies. Compressed payloads are
rejected with a coded `gltf.extension.unsupported` error when required.

To enable native codecs, reference `Axrone.AssetGltf.Codecs` and select the
policies explicitly:

```csharp
GltfImportResult result = await importer
    .ImportAsync<NativeDracoMeshPolicy, MeshoptCompressionPolicy, BasisKtx2TranscoderPolicy>(
        glbBytes,
        cancellationToken);
```

## Limits and errors

`GltfImporterOptions.Limits` caps source size, declared buffers, accessor and
node counts, and decoded bytes. Failures derive from `GltfException` and carry
a stable `Code` (`gltf.schema.invalid`, `gltf.accessor.invalid`,
`gltf.limit.exceeded`, `gltf.extension.unsupported`, ...), so callers can
branch without parsing messages.

## Threading

Importers are stateless per call. A single `GltfImportResult` is immutable and
safe to share; parsing and compilation happen on the caller's context.

## License

Apache-2.0. See `LICENSE`. Third-party notices are listed in
`THIRD-PARTY-NOTICES.md`.
