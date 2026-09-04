import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const workspaceDir = path.resolve(scriptDir, '..');
const benchmarkRunnerPath = path.resolve(scriptDir, 'engine-benchmark-runner.mjs');
const defaultReportPath = path.resolve(
    workspaceDir,
    '.tmp',
    'benchmarks',
    'engine-benchmark-report.json',
);

const defaultRunnerOptions = {
    iterations: '3',
    warmup: '1',
    durationSec: '5',
    objectCounts: '19600',
    comparisonModes: 'no-culling',
    workloads: 'draw-call,triangle,mixed,ui-widgets',
};

const scenarioBudgets = [
    {
        workload: 'draw-call',
        comparisonMode: 'no-culling',
        objectCount: 19600,
        minRunCount: 3,
        minAverageFpsMean: 3.5,
        maxSetupBuildMedianMs: 220,
        maxFirstRenderMedianMs: 165,
        maxSetupMedianMs: 380,
        maxComponentInstantiateMedianMs: 60,
        maxActorCreateMedianMs: 110,
        maxRenderableCreateMedianMs: 185,
        maxSceneSetupMedianMs: 12,
        requireFpsLeader: true,
        requireFirstRenderLeader: true,
    },
    {
        workload: 'triangle',
        comparisonMode: 'no-culling',
        objectCount: 19600,
        minRunCount: 3,
        minAverageFpsMean: 3.5,
        maxSetupBuildMedianMs: 225,
        maxFirstRenderMedianMs: 155,
        maxSetupMedianMs: 370,
        maxComponentInstantiateMedianMs: 58,
        maxActorCreateMedianMs: 110,
        maxRenderableCreateMedianMs: 195,
        maxSceneSetupMedianMs: 14,
        requireFpsLeader: true,
        requireFirstRenderLeader: true,
    },
    {
        workload: 'mixed',
        comparisonMode: 'no-culling',
        objectCount: 19600,
        minRunCount: 3,
        minAverageFpsMean: 3.5,
        maxSetupBuildMedianMs: 230,
        maxFirstRenderMedianMs: 180,
        maxSetupMedianMs: 385,
        maxComponentInstantiateMedianMs: 62,
        maxActorCreateMedianMs: 105,
        maxRenderableCreateMedianMs: 185,
        maxSceneSetupMedianMs: 14,
        requireFpsLeader: true,
        requireFirstRenderLeader: true,
    },
    {
        workload: 'ui-widgets',
        comparisonMode: '',
        objectCount: 0,
        minRunCount: 1,
        maxFrameTimeMedianMs: 25,
        maxFrameTimeP99Ms: 120,
        maxDrawCallsPerFrame: 5,
        maxGetParameterPerFrameMedian: 20,
    },
];

const fail = (message) => {
    throw new Error(message);
};

const scenarioKey = ({ workload, comparisonMode, objectCount }) =>
    `${workload}|${comparisonMode}|${objectCount}`;

const scenarioLabel = ({ workload, comparisonMode, objectCount }) =>
    `${workload}/${comparisonMode}/${objectCount.toLocaleString('en-US')}`;

const formatNumber = (value) => value.toFixed(2).padStart(8, ' ');

const getMetricSummary = (source, metricPath) => {
    const result = metricPath.split('.').reduce((current, segment) => current?.[segment], source);

    if (!result || typeof result !== 'object') {
        fail(`Missing metric summary at "${metricPath}".`);
    }

    return result;
};

const pushMedianBudgetFailure = (failures, label, metricLabel, metricSummary, budgetValue) => {
    if (typeof budgetValue !== 'number' || !Number.isFinite(budgetValue)) {
        return;
    }

    if (metricSummary.median > budgetValue) {
        failures.push(
            `${label} ${metricLabel} median ${metricSummary.median.toFixed(2)} ms exceeds budget ${budgetValue.toFixed(2)} ms.`,
        );
    }
};

const runBenchmarkRefresh = (reportPath, options) => {
    const runnerArgs = [
        benchmarkRunnerPath,
        `--iterations=${options.iterations ?? defaultRunnerOptions.iterations}`,
        `--warmup=${options.warmup ?? defaultRunnerOptions.warmup}`,
        `--durationSec=${options.durationSec ?? defaultRunnerOptions.durationSec}`,
        `--objectCounts=${options.objectCounts ?? defaultRunnerOptions.objectCounts}`,
        `--comparisonModes=${options.comparisonModes ?? defaultRunnerOptions.comparisonModes}`,
        `--workloads=${options.workloads ?? defaultRunnerOptions.workloads}`,
        '--isolateRuns',
        `--output=${reportPath}`,
    ];

    if (options.headless) {
        runnerArgs.push('--headless');
    }

    console.log('Refreshing engine benchmark report...');
    const result = spawnSync(process.execPath, runnerArgs, {
        cwd: workspaceDir,
        stdio: 'inherit',
    });

    if (result.status !== 0) {
        fail(`Engine benchmark refresh failed with exit code ${result.status ?? 1}.`);
    }
};

