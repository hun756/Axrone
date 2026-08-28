import { describe, expect, it } from 'vitest';
import { UIRuntime, checkboxToggleController } from '@axrone/ui';
import { WebGL2UIRenderer } from '../index';
import assetJson from '../../../../../../Assets/test.ui.json';

/**
 * Scratch benchmark: where does the per-keystroke preview cost actually go?
 * Mirrors UICanvasPreview.renderPreview's rebuild path against the real project
 * asset so the numbers are the ones the editor pays, not an invented fixture.
 */
describe('scratch: label keystroke cost profile', () => {
	it('times rebuild vs relayout vs snapshot', () => {
		const out: string[] = [];
		const runtime = new UIRuntime();
		runtime.registry.register(checkboxToggleController);
		const asset = assetJson as never;
		runtime.loadFromAsset(asset);
		runtime.commit();

		const time = (label: string, iterations: number, fn: () => void): void => {
			fn();
			const t0 = performance.now();
			for (let i = 0; i < iterations; i += 1) fn();
			const ms = (performance.now() - t0) / iterations;
			out.push(`${label}: ${ms.toFixed(3)} ms/op (${iterations}x)`);
		};

		time('loadFromAsset (full destroy+rebuild)', 30, () => runtime.loadFromAsset(asset));
		time('commit (layout+commands, clean)', 30, () => {
			runtime.commit();
		});

		// The stage the first run never measured: real WebGL2 upload + draw.
		const canvas = (window as any).createTestCanvas(1600, 900) as HTMLCanvasElement;
		const gl = (window as any).createWebGLContext(canvas, {
			alpha: true,
			antialias: true,
			premultipliedAlpha: true,
			preserveDrawingBuffer: true,
		}) as WebGL2RenderingContext;
		const renderer = new WebGL2UIRenderer({ gl });
		const buildCameraFrame = () => {
			const frame = runtime.commit();
			const scale = 4.83;
			return {
				viewportWidth: canvas.width,
				viewportHeight: canvas.height,
				metrics: frame.metrics,
				commands: frame.commands.map((cmd) => ({
					...cmd,
					transform: [scale, 0, 0, scale, 0, 0] as [number, number, number, number, number, number],
				})),
			} as never;
		};
		time('renderer.render (WebGL, editor zoom)', 60, () => renderer.render(buildCameraFrame()));
		time('keystroke pipeline: load + commit + map + render', 40, () => {
			runtime.loadFromAsset(asset);
			renderer.render(buildCameraFrame());
		});

		// Minimap cost: canvas resize + one rect per widget.
		const mini = document.createElement('canvas');
		time('drawMinimap shape: resize + 34 fillRect', 200, () => {
			mini.width = Math.round(176 * (window.devicePixelRatio || 1));
			mini.height = Math.round(112 * (window.devicePixelRatio || 1));
			const ctx = mini.getContext('2d');
			if (!ctx) return;
			for (let i = 0; i < 34; i += 1) ctx.fillRect(i, i, 4, 4);
		});
		renderer.dispose();

		// Mutate one text value the way a keystroke does, then rebuild.
		let n = 0;
		time('keystroke cycle: patch text + loadFromAsset + commit', 30, () => {
			const mutated = JSON.parse(JSON.stringify(assetJson));
			const walk = (w: { children?: unknown[]; text?: { value?: string } }): void => {
				if (w.text) w.text.value = `Checkbox${n++}`;
				(w.children as never[] | undefined)?.forEach(walk);
			};
			walk(mutated.root);
			runtime.loadFromAsset(mutated);
			runtime.commit();
		});

		// How expensive is the undo snapshot alone?
		time('JSON.stringify whole asset (undo snapshot)', 200, () => JSON.stringify(assetJson));

		// Count the tree so the per-node cost is interpretable.
		let widgets = 0;
		const count = (w: { children?: unknown[] }): void => {
			widgets += 1;
			(w.children as never[] | undefined)?.forEach(count);
		};
		count((assetJson as { root: { children?: unknown[] } }).root);
		out.push(`widgets in asset: ${widgets}`);

		// eslint-disable-next-line no-console
		console.log(`\n@@BENCH@@\n${out.join('\n')}\n@@END@@`);
		expect(widgets).toBeGreaterThan(0);
		runtime.dispose();
	});
});
