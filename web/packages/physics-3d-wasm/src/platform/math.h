#pragma once

#include "platform/wasm.h"

namespace axp {

constexpr float kFloatMax = 3.402823466e38f;

AXP_INLINE float sqrtf(float value) {
    return __builtin_sqrtf(value);
}

AXP_INLINE float fabsf(float value) {
    return __builtin_fabsf(value);
}

AXP_INLINE bool isFinite(float value) {
    return value == value && value <= kFloatMax && value >= -kFloatMax;
}

AXP_INLINE float clampf(float value, float low, float high) {
    return value < low ? low : (value > high ? high : value);
}

}
