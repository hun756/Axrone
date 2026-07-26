# @axrone/input-core

**Capability descriptor package — the input runtime lives in `@axrone/input`.**

This package exists so the runtime-profile capability graph can reference a
named `input/core` capability (`ownerPackage: '@axrone/input'`). For
convenience it re-exports the full `@axrone/input` public API from its root,
so `import('@axrone/input-core')` resolves to the same surface the Editor
preview script loader exposes to user scripts.

## What it exports

- `INPUT_CORE_CAPABILITY_ID` (`'input/core'`)
- `INPUT_CORE_CAPABILITY_PACKAGE` / `INPUT_CORE_OWNER_PACKAGE`
- `getInputCoreCapability()`
- Re-export of `@axrone/input` (owner package surface)

## What NOT to do

- Do not add input runtime code here; it belongs to `@axrone/input`.
- New engine code should import from `@axrone/input` directly; this package is
  for capability wiring and user-script module resolution.
