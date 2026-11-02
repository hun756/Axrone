import { WASM_BASE64, WASM_BYTE_LENGTH } from "./generated/wasm";

export interface WasmPhysicsExports {
    readonly memory: WebAssembly.Memory;
    worldInit(maxBodies: number, maxContacts: number, gx: number, gy: number, gz: number): number;
    world_init(maxBodies: number, maxContacts: number, gx: number, gy: number, gz: number): number;
    lib_init(maxBodies: number, maxContacts: number, gx: number, gy: number, gz: number): number;
    worldReset(): void;
    world_reset(): void;
    lib_reset(): void;
    worldSetGravity(gx: number, gy: number, gz: number): number;
    world_set_gravity(gx: number, gy: number, gz: number): number;
    bodyCreate(type: number, px: number, py: number, pz: number, qx: number, qy: number, qz: number, qw: number): number;
    body_create(type: number, px: number, py: number, pz: number, qx: number, qy: number, qz: number, qw: number): number;
    bodyDestroy(id: number): number;
    body_destroy(id: number): number;
    bodySetSphere(id: number, radius: number, density: number): number;
    body_set_sphere(id: number, radius: number, density: number): number;
    bodyAddSphere(id: number, radius: number, density: number, px: number, py: number, pz: number): number;
    body_add_sphere(id: number, radius: number, density: number, px: number, py: number, pz: number): number;
    bodySetBox(id: number, hx: number, hy: number, hz: number, density: number): number;
    body_set_box(id: number, hx: number, hy: number, hz: number, density: number): number;
    bodySetTransform(id: number, px: number, py: number, pz: number, qx: number, qy: number, qz: number, qw: number): number;
    body_set_transform(id: number, px: number, py: number, pz: number, qx: number, qy: number, qz: number, qw: number): number;
    bodySetVelocity(id: number, vx: number, vy: number, vz: number): number;
    body_set_velocity(id: number, vx: number, vy: number, vz: number): number;
    bodySetAngularVelocity(id: number, vx: number, vy: number, vz: number): number;
    body_set_angular_velocity(id: number, vx: number, vy: number, vz: number): number;
    bodySetMaterial(id: number, friction: number, restitution: number): number;
    body_set_material(id: number, friction: number, restitution: number): number;
    step(dt: number, velocityIterations: number, positionIterations: number): number;
    bodyCount(): number;
    body_count(): number;
    contactCount(): number;
    contact_count(): number;
    transformsPtr(): number;
    transforms_ptr(): number;
    velocitiesPtr(): number;
    velocities_ptr(): number;
    angularVelocitiesPtr(): number;
    angular_velocities_ptr(): number;
    contactsPtr(): number;
    contacts_ptr(): number;
    worldFingerprint(): bigint;
    world_fingerprint(): bigint;
    worldMemoryBytes(): number;
    world_memory_bytes(): number;
    get_transforms_view(): bigint;
    get_velocities_view(): bigint;
    get_angular_velocities_view(): bigint;
    get_contacts_view(): bigint;
    getTransformsView(): bigint;
    getVelocitiesView(): bigint;
    getAngularVelocitiesView(): bigint;
    getContactsView(): bigint;
    raycastAll(
        ox: number,
        oy: number,
        oz: number,
        dx: number,
        dy: number,
        dz: number,
        maxDistance: number,
        hits: number,
        capacity: number
    ): number;
    raycastClosest(
        ox: number,
        oy: number,
        oz: number,
        dx: number,
        dy: number,
        dz: number,
        maxDistance: number,
        hit: number
    ): number;
    overlapSphere(cx: number, cy: number, cz: number, radius: number, ids: number, capacity: number): number;
    queryScratchPtr(): number;
    queryScratchCapacity(): number;
}

const REQUIRED_EXPORTS = [
    "memory",
    "worldInit",
    "worldReset",
    "worldSetGravity",
    "bodyCreate",
    "bodyDestroy",
    "bodySetSphere",
    "bodyAddSphere",
    "bodySetBox",
    "bodySetTransform",
    "bodySetVelocity",
    "bodySetAngularVelocity",
    "bodySetMaterial",
    "step",
    "bodyCount",
    "contactCount",
    "transformsPtr",
    "velocitiesPtr",
    "angularVelocitiesPtr",
    "contactsPtr",
    "worldFingerprint",
    "worldMemoryBytes",
    "get_transforms_view",
    "get_velocities_view",
    "get_angular_velocities_view",
    "get_contacts_view",
    "raycastAll",
    "raycastClosest",
    "overlapSphere",
    "queryScratchPtr",
    "queryScratchCapacity"
] as const;

export function decodeBase64(base64: string): Uint8Array {
    const binary = globalThis.atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; index += 1) {
        bytes[index] = binary.charCodeAt(index);
    }
    return bytes;
}

async function instantiateModule(module: WebAssembly.Module): Promise<WasmPhysicsExports> {
    const imports = WebAssembly.Module.imports(module);
    if (imports.length !== 0) {
        throw new Error(`physics wasm must have zero imports, found ${imports.map((entry) => entry.name).join(", ")}`);
    }

    const instance = await WebAssembly.instantiate(module, {});
    const exports = instance.exports as unknown as Record<string, unknown>;
    for (const name of REQUIRED_EXPORTS) {
        if (!(name in exports)) {
            throw new Error(`physics wasm is missing export ${name}`);
        }
    }

    return exports as unknown as WasmPhysicsExports;
}

export async function instantiateWasm(bytes: Uint8Array): Promise<WasmPhysicsExports> {
    const module = await WebAssembly.compile(bytes);
    return instantiateModule(module);
}

let embeddedModulePromise: Promise<WebAssembly.Module> | null = null;

export async function loadEmbeddedWasm(): Promise<WasmPhysicsExports> {
    if (embeddedModulePromise === null) {
        const bytes = decodeBase64(WASM_BASE64);
        if (bytes.byteLength !== WASM_BYTE_LENGTH) {
            throw new Error(`physics wasm length mismatch: ${bytes.byteLength} !== ${WASM_BYTE_LENGTH}`);
        }
        embeddedModulePromise = WebAssembly.compile(bytes);
    }

    return instantiateModule(await embeddedModulePromise);
}
