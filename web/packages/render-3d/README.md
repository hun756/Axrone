# @axrone/render-3d

**Capability descriptor package — this is NOT the 3D renderer.**

This package exists so the runtime-profile capability graph can reference a
named `render/3d` capability. It intentionally contains no rendering code.

## What it exports

- `RENDER_3D_CAPABILITY_ID` (`'render/3d'`)
- `RENDER_3D_CAPABILITY_PACKAGE` / `RENDER_3D_OWNER_PACKAGE`
- `getRender3DCapability()`

The owner of the 3D rendering contracts is `@axrone/render-core`
(`ownerPackage`), and the actual 3D rendering chain is:

```
scene-runtime (orchestration, draw-executor, batch runtimes)
    -> render-core (RenderPipeline / RenderPassPlanner — platform-agnostic)
        -> render-webgl2 (GPU backend, real gl.draw* calls)
```

## What NOT to do

- Do not import pipeline/graph/types contracts from this package; import them
  from `@axrone/render-core` (contracts) or `@axrone/render-webgl2` (backend).
- Do not add runtime code here. If genuinely 3D-specific rendering logic ever
  needs a home of its own, that move must go through the architecture
  governance process (render-split boundary tests + AGENTS.md split map).

## Consumers

- `@axrone/runtime-profile-3d` / `@axrone/runtime-profile-full` — capability
  package lists only.
- Editor preview script loader — dynamic `import('@axrone/render-3d')` for
  user-script module resolution (root export only).
