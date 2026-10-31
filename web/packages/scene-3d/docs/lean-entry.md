# Lean scene-3d entry

`@axrone/scene-3d/lean` is an additive, tree-shakeable entry for consumers that
only need the 3D scene facade (render-only apps, benchmark harnesses, playable
ads). The root `@axrone/scene-3d` entry is unchanged.

## Surface

Values: `Scene`, `Scene3DActorRuntime`, `MeshRenderer`, `Camera`,
`SceneGeometryMeshBuilder`, `createUnlitColorShaderDefinition`.

Types: `SceneOptions`, `SceneRegistry`, `SceneRenderStats`,
`SceneRenderPlanningStats`, `SceneRenderableActorCreateOptions`,
`SceneRenderableActorInstance`, `Scene3DActorRuntimeOptions`,
`SceneProfilerProbe`, `MeshRendererConfig`, `CameraConfig`, and the
shader/material/mesh/texture definition + handle types from
`@axrone/scene-runtime/types`.

## Adoption

```ts
import {
    Scene,
    SceneGeometryMeshBuilder,
    createUnlitColorShaderDefinition,
} from '@axrone/scene-3d/lean';
import type { SceneRenderableActorInstance } from '@axrone/scene-3d/lean';
```

The identity test (`src/__tests__/lean-entry.test.ts`) proves every lean value
is the same reference as the root export, so switching entries cannot change
runtime behavior.

## Measured graph findings (2026-10-31)

Method: esbuild 0.25.12, browser ESM, minify, tree-shaken, gzip level 9,
`define: { 'process.env.NODE_ENV': '"production"' }` — the same settings as
`benchmark-web/scripts/measure-footprint.mjs`. `three` is imported by the
benchmark adapter itself (camera orientation), not by the engine.

| Entry | min | gzip |
| --- | --- | --- |
| `benchmark-web/src/adapters/axrone3d` baseline (snapshot in `results/footprint.json`) | 1680.6 KB | 408.0 KB |
| same entry re-measured today | 1685.2 KB | 409.4 KB |
| same entry with `@axrone/scene-3d/lean` | 1685.0 KB | 409.9 KB |
| `new Scene()` only (engine floor) | 1425.1 KB | 348.7 KB |

Adopting the lean entry alone is neutral (within ±0.5 KB): the barrel is not
the bottleneck. The measured graph is gated by `SceneRuntimeKernel` and the
default scene profile, which statically pull:

- `physics-2d` (137 KB min) and `physics-3d` (123 KB min) via
  `scene-runtime/src/scene-runtime-kernel.ts:31-33` and the bridge
  construction in the kernel constructor (`scene-runtime-kernel.ts:166`).
- `particle-system` (97 KB), `animation` (76 KB), `random` (40 KB),
  `scene-prefab` (24 KB), `terrain`, and 3D/2D/UI/animation registries via
  `scene-profile.ts` → `scene-full-profile.ts` →
  `DEFAULT_SCENE_BUILT_IN_MANIFESTS`. Every profile module also freezes a
  profile object at module scope, so importing a profile id retains its whole
  registry.
- `@axrone/particle-system` again via the static particle/line/billboard/
  morph/skinning batch runtimes in
  `scene-runtime/src/rendering/scene-render-runtime.ts:25-28`.

Physics is instantiated for *every* kernel regardless of profile:
`resolvePhysicsBridgesForProfile` selects at runtime, after both bridge
modules are already bundled.

## Projected ceiling (isolated temp copy, not shipped)

Re-measured in a throwaway copy of `scene-runtime` with (a) physics bridges
removed and (b) the kernel fallback profile limited to Camera/MeshRenderer/
SpriteRenderer/lights:

| Entry | baseline min/gzip | projected min/gzip | gzip delta |
| --- | --- | --- | --- |
| axrone3d adapter | 1685.2 / 409.4 | 1266.2 / 312.4 | −23.7 % |
| axrone2d adapter | 1433.5 / 351.7 | 1122.4 / 280.3 | −20.3 % |
| 3D engine-only probe | 1425.1 / 348.7 | 1008.7 / 252.3 | −27.6 % |

203 modules leave the graph (117 scene-runtime, 26 physics-3d, 25 animation,
20 physics-2d, plus terrain/asset-2d/utility).

## Required follow-up (owner: scene-runtime)

1. Inject physics bridge factories through `SceneOptions` instead of static
   imports in `scene-runtime-kernel.ts`. Root `Scene`/`Scene2D` pass the
   current bridges (behavior unchanged); lean entries pass none.
2. Move default-registry resolution out of the kernel fallback. Root entries
   keep the full manifest; lean entries get a minimal core registry. Every
   profile module currently freezes its registry at module scope, so this is
   the only way to make `particle-system`, `animation`, `terrain`, UI and
   `random` fall out of lean graphs.
3. Split `scene-2d/src/scene-2d-actor-runtime.ts` animated/mask actor creation
   out of the base sprite path — it statically pulls `animation` (76 KB min)
   into every 2D consumer.
4. Optional: make particle/line/billboard/morph/skinning batch runtimes in
   `rendering/scene-render-runtime.ts` opt-in (Rex/Atlas own this path).

None of these are behavior-preserving for raw `SceneRuntimeKernel` users and
therefore must be done by the runtime owners, not from a lean entry.

## Known unrelated risk

`physics-3d/package.json` declares `"sideEffects": false`, but
`physics-world-3d-constraints-{fixed,hinge,slider,cone-twist,configurable}.ts`
register constraint modules at import time and are imported for side effects
from `physics-world-3d-contact-runtime.ts:31-35`. esbuild drops them with an
`ignored-bare-import` warning. Fixing the metadata would add bytes to any
bundle that includes physics and conflicts with the "every package
sideEffects:false" governance test, so it is reported here instead of changed.
