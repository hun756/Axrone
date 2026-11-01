import fs from "node:fs";

export const STANDARD_BOX_COUNT = 256;
export const STANDARD_STEPS = 240;
export const STANDARD_DT = 1 / 60;
export const STANDARD_MAX_BODIES = 512;
export const STANDARD_MAX_CONTACTS = 16384;

const REQUIRED_EXPORTS = [
    "worldInit",
    "bodyCreate",
    "bodySetBox",
    "bodySetMaterial",
    "step",
    "bodyCount",
    "contactCount",
    "worldFingerprint",
    "worldMemoryBytes"
];

export async function loadWasm(filePath) {
    const bytes = fs.readFileSync(filePath);
    const module = await WebAssembly.compile(bytes);
    const imports = WebAssembly.Module.imports(module);
    if (imports.length !== 0) {
        throw new Error(`${filePath}: expected zero imports, found ${imports.map((entry) => entry.name).join(", ")}`);
    }
    const instance = await WebAssembly.instantiate(module, {});
    const exports = instance.exports;
    for (const name of REQUIRED_EXPORTS) {
        if (!(name in exports)) {
            throw new Error(`${filePath}: missing export ${name}`);
        }
    }
    return { exports, byteLength: bytes.byteLength, imports };
}

export function addStandardScene(exports) {
    if (exports.worldInit(STANDARD_MAX_BODIES, STANDARD_MAX_CONTACTS, 0, -9.81, 0) !== 0) {
        throw new Error("worldInit failed");
    }

    const ground = exports.bodyCreate(0, 0, -0.5, 0, 0, 0, 0, 1);
    if (ground < 0) {
        throw new Error("ground create failed");
    }
    if (exports.bodySetBox(ground, 20, 0.5, 20, 0) !== 0) {
        throw new Error("ground shape failed");
    }
    exports.bodySetMaterial(ground, 0.8, 0);

    for (let i = 0; i < STANDARD_BOX_COUNT; i += 1) {
        const layer = Math.floor(i / 64);
        const within = i % 64;
        const xIndex = within % 8;
        const zIndex = Math.floor(within / 8);
        const x = xIndex - 3;
        const y = 1 + layer;
        const z = zIndex - 3;

        const id = exports.bodyCreate(1, x, y, z, 0, 0, 0, 1);
        if (id < 0) {
            throw new Error(`box ${i} create failed`);
        }
        if (exports.bodySetBox(id, 0.4, 0.4, 0.4, 1) !== 0) {
            throw new Error(`box ${i} shape failed`);
        }
        exports.bodySetMaterial(id, 0.7, 0);
    }
}

export function runStandardScene(exports) {
    for (let i = 0; i < STANDARD_STEPS; i += 1) {
        exports.step(STANDARD_DT, 8, 3);
    }
}

export function fingerprintHex(exports) {
    return BigInt.asUintN(64, exports.worldFingerprint()).toString(16).padStart(16, "0");
}

export function memoryMegabytes(exports) {
    return exports.worldMemoryBytes() / (1024 * 1024);
}
