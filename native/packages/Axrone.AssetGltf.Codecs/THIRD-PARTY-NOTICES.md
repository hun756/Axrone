# Third-Party Notices — Axrone.AssetGltf.Codecs

This package is distributed with the following third-party dependencies.

## Runtime dependencies

### google/draco 1.5.7 (native, built from source)

- License: Apache-2.0 (sources fetched at build time, never vendored).
- Repository: https://github.com/google/draco
- Built via `native/extern/draco-bridge` into `runtimes/<rid>/native` by CI.
- Apache-2.0 Section 3 patent grant applies to the official sources used.

### Alimer.Bindings.MeshOptimizer 1.2.1

- License: MIT (bindings and meshoptimizer).
- Repository: https://github.com/amerkoleci/Alimer.Bindings
- Upstream: https://github.com/zeux/meshoptimizer (MIT).
- Native runtime assets are selected by .NET RID.

### BasisUniversal.NET 1.0.0

- License: Apache-2.0.
- Repository: https://github.com/YingFengTingYu/BasisUniversal.NET
- Upstream: BinomialLLC/basis_universal (Apache-2.0).

### BasisUniversal.Native 1.0.0

- License: Apache-2.0 AND BSD-3-Clause.
- Native runtime assets are selected by .NET RID.
- See the package's `THIRD-PARTY-NOTICES.md` for the complete upstream notices.

## Test data

- `tests/TestData/draco-bunny.drc` — sample from the google/draco test
  corpus (Apache-2.0), via the `draco3dgltf` distribution.
- `tests/TestData/draco-grid-1k.drc` — synthetic grid mesh encoded to the
  Draco bitstream for unit tests.

## Test and benchmark dependencies

- xUnit — Apache-2.0.
- Microsoft.NET.Test.Sdk — MIT.
- AwesomeAssertions 9.6.0 — Apache-2.0.
- BenchmarkDotNet — MIT.

## Note on FluentAssertions

These projects use **AwesomeAssertions**, the Apache-2.0 fork of
FluentAssertions. They do not reference FluentAssertions 8, which has separate
commercial licensing terms. Other packages in the repository may still use
FluentAssertions and should be reviewed independently.
