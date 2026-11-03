import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { loadWasm } from "../scripts/scene.mjs";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const counts = [250, 1000, 2000];
const recordedTs = new Map([
    [250, 1.83],
    [1000, 8.82],
    [2000, 23.29]
]);
const warmupSteps = 60;
const measuredSteps = 180;
const repeats = 3;
const velocityIterations = 8;
const positionIterations = 3;
const dt = 1 / 60;

const wasmPath = path.resolve(root, process.argv[2] ?? "build/wasm-release/physics.opt.wasm");
if (!fs.existsSync(wasmPath)) {
    throw new Error(`missing wasm: ${wasmPath}`);
}

const { exports, byteLength } = await loadWasm(wasmPath);

function median(values) {
    const sorted = [...values].sort((left, right) => left - right);
    const middle = Math.floor(sorted.length / 2);
    return sorted.length % 2 === 0
        ? (sorted[middle - 1] + sorted[middle]) * 0.5
        : sorted[middle];
}

function measure(count) {
    const maxContacts = Math.max(1024, count * 8 + 64);
    if (exports.worldInit(count + 1, maxContacts, 0, -9.81, 0) !== 0) {
        throw new Error("worldInit failed");
    }

    const ground = exports.bodyCreate(0, 0, -0.5, 0, 0, 0, 0, 1);
    exports.bodySetBox(ground, 120, 0.5, 120, 0);
    exports.bodySetMaterial(ground, 0.6, 0);

    const side = Math.ceil(Math.sqrt(count));
    for (let index = 0; index < count; index += 1) {
        const x = ((index % side) - (side - 1) / 2) * 1.05;
        const z = (Math.floor(index / side) - (side - 1) / 2) * 1.05;
        const id = exports.bodyCreate(1, x, 0.5, z, 0, 0, 0, 1);
        if (id < 0) {
            throw new Error("body create failed");
        }
        exports.bodySetBox(id, 0.5, 0.5, 0.5, 1);
        exports.bodySetMaterial(id, 0.6, 0);
    }

    for (let step = 0; step < warmupSteps; step += 1) {
        exports.step(dt, velocityIterations, positionIterations);
    }

    const start = performance.now();
    for (let step = 0; step < measuredSteps; step += 1) {
        exports.step(dt, velocityIterations, positionIterations);
    }
    const elapsed = (performance.now() - start) / measuredSteps;
    return {
        ms: elapsed,
        contacts: exports.contactCount(),
        memoryMb: exports.worldMemoryBytes() / (1024 * 1024)
    };
}

const results = [];
for (const count of counts) {
    const samples = [];
    let last = null;
    for (let repeat = 0; repeat < repeats; repeat += 1) {
        last = measure(count);
        samples.push(last.ms);
    }
    const ms = median(samples);
    const ts = recordedTs.get(count);
    results.push({
        count,
        wasmMs: ms,
        recordedTsMs: ts,
        speedup: ts / ms,
        contacts: last.contacts,
        memoryMb: last.memoryMb
    });
}

const header = "bodies".padStart(8) + "wasm ms".padStart(12) + "ts ms".padStart(10) + "speedup".padStart(10) + "contacts".padStart(10);
console.log(header);
for (const result of results) {
    console.log(
        String(result.count).padStart(8) +
        result.wasmMs.toFixed(3).padStart(12) +
        result.recordedTsMs.toFixed(2).padStart(10) +
        `${result.speedup.toFixed(2)}x`.padStart(10) +
        String(result.contacts).padStart(10)
    );
}

const outputDir = path.join(root, "bench/results");
fs.mkdirSync(outputDir, { recursive: true });
const stamp = new Date().toISOString().replace(/[:.]/g, "-");
const outputPath = path.join(outputDir, `compare-recorded-ts-${stamp}.json`);
fs.writeFileSync(outputPath, JSON.stringify({
    environment: {
        date: new Date().toISOString(),
        node: process.version,
        platform: `${process.platform}-${process.arch}`,
        cpu: os.cpus()[0]?.model ?? "unknown",
        threads: os.cpus().length,
        warmupSteps,
        measuredSteps,
        repeats,
        velocityIterations,
        positionIterations,
        dt,
        wasm: path.relative(root, wasmPath),
        wasmBytes: byteLength,
        note: "TypeScript baseline is the recorded same-scene median from the discarded AS package BENCHMARKS.md (2026-11-01); it was not re-run in this session."
    },
    results
}, null, 2));

console.log(`\nresults=${path.relative(root, outputPath)}`);