const { values: cli } = parseArgs({
    options: {
        report: { type: 'string' },
        refresh: { type: 'boolean' },
        strictStability: { type: 'boolean' },
        headless: { type: 'boolean' },
        iterations: { type: 'string' },
        warmup: { type: 'string' },
        durationSec: { type: 'string' },
        objectCounts: { type: 'string' },
        workloads: { type: 'string' },
        comparisonModes: { type: 'string' },
    },
    strict: true,
    allowPositionals: false,
});

const reportPath = path.resolve(workspaceDir, cli.report ?? defaultReportPath);

// When --workloads is given without --refresh, filter which budgets are evaluated.
const evaluateWorkloads = cli.workloads
    ? cli.workloads.split(',').map((s) => s.trim()).filter(Boolean)
    : null;
const activeBudgets = evaluateWorkloads
    ? scenarioBudgets.filter((b) => evaluateWorkloads.includes(b.workload))
    : scenarioBudgets;

if (cli.refresh) {
    runBenchmarkRefresh(reportPath, cli);
}

if (!fs.existsSync(reportPath)) {
    fail(
        `Benchmark report not found at ${reportPath}. Run this script with --refresh or generate a report with npm run bench:engine:startup.`,
    );
}

const report = JSON.parse(fs.readFileSync(reportPath, 'utf8'));
const scenarioReports = new Map(
    (report.scenarios ?? []).map((scenarioReport) => [scenarioKey(scenarioReport.scenario), scenarioReport]),
);

const reportRows = [];
const failures = [];
const stabilityWarnings = [];

