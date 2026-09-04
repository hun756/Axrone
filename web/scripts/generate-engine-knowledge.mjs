/**
 * Engine knowledge manifest generator — single source of truth for what the
 * Axrone AI assistant may claim about the engine API surface.
 *
 * Reads every `packages/*` package (package.json + built `dist/*.d.ts`
 * declarations) and emits a deterministic `engine-knowledge.json`:
 *
 *   {
 *     version, generatedAt, engineVersion,
 *     packages: [{ name, version, description, exports, typeExports,
 *                  hasDefaultExport, subpaths: [{ path, exports, typeExports }] }],
 *     componentKinds: [{ kind, package }],
 *     symbols: [{ name, package, kind }]
 *   }
 *
 * Why dist/*.d.ts and not src? The flattened bundle declarations are exactly
 * what consumers (Editor, preview bridge, user scripts) can import — the same
 * surface the TypeScript compiler resolves. No build is triggered here; the
 * Editor sync script regenerates this file whenever the engine is present, so
 * consumption is always fresh without coupling engine builds to AI prompts.
 *
 * Usage:
 *   node ./scripts/generate-engine-knowledge.mjs [--output <path>] [--check]
 *
 * `--check` exits 1 when the committed file differs from a fresh generation
 * (drift gate for CI / local verification).
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const webDir = path.resolve(scriptDir, '..');
const packagesDir = path.resolve(webDir, 'packages');
const defaultOutputPath = path.resolve(webDir, 'engine-knowledge.json');

const MANIFEST_VERSION = 1;

// ---------------------------------------------------------------------------
// .d.ts parsing (regex-based, no TS dependency — keeps the script runnable
// with plain node in every environment that can run the other governance
// scripts).
// ---------------------------------------------------------------------------

/** Split an `export { A, B as C, ... }` list into public names. */
const parseExportList = (listText) => {
	const names = [];
	for (const entry of listText.split(',')) {
		const trimmed = entry.trim();
		if (!trimmed) continue;
		// `Original$1 as Public` (bundler renames) or `A as B` → take alias.
		const aliasMatch = /\bas\s+([A-Za-z_$][\w$]*)\s*$/.exec(trimmed);
		const raw = aliasMatch ? aliasMatch[1] : trimmed;
		const nameMatch = /^([A-Za-z_$][\w$]*)/.exec(raw);
		if (nameMatch && nameMatch[1] !== 'default') names.push(nameMatch[1]);
	}
	return names;
};

const collectMatches = (text, pattern) => {
	const out = [];
	let match;
	pattern.lastIndex = 0;
	while ((match = pattern.exec(text)) !== null) {
		out.push(match);
		if (match[0].length === 0) pattern.lastIndex += 1;
	}
	return out;
};

