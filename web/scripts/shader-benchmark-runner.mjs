import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const workspaceDir = path.resolve(scriptDir, '..');
const viteBinPath = path.resolve(workspaceDir, 'node_modules', 'vite', 'bin', 'vite.js');
const DEFAULT_VIEWPORT = { width: 1280, height: 720 };
const DEFAULT_TIMEOUT_MS = 120000;

const fail = (message) => {
    throw new Error(message);
};

const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

const { values: cli } = parseArgs({
    options: {
        warmup: { type: 'string' },
        iterations: { type: 'string' },
        host: { type: 'string' },
        port: { type: 'string' },
        url: { type: 'string' },
        output: { type: 'string' },
        label: { type: 'string' },
        headless: { type: 'boolean' },
        keepServer: { type: 'boolean' },
        help: { type: 'boolean' },
    },
    strict: true,
    allowPositionals: false,
});

const printHelp = () => {
    console.log(`Axrone shader compile benchmark runner

Options:
  --warmup=5              Warmup runs discarded before measurement
  --iterations=30         Measured repetitions per shader
  --host=127.0.0.1        Local examples server host
  --port=4273             Local examples server port
  --url=http://...        Reuse an existing server instead of starting Vite
  --output=<path>         Output JSON report path
  --label=<name>          Label for this run (e.g. "baseline" or "optimized")
  --headless              Run Chromium headless
  --keepServer            Leave the spawned Vite server running
  --help                  Show this help
`);
};

if (cli.help) {
    printHelp();
    process.exit(0);
}

const options = {
    warmup: Number.parseInt(cli.warmup ?? '5', 10),
    iterations: Number.parseInt(cli.iterations ?? '30', 10),
    host: cli.host ?? '127.0.0.1',
    port: Number.parseInt(cli.port ?? '4273', 10),
    url: cli.url ?? null,
    output: path.resolve(workspaceDir, cli.output ?? '.tmp/benchmarks/shader-compile-report.json'),
    label: cli.label ?? 'unlabeled',
    headless: Boolean(cli.headless),
    keepServer: Boolean(cli.keepServer),
};

if (options.warmup < 0) {
    fail('warmup must be zero or greater.');
}
if (options.iterations <= 0) {
    fail('iterations must be greater than zero.');
}

const benchmarkPageUrl = (baseUrl) => `${baseUrl.replace(/\/$/, '')}/shader-benchmark.html`;

const startExamplesServer = async () => {
    if (!fs.existsSync(viteBinPath)) {
        fail('Missing local Vite binary. Run yarn install in Axrone/web first.');
    }

    const server = spawn(
        process.execPath,
        [
            viteBinPath,
            '--config',
            'vite.examples.config.ts',
            '--host',
            options.host,
            '--port',
            String(options.port),
            '--strictPort',
        ],
        {
            cwd: workspaceDir,
            stdio: ['ignore', 'pipe', 'pipe'],
        }
    );

    const output = [];
    let ready = false;
    const pushOutput = (chunk) => {
        const text = chunk.toString();
        output.push(text);
        if (output.length > 30) {
            output.shift();
        }

        if (text.includes('ready in') || text.includes('Local:')) {
            ready = true;
        }
    };

    server.stdout.on('data', pushOutput);
    server.stderr.on('data', pushOutput);

    const url = `http://${options.host}:${options.port}`;
    const deadline = Date.now() + 30_000;

    while (Date.now() < deadline) {
        const combinedOutput = output.join('');

        if (server.exitCode !== null) {
            fail(`Examples server exited early: ${combinedOutput.trim() || 'unknown error'}`);
        }

        if (
            combinedOutput.includes('Port ') &&
            combinedOutput.includes(' is already in use')
        ) {
            server.kill('SIGTERM');
            fail(`Examples server could not claim ${url}: ${combinedOutput.trim()}`);
        }

        if (ready) {
            try {
                const response = await fetch(benchmarkPageUrl(url), { cache: 'no-store' });
                if (response.ok) {
                    return { server, url };
                }
            } catch (error) {
                // Retry
            }
        }

        await delay(250);
    }

    server.kill('SIGTERM');
    fail(`Timed out waiting for examples server at ${url}. Last output:\n${output.join('').trim()}`);
};

const closeServer = async (server) => {
    if (!server || server.exitCode !== null) {
        return;
    }

    server.kill('SIGTERM');
    const deadline = Date.now() + 5_000;
    while (server.exitCode === null && Date.now() < deadline) {
        await delay(100);
    }

    if (server.exitCode === null) {
        server.kill('SIGKILL');
    }
};

