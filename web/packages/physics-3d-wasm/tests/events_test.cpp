#include <stdint.h>
#include <stdio.h>
#include <string.h>

#include "core/events.h"

using axrone::physics::collectContactEvents;
using axrone::physics::Contact;
using axrone::physics::contactPairKey;
using axrone::physics::ContactEvent;
using axrone::physics::ContactEventState;
using axrone::physics::kContactEventBegin;
using axrone::physics::kContactEventEnd;
using axrone::physics::kContactEventStay;
using axrone::physics::Vec3;
using axrone::physics::World;

namespace {

int gChecks = 0;
int gFailures = 0;

#define CHECK(cond)                                                              \
    do {                                                                         \
        ++gChecks;                                                               \
        if (!(cond)) {                                                           \
            ++gFailures;                                                         \
            printf("FAIL %s:%d: %s\n", __FILE__, __LINE__, #cond);              \
        }                                                                        \
    } while (0)

constexpr int kMaxContacts = 1024;
constexpr int kMaxEvents = 2048;
constexpr int kMaxKeys = 1024;

World gWorld;
Contact gContacts[kMaxContacts];
ContactEvent gEvents[kMaxEvents];
uint64_t gKeyStore[kMaxKeys];
uint64_t gPrevKeys[kMaxKeys];

ContactEventState newState() {
    ContactEventState state;
    state.keys = gKeyStore;
    state.capacity = kMaxKeys;
    state.count = 0;
    return state;
}

void resetWorld() {
    memset(&gWorld, 0, sizeof(gWorld));
    memset(gContacts, 0, sizeof(gContacts));
    gWorld.contacts = gContacts;
    gWorld.contactCount = 0;
}

void addContact(int32_t a, int32_t b, float nx, float ny, float nz, float pen) {
    Contact& c = gContacts[gWorld.contactCount++];
    c.a = a;
    c.b = b;
    c.normal = Vec3(nx, ny, nz);
    c.pen = pen;
}

bool near(float left, float right) {
    float delta = left - right;
    if (delta < 0.0f) {
        delta = -delta;
    }
    return delta < 1e-6f;
}

bool samePair(const ContactEvent& event, int32_t a, int32_t b) {
    const int32_t low = a < b ? a : b;
    const int32_t high = a < b ? b : a;
    return event.a == low && event.b == high;
}

void testKeyContract() {
    static_assert(contactPairKey(3, 7) == contactPairKey(7, 3), "key must be symmetric");
    static_assert(contactPairKey(0, 1) == 1, "key formula");
    static_assert(contactPairKey(1, 0) == 1, "key formula symmetric");
    static_assert(contactPairKey(3, 7) == ((uint64_t(3) << 32) | uint64_t(7)), "key formula ordered");
    CHECK(contactPairKey(3, 7) == contactPairKey(7, 3));
    CHECK(contactPairKey(5, 5) == ((uint64_t(5) << 32) | uint64_t(5)));
}

void testBeginFromEmpty() {
    resetWorld();
    addContact(2, 5, 0.0f, 1.0f, 0.0f, 0.25f);
    ContactEventState state = newState();
    const int32_t count = collectContactEvents(gWorld, state, gEvents, kMaxEvents);
    CHECK(count == 1);
    CHECK(gEvents[0].kind == kContactEventBegin);
    CHECK(samePair(gEvents[0], 2, 5));
    CHECK(near(gEvents[0].nx, 0.0f));
    CHECK(near(gEvents[0].ny, 1.0f));
    CHECK(near(gEvents[0].nz, 0.0f));
    CHECK(near(gEvents[0].penetration, 0.25f));
    CHECK(state.count == 1);
    CHECK(state.keys[0] == contactPairKey(2, 5));
}

void testStay() {
    resetWorld();
    addContact(2, 5, 0.0f, 1.0f, 0.0f, 0.25f);
    ContactEventState state = newState();
    CHECK(collectContactEvents(gWorld, state, gEvents, kMaxEvents) == 1);

    resetWorld();
    addContact(2, 5, 0.1f, 0.2f, 0.9f, 0.4f);
    CHECK(collectContactEvents(gWorld, state, gEvents, kMaxEvents) == 1);
    CHECK(gEvents[0].kind == kContactEventStay);
    CHECK(samePair(gEvents[0], 2, 5));
    CHECK(near(gEvents[0].nx, 0.1f));
    CHECK(near(gEvents[0].penetration, 0.4f));
    CHECK(state.count == 1);
}

void testEnd() {
    resetWorld();
    addContact(2, 5, 0.0f, 1.0f, 0.0f, 0.25f);
    ContactEventState state = newState();
    CHECK(collectContactEvents(gWorld, state, gEvents, kMaxEvents) == 1);

    gWorld.contactCount = 0;
    const int32_t count = collectContactEvents(gWorld, state, gEvents, kMaxEvents);
    CHECK(count == 1);
    CHECK(gEvents[0].kind == kContactEventEnd);
    CHECK(samePair(gEvents[0], 2, 5));
    CHECK(near(gEvents[0].nx, 0.0f));
    CHECK(near(gEvents[0].ny, 0.0f));
    CHECK(near(gEvents[0].nz, 0.0f));
    CHECK(near(gEvents[0].penetration, 0.0f));
    CHECK(state.count == 0);
}

void testReverseInsertionOrderAscends() {
    resetWorld();
    addContact(10, 20, 1.0f, 0.0f, 0.0f, 0.5f);
    addContact(1, 3, 0.0f, 1.0f, 0.0f, 0.7f);
    ContactEventState state = newState();
    const int32_t count = collectContactEvents(gWorld, state, gEvents, kMaxEvents);
    CHECK(count == 2);
    CHECK(gEvents[0].kind == kContactEventBegin);
    CHECK(samePair(gEvents[0], 1, 3));
    CHECK(gEvents[1].kind == kContactEventBegin);
    CHECK(samePair(gEvents[1], 10, 20));
    CHECK(contactPairKey(gEvents[0].a, gEvents[0].b) < contactPairKey(gEvents[1].a, gEvents[1].b));
    CHECK(state.count == 2);
    CHECK(state.keys[0] == contactPairKey(1, 3));
    CHECK(state.keys[1] == contactPairKey(10, 20));
}

void testMultiContactPerPairCollapses() {
    resetWorld();
    addContact(4, 9, 1.0f, 2.0f, 3.0f, 0.11f);
    addContact(4, 9, 4.0f, 5.0f, 6.0f, 0.99f);
    addContact(4, 9, 7.0f, 8.0f, 9.0f, 0.55f);
    ContactEventState state = newState();
    const int32_t count = collectContactEvents(gWorld, state, gEvents, kMaxEvents);
    CHECK(count == 1);
    CHECK(gEvents[0].kind == kContactEventBegin);
    CHECK(samePair(gEvents[0], 4, 9));
    CHECK(near(gEvents[0].nx, 1.0f));
    CHECK(near(gEvents[0].ny, 2.0f));
    CHECK(near(gEvents[0].nz, 3.0f));
    CHECK(near(gEvents[0].penetration, 0.11f));
    CHECK(state.count == 1);
}

void testOrderedGroups() {
    resetWorld();
    addContact(1, 2, 0.0f, 1.0f, 0.0f, 0.1f);
    addContact(3, 4, 0.0f, 1.0f, 0.0f, 0.2f);
    ContactEventState state = newState();
    CHECK(collectContactEvents(gWorld, state, gEvents, kMaxEvents) == 2);

    resetWorld();
    addContact(3, 4, 0.5f, 0.0f, 0.0f, 0.3f);
    addContact(5, 6, 0.5f, 0.0f, 0.0f, 0.4f);
    addContact(7, 8, 0.5f, 0.0f, 0.0f, 0.6f);
    const int32_t count = collectContactEvents(gWorld, state, gEvents, kMaxEvents);
    CHECK(count == 4);
    CHECK(gEvents[0].kind == kContactEventBegin);
    CHECK(gEvents[1].kind == kContactEventBegin);
    CHECK(gEvents[2].kind == kContactEventStay);
    CHECK(gEvents[3].kind == kContactEventEnd);
    CHECK(samePair(gEvents[0], 5, 6));
    CHECK(samePair(gEvents[1], 7, 8));
    CHECK(contactPairKey(gEvents[0].a, gEvents[0].b) < contactPairKey(gEvents[1].a, gEvents[1].b));
    CHECK(samePair(gEvents[2], 3, 4));
    CHECK(near(gEvents[2].penetration, 0.3f));
    CHECK(samePair(gEvents[3], 1, 2));

    resetWorld();
    addContact(5, 6, 0.5f, 0.0f, 0.0f, 0.4f);
    const int32_t stayCount = collectContactEvents(gWorld, state, gEvents, kMaxEvents);
    CHECK(stayCount == 3);
    CHECK(gEvents[0].kind == kContactEventStay);
    CHECK(gEvents[1].kind == kContactEventEnd);
    CHECK(gEvents[2].kind == kContactEventEnd);
    CHECK(samePair(gEvents[0], 5, 6));
    CHECK(samePair(gEvents[1], 3, 4));
    CHECK(samePair(gEvents[2], 7, 8));
    CHECK(contactPairKey(gEvents[1].a, gEvents[1].b) < contactPairKey(gEvents[2].a, gEvents[2].b));
}

void testThreeFramesStateUpdate() {
    resetWorld();
    ContactEventState state = newState();

    addContact(1, 4, 1.0f, 0.0f, 0.0f, 0.5f);
    addContact(2, 7, 1.0f, 0.0f, 0.0f, 0.5f);
    CHECK(collectContactEvents(gWorld, state, gEvents, kMaxEvents) == 2);
    CHECK(state.count == 2);
    CHECK(state.keys[0] == contactPairKey(1, 4));
    CHECK(state.keys[1] == contactPairKey(2, 7));

    resetWorld();
    addContact(2, 7, 1.0f, 0.0f, 0.0f, 0.5f);
    addContact(9, 3, 1.0f, 0.0f, 0.0f, 0.5f);
    CHECK(collectContactEvents(gWorld, state, gEvents, kMaxEvents) == 3);
    CHECK(state.count == 2);
    CHECK(state.keys[0] == contactPairKey(2, 7));
    CHECK(state.keys[1] == contactPairKey(3, 9));

    resetWorld();
    CHECK(collectContactEvents(gWorld, state, gEvents, kMaxEvents) == 2);
    CHECK(state.count == 0);
}

void testCapacityOneShortRejected() {
    resetWorld();
    addContact(1, 2, 0.0f, 1.0f, 0.0f, 0.1f);
    addContact(3, 4, 0.0f, 1.0f, 0.0f, 0.2f);
    ContactEventState state = newState();
    CHECK(collectContactEvents(gWorld, state, gEvents, kMaxEvents) == 2);

    resetWorld();
    addContact(5, 6, 0.0f, 1.0f, 0.0f, 0.1f);
    memcpy(gPrevKeys, state.keys, sizeof(uint64_t) * state.count);
    const int32_t savedCount = state.count;
    const int32_t result = collectContactEvents(gWorld, state, gEvents, savedCount);
    CHECK(result == -1);
    CHECK(state.count == savedCount);
    CHECK(memcmp(gPrevKeys, state.keys, sizeof(uint64_t) * savedCount) == 0);

    const int32_t exact = collectContactEvents(gWorld, state, gEvents, savedCount + 1);
    CHECK(exact == 3);
    CHECK(gEvents[0].kind == kContactEventBegin);
    CHECK(gEvents[1].kind == kContactEventEnd);
    CHECK(gEvents[2].kind == kContactEventEnd);
    CHECK(samePair(gEvents[0], 5, 6));
    CHECK(samePair(gEvents[1], 1, 2));
    CHECK(samePair(gEvents[2], 3, 4));
}

void testChunkedFrameOverStackLimit() {
    resetWorld();
    for (int32_t i = 599; i >= 0; --i) {
        addContact(1000 + i, 5000 + i, 0.0f, float(i), 1.0f, float(i) * 0.01f);
    }
    ContactEventState state = newState();
    const int32_t count = collectContactEvents(gWorld, state, gEvents, kMaxEvents);
    CHECK(count == 600);
    CHECK(state.count == 600);
    bool ascending = true;
    for (int32_t i = 1; i < 600; ++i) {
        if (gEvents[i - 1].kind != kContactEventBegin || gEvents[i].kind != kContactEventBegin) {
            ascending = false;
        }
        if (contactPairKey(gEvents[i - 1].a, gEvents[i - 1].b) >=
            contactPairKey(gEvents[i].a, gEvents[i].b)) {
            ascending = false;
        }
    }
    CHECK(ascending);
    CHECK(gEvents[0].a == 1000);
    CHECK(gEvents[0].b == 5000);
    CHECK(near(gEvents[0].ny, 0.0f));
    CHECK(gEvents[599].a == 1599);
    CHECK(gEvents[599].b == 5599);
    CHECK(state.keys[0] == contactPairKey(1000, 5000));
    CHECK(state.keys[599] == contactPairKey(1599, 5599));

    resetWorld();
    const int32_t endCount = collectContactEvents(gWorld, state, gEvents, kMaxEvents);
    CHECK(endCount == 600);
    CHECK(gEvents[0].kind == kContactEventEnd);
    CHECK(gEvents[0].a == 1000);
    CHECK(state.count == 0);
}

void testDeterministicRepeat() {
    resetWorld();
    addContact(7, 3, 1.0f, 0.0f, 0.0f, 0.3f);
    addContact(8, 2, 0.0f, 1.0f, 0.0f, 0.4f);
    addContact(7, 3, 0.0f, 0.0f, 1.0f, 0.9f);
    ContactEventState first = newState();
    first.keys = gPrevKeys;
    const int32_t firstCount = collectContactEvents(gWorld, first, gEvents, 64);
    CHECK(firstCount == 2);
    if (firstCount < 0) {
        return;
    }
    ContactEvent copy[64];
    memcpy(copy, gEvents, sizeof(ContactEvent) * firstCount);
    CHECK(gEvents[0].kind == kContactEventBegin);
    CHECK(samePair(gEvents[0], 2, 8));
    CHECK(near(gEvents[0].nx, 0.0f));
    CHECK(near(gEvents[0].ny, 1.0f));
    CHECK(near(gEvents[0].penetration, 0.4f));
    CHECK(gEvents[1].kind == kContactEventBegin);
    CHECK(samePair(gEvents[1], 3, 7));
    CHECK(near(gEvents[1].nx, 1.0f));
    CHECK(near(gEvents[1].ny, 0.0f));
    CHECK(near(gEvents[1].nz, 0.0f));
    CHECK(near(gEvents[1].penetration, 0.3f));

    ContactEventState second = newState();
    const int32_t secondCount = collectContactEvents(gWorld, second, gEvents, 64);
    CHECK(firstCount == secondCount);
    CHECK(first.count == second.count);
    bool same = true;
    for (int32_t i = 0; i < firstCount; ++i) {
        if (memcmp(&copy[i], &gEvents[i], sizeof(ContactEvent)) != 0) {
            same = false;
        }
    }
    for (int32_t i = 0; i < first.count; ++i) {
        if (first.keys[i] != second.keys[i]) {
            same = false;
        }
    }
    CHECK(same);
}

void testInvalidInputsRejected() {
    resetWorld();
    addContact(1, 2, 0.0f, 1.0f, 0.0f, 0.1f);

    ContactEventState state = newState();
    CHECK(collectContactEvents(gWorld, state, nullptr, 16) == -1);
    CHECK(collectContactEvents(gWorld, state, gEvents, -1) == -1);

    ContactEventState nullKeys = newState();
    nullKeys.keys = nullptr;
    CHECK(collectContactEvents(gWorld, nullKeys, gEvents, 16) == -1);

    ContactEventState zeroCapacity = newState();
    zeroCapacity.capacity = 0;
    CHECK(collectContactEvents(gWorld, zeroCapacity, gEvents, 16) == -1);

    ContactEventState negativeCount = newState();
    negativeCount.count = -1;
    CHECK(collectContactEvents(gWorld, negativeCount, gEvents, 16) == -1);

    ContactEventState overCount = newState();
    overCount.count = overCount.capacity + 1;
    CHECK(collectContactEvents(gWorld, overCount, gEvents, 16) == -1);

    const int32_t savedContacts = gWorld.contactCount;
    gWorld.contactCount = -1;
    CHECK(collectContactEvents(gWorld, state, gEvents, 16) == -1);
    gWorld.contactCount = savedContacts;

    CHECK(state.count == 0);
}

void testEmptyFrameEmptyState() {
    resetWorld();
    ContactEventState state = newState();
    CHECK(collectContactEvents(gWorld, state, gEvents, 0) == 0);
    CHECK(collectContactEvents(gWorld, state, gEvents, kMaxEvents) == 0);
    CHECK(state.count == 0);
}

}

int main() {
    testKeyContract();
    testBeginFromEmpty();
    testStay();
    testEnd();
    testReverseInsertionOrderAscends();
    testMultiContactPerPairCollapses();
    testOrderedGroups();
    testThreeFramesStateUpdate();
    testCapacityOneShortRejected();
    testChunkedFrameOverStackLimit();
    testDeterministicRepeat();
    testInvalidInputsRejected();
    testEmptyFrameEmptyState();

    printf("events_test: %d checks, %d failures\n", gChecks, gFailures);
    return gFailures == 0 ? 0 : 1;
}