const parseDeclarationFile = (content) => {
	const valueExports = new Set();
	const typeExports = new Set();
	let hasDefaultExport = false;
	// Umbrella facades (e.g. @axrone/physics) re-export whole packages via
	// `export * from '@axrone/x'` — resolved in a second pass (see below).
	const starSources = [];
	const namedReexports = [];

	for (const match of collectMatches(content, /export\s*\{([^}]*)\}\s*from\s*['"]([^'"]+)['"]\s*;/g)) {
		namedReexports.push({ names: parseExportList(match[1]), from: match[2] });
	}
	for (const match of collectMatches(content, /export\s*\{([^}]*)\}\s*;/g)) {
		for (const name of parseExportList(match[1])) valueExports.add(name);
	}
	for (const match of collectMatches(content, /export\s+type\s*\{([^}]*)\}\s*from\s*['"]([^'"]+)['"]\s*;/g)) {
		namedReexports.push({ names: parseExportList(match[1]), from: match[2], typeOnly: true });
	}
	for (const match of collectMatches(content, /export\s+type\s*\{([^}]*)\}\s*;/g)) {
		for (const name of parseExportList(match[1])) typeExports.add(name);
	}
	for (const match of collectMatches(content, /export\s*\*\s*from\s*['"]([^'"]+)['"]\s*;/g)) {
		starSources.push(match[1]);
	}
	if (/\bexport\s+default\b/.test(content)) hasDefaultExport = true;

	// Symbol kinds for the search index — only public (exported) names kept.
	const kinds = new Map();
	const recordKind = (name, kind) => {
		if (!name || kinds.has(name)) return;
		kinds.set(name, kind);
	};
	for (const match of collectMatches(
		content,
		/declare\s+abstract\s+class\s+([A-Za-z_$][\w$]*)/g,
	)) {
		recordKind(match[1], 'class');
	}
	for (const match of collectMatches(content, /declare\s+class\s+([A-Za-z_$][\w$]*)/g)) {
		recordKind(match[1], 'class');
	}
	for (const match of collectMatches(
		content,
		/declare\s+function\s+([A-Za-z_$][\w$]*)/g,
	)) {
		recordKind(match[1], 'function');
	}
	for (const match of collectMatches(
		content,
		/declare\s+const\s+([A-Za-z_$][\w$]*)/g,
	)) {
		recordKind(match[1], 'const');
	}
	for (const match of collectMatches(content, /declare\s+enum\s+([A-Za-z_$][\w$]*)/g)) {
		recordKind(match[1], 'enum');
	}
	for (const match of collectMatches(content, /(^|\n)\s*interface\s+([A-Za-z_$][\w$]*)/g)) {
		recordKind(match[2], 'interface');
	}
	for (const match of collectMatches(content, /(^|\n)\s*type\s+([A-Za-z_$][\w$]*)\s*[=<]/g)) {
		recordKind(match[2], 'type');
	}

	const snippets = extractDeclarationSnippets(content, valueExports, typeExports, kinds);

	return { valueExports, typeExports, hasDefaultExport, kinds, starSources, namedReexports, snippets };
};

/**
 * P2-lite: capture short declaration signatures for directly-declared
 * exported classes/functions/enums (NOT star re-exports — the defining
 * package owns the snippet, umbrellas contribute names only). Powers the
 * `get_symbol_detail` tool and the component-schema fallback without
 * shipping full source.
 */
const SNIPPET_MAX_LENGTH = 300;

const collapseWhitespace = (text) => text.replace(/\s+/g, ' ').trim();

const extractBalancedSnippet = (content, startIndex) => {
	let braceDepth = 0;
	let seenBrace = false;
	for (let i = startIndex; i < content.length && i - startIndex < SNIPPET_MAX_LENGTH * 3; i++) {
		const char = content[i];
		if (char === '{') {
			braceDepth += 1;
			seenBrace = true;
		} else if (char === '}') {
			braceDepth -= 1;
			if (seenBrace && braceDepth === 0) {
				return content.slice(startIndex, i + 1);
			}
		} else if (char === ';' && !seenBrace && braceDepth === 0) {
			return content.slice(startIndex, i + 1);
		}
	}
	return content.slice(startIndex, startIndex + SNIPPET_MAX_LENGTH);
};

const DECLARATION_PATTERNS = [
	{ kind: 'class', pattern: (name) => new RegExp(`(?:declare\\s+)?(?:abstract\\s+)?class\\s+${name}\\b`, 'g') },
	{ kind: 'function', pattern: (name) => new RegExp(`declare\\s+function\\s+${name}\\b`, 'g') },
	{ kind: 'const', pattern: (name) => new RegExp(`declare\\s+const\\s+${name}\\b`, 'g') },
	{ kind: 'enum', pattern: (name) => new RegExp(`declare\\s+(?:const\\s+)?enum\\s+${name}\\b`, 'g') },
];

/**
 * Compose a class snippet from its public member signatures instead of its
 * first N bytes (which are always private fields). Scans the full class body
 * for constructor/getters/setters/methods/public fields and keeps the first
 * few — the usable surface, not the storage.
 */
