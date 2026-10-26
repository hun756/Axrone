# Axrone.AssetGltf

Static glTF 2.0 importer for the Axrone engine. Parses `.gltf` and `.glb`
documents into canonical, engine-ready assets (scenes, meshes, materials,
textures, skins, animations) with no native dependencies and no reflection.

## Highlights

- AOT-safe manual `Utf8JsonReader` parser; trimming and AOT analyzers enabled.
- Value-type slab parser: nodes, meshes, primitives and accessors live in pooled
  arrays; strings and child/attribute/weight pools use a single UTF-8 arena.
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

## Parser architecture

- `Utf8JsonReader` streams directly into value-type slabs. Attributes are
  mapped to `GltfMeshSemantic` during parse; only `_CUSTOM` names are retained
  as UTF-8 arena references.
- `GltfRawModel` owns the rented slabs and is an internal, single-use parse
  result. `GltfImporter` disposes it after compilation, so no pooled memory
  escapes into `GltfImportResult`.
- Low-volume/deeply nested blocks (materials, textures, animations) remain
  reference types; the slab targets the high-volume geometry path.
- Cancellation is checked per node, mesh, primitive and accessor.

Measured with BenchmarkDotNet (`--job short`, same machine):

| Scenario | Before | After |
|---|---:|---:|
| Parser, 5k nodes / 500 meshes / 4k accessors | 2.15 MB, 1.36 ms | **0.31 MB, 1.20 ms** |
| Full import, 50k vertices / 75k triangles | 2.58 ms, 4.06 MB | **1.30 ms, 4.06 MB** |

The parser is allocation-free per entity, not literally zero-allocation: the
arena, pool rentals and start-up arrays are amortized across the parse.

## Limits and errors

`GltfImporterOptions.Limits` caps source size, declared buffers, node, mesh,
primitive, accessor, attribute, morph-target and variant counts, transient
parse bytes, and decoded bytes. Failures derive from `GltfException` and carry
a stable `Code` (`gltf.schema.invalid`, `gltf.accessor.invalid`,
`gltf.limit.exceeded`, `gltf.extension.unsupported`, ...), so callers can
branch without parsing messages.

## Threading

Importers are stateless per call. A single `GltfImportResult` is immutable and
safe to share; parsing and compilation happen on the caller's context.

## License

Apache-2.0. See `LICENSE`. Third-party notices are listed in
`THIRD-PARTY-NOTICES.md`.
