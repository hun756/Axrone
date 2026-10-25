# runtimes/

RID-specific `axrone_draco` native libraries live here after a build:

```text
runtimes/
  win-x64/native/axrone_draco.dll
  linux-x64/native/libaxrone_draco.so
  osx-x64/native/libaxrone_draco.dylib
  osx-arm64/native/libaxrone_draco.dylib
  android-arm64/native/libaxrone_draco.so
```

Built from `native/extern/draco-bridge` (official google/draco sources).
Binaries are produced by the `draco-native` CI job and packed from CI
artifacts — they are git-ignored here and never committed. `IsSupported`
only covers RIDs with a shipped binary; everywhere else Draco payloads are
rejected with `gltf.extension.unsupported`.