const PUBLIC_MEMBER_PATTERN = /^(constructor|get\s+[A-Za-z_$]|set\s+[A-Za-z_$]|[A-Za-z_$][\w$]*\s*(\(|:))/;

const skimClassSnippet = (content, startIndex, maxSignatures = 9) => {
	let braceDepth = 0;
	let bodyEnd = -1;
	for (let i = startIndex; i < content.length; i++) {
		const char = content[i];
		if (char === '{') braceDepth += 1;
		else if (char === '}') {
			braceDepth -= 1;
			if (braceDepth === 0) {
				bodyEnd = i;
				break;
			}
		}
	}
	if (bodyEnd < 0) return null;
	const headerEnd = content.indexOf('{', startIndex);
	const header = collapseWhitespace(content.slice(startIndex, headerEnd + 1));
	const signatures = [];
	for (const line of content.slice(headerEnd + 1, bodyEnd).split('\n')) {
		const trimmed = line.trim().replace(/;$/, '');
		if (!trimmed || /^(private|protected)\b/.test(trimmed)) continue;
		if (!PUBLIC_MEMBER_PATTERN.test(trimmed)) continue;
		if (!signatures.includes(trimmed)) signatures.push(trimmed);
		if (signatures.length >= maxSignatures) break;
	}
	if (signatures.length === 0) return header;
	return `${header} ${signatures.join('; ')}; … }`;
};

/**
 * Class bodies in bundled d.ts files lead with private fields — noise that
 * pushes the public surface out of the snippet budget. Drop private/protected
 * member lines so the snippet shows the usable API first. Line-based (not a
 * multiline regex) so declaration bodies can never interact across lines.
 */
const stripNonPublicMembers = (code) => {
	const kept = [];
	for (const line of code.split('\n')) {
		if (/^\s*(private|protected)\b/.test(line)) continue;
		kept.push(line);
	}
	return collapseWhitespace(kept.join('\n'));
};

const escapeRegExp = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

const extractDeclarationSnippets = (content, valueExports, typeExports, kinds) => {
	const snippets = [];
	const publicNames = new Set([...valueExports, ...typeExports]);
	for (const [name, kind] of kinds) {
		if (!publicNames.has(name)) continue;
		const matcher = DECLARATION_PATTERNS.find((entry) => entry.kind === kind);
		if (!matcher) continue;
		const pattern = matcher.pattern(escapeRegExp(name));
		const match = pattern.exec(content);
		if (!match || match.index === undefined) continue;
		let collapsed;
		if (kind === 'class') {
			collapsed = skimClassSnippet(content, match.index) ?? stripNonPublicMembers(extractBalancedSnippet(content, match.index));
		} else {
			collapsed = collapseWhitespace(extractBalancedSnippet(content, match.index));
		}
		if (collapsed.length < name.length + 4) continue;
		snippets.push({
			name,
			kind,
			code:
				collapsed.length > SNIPPET_MAX_LENGTH
					? `${collapsed.slice(0, SNIPPET_MAX_LENGTH - 1)}…`
					: collapsed,
		});
	}
	snippets.sort((a, b) => a.name.localeCompare(b.name));
	return snippets;
};

const readDeclarationFile = (filePath) => {
	try {
		return fs.readFileSync(filePath, 'utf8');
	} catch {
		return null;
	}
};

// ---------------------------------------------------------------------------
// Package scanning
// ---------------------------------------------------------------------------

const listPackageDirs = () => {
	if (!fs.existsSync(packagesDir)) return [];
	return fs
		.readdirSync(packagesDir, { withFileTypes: true })
		.filter((entry) => entry.isDirectory())
		.map((entry) => entry.name)
		.sort((a, b) => a.localeCompare(b));
};

const resolveSubpathDeclaration = (packageDir, exportValue) => {
	const candidates = [];
	if (typeof exportValue === 'string') {
		candidates.push(exportValue);
	} else if (exportValue && typeof exportValue === 'object' && !Array.isArray(exportValue)) {
		if (typeof exportValue.types === 'string') candidates.push(exportValue.types);
		if (typeof exportValue.import === 'string') candidates.push(exportValue.import);
		if (typeof exportValue.default === 'string') candidates.push(exportValue.default);
	}
	for (const candidate of candidates) {
		if (typeof candidate !== 'string' || !candidate.endsWith('.d.ts')) continue;
		const resolved = path.resolve(packageDir, candidate);
		if (fs.existsSync(resolved)) return resolved;
	}
	return null;
};

const readPackageJson = (dirName) => {
	const packageDir = path.resolve(packagesDir, dirName);
	const packageJsonPath = path.resolve(packageDir, 'package.json');
	if (!fs.existsSync(packageJsonPath)) return null;
	const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8'));
	if (typeof packageJson.name !== 'string' || !packageJson.name.startsWith('@axrone/')) {
		return null;
	}
	return { packageDir, packageJson };
};

const resolveAxroneSpecifierDeclaration = (specifier, packageByName) => {
	// '@axrone/name' → root dist/index.d.ts; '@axrone/name/sub' → subpath
	// declaration resolved through the target package.json exports map.
	const match = /^(@axrone\/[^/]+)(\/(.+))?$/.exec(specifier);
	if (!match) return null;
	const target = packageByName.get(match[1]);
	if (!target) return null;
	if (!match[3]) return path.resolve(target.packageDir, 'dist', 'index.d.ts');
	const subKey = `./${match[3]}`;
	const exportsField = target.packageJson.exports;
	if (exportsField && typeof exportsField === 'object' && !Array.isArray(exportsField)) {
		const resolved = resolveSubpathDeclaration(target.packageDir, exportsField[subKey]);
		if (resolved) return resolved;
	}
	for (const candidate of [`${match[3]}.d.ts`, `${match[3]}/index.d.ts`]) {
		const resolved = path.resolve(target.packageDir, 'dist', candidate);
		if (fs.existsSync(resolved)) return resolved;
	}
	return null;
};

const resolveRelativeDeclaration = (fromFile, specifier) => {
	const base = path.resolve(path.dirname(fromFile), specifier);
	for (const candidate of [`${base}.d.ts`, path.join(base, 'index.d.ts')]) {
		if (fs.existsSync(candidate)) return candidate;
	}
	return null;
};

const scanPackage = (dirName, packageByName) => {
	const meta = readPackageJson(dirName);
	if (!meta) return null;
	const { packageDir, packageJson } = meta;

	const rootDeclarationPath = path.resolve(packageDir, 'dist', 'index.d.ts');
	const rootContent = readDeclarationFile(rootDeclarationPath);
	if (!rootContent) return null; // Not built yet — skipped, caller warns.

	const root = parseDeclarationFile(rootContent);
	const snippets = new Map();
	for (const snippet of root.snippets) {
		if (!snippets.has(snippet.name)) {
			snippets.set(snippet.name, { ...snippet, package: packageJson.name });
		}
	}
	const subpaths = [];
	const pendingResolutions = [
		{ parsed: root, file: rootDeclarationPath, mergeIntoRoot: false },
	];
	const exportsField = packageJson.exports;
	if (exportsField && typeof exportsField === 'object' && !Array.isArray(exportsField)) {
		const keys = Object.keys(exportsField)
			.filter((key) => key.startsWith('./'))
			.sort();
		for (const key of keys) {
			const declarationPath = resolveSubpathDeclaration(packageDir, exportsField[key]);
			if (!declarationPath) continue;
			const content = readDeclarationFile(declarationPath);
			if (!content) continue;
			const parsed = parseDeclarationFile(content);
			const entry = {
				path: `${packageJson.name}/${key.slice(2)}`,
				exports: parsed.valueExports,
				typeExports: parsed.typeExports,
			};
			for (const snippet of parsed.snippets) {
				if (!snippets.has(snippet.name)) {
					snippets.set(snippet.name, { ...snippet, package: packageJson.name });
				}
			}
			subpaths.push(entry);
			pendingResolutions.push({ parsed, file: declarationPath, mergeIntoRoot: false, subpathEntry: entry });
			for (const name of parsed.valueExports) root.valueExports.add(name);
			for (const name of parsed.typeExports) root.typeExports.add(name);
			if (parsed.hasDefaultExport) root.hasDefaultExport = true;
		}
	}

	// Second pass: resolve `export *` and `export { X } from '...'` against
	// same-package files and sibling @axrone packages (cycle-guarded).
	const visited = new Set();
	const resolveInto = (parsed, file, valueTarget, typeTarget) => {
		const visit = (current, currentFile) => {
			const key = `${currentFile}`;
			if (visited.has(key)) return;
			visited.add(key);
			for (const source of current.starSources) {
				const resolved = source.startsWith('.')
					? resolveRelativeDeclaration(currentFile, source)
					: resolveAxroneSpecifierDeclaration(source, packageByName);
				if (!resolved) continue;
				const content = readDeclarationFile(resolved);
				if (!content) continue;
				const target = parseDeclarationFile(content);
				for (const name of target.valueExports) valueTarget.add(name);
				for (const name of target.typeExports) typeTarget.add(name);
				visit(target, resolved);
			}
			for (const reexport of current.namedReexports) {
				const resolved = reexport.from.startsWith('.')
					? resolveRelativeDeclaration(currentFile, reexport.from)
					: resolveAxroneSpecifierDeclaration(reexport.from, packageByName);
				// Unresolvable targets (external deps) still contribute their
				// names — the import surface genuinely offers them.
				if (!resolved) {
					for (const name of reexport.names) {
						(reexport.typeOnly ? typeTarget : valueTarget).add(name);
					}
					continue;
				}
				const content = readDeclarationFile(resolved);
				if (!content) continue;
				const target = parseDeclarationFile(content);
				for (const name of reexport.names) {
					if (target.valueExports.has(name) || target.typeExports.has(name)) {
						(reexport.typeOnly ? typeTarget : valueTarget).add(name);
					} else {
						// Re-exported through chains (e.g. umbrella facades):
						// accept declared names to avoid false negatives.
						(reexport.typeOnly ? typeTarget : valueTarget).add(name);
					}
				}
				visit(target, resolved);
			}
		};
		visit(parsed, file);
	};
	for (const pending of pendingResolutions) {
		if (pending.subpathEntry) {
			resolveInto(pending.parsed, pending.file, pending.subpathEntry.exports, pending.subpathEntry.typeExports);
		} else {
			resolveInto(pending.parsed, pending.file, root.valueExports, root.typeExports);
		}
	}

	return {
		name: packageJson.name,
		version: typeof packageJson.version === 'string' ? packageJson.version : '0.0.1',
		description: typeof packageJson.description === 'string' ? packageJson.description : '',
		exports: [...root.valueExports].sort(),
		typeExports: [...root.typeExports].sort(),
		hasDefaultExport: root.hasDefaultExport,
		subpaths: subpaths.map((entry) => ({
			path: entry.path,
			exports: [...entry.exports].sort(),
			typeExports: [...entry.typeExports].sort(),
		})),
		snippets: [...snippets.values()].sort((a, b) => a.name.localeCompare(b.name)),
		declarationKinds: root.kinds,
		packageDir,
	};
};

// ---------------------------------------------------------------------------
// Component-kind extraction (grounded in registry manifests + component files)
// ---------------------------------------------------------------------------

const collectBuiltInKinds = () => {
	// scene-*-registry.ts `builtIns: [...]` arrays are the authoritative list
	// of scene component kinds per profile.
	const found = [];
	const sceneRuntimeSrc = path.resolve(packagesDir, 'scene-runtime', 'src');
	if (!fs.existsSync(sceneRuntimeSrc)) return found;
	for (const file of fs.readdirSync(sceneRuntimeSrc)) {
		if (!/^scene-.*registry\.ts$/.test(file)) continue;
		const content = fs.readFileSync(path.resolve(sceneRuntimeSrc, file), 'utf8');
		for (const match of collectMatches(content, /builtIns\s*:\s*\[([\s\S]*?)\]/g)) {
			for (const name of collectMatches(match[1], /['"]([A-Za-z_$][\w$]*)['"]/g)) {
				found.push({ kind: name[1], package: '@axrone/scene-runtime' });
			}
		}
	}
	return found;
};

const isComponentFile = (fileName) =>
	fileName.endsWith('.ts') &&
	!fileName.endsWith('.test.ts') &&
	!fileName.endsWith('.spec.ts') &&
	!fileName.endsWith('.d.ts');

const collectComponentFileKinds = () => {
	// Grounded component detection: a class counts as a component kind only
	// when it transitively extends the ECS `Component` base (directly, via an
	// intermediate base such as Collider3D/Joint3D, or via an `@script`
	// decorator). This excludes builders, errors, binders and systems that
	// merely live next to components.
	const found = [];
	const walk = (dir, packageName) => {
		let entries;
		try {
			entries = fs.readdirSync(dir, { withFileTypes: true });
		} catch {
			return;
		}
		for (const entry of entries) {
			const full = path.resolve(dir, entry.name);
			if (entry.isDirectory()) {
				if (entry.name === '__tests__' || entry.name === 'node_modules' || entry.name === 'dist') {
					continue;
				}
				walk(full, packageName);
				continue;
			}
			if (!isComponentFile(entry.name)) continue;
			let content;
			try {
				content = fs.readFileSync(full, 'utf8');
			} catch {
				continue;
			}
			const hasScriptDecorator = /@script\s*\(/.test(content);
			for (const match of collectMatches(
				content,
				/export\s+(?:abstract\s+)?class\s+([A-Za-z_$][\w$]*)(?:\s+extends\s+([A-Za-z_$][\w$]*))?/g,
			)) {
				found.push({
					kind: match[1],
					superClass: match[2] ?? null,
					scriptDecorated: hasScriptDecorator,
					package: packageName,
				});
			}
		}
	};
	for (const dirName of listPackageDirs()) {
		const packageJsonPath = path.resolve(packagesDir, dirName, 'package.json');
		if (!fs.existsSync(packageJsonPath)) continue;
		let packageName = null;
		try {
			packageName = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8')).name;
		} catch {
			continue;
		}
		if (typeof packageName !== 'string' || !packageName.startsWith('@axrone/')) continue;
		walk(path.resolve(packagesDir, dirName, 'src'), packageName);
	}

	const superClassOf = new Map(found.map((entry) => [entry.kind, entry.superClass]));
	const extendsComponent = (kind, seen = new Set()) => {
		if (kind === 'Component') return true;
		if (seen.has(kind)) return false;
		seen.add(kind);
		const parent = superClassOf.get(kind);
		if (!parent) return false;
		return extendsComponent(parent, seen);
	};

	return found.filter(
		(entry) => entry.scriptDecorated || extendsComponent(entry.kind),
	);
};

// ---------------------------------------------------------------------------
// Manifest assembly
// ---------------------------------------------------------------------------

const buildManifest = () => {
	const packages = [];
	const skipped = [];
	const kindsByPackage = new Map();
	// Two phases: cross-package `export *` resolution needs every package's
	// directory up front.
	const packageByName = new Map();
	for (const dirName of listPackageDirs()) {
		const meta = readPackageJson(dirName);
		if (meta) packageByName.set(meta.packageJson.name, meta);
	}
	for (const dirName of listPackageDirs()) {
		const scanned = scanPackage(dirName, packageByName);
		if (!scanned) {
			const packageJsonPath = path.resolve(packagesDir, dirName, 'package.json');
			if (fs.existsSync(packageJsonPath)) skipped.push(dirName);
			continue;
		}
		const { declarationKinds, packageDir, ...entry } = scanned;
		packages.push(entry);
		kindsByPackage.set(entry.name, declarationKinds);
	}

	const componentKindMap = new Map();
	for (const { kind, package: packageName } of [
		...collectBuiltInKinds(),
		...collectComponentFileKinds(),
	]) {
		if (!componentKindMap.has(kind)) componentKindMap.set(kind, packageName);
	}
	const componentKinds = [...componentKindMap.entries()]
		.map(([kind, packageName]) => ({ kind, package: packageName }))
		.sort((a, b) => a.kind.localeCompare(b.kind));

	const symbols = [];
	const snippets = [];
	for (const pkg of packages) {
		const kinds = kindsByPackage.get(pkg.name) ?? new Map();
		const publicNames = new Set([...pkg.exports, ...pkg.typeExports]);
		for (const name of publicNames) {
			symbols.push({
				name,
				package: pkg.name,
				kind: kinds.get(name) ?? 'unknown',
			});
		}
		for (const snippet of pkg.snippets ?? []) {
			snippets.push(snippet);
		}
		delete pkg.snippets;
	}
	symbols.sort(
		(a, b) => a.name.localeCompare(b.name) || a.package.localeCompare(b.package),
	);
	snippets.sort(
		(a, b) => a.name.localeCompare(b.name) || a.package.localeCompare(b.package),
	);

	let engineVersion = '0.0.1';
	try {
		engineVersion = JSON.parse(fs.readFileSync(path.resolve(webDir, 'package.json'), 'utf8')).version ?? engineVersion;
	} catch {
		// Keep default.
	}

	return {
		manifest: {
			version: MANIFEST_VERSION,
			generatedAt: new Date().toISOString(),
			engineVersion,
			packages,
			componentKinds,
			symbols,
			snippets,
		},
		skipped,
	};
};

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

const parseArgs = (argv) => {
	const args = { output: defaultOutputPath, check: false };
	for (let i = 0; i < argv.length; i++) {
		if (argv[i] === '--output' && argv[i + 1]) {
			args.output = path.resolve(argv[i + 1]);
			i += 1;
		} else if (argv[i] === '--check') {
			args.check = true;
		}
	}
	return args;
};

const main = () => {
	const args = parseArgs(process.argv.slice(2));
	const { manifest, skipped } = buildManifest();
	const serialized = `${JSON.stringify(manifest, null, 2)}\n`;

	if (args.check) {
		let current = null;
		try {
			current = fs.readFileSync(args.output, 'utf8');
		} catch {
			current = null;
		}
		const normalize = (text) =>
			text ? JSON.stringify({ ...JSON.parse(text), generatedAt: undefined }) : null;
		if (normalize(current) !== normalize(serialized)) {
			console.error(
				`[generate-engine-knowledge] Drift detected: ${path.relative(process.cwd(), args.output)} is stale. ` +
					`Regenerate with: node ./scripts/generate-engine-knowledge.mjs`,
			);
			if (skipped.length > 0) {
				console.error(`[generate-engine-knowledge] Skipped (not built): ${skipped.join(', ')}`);
			}
			process.exit(1);
		}
			console.log(
				`[generate-engine-knowledge] OK: ${manifest.packages.length} packages, ` +
					`${manifest.symbols.length} symbols, ${manifest.snippets.length} snippets, ${manifest.componentKinds.length} component kinds.`,
			);
		return;
	}

	// Deterministic output: a fresh timestamp alone must not dirty the file
	// (avoids phantom rebuilds / noisy diffs on every Editor pretest run).
	let current = null;
	try {
		current = fs.readFileSync(args.output, 'utf8');
	} catch {
		current = null;
	}
	const normalize = (text) =>
		JSON.stringify({ ...JSON.parse(text), generatedAt: undefined });
	if (current) {
		try {
			if (normalize(current) === normalize(serialized)) {
				console.log(
					`[generate-engine-knowledge] Up to date: ${path.relative(process.cwd(), args.output)} ` +
						`(${manifest.packages.length} packages, ${manifest.symbols.length} symbols, ` +
						`${manifest.snippets.length} snippets, ${manifest.componentKinds.length} component kinds).`,
				);
				if (skipped.length > 0) {
					console.warn(
						`[generate-engine-knowledge] Warning: skipped unbuilt packages: ${skipped.join(', ')}. ` +
							`Run \`yarn build\` for complete coverage.`,
					);
				}
				return;
			}
		} catch {
			// Unparseable existing file — overwrite below.
		}
	}
	fs.mkdirSync(path.dirname(args.output), { recursive: true });
	fs.writeFileSync(args.output, serialized, 'utf8');
	console.log(
		`[generate-engine-knowledge] Wrote ${path.relative(process.cwd(), args.output)}: ` +
			`${manifest.packages.length} packages, ${manifest.symbols.length} symbols, ` +
			`${manifest.snippets.length} snippets, ${manifest.componentKinds.length} component kinds.`,
	);
	if (skipped.length > 0) {
		console.warn(
			`[generate-engine-knowledge] Warning: skipped unbuilt packages: ${skipped.join(', ')}. ` +
				`Run \`yarn build\` for complete coverage.`,
		);
	}
};

main();
