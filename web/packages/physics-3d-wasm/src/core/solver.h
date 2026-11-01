#pragma once

#include <stdint.h>

#include "core/world.h"

namespace axp {

void prepareContacts(World& world);
void solveVelocities(World& world, int32_t iterations);
void solvePositions(World& world, int32_t iterations);
void writePackedContacts(World& world);

}
