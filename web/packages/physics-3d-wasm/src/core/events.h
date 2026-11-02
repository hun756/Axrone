#pragma once

#include <stdint.h>

#include "core/world.h"

namespace axrone::physics {

enum ContactEventKind : int32_t {
    kContactEventBegin = 0,
    kContactEventStay = 1,
    kContactEventEnd = 2
};

struct ContactEvent {
    int32_t kind;
    int32_t a;
    int32_t b;
    float nx;
    float ny;
    float nz;
    float penetration;
};

struct ContactEventState {
    uint64_t* keys;
    int32_t capacity;
    int32_t count;
};

AXRONE_PHYSICS_INLINE constexpr uint64_t contactPairKey(int32_t a, int32_t b) {
    const int32_t low = a < b ? a : b;
    const int32_t high = a < b ? b : a;
    return (uint64_t(low) << 32) | uint64_t(high);
}

int32_t collectContactEvents(const World& world, ContactEventState& state, ContactEvent* events, int32_t capacity);

}
