#include "runtime/arena.h"

#include "platform/wasm.h"

namespace axrone::physics {

void Arena::init(unsigned char* base, size_t capacity) {
    base_ = base;
    capacity_ = capacity;
    offset_ = 0;
}

void Arena::reset() {
    offset_ = 0;
}

void* Arena::alloc(size_t size, size_t alignment) {
    if (base_ == nullptr || size == 0 || alignment == 0) {
        return nullptr;
    }
    if ((alignment & (alignment - 1)) != 0) {
        return nullptr;
    }

    const uintptr_t base = reinterpret_cast<uintptr_t>(base_);
    const uintptr_t start = base + offset_;
    if (start > static_cast<uintptr_t>(-1) - (alignment - 1)) {
        return nullptr;
    }
    const uintptr_t aligned = (start + (alignment - 1)) & ~(static_cast<uintptr_t>(alignment) - 1);
    if (size > static_cast<uintptr_t>(-1) - aligned) {
        return nullptr;
    }
    const uintptr_t end = aligned + size;

    const size_t endOffset = static_cast<size_t>(end - base);
    if (endOffset > capacity_ && !growTo(endOffset)) {
        return nullptr;
    }

    offset_ = endOffset;
    return reinterpret_cast<void*>(aligned);
}

size_t Arena::used() const {
    return offset_;
}

size_t Arena::capacity() const {
    return capacity_;
}

bool Arena::growTo(size_t requiredBytes) {
#if AXRONE_PHYSICS_TARGET_WASM
    const uintptr_t base = reinterpret_cast<uintptr_t>(base_);
    const size_t currentBytes = __builtin_wasm_memory_size(0) * 65536;
    const size_t currentCapacity = currentBytes > base ? currentBytes - base : 0;
    if (requiredBytes <= currentCapacity) {
        capacity_ = currentCapacity;
        return true;
    }

    const size_t neededBytes = requiredBytes - currentCapacity;
    const size_t neededPages = (neededBytes + 65535) / 65536;
    if (__builtin_wasm_memory_grow(0, neededPages) == static_cast<size_t>(-1)) {
        return false;
    }

    capacity_ = currentCapacity + neededPages * 65536;
    return true;
#else
    (void)requiredBytes;
    return false;
#endif
}

}
