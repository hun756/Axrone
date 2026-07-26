# @axrone/render-2d

2D sprite batching layer of the render split.

- **Capability descriptor:** exports `RENDER_2D_CAPABILITY_ID` (`'render/2d'`,
  `ownerPackage: '@axrone/render-core'`) for the runtime-profile capability
  graph.
- **Runtime:** `Render2DSpriteBatchBuilder` (CPU-side sprite batch/vertex
  construction), the default sprite shader effect definition, and the 2D
  submission/batch types.

This package issues no GL calls itself; `@axrone/scene-runtime`'s sprite batch
runtime uploads and draws the batches it produces through the WebGL2 backend.

```
scene-runtime (SceneSpriteBatchRuntime — GL upload/draw)
    -> render-2d (Render2DSpriteBatchBuilder — batching, no GL)
        -> render-core (shader-effect compilation, shared contracts)
```
