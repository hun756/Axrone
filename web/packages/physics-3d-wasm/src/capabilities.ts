export interface WasmPhysicsCapabilities {
    readonly backend: "wasm";
    readonly bodyTypes: {
        readonly static: boolean;
        readonly dynamic: boolean;
        readonly kinematic: boolean;
    };
    readonly shapes: {
        readonly sphere: boolean;
        readonly box: boolean;
        readonly capsule: boolean;
        readonly cylinder: boolean;
        readonly cone: boolean;
        readonly convexHull: boolean;
        readonly triangleMesh: boolean;
        readonly heightField: boolean;
    };
    readonly multiShape: boolean;
    readonly shapeOffsets: boolean;
    readonly materials: boolean;
    readonly restitution: boolean;
    readonly sleeping: boolean;
    readonly contactEvents: boolean;
    readonly constraints: boolean;
    readonly queries: boolean;
    readonly characterController: boolean;
}

export const WASM_PHYSICS_CAPABILITIES: WasmPhysicsCapabilities = Object.freeze({
    backend: "wasm",
    bodyTypes: Object.freeze({
        static: true,
        dynamic: true,
        kinematic: false
    }),
    shapes: Object.freeze({
        sphere: true,
        box: true,
        capsule: false,
        cylinder: false,
        cone: false,
        convexHull: false,
        triangleMesh: false,
        heightField: false
    }),
    multiShape: false,
    shapeOffsets: false,
    materials: true,
    restitution: true,
    sleeping: false,
    contactEvents: false,
    constraints: false,
    queries: false,
    characterController: false
});
