export { WasmPhysicsWorld3D } from "./world";
export { WasmPhysicsWorld3DCompat } from "./compat";
export { WASM_PHYSICS_CAPABILITIES } from "./capabilities";
export { BODY_FLAGS } from "./types";
export type { WasmPhysicsCapabilities } from "./capabilities";
export { decodeBase64, instantiateWasm, loadEmbeddedWasm } from "./loader";
export type {
    CompatBodyDef,
    CompatBodyHandle,
    CompatMaterial,
    CompatQuat,
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
    ContactEventSnapshot,
    ContactSnapshot,
    MutableQuat,
    MutableVec3,
    QuatTuple,
    RaycastHit,
    Shape,
    SphereShape,
    StepOptions,
    TransformSnapshot,
    Vec3Tuple,
    WorldOptions
} from "./types";
