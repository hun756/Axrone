#pragma once

#if defined(__wasm__)
#define AXP_WASM 1
#else
#define AXP_WASM 0
#endif

#define AXP_INLINE inline __attribute__((always_inline))

#if AXP_WASM
#define AXP_EXPORT(name) extern "C" __attribute__((visibility("default"), export_name(#name)))
#else
#define AXP_EXPORT(name) extern "C"
#endif
