/**
 * Physics Allocation Regression Harness
 *
 * Measures heap allocation during physics simulation steps to detect
 * performance regressions. Uses --expose-gc for accurate measurement.
 *
 * Scenarios:
 *   - 2d-50body-step: 50 dynamic bodies, 500 steps
 *   - 2d-contact-heavy: 10-box stack (contact-heavy), 500 steps
 *   - 3d-50body-step: 50 dynamic bodies, 120 steps
 *   - 3d-sphere-stack: 10-sphere stack, 120 steps
 *
 * Why different step counts:
 *   2D scenarios allocate ~60-80 B/step. At 120 steps the total heap delta
 *   is ~7-10 KB, which is within GC timing noise. At 500 steps the delta
 *   is ~30-40 KB, giving a better signal-to-noise ratio.
 *   3D scenarios allocate ~200 B/step. At 120 steps the delta is ~24 KB
 *   with 0% spread across 5 independent process runs — already reliable.
 *   Changing 3D step count would needlessly shift the baseline.
 *
 * Measurement method:
 *   - 2D: 7 iterations, drop min/max, median of 5
 *   - 3D: 5 iterations, drop min/max, median of 3
 *   - No warm-up phase — warm-up stabilizes JIT but eliminates the allocation
 *     signal we're trying to measure (lazy init, first-pass IC stubs allocate)
 *   - Aggressive GC discipline: 3x gc() before + settle, 3x gc() after + settle
 *   - Negative samples (GC interference) are rejected and retried (max 3 retries)
 *   - Spread is computed from trimmed values (after dropping min/max), not raw
 *     min/max, because extreme values are GC artifacts, not real allocation variance
 *
 * Negative sample handling (option A — reject & retry):
 *   A negative B/step means heap shrank during measurement → GC collected more
 *   than was allocated between snapshots. This invalidates the sample. We retry
 *   up to MAX_NEG_RETRIES times with additional GC settling between retries.
 *   If all retries produce negative values, the iteration is excluded from median
 *   calculation. This is preferred over:
 *     (B) clamping to 0 — would bias median downward, masking real allocation
 *     (C) skip without retry — reduces effective sample count unnecessarily
 *
 * GC discipline:
 *   V8 has multiple collector generations (young/old/code/map). A single gc()
 *   call may not run all collectors. We call gc() 3 times before measurement
 *   to ensure all generations are settled, then read heapUsed. After the step
 *   loop we repeat 3x gc() before reading. This minimizes GC noise.
 *
 * Baseline:
 *   - Baseline stored at .tmp/benchmarks/physics-allocation-baseline.json (gitignored)
 *   - --update-baseline flag updates baseline after measurement
 *   - If no baseline exists, first run creates one automatically (PASS)
 *   - 10% regression threshold triggers FAIL
 *
 * Self-test mode:
 *   --selfTest=<bytes> adds a known allocation per step (accumulates Uint8Array
 *   buffers of <bytes> each step, kept alive via reference array). This proves
 *   the gate can detect regressions of known magnitude. Without --selfTest,
 *   no extra allocation occurs.
 */

import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const workspaceDir = path.resolve(scriptDir, '..');
const defaultBaselinePath = path.resolve(
    workspaceDir,
    '.tmp',
    'benchmarks',
    'physics-allocation-baseline.json',
);
const defaultReportPath = path.resolve(
    workspaceDir,
    '.tmp',
    'benchmarks',
    'physics-allocation-report.json',
);

// ── Per-scenario configuration ──────────────────────────────────────────────
const SCENARIO_CONFIG = {
    '2d-50body-step':   { steps: 500, iterations: 7 },
    '2d-contact-heavy': { steps: 500, iterations: 7 },
    '3d-50body-step':   { steps: 120,  iterations: 5 },
    '3d-sphere-stack':  { steps: 120,  iterations: 5 },
};

const REGRESSION_THRESHOLD_PCT = 10;
const MAX_NEG_RETRIES = 3;
const GC_CALLS = 3;

// ── CLI argument parsing ────────────────────────────────────────────────────
const { values: cli } = parseArgs({
    options: {
        baseline: { type: 'string' },
        report: { type: 'string' },
        'update-baseline': { type: 'boolean' },
        scenario: { type: 'string' },
        selfTest: { type: 'string' },
    },
    strict: true,
    allowPositionals: false,
});

const baselinePath = path.resolve(workspaceDir, cli.baseline ?? defaultBaselinePath);
const reportPath = path.resolve(workspaceDir, cli.report ?? defaultReportPath);
const scenarioFilter = cli.scenario ? cli.scenario.split(',').map((s) => s.trim()) : null;
const selfTestBytes = cli.selfTest ? parseInt(cli.selfTest, 10) : 0;

