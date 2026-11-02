#include "core/events.h"

namespace axrone::physics {

namespace {

constexpr int32_t kEventChunkCapacity = 512;

struct EventChunkData {
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

void buildNextChunk(const World& world, bool hasLower, uint64_t lower, EventChunk& chunk) {
    chunk.size = 0;
    const int32_t contactCount = world.contactCount;
    for (int32_t i = 0; i < contactCount; ++i) {
        const Contact& contact = world.contacts[i];
        const uint64_t key = contactPairKey(contact.a, contact.b);
        if (hasLower && key <= lower) {
            continue;
        }
        if (chunk.size == kEventChunkCapacity && key > chunk.keys[kEventChunkCapacity - 1]) {
            continue;
        }
        EventChunkData item;
        item.nx = contact.normal.x;
        item.ny = contact.normal.y;
        item.nz = contact.normal.z;
        item.pen = contact.pen;
        chunkInsert(chunk, key, item);
    }
}

void writePairEvent(
    ContactEvent* events,
    int32_t index,
    int32_t kind,
    uint64_t key,
    const EventChunkData* item
) {
    events[index].kind = kind;
    events[index].a = int32_t(key >> 32);
    events[index].b = int32_t(uint32_t(key));
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

}

int32_t collectContactEvents(
    const World& world,
    ContactEventState& state,
    ContactEvent* events,
    int32_t capacity
) {
    if (events == nullptr || capacity < 0 || state.keys == nullptr || state.capacity < 1 ||
        state.count < 0 || state.count > state.capacity || world.contactCount < 0 ||
        (world.contacts == nullptr && world.contactCount > 0)) {
        return -1;
    }

    const uint64_t* prev = state.keys;
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
                writePairEvent(events, written, kContactEventBegin, chunk.keys[i], &chunk.data[i]);
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
                writePairEvent(events, written, kContactEventStay, chunk.keys[i], &chunk.data[i]);
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
                writePairEvent(events, written, kContactEventEnd, prev[prevIndex], nullptr);
                ++written;
            }
            ++prevIndex;
        }
        hasLower = true;
        lower = chunkMax;
        buildNextChunk(world, hasLower, lower, chunk);
    }
    while (prevIndex < prevCount) {
        writePairEvent(events, written, kContactEventEnd, prev[prevIndex], nullptr);
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
