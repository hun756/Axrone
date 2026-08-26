import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import {
    collectTypeScriptFiles,
    isTestSourceFile,
} from '../_helpers/import-specifiers';

const testDir = path.dirname(fileURLToPath(import.meta.url));
const workspaceDir = path.resolve(testDir, '../../..');
const animationSrcDir = path.resolve(workspaceDir, 'packages/animation/src');

const MODULE_MUTABLE_PATTERNS: readonly RegExp[] = [
    /^let\s+\w+\s*=\s*new\s+Float32Array/m,
    /^let\s+\w+\s*=\s*\{/m,
    /^let\s+\w+\s*=\s*\[/m,
    /^let\s+\w+\s*=\s*new\s+Map/m,
    /^let\s+\w+\s*=\s*new\s+Set/m,
];

const ALLOWED_FILES = new Set([
    'blend-scratch.ts',
    'streaming.ts',
]);

const hasModuleLevelMutableState = (source: string): string[] => {
    const violations: string[] = [];
    const lines = source.split('\n');
    let insideFunction = 0;
    let insideClass = 0;

    for (let lineIndex = 0; lineIndex < lines.length; lineIndex += 1) {
        const line = lines[lineIndex]!;
        const trimmed = line.trim();

        if (trimmed.startsWith('//') || trimmed.startsWith('*') || trimmed.startsWith('/*')) {
            continue;
        }

        for (let charIndex = 0; charIndex < line.length; charIndex += 1) {
            const char = line[charIndex];
            if (char === '{') {
                const before = line.slice(0, charIndex).trim();
                if (
                    before.endsWith('=>') ||
                    before.match(/\)\s*$/) ||
                    before.match(/\bfunction\b/) ||
                    before.match(/\bif\b/) ||
                    before.match(/\bfor\b/) ||
                    before.match(/\bwhile\b/) ||
                    before.match(/\bswitch\b/) ||
                    before.match(/\belse\b/) ||
                    before.match(/\bcatch\b/)
                ) {
                    insideFunction += 1;
                } else if (before.match(/\bclass\b/)) {
                    insideClass += 1;
                } else {
                    insideFunction += 1;
                }
            } else if (char === '}') {
                if (insideFunction > 0) {
                    insideFunction -= 1;
                } else if (insideClass > 0) {
                    insideClass -= 1;
                }
            }
        }

        if (insideFunction > 0 || insideClass > 0) {
            continue;
        }

        if (trimmed.startsWith('export const') || trimmed.startsWith('const ')) {
            for (const pattern of MODULE_MUTABLE_PATTERNS) {
                if (pattern.test(trimmed.replace(/^(export\s+)?const\s+\w+\s*=\s*/, 'let x = '))) {
                    if (trimmed.includes('Object.freeze(') || trimmed.includes('as const')) {
                        continue;
                    }
                    violations.push(`Line ${lineIndex + 1}: ${trimmed.slice(0, 100)}`);
                    break;
                }
            }
        }

        if (trimmed.startsWith('let ') && !trimmed.startsWith('let ') === false) {
            if (insideFunction === 0 && insideClass === 0) {
                violations.push(`Line ${lineIndex + 1}: mutable let at module scope: ${trimmed.slice(0, 100)}`);
            }
        }
    }

    return violations;
};

describe('animation module-level mutable state governance', () => {
    it('prevents module-level mutable buffers that cause reentrancy bugs', () => {
        const sourceFiles = collectTypeScriptFiles(animationSrcDir, {
            exclude: (filePath) => isTestSourceFile(filePath),
        });

        const violations: string[] = [];

        for (const filePath of sourceFiles) {
            const fileName = path.basename(filePath);
            if (ALLOWED_FILES.has(fileName)) {
                continue;
            }

            const source = fs.readFileSync(filePath, 'utf8');
            const fileViolations = hasModuleLevelMutableState(source);
            if (fileViolations.length > 0) {
                const relativePath = filePath.replace(workspaceDir, '').replace(/\\/g, '/');
                violations.push(
                    `${relativePath}:\n  ${fileViolations.join('\n  ')}`
                );
            }
        }

        expect(violations).toEqual([]);
    });
});
