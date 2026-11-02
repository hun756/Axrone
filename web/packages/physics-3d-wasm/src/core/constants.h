#pragma once

#include <stdint.h>

namespace axrone::physics {

constexpr float kDefaultFriction = 0.6f;
constexpr float kPositionSlop = 0.005f;
constexpr float kPositionPercent = 0.2f;
constexpr float kMaxPositionCorrection = 0.2f;
constexpr float kFaceEdgeBias = 0.001f;
constexpr float kQuatEps = 1e-12f;
constexpr float kEps = 1e-9f;
constexpr float kCrossEps = 1e-8f;
constexpr float kTangentThreshold = 0.57735026f;
constexpr float kSphereVolume = 4.1887902047863905f;
constexpr float kSphereInertia = 0.4f;
constexpr float kBoxVolume = 8.0f;
constexpr float kMaxDt = 0.1f;
constexpr int32_t kMaxIterations = 64;
constexpr int32_t kMaxBodies = 262144;
constexpr int32_t kMaxContacts = 262144;
constexpr int32_t kMaxShapesPerBody = 8;
constexpr float kBig = 1e30f;
constexpr float kRestitutionThreshold = 1.0f;

constexpr int32_t kKindSphereSphere = 1;
constexpr int32_t kKindSphereBox = 2;
constexpr int32_t kKindBoxFace = 3;
constexpr int32_t kKindBoxEdge = 4;

constexpr int32_t kBodyTypeStatic = 0;
constexpr int32_t kBodyTypeDynamic = 1;
constexpr int32_t kBodyTypeKinematic = 2;

constexpr int32_t kBodyFreezeRotationX = 1;
constexpr int32_t kBodyFreezeRotationY = 2;
constexpr int32_t kBodyFreezeRotationZ = 4;
constexpr int32_t kBodyFreezePositionX = 8;
constexpr int32_t kBodyFreezePositionY = 16;
constexpr int32_t kBodyFreezePositionZ = 32;

constexpr float kSleepTimeLimit = 0.5f;
constexpr float kSleepLinearThresholdSq = 1e-4f;
constexpr float kSleepAngularThresholdSq = 1.218e-3f;
constexpr float kSleepWakeSpeedSq = 1e-4f;

}