if (cli.selfTest && (isNaN(selfTestBytes) || selfTestBytes < 0)) {
    console.error(`Invalid --selfTest value: ${cli.selfTest} (must be a non-negative integer)`);
    process.exit(1);
}

// ── GC requirement check ────────────────────────────────────────────────────
if (typeof global.gc !== 'function') {
    console.log('Respawning with --expose-gc for accurate allocation measurement...');
    const args = ['--expose-gc', fileURLToPath(import.meta.url)];
    for (const [key, value] of Object.entries(cli)) {
        if (value === true) {
            args.push(`--${key}`);
        } else if (typeof value === 'string') {
            args.push(`--${key}=${value}`);
        }
    }
    const result = spawnSync(process.execPath, args, {
        cwd: workspaceDir,
        stdio: 'inherit',
    });
    process.exit(result.status ?? 1);
}

// ── GC discipline helpers ───────────────────────────────────────────────────

/**
 * Run multiple GC passes to settle all V8 collector generations.
 * V8 has young generation (scavenger), old generation (mark-sweep-compact),
 * code space, and map space. A single gc() may not collect all.
 */
function settleGC() {
    for (let i = 0; i < GC_CALLS; i++) {
        global.gc();
    }
}

// ── Scenario definitions ────────────────────────────────────────────────────

async function runScenario2D50Body() {
    const { PhysicsWorld2D } = await import('@axrone/physics-2d');
    const { BodyType } = await import('@axrone/physics-core');

    const world = new PhysicsWorld2D({ gravity: { x: 0, y: -9.81 } });

    for (let i = 0; i < 50; i++) {
        const bodyId = world.createBody({
            type: BodyType.Dynamic,
            position: { x: (i % 10) * 1.5 - 7, y: Math.floor(i / 10) * 1.5 + 5 },
        });
        world.createCircleShape(bodyId, { radius: 0.4 });
    }

    return {
        world,
        stepFn: () => world.step(1 / 60),
        steps: SCENARIO_CONFIG['2d-50body-step'].steps,
    };
}

async function runScenario2DContactHeavy() {
    const { PhysicsWorld2D } = await import('@axrone/physics-2d');
    const { BodyType } = await import('@axrone/physics-core');

    const world = new PhysicsWorld2D({ gravity: { x: 0, y: -9.81 } });

    const groundId = world.createBody({
        type: BodyType.Static,
        position: { x: 0, y: 0 },
    });
    world.createBoxShape(groundId, { halfWidth: 50, halfHeight: 1 });

    for (let i = 0; i < 10; i++) {
        const bodyId = world.createBody({
            type: BodyType.Dynamic,
            position: { x: 0, y: 1.5 + i * 1.1 },
        });
        world.createBoxShape(bodyId, { halfWidth: 0.5, halfHeight: 0.5 });
    }

    return {
        world,
        stepFn: () => world.step(1 / 60),
        steps: SCENARIO_CONFIG['2d-contact-heavy'].steps,
    };
}

async function runScenario3D50Body() {
    const { PhysicsWorld3D } = await import('@axrone/physics-3d');
    const { BodyType } = await import('@axrone/physics-core');

    const world = new PhysicsWorld3D({ gravity: { x: 0, y: -9.81, z: 0 } });

    for (let i = 0; i < 50; i++) {
        const bodyId = world.createBody({
            type: BodyType.Dynamic,
            position: { x: (i % 10) * 1.5 - 7, y: Math.floor(i / 10) * 1.5 + 5, z: 0 },
        });
        world.createSphereShape(bodyId, {
            center: { x: 0, y: 0, z: 0 },
            radius: 0.4,
        });
    }

    return {
        world,
        stepFn: () => world.step(1 / 60),
        steps: SCENARIO_CONFIG['3d-50body-step'].steps,
    };
}

async function runScenario3DSphereStack() {
    const { PhysicsWorld3D } = await import('@axrone/physics-3d');
    const { BodyType } = await import('@axrone/physics-core');

    const world = new PhysicsWorld3D({ gravity: { x: 0, y: -9.81, z: 0 } });

    for (let i = 0; i < 10; i++) {
        const bodyId = world.createBody({
            type: BodyType.Dynamic,
            position: { x: 0, y: 1 + i * 1.1, z: 0 },
        });
        world.createSphereShape(bodyId, {
            center: { x: 0, y: 0, z: 0 },
            radius: 0.5,
        });
    }

    return {
        world,
        stepFn: () => world.step(1 / 60),
        steps: SCENARIO_CONFIG['3d-sphere-stack'].steps,
    };
}

