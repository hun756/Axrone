#pragma once

#include <stdint.h>

#include "core/world.h"

namespace axp {

void updateBounds(World& world);
int32_t buildSweep(World& world);
void broadphase(World& world);

}
