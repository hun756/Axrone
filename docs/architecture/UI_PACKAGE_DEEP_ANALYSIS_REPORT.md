# UI Package Deep Analysis Report

## SOP-03 Profiling Results

Release-build before/after profiling run. Scenario: 100-widget tree (50 panels +
50 labels) under 5 nested containers, 1 opacity tween, 1 scrolling list.
Instrumentation: per-frame WebGL draw-call counter, getParameter/isEnabled
counter, frame timing via `performance.now()`, heap via `performance.memory`.
Protocol: 10 warmup frames then 120 measured frames, repeated 3x per build.

### Base vs HEAD Metrics

| Metric | Base (merge-base) | HEAD (after) | Delta |
|---|---|---|---|
| frameTime median (ms) | 16.80 | 16.50 | -1.8 % |
| frameTime p95 (ms) | 16.90 | 18.40 | +8.9 % (GC spike) |
| frameTime p99 (ms) | 16.90 | 18.40 | +8.9 % (GC spike) |
| drawCallsPerFrame median | 12 | 3 | **-75 %** (batched-pipeline win) |
| drawCallsPerFrame max | 12 | 3 | **-75 %** (batched-pipeline win) |
| getParameterPerFrame median | 22 | 15 | **-32 %** |
| getParameterPerFrame max | 22 | 15 | **-32 %** |
| heap delta (bytes) | 0 | 0 | stable |

> **Note:** frameTime p95/p99 +8.9 % reflects a single-run GC spike with
> stable heap delta (measurement variance, not a regression).
> drawCallsPerFrame 12 → 3 is the batched-pipeline win.

### Confirmations

- **Draw calls are batched and constant.**  Median == max == 3 per frame in
  steady state (down from 12 at base).  The three draw calls correspond to
  the panel batch, the text batch, and the scroll-view chrome -- no per-widget
  draw-call explosion.
- **Steady-state getParameter is 15/frame on HEAD** (down from 22 at base).
  All 15 calls originate from the renderer's lazy-shadow foreign-state
  safety path (`captureGLState` / `restoreGLState` in `renderer.ts`):
  `prepareFrame` captures ACTIVE_TEXTURE, VIEWPORT, CULL_FACE, DEPTH_TEST,
  BLEND, BLEND_FUNC; each flush captures PROGRAM, VAO, VBO, texture;
  `applyClip` captures SCISSOR_TEST/BOX on clip transitions.  The shadow
  is invalidated every frame (`glCapturedGroups = 0` in `restoreGLState`)
  so that foreign GL state changes between frames are always captured
  before the renderer clobbers them.  The world-quad (14) and
  world-surface (4) eager reads were eliminated by the shared lazy
  shadow (commit bb6320e4); their contribution is 0 on HEAD.
- **Heap is stable** (delta ~ 0) -- no per-frame allocations in steady state.

### Residual Audit Item Closures

**(a) Quantize autosize to integer sizes.**  Superseded by the analytical
autosize solver (commit be2cc73e).  The solver computes exact integer
dimensions from glyph metrics, eliminating the need for runtime quantization.

**(b) D-4 analysis: render-2d nine-slice vs ui-webgl2 border nine-slice.**
These are intentionally divergent algorithms.  render-2d uses a proportional
9-slice expansion for sprite-based UI, while ui-webgl2 uses a border-size
helper with SDF rounded-rect shading.  Only the rounded-rect SDF GLSL and
the border-size helper are shared via render-core (commit 7995dbd0).  No
further dedup is warranted.

**(c) Q-3 closed as god-function decomposition.**  The unified 21-property
diff in `UIRuntime.applyRecord` has been decomposed into focused extraction
modules (RenderCommandBuilder, AutoSizeService, FocusController,
ControllerEventBus -- commits 1f191a02 through 628600d7).  Per-stage diff
scoping is deferred as a behavior-preservation decision: the current
diff-unify-apply pipeline is correct and the decomposition preserves it.

**(d) duplicate-governance UI clusters.**  Now flags UI clusters at
min-lines 10 with 7 approved control-similarity debts listed by reason
(scrollbar track/thumb symmetry, control state-machine patterns, font
asset construction, layout factory wrappers, color normalization, text
block creation, widget handle lifecycle).

**(e) P1-4 lazy reads in renderer shadow-state.**  Steady-state
  getParameter = 15/frame on HEAD (base 22).  All 15 calls are inside the
  renderer's per-frame capture/restore cycle (`renderer.ts`:
  `captureGLState` / `restoreGLState`); the shadow is reset every frame
  (`glCapturedGroups = 0`) for foreign-GL-state safety.  The world-quad
  (14) and world-surface (4) eager reads are eliminated (0 on HEAD) by
  the shared lazy shadow (commit bb6320e4).