const scenarios = {
    '2d-50body-step': runScenario2D50Body,
    '2d-contact-heavy': runScenario2DContactHeavy,
    '3d-50body-step': runScenario3D50Body,
    '3d-sphere-stack': runScenario3DSphereStack,
};

// ── Measurement ─────────────────────────────────────────────────────────────

/**
 * Measure allocation per scenario with negative-sample rejection.
 *
 * For each iteration:
 *   1. Settle GC (3x gc())
 *   2. Read heapUsed (before)
 *   3. Run step loop (with optional selfTest allocation per step)
 *   4. Settle GC (3x gc())
 *   5. Read heapUsed (after)
 *   6. Compute bytesPerStep = (after - before) / steps
 *   7. If negative → retry up to MAX_NEG_RETRIES times
 *   8. If still negative after retries → exclude from median
 *
 * Median: sort valid results, drop min/max, take middle value.
 * Spread: computed from trimmed values (after dropping min/max) to avoid
 *         GC outlier distortion.
 */
function measureAllocation(scenarioSetup, iterations, steps) {
    const results = [];
    const excludedCount = [];

    // Self-test leak accumulator — keeps references alive to force measurable allocation
    const selfTestLeaks = selfTestBytes > 0 ? [] : null;

    for (let i = 0; i < iterations; i++) {
        let bytesPerStep = null;
        let retries = 0;

        while (retries <= MAX_NEG_RETRIES) {
            // Pre-measurement GC settle
            settleGC();

            const before = process.memoryUsage().heapUsed;

            // Measured step loop
            for (let s = 0; s < steps; s++) {
                scenarioSetup.stepFn();

                // Self-test: intentionally allocate bytes that survive GC
                if (selfTestLeaks) {
                    selfTestLeaks.push(new Uint8Array(selfTestBytes));
                }
            }

            // Post-measurement GC settle
            settleGC();

            const after = process.memoryUsage().heapUsed;
            const totalBytes = after - before;
            bytesPerStep = totalBytes / steps;

            if (bytesPerStep >= 0) {
                break; // Valid sample
            }

            retries++;
            if (retries <= MAX_NEG_RETRIES) {
                // Extra settle before retry
                settleGC();
            }
        }

        if (bytesPerStep < 0) {
            excludedCount.push(i);
        } else {
            results.push({
                totalBytes: bytesPerStep * steps,
                bytesPerStep,
            });
        }
    }

    if (results.length < 3) {
        console.warn(
            `\n  WARNING: Only ${results.length} valid iterations (excluded ${excludedCount.length}). ` +
            `Median may be unreliable.`,
        );
    }

    // Sort by bytesPerStep, drop min/max, take median of remaining
    results.sort((a, b) => a.bytesPerStep - b.bytesPerStep);
    const trimmed = results.length > 2
        ? results.slice(1, -1)
        : results;
    const median = trimmed[Math.floor(trimmed.length / 2)];

    // Spread from trimmed values (excludes GC outlier min/max)
    const trimmedMin = trimmed[0]?.bytesPerStep ?? null;
    const trimmedMax = trimmed[trimmed.length - 1]?.bytesPerStep ?? null;
    const spreadPct = (trimmedMin !== null && trimmedMin > 0 && trimmedMax !== null)
        ? ((trimmedMax - trimmedMin) / trimmedMin * 100)
        : null;

    return {
        iterations: results,
        excludedIterations: excludedCount,
        median: {
            totalBytes: median.totalBytes,
            bytesPerStep: median.bytesPerStep,
        },
        trimmedMin,
        trimmedMax,
        spreadPct,
    };
}

// ── Main ────────────────────────────────────────────────────────────────────

