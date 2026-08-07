import { describe, expect, it, vi } from 'vitest';
import { SceneMaterialObservables } from '@axrone/scene-3d';

describe('SceneMaterialObservables', () => {
    it('creates all subject streams', () => {
        const observables = new SceneMaterialObservables();

        expect(observables.materialCreated).toBeDefined();
        expect(observables.materialDeleted).toBeDefined();
        expect(observables.materialCloned).toBeDefined();
        expect(observables.uniformChanged).toBeDefined();
        expect(observables.keywordChanged).toBeDefined();
        expect(observables.textureChanged).toBeDefined();
    });

    it('notifies materialCreated observers', async () => {
        const observables = new SceneMaterialObservables();
        const callback = vi.fn();
        observables.materialCreated.addObserver(callback);

        observables._notifyMaterialCreated('mat/test');

        await new Promise((resolve) => setTimeout(resolve, 0));
        expect(callback).toHaveBeenCalledWith(
            expect.objectContaining({ materialId: 'mat/test' }),
            expect.anything()
        );
    });

    it('notifies materialDeleted observers', async () => {
        const observables = new SceneMaterialObservables();
        const callback = vi.fn();
        observables.materialDeleted.addObserver(callback);

        observables._notifyMaterialDeleted('mat/test');

        await new Promise((resolve) => setTimeout(resolve, 0));
        expect(callback).toHaveBeenCalledWith(
            expect.objectContaining({ materialId: 'mat/test' }),
            expect.anything()
        );
    });

    it('notifies materialCloned observers', async () => {
        const observables = new SceneMaterialObservables();
        const callback = vi.fn();
        observables.materialCloned.addObserver(callback);

        observables._notifyMaterialCloned('mat/source', 'mat/clone');

        await new Promise((resolve) => setTimeout(resolve, 0));
        expect(callback).toHaveBeenCalledWith(
            expect.objectContaining({ sourceId: 'mat/source', cloneId: 'mat/clone' }),
            expect.anything()
        );
    });

    it('notifies uniformChanged observers', async () => {
        const observables = new SceneMaterialObservables();
        const callback = vi.fn();
        observables.uniformChanged.addObserver(callback);

        observables._notifyUniformChanged('mat/test', 'u_Color');

        await new Promise((resolve) => setTimeout(resolve, 0));
        expect(callback).toHaveBeenCalledWith(
            expect.objectContaining({ materialId: 'mat/test', uniformName: 'u_Color' }),
            expect.anything()
        );
    });

    it('notifies keywordChanged observers', async () => {
        const observables = new SceneMaterialObservables();
        const callback = vi.fn();
        observables.keywordChanged.addObserver(callback);

        observables._notifyKeywordChanged('mat/test', 'FOG', true);

        await new Promise((resolve) => setTimeout(resolve, 0));
        expect(callback).toHaveBeenCalledWith(
            expect.objectContaining({
                materialId: 'mat/test',
                keyword: 'FOG',
                enabled: true,
            }),
            expect.anything()
        );
    });

    it('notifies textureChanged observers', async () => {
        const observables = new SceneMaterialObservables();
        const callback = vi.fn();
        observables.textureChanged.addObserver(callback);

        observables._notifyTextureChanged('mat/test', 'u_MainTex');

        await new Promise((resolve) => setTimeout(resolve, 0));
        expect(callback).toHaveBeenCalledWith(
            expect.objectContaining({ materialId: 'mat/test', slotName: 'u_MainTex' }),
            expect.anything()
        );
    });

    it('dispose prevents further observer notifications', async () => {
        const observables = new SceneMaterialObservables();
        const callback = vi.fn();
        observables.materialCreated.addObserver(callback);

        observables.dispose();

        observables._notifyMaterialCreated('mat/test');
        await new Promise((resolve) => setTimeout(resolve, 0));

        expect(callback).not.toHaveBeenCalled();
    });
});
