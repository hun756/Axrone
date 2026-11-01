#include <stddef.h>
#include <stdint.h>

extern "C" void* memset(void* dest, int value, size_t count) {
    unsigned char* out = static_cast<unsigned char*>(dest);
    const unsigned char byte = static_cast<unsigned char>(value);

    if (count >= 16 && (reinterpret_cast<uintptr_t>(out) & 3) == 0) {
        const uint32_t word = (static_cast<uint32_t>(byte) << 24) |
                              (static_cast<uint32_t>(byte) << 16) |
                              (static_cast<uint32_t>(byte) << 8) |
                              static_cast<uint32_t>(byte);
        uint32_t* out32 = reinterpret_cast<uint32_t*>(out);
        while (count >= 4) {
            *out32++ = word;
            count -= 4;
        }
        out = reinterpret_cast<unsigned char*>(out32);
    }

    while (count > 0) {
        *out++ = byte;
        count -= 1;
    }
    return dest;
}

extern "C" void* memcpy(void* dest, const void* source, size_t count) {
    unsigned char* out = static_cast<unsigned char*>(dest);
    const unsigned char* in = static_cast<const unsigned char*>(source);

    if (count >= 16 &&
        ((reinterpret_cast<uintptr_t>(out) | reinterpret_cast<uintptr_t>(in)) & 3) == 0) {
        uint32_t* out32 = reinterpret_cast<uint32_t*>(out);
        const uint32_t* in32 = reinterpret_cast<const uint32_t*>(in);
        while (count >= 4) {
            *out32++ = *in32++;
            count -= 4;
        }
        out = reinterpret_cast<unsigned char*>(out32);
        in = reinterpret_cast<const unsigned char*>(in32);
    }

    while (count > 0) {
        *out++ = *in++;
        count -= 1;
    }
    return dest;
}

extern "C" void* memmove(void* dest, const void* source, size_t count) {
    unsigned char* out = static_cast<unsigned char*>(dest);
    const unsigned char* in = static_cast<const unsigned char*>(source);
    if (out == in || count == 0) {
        return dest;
    }
    if (out < in) {
        return memcpy(dest, source, count);
    }
    for (size_t index = count; index > 0; --index) {
        out[index - 1] = in[index - 1];
    }
    return dest;
}

extern "C" int memcmp(const void* left, const void* right, size_t count) {
    const unsigned char* a = static_cast<const unsigned char*>(left);
    const unsigned char* b = static_cast<const unsigned char*>(right);
    for (size_t index = 0; index < count; ++index) {
        if (a[index] != b[index]) {
            return a[index] < b[index] ? -1 : 1;
        }
    }
    return 0;
}
