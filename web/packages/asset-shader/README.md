# Asset Shader

`@axrone/asset-shader` owns the authored shader-effect asset import surface for Axrone's JSON-native shader workflow.

## Canonical Authored Extensions

Shader effect assets should use one of these canonical file extensions:

- `.effect.json`
- `.shader.json`

Generic `.json` files are intentionally not matched by the shader importer. This prevents unrelated JSON assets from being claimed accidentally and keeps authored shader assets explicit in the asset pipeline.

## What The Package Provides

- normalization and validation for JSON-authored shader effects
- import-pipeline integration through `createAssetShaderImportPipeline()`
- canonical `RenderShaderEffectDefinition` output
- compatibility with runtime consumption through `@axrone/scene-runtime`
- schema v2 metadata for keywords, property target-channel bindings, and techniques/passes

## Authoring Flow

1. Create a JSON shader asset using `.effect.json` or `.shader.json`.
2. Import it through `createAssetShaderImportPipeline()`.
3. Consume the imported `shaderEffect` asset data as a `RenderShaderEffectDefinition`.
4. Convert that effect into a runtime shader definition with `createSceneShaderDefinitionFromEffect()` from `@axrone/scene-runtime`.

## Example Assets

The package includes canonical examples under `examples/`:

- `hero-tint.effect.json`: minimal shorthand authored effect asset
- `rig-preview.shader.json`: wrapped effect payload with inspector select options and array uniforms
- `advanced-surface.effect.json`: schema v2 metadata with keywords, property bindings, and a technique/pass layout