for (const budget of activeBudgets) {
    const label = scenarioLabel(budget);
    const scenarioReport = scenarioReports.get(scenarioKey(budget));

    if (!scenarioReport) {
        failures.push(
            `${label} is missing from ${reportPath}. Refresh the report with the startup benchmark scenario set before evaluating governance.`,
        );
        continue;
    }

    if ((scenarioReport.summary?.runCount ?? 0) < budget.minRunCount) {
        failures.push(
            `${label} only has ${scenarioReport.summary?.runCount ?? 0} measured run(s); expected at least ${budget.minRunCount}.`,
        );
    }

    // ── ui-widgets workload has its own metric layout ─────────────────
    if (budget.workload === 'ui-widgets') {
        const frameTime = scenarioReport.summary?.frameTimeMs;
        const drawCalls = scenarioReport.summary?.drawCallsPerFrame;
        const getParams = scenarioReport.summary?.getParameterPerFrame;

        if (!frameTime || !drawCalls || !getParams) {
            failures.push(`${label} is missing ui-widgets summary data.`);
            continue;
        }

        reportRows.push({
            label,
            buildMedianMs: frameTime.median,
            firstRenderMedianMs: frameTime.p99,
            setupMedianMs: 0,
            componentInstantiateMedianMs: 0,
            sceneSetupMedianMs: 0,
            warningCount: 0,
        });

        pushMedianBudgetFailure(failures, label, 'frameTimeMs', frameTime, budget.maxFrameTimeMedianMs);
        pushMedianBudgetFailure(failures, label, 'frameTimeP99Ms', { median: frameTime.p99 }, budget.maxFrameTimeP99Ms);
        pushMedianBudgetFailure(failures, label, 'drawCallsPerFrame', { median: drawCalls.median }, budget.maxDrawCallsPerFrame);
        pushMedianBudgetFailure(failures, label, 'getParameterPerFrame', { median: getParams.median }, budget.maxGetParameterPerFrameMedian);
        continue;
    }

    const axrone = scenarioReport.summary?.engines?.axrone;
    const deltas = scenarioReport.summary?.deltas;
    if (!axrone || !deltas) {
        failures.push(`${label} is missing Axrone summary data.`);
        continue;
    }

    const buildMetric = getMetricSummary(axrone, 'setupBuildTimeMs');
    const firstRenderMetric = getMetricSummary(axrone, 'firstRenderTimeMs');
    const setupMetric = getMetricSummary(axrone, 'setupTimeMs');
    const fpsMetric = getMetricSummary(axrone, 'averageFps');
    const componentInstantiateMetric = getMetricSummary(axrone, 'buildPhases.componentInstantiateMs');
    const actorCreateMetric = getMetricSummary(axrone, 'buildPhases.actorCreateMs');
    const renderableCreateMetric = getMetricSummary(axrone, 'buildPhases.renderableCreateMs');
    const sceneSetupMetric = getMetricSummary(axrone, 'buildPhases.sceneSetupMs');

    reportRows.push({
        label,
        buildMedianMs: buildMetric.median,
        firstRenderMedianMs: firstRenderMetric.median,
        setupMedianMs: setupMetric.median,
        componentInstantiateMedianMs: componentInstantiateMetric.median,
        sceneSetupMedianMs: sceneSetupMetric.median,
        warningCount: (scenarioReport.summary?.qualityFlags ?? []).filter(
            (warning) => warning.engine === 'axrone',
        ).length,
    });

    if (fpsMetric.mean < budget.minAverageFpsMean) {
        failures.push(
            `${label} average FPS mean ${fpsMetric.mean.toFixed(2)} is below budget ${budget.minAverageFpsMean.toFixed(2)}.`,
        );
    }

    pushMedianBudgetFailure(failures, label, 'setupBuildTimeMs', buildMetric, budget.maxSetupBuildMedianMs);
    pushMedianBudgetFailure(failures, label, 'firstRenderTimeMs', firstRenderMetric, budget.maxFirstRenderMedianMs);
    pushMedianBudgetFailure(failures, label, 'setupTimeMs', setupMetric, budget.maxSetupMedianMs);
    pushMedianBudgetFailure(
        failures,
        label,
        'buildPhases.componentInstantiateMs',
        componentInstantiateMetric,
        budget.maxComponentInstantiateMedianMs,
    );
    pushMedianBudgetFailure(
        failures,
        label,
        'buildPhases.actorCreateMs',
        actorCreateMetric,
        budget.maxActorCreateMedianMs,
    );
    pushMedianBudgetFailure(
        failures,
        label,
        'buildPhases.renderableCreateMs',
        renderableCreateMetric,
        budget.maxRenderableCreateMedianMs,
    );
    pushMedianBudgetFailure(
        failures,
        label,
        'buildPhases.sceneSetupMs',
        sceneSetupMetric,
        budget.maxSceneSetupMedianMs,
    );

    if (budget.requireFpsLeader && deltas.averageFps?.leader !== 'axrone') {
        failures.push(`${label} lost the FPS leadership check against Three.js.`);
    }

    if (budget.requireFirstRenderLeader && deltas.firstRenderTimeMs?.leader !== 'axrone') {
        failures.push(`${label} lost the first-render leadership check against Three.js.`);
    }

    const axroneWarnings = (scenarioReport.summary?.qualityFlags ?? []).filter(
        (warning) => warning.engine === 'axrone',
    );
    if (axroneWarnings.length > 0) {
        stabilityWarnings.push({
            label,
            warnings: axroneWarnings,
        });

        if (cli.strictStability) {
            failures.push(
                `${label} produced ${axroneWarnings.length} Axrone stability warning(s) under --strictStability.`,
            );
        }
    }
}

console.log('Engine benchmark governance report');
console.log(
    'Scenario'.padEnd(30) +
        'Build'.padStart(8) +
        '  ' +
        'First'.padStart(8) +
        '  ' +
        'Setup'.padStart(8) +
        '  ' +
        'Comp'.padStart(8) +
        '  ' +
        'Scene'.padStart(8) +
        '  ' +
        'Warn'.padStart(6),
);
for (const row of reportRows) {
    console.log(
        row.label.padEnd(30) +
            formatNumber(row.buildMedianMs) +
            '  ' +
            formatNumber(row.firstRenderMedianMs) +
            '  ' +
            formatNumber(row.setupMedianMs) +
            '  ' +
            formatNumber(row.componentInstantiateMedianMs) +
            '  ' +
            formatNumber(row.sceneSetupMedianMs) +
            '  ' +
            String(row.warningCount).padStart(6),
    );
}

if (stabilityWarnings.length > 0) {
    console.warn('\nAxrone stability warnings');
    for (const warningGroup of stabilityWarnings) {
        console.warn(
            `- ${warningGroup.label}: ${warningGroup.warnings
                .map(
                    (warning) =>
                        `${warning.metric} cv=${warning.coefficientOfVariationPct.toFixed(2)}% ratio=${warning.maxOverMedianRatio.toFixed(2)}`,
                )
                .join(' | ')}`,
        );
    }

    if (!cli.strictStability) {
        console.warn('Stability warnings were surfaced but did not fail governance because --strictStability was not set.');
    }
}

if (failures.length > 0) {
    console.error('\nEngine benchmark governance violations');
    for (const failure of failures) {
        console.error(`- ${failure}`);
    }

    process.exit(1);
}

console.log('\nEngine benchmark budgets satisfied.');