import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { fingerprintHex, loadWasm } from "../scripts/scene.mjs";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const counts = [250, 1000, 2000, 5000];
const warmupSteps = 40;
const measuredSteps = 120;
const repeats = (count) => (count <= 1000 ? 5 : 3);
const dt = 1 / 60;

const defaultVariants = [
    "build/wasm-release/physics.wasm",
    "build/wasm-simd/physics.wasm",
    "build/wasm-fast/physics.wasm",
    "build/wasm-fast-simd/physics.wasm"
];

const requested = process.argv.slice(2);
const variantPaths = (requested.length > 0 ? requested : defaultVariants)
    .map((entry) => path.resolve(root, entry))
    .filter((entry) => fs.existsSync(entry));

if (variantPaths.length === 0) {
    throw new Error("no wasm variants found; build a preset first");
}

function labelFor(filePath) {
    const parent = path.basename(path.dirname(filePath));
    const file = path.basename(filePath);
    return file.includes(".opt.") ? `${parent}-opt` : parent;
}

function createScene(exports, count) {
    const maxContacts = Math.max(1024, count * 24);
    if (exports.worldInit(count + 1, maxContacts, 0, -9.81, 0) !== 0) {
        throw new Error(`worldInit failed for ${count} bodies`);
    }

    const side = Math.ceil(Math.cbrt(count));
    const ground = exports.bodyCreate(0, 0, -0.5, 0, 0, 0, 0, 1);
    exports.bodySetBox(ground, 40, 0.5, 40, 0);
    exports.bodySetMaterial(ground, 0.8, 0);

    const offset = (side - 1) / 2;
    for (let i = 0; i < count; i += 1) {
        const xIndex = i % side;
        const zIndex = Math.floor(i / side) % side;
        const layer = Math.floor(i / (side * side));
        const x = xIndex - offset;
        const y = 1 + layer;
        const z = zIndex - offset;

        const id = exports.bodyCreate(1, x, y, z, 0, 0, 0, 1);
        if (id < 0) {
            throw new Error(`body ${i} creation failed`);
        }
        if (exports.bodySetBox(id, 0.4, 0.4, 0.4, 1) !== 0) {
            throw new Error(`body ${i} shape failed`);
        }
        exports.bodySetMaterial(id, 0.7, 0);
    }
}

function median(values) {
    const sorted = [...values].sort((left, right) => left - right);
    const middle = Math.floor(sorted.length / 2);
    return sorted.length % 2 === 0
        ? (sorted[middle - 1] + sorted[middle]) * 0.5
        : sorted[middle];
}

function measure(exports, count) {
    const samples = [];
    let contacts = 0;
    let fingerprint = "0000000000000000";
    let memory = 0;

    for (let repeat = 0; repeat < repeats(count); repeat += 1) {
        createScene(exports, count);

        for (let i = 0; i < warmupSteps; i += 1) {
            exports.step(dt, 8, 3);
        }

        const start = process.hrtime.bigint();
        for (let i = 0; i < measuredSteps; i += 1) {
            exports.step(dt, 8, 3);
        }
        const end = process.hrtime.bigint();

        samples.push(Number(end - start) / measuredSteps / 1e6);
        contacts = exports.contactCount();
        fingerprint = fingerprintHex(exports);
        memory = exports.worldMemoryBytes();
    }

    return {
        ms: median(samples),
        minMs: Math.min(...samples),
        maxMs: Math.max(...samples),
        contacts,
        fingerprint,
        memoryMb: memory / (1024 * 1024)
    };
}

const results = [];
for (const filePath of variantPaths) {
    const label = labelFor(filePath);
    const { exports, byteLength, imports } = await loadWasm(filePath);

    for (const count of counts) {
        const result = measure(exports, count);
        results.push({
            variant: label,
            file: path.relative(root, filePath),
            count,
            sizeBytes: byteLength,
            imports: imports.length,
            ...result
        });
    }
}

const environment = {
    date: new Date().toISOString(),
    node: process.version,
    platform: `${process.platform}-${process.arch}`,
    cpu: os.cpus()[0]?.model ?? "unknown",
    threads: os.cpus().length,
    warmupSteps,
    measuredSteps,
    repeats: Object.fromEntries(counts.map((count) => [count, repeats(count)])),
    dt
};

const outputDir = path.join(root, "bench/results");
fs.mkdirSync(outputDir, { recursive: true });
const stamp = new Date().toISOString().replace(/[:.]/g, "-");
const outputPath = path.join(outputDir, `bench-${stamp}.json`);
fs.writeFileSync(outputPath, JSON.stringify({ environment, results }, null, 2));

const header = "variant".padEnd(20) + "bodies".padStart(8) + "step ms".padStart(12) + "contacts".padStart(10) + "memory MB".padStart(12) + "size KB".padStart(10) + "fingerprint".padStart(20);
console.log(header);
for (const result of results) {
    console.log(
        result.variant.padEnd(20) +
        String(result.count).padStart(8) +
        result.ms.toFixed(3).padStart(12) +
        String(result.contacts).padStart(10) +
        result.memoryMb.toFixed(2).padStart(12) +
        (result.sizeBytes / 1024).toFixed(1).padStart(10) +
        result.fingerprint.padStart(20)
    );
}
console.log(`\nresults=${path.relative(root, outputPath)}`);
console.log(`environment=${environment.cpu} / ${environment.threads} threads / node ${environment.node}`);
