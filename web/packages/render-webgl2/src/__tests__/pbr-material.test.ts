import { describe, expect, it } from 'vitest';
import { PBRMaterialComponent } from '../material/pbr-material';

describe('PBRMaterialComponent clearcoat support', () => {
    it('round-trips glTF clearcoat extension metadata', () => {
        const material = new PBRMaterialComponent();

        material.copyFromGLTF({
            extensions: {
                KHR_materials_clearcoat: {
                    clearcoatFactor: 0.7,
                    clearcoatRoughnessFactor: 0.2,
                    clearcoatNormalTexture: {
                        scale: 0.8,
                    },
                },
            },
            alphaMode: 'BLEND',
        });

        expect(material.clearcoatFactor).toBe(0.7);
        expect(material.clearcoatRoughnessFactor).toBe(0.2);
        expect(material.clearcoatNormalScale).toBe(0.8);
        expect(material.alphaMode).toBe('BLEND');

        const exported = material.exportToGLTF();
        expect(exported.extensions?.KHR_materials_clearcoat).toMatchObject({
            clearcoatFactor: 0.7,
            clearcoatRoughnessFactor: 0.2,
        });
    });
});