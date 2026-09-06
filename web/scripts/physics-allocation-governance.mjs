/**
 * Physics Allocation Regression Harness
 *
 * Measures heap allocation during physics simulation steps to detect
 * performance regressions. Uses --expose-gc for accurate measurement.
 *
 * Scenarios:
 *   - 2d-50body-step: 50 dynamic bodies, 120 steps
 *   - 2d-contact-heavy: 10-box stack (contact-heavy), 120 steps
 *   - 3d-50body-step: 50 dynamic bodies, 120 steps
 *   - 3d-sphere-stack: 10-sphere stack, 120 steps
 *
 * Measurement method:
 *   - 5 iterations per scenario, drop min/max, median of remaining 3
 *   - global.gc() before/after each iteration
 *   - heapUsed delta / steps = bytes per step
 *
 * Baseline:
 *   - First run writes baseline to .tmp/benchmarks/physics-allocation-baseline.json
 *   - Subsequent runs compare against baseline (10% regression threshold)
 *   - --update-baseline flag updates baseline on regression
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

const ITERATIONS = 5;
const STEPS = 120;
const REGRESSION_THRESHOLD_PCT = 10;

// ── CLI argument parsing ────────────────────────────────────────────────────
const { values: cli } = parseArgs({
    options: {
        baseline: { type: 'string' },
        report: { type: 'string' },
        updateBaseline: { type: 'boolean' },
        scenario: { type: 'string' },
    },
    strict: true,
    allowPositionals: false,
});

const baselinePath = path.resolve(workspaceDir, cli.baseline ?? defaultBaselinePath);
const reportPath = path.resolve(workspaceDir, cli.report ?? defaultReportPath);
const scenarioFilter = cli.scenario ? cli.scenario.split(',').map((s) => s.trim()) : null;

// ── GC requirement check ────────────────────────────────────────────────────
// If --expose-gc was not passed, re-spawn ourselves with it.
if (typeof global.gc !== 'function') {
    console.log('Respawning with --expose-gc for accurate allocation measurement...');
    const args = ['--expose-gc', fileURLToPath(import.meta.url)];
    // Forward original CLI args
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

// ── Scenario definitions ────────────────────────────────────────────────────
// Each scenario sets up a world, then measures allocation during step() calls.
// Setup allocation is NOT measured — only the step loop.

async function runScenario2D50Body() {
    const { PhysicsWorld2D } = await import('@axrone/physics-2d');
    const { BodyType } = await import('@axrone/physics-core');

    const world = new PhysicsWorld2D({ gravity: { x: 0, y: -9.81 } });

    // Create 50 dynamic bodies with circle shapes scattered around
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
        steps: STEPS,
    };
}

async function runScenario2DContactHeavy() {
    const { PhysicsWorld2D } = await import('@axrone/physics-2d');
    const { BodyType } = await import('@axrone/physics-core');

    const world = new PhysicsWorld2D({ gravity: { x: 0, y: -9.81 } });

    // Ground
    const groundId = world.createBody({
        type: BodyType.Static,
        position: { x: 0, y: 0 },
    });
    world.createBoxShape(groundId, { halfWidth: 50, halfHeight: 1 });

    // 10 boxes stacked
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
        steps: STEPS,
    };
}

async function runScenario3D50Body() {
    const { PhysicsWorld3D } = await import('@axrone/physics-3d');
    const { BodyType } = await import('@axrone/physics-core');

    const world = new PhysicsWorld3D({ gravity: { x: 0, y: -9.81, z: 0 } });

    // Create 50 dynamic bodies with sphere shapes
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
        steps: STEPS,
    };
}

async function runScenario3DSphereStack() {
    const { PhysicsWorld3D } = await import('@axrone/physics-3d');
    const { BodyType } = await import('@axrone/physics-core');

    const world = new PhysicsWorld3D({ gravity: { x: 0, y: -9.81, z: 0 } });

    // 10 spheres stacked
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
        steps: STEPS,
    };
}

const scenarios = {
    '2d-50body-step': runScenario2D50Body,
    '2d-contact-heavy': runScenario2DContactHeavy,
    '3d-50body-step': runScenario3D50Body,
    '3d-sphere-stack': runScenario3DSphereStack,
};

// ── Measurement ─────────────────────────────────────────────────────────────

function measureAllocation(scenarioSetup, iterations, steps) {
    const results = [];

    for (let i = 0; i < iterations; i++) {
        global.gc();
        global.gc();

        const before = process.memoryUsage().heapUsed;

        for (let s = 0; s < steps; s++) {
            scenarioSetup.stepFn();
        }

        global.gc();
        global.gc();

        const after = process.memoryUsage().heapUsed;
        const totalBytes = after - before;
        const bytesPerStep = totalBytes / steps;

        results.push({
            totalBytes,
            bytesPerStep,
        });
    }

    // Sort by bytesPerStep, drop min/max, take median of remaining 3
    results.sort((a, b) => a.bytesPerStep - b.bytesPerStep);
    const trimmed = results.slice(1, -1);
    const median = trimmed[Math.floor(trimmed.length / 2)];

    return {
        iterations: results,
        median: {
            totalBytes: median.totalBytes,
            bytesPerStep: median.bytesPerStep,
        },
    };
}

// ── Main ────────────────────────────────────────────────────────────────────

async function main() {
    console.log('Physics Allocation Regression Harness');
    console.log(`Iterations: ${ITERATIONS}, Steps per iteration: ${STEPS}`);
    console.log(`Regression threshold: ${REGRESSION_THRESHOLD_PCT}%`);
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
        iterations: ITERATIONS,
        steps: STEPS,
        regressionThresholdPct: REGRESSION_THRESHOLD_PCT,
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
        process.stdout.write(`Running scenario: ${scenarioName}... `);

        const setup = await scenarios[scenarioName]();
        const measurement = measureAllocation(setup, ITERATIONS, setup.steps);

        report.scenarios[scenarioName] = {
            medianBytesPerStep: measurement.median.bytesPerStep,
            medianTotalBytes: measurement.median.totalBytes,
            iterations: measurement.iterations,
        };

        const bytesPerStep = measurement.median.bytesPerStep;
        const kbPerStep = (bytesPerStep / 1024).toFixed(2);

        // Compare against baseline
        if (baseline && baseline.scenarios[scenarioName]) {
            const baselineBytesPerStep = baseline.scenarios[scenarioName].medianBytesPerStep;
            const changePct = ((bytesPerStep - baselineBytesPerStep) / baselineBytesPerStep) * 100;
            const changeStr = changePct >= 0 ? `+${changePct.toFixed(1)}%` : `${changePct.toFixed(1)}%`;

            console.log(`${kbPerStep} KB/step (${changeStr} vs baseline)`);

            if (changePct > REGRESSION_THRESHOLD_PCT) {
                failures.push(
                    `${scenarioName}: ${kbPerStep} KB/step is ${changePct.toFixed(1)}% above baseline (${(baselineBytesPerStep / 1024).toFixed(2)} KB/step)`,
                );
            }
        } else {
            console.log(`${kbPerStep} KB/step (no baseline)`);
        }
    }

    // Write report
    fs.writeFileSync(reportPath, JSON.stringify(report, null, 2));
    console.log(`\nReport written to ${reportPath}`);

    // Update baseline if requested or if no baseline exists
    if (cli.updateBaseline || !baseline) {
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
        if (!cli.updateBaseline) {
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
