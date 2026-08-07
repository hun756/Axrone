/**
 * Cleans all build caches for a full clean rebuild.
 *
 * Removes:
 *   - Nx task runner cache (.nx/cache)
 *   - rollup-plugin-typescript2 caches (.rpt2_cache, .rts2_cache_*)
 *
 * Usage: node ./scripts/clean-build-cache.mjs
 *   or:  yarn build:clean
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const workspaceDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const packagesDir = path.join(workspaceDir, 'packages');

const removeDir = (dirPath) => {
    if (fs.existsSync(dirPath)) {
        fs.rmSync(dirPath, { recursive: true, force: true });
        return true;
    }
    return false;
};

let cleaned = 0;

// Nx task runner cache
const nxCacheDir = path.join(workspaceDir, '.nx', 'cache');
if (removeDir(nxCacheDir)) {
    console.log('  Removed .nx/cache');
    cleaned++;
}

// Per-package rollup-plugin-typescript2 caches
const cacheDirPatterns = ['.rpt2_cache', '.rts2_cache_cjs', '.rts2_cache_es', '.rts2_cache_umd'];

if (fs.existsSync(packagesDir)) {
    const packages = fs.readdirSync(packagesDir, { withFileTypes: true });
    for (const pkg of packages) {
        if (!pkg.isDirectory()) continue;
        for (const cacheName of cacheDirPatterns) {
            const cachePath = path.join(packagesDir, pkg.name, cacheName);
            if (removeDir(cachePath)) {
                console.log(`  Removed packages/${pkg.name}/${cacheName}`);
                cleaned++;
            }
        }
    }
}

if (cleaned === 0) {
    console.log('No caches to clean.');
} else {
    console.log(`Cleaned ${cleaned} cache director${cleaned === 1 ? 'y' : 'ies'}.`);
}
