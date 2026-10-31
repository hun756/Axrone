# Lean scene-2d entry

`@axrone/scene-2d/lean` is an additive, tree-shakeable entry for consumers that
only need the 2D scene facade. The root `@axrone/scene-2d` entry is unchanged.

## Surface

Values: `Scene2D`, `Camera`, `SpriteRenderer`, `Color`.

Types: `SceneOptions`, `SceneRegistry`, `SceneRenderStats`,
`SceneRenderPlanningStats`, `SpriteRendererConfig`, `CameraConfig`, plus the
shader/material/mesh/texture definition + handle types from
`@axrone/scene-runtime/types`.

## Adoption

```ts
import { Scene2D } from '@axrone/scene-2d/lean';
```

The identity test (`src/__tests__/lean-entry.test.ts`) proves every lean value
is the same reference as the root export.

## Measurement

| Entry | min | gzip |
| --- | --- | --- |
| `benchmark-web/src/adapters/axrone2d` baseline (snapshot) | 1429.1 KB | 350.4 KB |
| same entry re-measured today | 1433.5 KB | 351.7 KB |
| same entry with `@axrone/scene-2d/lean` | 1433.3 KB | 351.4 KB |

Adoption alone is neutral. The graph is dominated by `SceneRuntimeKernel`
(static physics-2d + physics-3d bridges, full default registry) and by
`scene-2d-actor-runtime.ts`, which statically imports `SpriteAnimator` and
`SpriteMask` and therefore pulls `animation` (76 KB min) into every sprite-only
consumer.

See `../../scene-3d/docs/lean-entry.md` for the full module-graph analysis,
the isolated-copy ceiling (2D: 351.7 → 280.3 KB gzip, −20.3 %) and the
required runtime-owner follow-up.