async function main() {
    console.log('Physics Allocation Regression Harness');
    if (selfTestBytes > 0) {
        console.log(`*** SELF-TEST MODE: injecting ${selfTestBytes} bytes/step allocation ***`);
    }
    console.log(`Regression threshold: ${REGRESSION_THRESHOLD_PCT}%`);
    console.log(`GC discipline: ${GC_CALLS}x gc() per settle, negative retry: ${MAX_NEG_RETRIES}`);
    console.log('');

    for (const [name, cfg] of Object.entries(SCENARIO_CONFIG)) {
        console.log(`  ${name}: ${cfg.steps} steps, ${cfg.iterations} iterations`);
    }
    console.log('');

    // Ensure output directory exists
    const reportDir = path.dirname(reportPath);
    if (!fs.existsSync(reportDir)) {
        fs.mkdirSync(reportDir, { recursive: true });
    }

    // Load baseline if exists
    let baseline = null;
    if (fs.existsSync(baselinePath)) {
        baseline = JSON.parse(fs.readFileSync(baselinePath, 'utf8'));
        console.log(`Loaded baseline from ${baselinePath}`);
    } else {
        console.log('No baseline found — will create one after measurement.');
    }

    const report = {
        timestamp: new Date().toISOString(),
        regressionThresholdPct: REGRESSION_THRESHOLD_PCT,
        gcCallsPerSettle: GC_CALLS,
        maxNegRetries: MAX_NEG_RETRIES,
        selfTestBytes: selfTestBytes || undefined,
        scenarios: {},
    };

    const failures = [];
    const scenarioNames = scenarioFilter
        ? Object.keys(scenarios).filter((name) => scenarioFilter.includes(name))
        : Object.keys(scenarios);

    if (scenarioFilter) {
        const unknown = scenarioFilter.filter((name) => !scenarios[name]);
        if (unknown.length > 0) {
            console.error(`Unknown scenarios: ${unknown.join(', ')}`);
            process.exit(1);
        }
    }

    for (const scenarioName of scenarioNames) {
        const cfg = SCENARIO_CONFIG[scenarioName];
        process.stdout.write(`Running scenario: ${scenarioName} (${cfg.steps} steps, ${cfg.iterations} iters)... `);

        const setup = await scenarios[scenarioName]();
        const measurement = measureAllocation(setup, cfg.iterations, cfg.steps);

        report.scenarios[scenarioName] = {
            steps: cfg.steps,
            iterations: cfg.iterations,
            medianBytesPerStep: measurement.median.bytesPerStep,
            medianTotalBytes: measurement.median.totalBytes,
            trimmedMinBytesPerStep: measurement.trimmedMin,
            trimmedMaxBytesPerStep: measurement.trimmedMax,
            spreadPct: measurement.spreadPct,
            excludedIterations: measurement.excludedIterations,
            iterations: measurement.iterations,
        };

        const bytesPerStep = measurement.median.bytesPerStep;
        const kbPerStep = (bytesPerStep / 1024).toFixed(3);
        const spreadStr = measurement.spreadPct !== null
            ? `spread ${measurement.spreadPct.toFixed(1)}%`
            : 'spread N/A';

        if (baseline && baseline.scenarios[scenarioName]) {
            const baselineBytesPerStep = baseline.scenarios[scenarioName].medianBytesPerStep;
            const changePct = ((bytesPerStep - baselineBytesPerStep) / baselineBytesPerStep) * 100;
            const changeStr = changePct >= 0 ? `+${changePct.toFixed(1)}%` : `${changePct.toFixed(1)}%`;

            console.log(`${kbPerStep} KB/step (${changeStr} vs baseline, ${spreadStr})`);

            if (changePct > REGRESSION_THRESHOLD_PCT) {
                failures.push(
                    `${scenarioName}: ${kbPerStep} KB/step is ${changePct.toFixed(1)}% above baseline (${(baselineBytesPerStep / 1024).toFixed(3)} KB/step)`,
                );
            }
        } else {
            console.log(`${kbPerStep} KB/step (no baseline, ${spreadStr})`);
        }

        if (measurement.excludedIterations.length > 0) {
            console.log(`  Excluded ${measurement.excludedIterations.length} iteration(s) due to negative samples: [${measurement.excludedIterations.join(', ')}]`);
        }
    }

    // Write report
    fs.writeFileSync(reportPath, JSON.stringify(report, null, 2));
    console.log(`\nReport written to ${reportPath}`);

    // Update baseline if requested or if no baseline exists
    if (cli['update-baseline'] || !baseline) {
        const baselineDir = path.dirname(baselinePath);
        if (!fs.existsSync(baselineDir)) {
            fs.mkdirSync(baselineDir, { recursive: true });
        }
        fs.writeFileSync(baselinePath, JSON.stringify(report, null, 2));
        console.log(`Baseline written to ${baselinePath}`);
    }

    // Report failures
    if (failures.length > 0) {
        console.error('\nPhysics allocation regression violations:');
        for (const failure of failures) {
            console.error(`  - ${failure}`);
        }
        if (cli['update-baseline']) {
            console.error('\nRun with --update-baseline to update the baseline if the regression is expected.');
        }
        process.exit(1);
    }

    console.log('\nPhysics allocation budgets satisfied.');
}

main().catch((err) => {
    console.error('Physics allocation harness failed:', err);
    process.exit(1);
});
