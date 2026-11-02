export { WasmPhysicsWorld3D } from "./world";
export { WasmPhysicsWorld3DCompat } from "./compat";
export { WASM_PHYSICS_CAPABILITIES } from "./capabilities";
export { BODY_FLAGS } from "./types";
export type { WasmPhysicsCapabilities } from "./capabilities";
export { decodeBase64, instantiateWasm, loadEmbeddedWasm } from "./loader";
export type {
    CompatBodyDef,
    CompatBodyHandle,
    CompatCollisionEvent,
    CompatCollisionEventType,
    CompatContactListener,
    CompatContactManifold,
    CompatContactPoint,
    CompatMaterial,
    CompatQuat,
    CompatSensorEvent,
    CompatSensorEventType,
    CompatShapeDef,
    CompatStatistics,
    CompatVec3,
    CompatWorldConfig
} from "./compat";
export type { WasmPhysicsExports } from "./loader";
export type {
    BodyOptions,
    BodyType,
    BoxShape,
    CapsuleAttachOptions,
    CapsuleShape,
    ContactEventKind,
    ContactEventSnapshot,
    ContactSnapshot,
    CylinderAttachOptions,
    CylinderShape,
    MutableQuat,
    MutableVec3,
    QuatTuple,
    RaycastHit,
    Shape,
    ShapeFilter,
    ShapeMaterial,
    SphereShape,
    StepOptions,
    TransformSnapshot,
    Vec3Tuple,
    WorldOptions
} from "./types";