const waitForBenchmarkApi = async (page, baseUrl) => {
    const errors = [];
    const recordError = (message) => {
        if (typeof message !== 'string' || message.length === 0) {
            return;
        }
        errors.push(message);
        if (errors.length > 10) {
            errors.shift();
        }
    };

    page.on('console', (message) => {
        if (message.type() === 'error') {
            recordError(message.text());
        }
    });
    page.on('pageerror', (error) => {
        recordError(error instanceof Error ? error.stack ?? error.message : String(error));
    });

    for (let attempt = 0; attempt < 2; attempt += 1) {
        await page.goto(benchmarkPageUrl(baseUrl), {
            waitUntil: 'domcontentloaded',
            timeout: DEFAULT_TIMEOUT_MS,
        });

        try {
            await page.waitForFunction(
                () => Boolean(window.__AXRONE_SHADER_BENCHMARK__?.isReady),
                undefined,
                { timeout: DEFAULT_TIMEOUT_MS }
            );
            return;
        } catch (error) {
            if (attempt === 1) {
                const detail =
                    errors.length > 0 ? ` Recent page errors: ${errors.join(' | ')}` : '';
                throw new Error(
                    `Shader benchmark API did not become available at ${benchmarkPageUrl(baseUrl)} within ${DEFAULT_TIMEOUT_MS} ms.${detail}`
                );
            }
        }
    }
};

const printReport = (report, label) => {
    console.log(`\n=== Shader Compile Benchmark [${label}] ===`);
    console.log(
        `  Browser: ${report.environment.unmaskedRenderer} (${report.environment.userAgent.slice(0, 60)}...)`
    );
    console.log(
        `  KHR_parallel_shader_compile: ${report.environment.parallelShaderCompileAvailable ? 'available' : 'NOT available'}`
    );
    console.log(`  Config: ${report.config.warmup} warmup + ${report.config.iterations} iterations`);

    console.log('\n  Per-shader (mean / median / p95 ms):');
    for (const shader of report.shaders) {
        const total = shader.totalTimeMs;
        console.log(
            `    ${shader.label.padEnd(32)} ${String(shader.totalSourceChars).padStart(6)} chars  ` +
                `compile ${total.mean.toFixed(2)} / ${total.median.toFixed(2)} / ${total.p95.toFixed(2)} ms`
        );
    }

    const seq = report.sequentialBatch.totalTimeMs;
    console.log(
        `\n  Sequential batch (${report.sequentialBatch.programCount} programs): ` +
            `${seq.mean.toFixed(2)} / ${seq.median.toFixed(2)} / ${seq.p95.toFixed(2)} ms (mean / median / p95)`
    );

    if (report.parallelBatch) {
        const par = report.parallelBatch.totalTimeMs;
        const speedup = seq.mean / par.mean;
        console.log(
            `  Parallel batch   (${report.parallelBatch.programCount} programs): ` +
                `${par.mean.toFixed(2)} / ${par.median.toFixed(2)} / ${par.p95.toFixed(2)} ms (mean / median / p95)`
        );
        console.log(`  Parallel speedup: ${speedup.toFixed(2)}x`);
    }
};

let server = null;
let browser = null;

try {
    const startedServer = options.url ? null : await startExamplesServer();
    const baseUrl = options.url ? options.url.replace(/\/$/, '') : startedServer.url;
    server = startedServer?.server ?? null;

    browser = await chromium.launch({
        headless: options.headless,
        args: [
            '--enable-webgl',
            '--enable-accelerated-2d-canvas',
            '--disable-web-security',
            '--allow-running-insecure-content',
            '--ignore-gpu-blocklist',
            '--use-angle=d3d11',
            '--use-gl=angle',
        ],
    });

    const context = await browser.newContext({
        viewport: DEFAULT_VIEWPORT,
        deviceScaleFactor: 1,
    });
    const page = await context.newPage();

    await waitForBenchmarkApi(page, baseUrl);

    console.log(`Running shader compile benchmark [${options.label}]...`);
    console.log(`  ${options.warmup} warmup + ${options.iterations} iterations per measurement`);

    const report = await page.evaluate((config) => {
        return window.__AXRONE_SHADER_BENCHMARK__.runBenchmark(config);
    }, { warmup: options.warmup, iterations: options.iterations });

    report.label = options.label;

    printReport(report, options.label);

    fs.mkdirSync(path.dirname(options.output), { recursive: true });
    fs.writeFileSync(options.output, JSON.stringify(report, null, 2));
    console.log(`\nReport written to ${path.relative(workspaceDir, options.output)}`);

    await context.close();
} catch (error) {
    console.error(error instanceof Error ? error.stack ?? error.message : String(error));
    process.exitCode = 1;
} finally {
    if (browser) {
        await browser.close();
    }
    if (server && !options.keepServer) {
        await closeServer(server);
    }
}
