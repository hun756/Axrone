#pragma once

#include <stddef.h>
#include <stdint.h>

namespace axrone::physics {

class Arena {
public:
    void init(unsigned char* base, size_t capacity);
    void reset();
    void* alloc(size_t size, size_t alignment);

    size_t used() const;
    size_t capacity() const;

private:
    bool growTo(size_t requiredBytes);

    unsigned char* base_;
    size_t capacity_;
    size_t offset_;
};

}
