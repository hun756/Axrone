import { defineConfig } from 'vitest/config';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createWorkspacePackageAliasEntries } from './build/workspace-package-aliases.mjs';

const workspaceDir = path.dirname(fileURLToPath(import.meta.url));
const workspaceAliases = createWorkspacePackageAliasEntries(workspaceDir);

/**
 * Multi-browser Vitest config for cross-browser WebGL2 testing.
 *
 * Usage:
 *   1. Install browsers:  npx playwright install chromium firefox webkit
 *   2. Run all browsers:  yarn vitest run --config vitest.config.browser.firefox.ts
 *
 * This config defines three projects — one per browser — so tests run on
 * Chromium, Firefox, and WebKit in a single invocation.
 *
 * If a browser is not installed, Playwright will report an error.
 * Install missing browsers with: npx playwright install <browser-name>
 */
export default defineConfig({
    test: {
        browser: {
            enabled: true,
            provider: 'playwright',
            headless: false,
        },
        globals: true,
        setupFiles: ['./vitest.browser.setup.ts'],
        include: [
            'packages/**/*.browser.{test,spec}.{js,ts}',
            'packages/**/renderer/**/*.{test,spec}.{js,ts}',
        ],
        projects: [
            {
                test: {
                    name: 'chromium',
                    browser: {
                        enabled: true,
                        name: 'chromium',
                        provider: 'playwright',
                        headless: false,
                    },
                },
            },
            {
                test: {
                    name: 'firefox',
                    browser: {
                        enabled: true,
                        name: 'firefox',
                        provider: 'playwright',
                        headless: false,
                    },
                },
            },
            {
                test: {
                    name: 'webkit',
                    browser: {
                        enabled: true,
                        name: 'webkit',
                        provider: 'playwright',
                        headless: false,
                    },
                },
            },
        ],
    },
    resolve: {
        alias: workspaceAliases,
    },
    esbuild: {
        target: 'es2022',
    },
});
