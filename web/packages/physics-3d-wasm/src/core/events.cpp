#include "core/events.h"

namespace axrone::physics {

namespace {

constexpr int32_t kEventChunkCapacity = 512;

struct EventChunkData {
    int32_t sensorBits;
    float nx;
    float ny;
    float nz;
    float pen;
};

struct EventChunk {
    uint64_t keys[kEventChunkCapacity];
    EventChunkData data[kEventChunkCapacity];
    int32_t size;
};

int32_t findSortedKey(const uint64_t* keys, int32_t count, uint64_t key) {
    int32_t lo = 0;
    int32_t hi = count;
    while (lo < hi) {
        const int32_t mid = lo + (hi - lo) / 2;
        if (keys[mid] < key) {
            lo = mid + 1;
        } else {
            hi = mid;
        }
    }
    if (lo < count && keys[lo] == key) {
        return lo;
    }
    return -1;
}

void chunkInsert(EventChunk& chunk, uint64_t key, const EventChunkData& item) {
    int32_t lo = 0;
    int32_t hi = chunk.size;
    while (lo < hi) {
        const int32_t mid = lo + (hi - lo) / 2;
        if (chunk.keys[mid] < key) {
            lo = mid + 1;
        } else {
            hi = mid;
        }
    }
    if (lo < chunk.size && chunk.keys[lo] == key) {
        return;
    }
    const int32_t count = chunk.size < kEventChunkCapacity ? chunk.size : kEventChunkCapacity - 1;
    for (int32_t i = count; i > lo; --i) {
        chunk.keys[i] = chunk.keys[i - 1];
        chunk.data[i] = chunk.data[i - 1];
    }
    chunk.keys[lo] = key;
    chunk.data[lo] = item;
    if (chunk.size < kEventChunkCapacity) {
        ++chunk.size;
    }
}

int32_t eventShapeId(const World& world, int32_t bodyId, int32_t slot) {
    int32_t generation = 0;
    if (world.slotGen != nullptr && bodyId >= 0 && bodyId < world.highWater) {
        const uintptr_t index = static_cast<uintptr_t>(bodyId) * kMaxShapesPerBody + static_cast<uintptr_t>(slot);
        generation = static_cast<int32_t>(world.slotGen[index] & kShapeIdGenMask);
    }
    return shapeEncodeId(generation, bodyId, slot);
}

void buildNextChunk(const World& world, bool hasLower, uint64_t lower, EventChunk& chunk) {
    chunk.size = 0;
    const int32_t contactCount = world.contactCount;
    for (int32_t i = 0; i < contactCount; ++i) {
        const Contact& contact = world.contacts[i];
        const int32_t storageA = shapeStorageKey(contact.a, contact.shapeA);
        const int32_t storageB = shapeStorageKey(contact.b, contact.shapeB);
        const uint64_t key = contactPairKey(storageA, storageB);
        if (hasLower && key <= lower) {
            continue;
        }
        if (chunk.size == kEventChunkCapacity && key > chunk.keys[kEventChunkCapacity - 1]) {
            continue;
        }

        int32_t lowStorage = storageA;
        int32_t highStorage = storageB;
        const bool storageSwapped = storageB < storageA;
        if (storageSwapped) {
            lowStorage = storageB;
            highStorage = storageA;
        }

        const int32_t lowBody = shapeStorageBody(lowStorage);
        const int32_t lowSlot = shapeStorageSlot(lowStorage);
        const int32_t highBody = shapeStorageBody(highStorage);
        const int32_t highSlot = shapeStorageSlot(highStorage);

        const bool sensorLow = world.bodies[lowBody].shapes[lowSlot].sensor != 0;
        const bool sensorHigh = world.bodies[highBody].shapes[highSlot].sensor != 0;

        EventChunkData item;
        item.sensorBits = (sensorLow ? 1 : 0) | (sensorHigh ? 2 : 0);
        const bool normalSwap = storageSwapped != (item.sensorBits == 2);
        const float sign = normalSwap ? -1.0f : 1.0f;
        item.nx = contact.normal.x * sign;
        item.ny = contact.normal.y * sign;
        item.nz = contact.normal.z * sign;
        item.pen = contact.pen;
        chunkInsert(chunk, key, item);
    }
}

void writePairEvent(
    const World& world,
    ContactEvent* events,
    int32_t index,
    int32_t kind,
    uint64_t key,
    const EventChunkData* item,
    int32_t sensorBits
) {
    events[index].kind = kind;
    events[index].reserved = 0.0f;
    int32_t storageA = int32_t(key >> 32);
    int32_t storageB = int32_t(uint32_t(key));
    if (sensorBits == 2) {
        const int32_t swap = storageA;
        storageA = storageB;
        storageB = swap;
    }
    events[index].bodyA = shapeStorageBody(storageA);
    events[index].bodyB = shapeStorageBody(storageB);
    events[index].shapeA = eventShapeId(world, shapeStorageBody(storageA), shapeStorageSlot(storageA));
    events[index].shapeB = eventShapeId(world, shapeStorageBody(storageB), shapeStorageSlot(storageB));
    if (item != nullptr) {
        events[index].nx = item->nx;
        events[index].ny = item->ny;
        events[index].nz = item->nz;
        events[index].penetration = item->pen;
        return;
    }
    events[index].nx = 0.0f;
    events[index].ny = 0.0f;
    events[index].nz = 0.0f;
    events[index].penetration = 0.0f;
}

int32_t eventKind(const int32_t baseKind, const int32_t sensorBits) {
    return sensorBits != 0 ? baseKind + 3 : baseKind;
}

}

int32_t collectContactEvents(
    const World& world,
    ContactEventState& state,
    ContactEvent* events,
    int32_t capacity
) {
    if (events == nullptr || capacity < 0 || state.keys == nullptr || state.sensors == nullptr ||
        state.capacity < 1 || state.count < 0 || state.count > state.capacity ||
        world.contactCount < 0 || world.bodies == nullptr ||
        (world.contacts == nullptr && world.contactCount > 0)) {
        return -1;
    }

    const uint64_t* prev = state.keys;
    const uint8_t* prevSensors = state.sensors;
    const int32_t prevCount = state.count;

    EventChunk chunk;
    bool hasLower = false;
    uint64_t lower = 0;

    int32_t currentCount = 0;
    int32_t stayCount = 0;
    buildNextChunk(world, hasLower, lower, chunk);
    while (chunk.size > 0) {
        for (int32_t i = 0; i < chunk.size; ++i) {
            if (findSortedKey(prev, prevCount, chunk.keys[i]) >= 0) {
                ++stayCount;
            }
        }
        currentCount += chunk.size;
        hasLower = true;
        lower = chunk.keys[chunk.size - 1];
        buildNextChunk(world, hasLower, lower, chunk);
    }

    const int32_t endCount = prevCount - stayCount;
    const int32_t totalEvents = currentCount + endCount;
    if (currentCount > state.capacity || totalEvents > capacity) {
        return -1;
    }

    int32_t written = 0;

    hasLower = false;
    lower = 0;
    buildNextChunk(world, hasLower, lower, chunk);
    while (chunk.size > 0) {
        for (int32_t i = 0; i < chunk.size; ++i) {
            if (findSortedKey(prev, prevCount, chunk.keys[i]) < 0) {
                writePairEvent(world, events, written, eventKind(kContactEventBegin, chunk.data[i].sensorBits), chunk.keys[i], &chunk.data[i], chunk.data[i].sensorBits);
                ++written;
            }
        }
        hasLower = true;
        lower = chunk.keys[chunk.size - 1];
        buildNextChunk(world, hasLower, lower, chunk);
    }

    hasLower = false;
    lower = 0;
    buildNextChunk(world, hasLower, lower, chunk);
    while (chunk.size > 0) {
        for (int32_t i = 0; i < chunk.size; ++i) {
            if (findSortedKey(prev, prevCount, chunk.keys[i]) >= 0) {
                writePairEvent(world, events, written, eventKind(kContactEventStay, chunk.data[i].sensorBits), chunk.keys[i], &chunk.data[i], chunk.data[i].sensorBits);
                ++written;
            }
        }
        hasLower = true;
        lower = chunk.keys[chunk.size - 1];
        buildNextChunk(world, hasLower, lower, chunk);
    }

    hasLower = false;
    lower = 0;
    int32_t prevIndex = 0;
    buildNextChunk(world, hasLower, lower, chunk);
    while (chunk.size > 0) {
        const uint64_t chunkMax = chunk.keys[chunk.size - 1];
        while (prevIndex < prevCount && prev[prevIndex] <= chunkMax) {
            if (findSortedKey(chunk.keys, chunk.size, prev[prevIndex]) < 0) {
                writePairEvent(world, events, written, eventKind(kContactEventEnd, prevSensors[prevIndex]), prev[prevIndex], nullptr, prevSensors[prevIndex]);
                ++written;
            }
            ++prevIndex;
        }
        hasLower = true;
        lower = chunkMax;
        buildNextChunk(world, hasLower, lower, chunk);
    }
    while (prevIndex < prevCount) {
        writePairEvent(world, events, written, eventKind(kContactEventEnd, prevSensors[prevIndex]), prev[prevIndex], nullptr, prevSensors[prevIndex]);
        ++written;
        ++prevIndex;
    }

    int32_t stateCount = 0;
    hasLower = false;
    lower = 0;
    buildNextChunk(world, hasLower, lower, chunk);
    while (chunk.size > 0) {
        for (int32_t i = 0; i < chunk.size; ++i) {
            state.keys[stateCount] = chunk.keys[i];
            state.sensors[stateCount] = static_cast<uint8_t>(chunk.data[i].sensorBits);
            ++stateCount;
        }
        hasLower = true;
        lower = chunk.keys[chunk.size - 1];
        buildNextChunk(world, hasLower, lower, chunk);
    }
    state.count = stateCount;

    return written;
}

}
