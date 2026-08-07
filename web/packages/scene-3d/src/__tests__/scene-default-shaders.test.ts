import { describe, expect, it } from 'vitest';
import { createUnlitColorShaderDefinition } from '@axrone/scene-3d';

describe('createUnlitColorShaderDefinition', () => {
	it('returns a shader definition with the default unlit-color id', () => {
		const definition = createUnlitColorShaderDefinition();

		expect(definition.id).toBe('Scene/UnlitColor');
	});

	it('accepts a custom id override', () => {
		const definition = createUnlitColorShaderDefinition('Custom/FlatColor');

		expect(definition.id).toBe('Custom/FlatColor');
	});

	it('maps the position semantic to the a_Position attribute', () => {
		const definition = createUnlitColorShaderDefinition();

		expect(definition.attributes).toBeDefined();
		expect(definition.attributes?.position).toBe('a_Position');
	});

	it('produces compiled vertex and fragment source strings', () => {
		const definition = createUnlitColorShaderDefinition();

		expect(typeof definition.vertexSource).toBe('string');
		expect(definition.vertexSource!.length).toBeGreaterThan(0);
		expect(typeof definition.fragmentSource).toBe('string');
		expect(definition.fragmentSource!.length).toBeGreaterThan(0);
	});

	it('carries the four documented uniforms in the resolved list', () => {
		const definition = createUnlitColorShaderDefinition();

		expect(definition.uniforms).toBeDefined();
		const uniforms = definition.uniforms ?? [];
		expect(uniforms).toContain('u_Model');
		expect(uniforms).toContain('u_View');
		expect(uniforms).toContain('u_Projection');
		expect(uniforms).toContain('u_Color');
	});

	it('preserves the source effect with correct property scopes', () => {
		const definition = createUnlitColorShaderDefinition();
		const effect = definition.effect;

		expect(effect).toBeDefined();
		const properties = effect?.properties ?? [];
		const scopeByName = Object.fromEntries(properties.map((p) => [p.name, p.scope]));

		expect(scopeByName['u_Model']).toBe('object');
		expect(scopeByName['u_View']).toBe('camera');
		expect(scopeByName['u_Projection']).toBe('camera');
		expect(scopeByName['u_Color']).toBe('material');
	});

	it('applies the documented render-state defaults', () => {
		const definition = createUnlitColorShaderDefinition();

		expect(definition.depthTest).toBe(true);
		expect(definition.cull).toBe(true);
		expect(definition.blend).toBe(false);
	});

	it('uses highp precision in the fragment stage', () => {
		const definition = createUnlitColorShaderDefinition();
		const effect = definition.effect;

		expect(effect).toBeDefined();
		expect(effect?.fragment?.precision).toBe('highp');
	});

	it('declares a single vec4 color output in the fragment stage', () => {
		const definition = createUnlitColorShaderDefinition();
		const outputs = definition.effect?.fragment?.outputs ?? [];

		expect(outputs).toEqual([
			expect.objectContaining({ name: 'o_Color', type: 'vec4' }),
		]);
	});
});
