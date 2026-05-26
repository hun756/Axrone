import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import {
    MaterialManager,
    MaterialType,
    BlendMode,
    type MaterialConfig,
} from '../material';

function resetManager(): void {
    MaterialManager.destroy();
}
const originalConsole = { ...console };

function muteConsole(): void {
    vi.spyOn(console, 'log').mockImplementation(() => {});
    vi.spyOn(console, 'warn').mockImplementation(() => {});
    vi.spyOn(console, 'error').mockImplementation(() => {});
}

function restoreConsole(): void {
    vi.restoreAllMocks();
}

describe('MaterialManager – Singleton Lifecycle', () => {
    beforeEach(() => {
        resetManager();
        muteConsole();
    });

    afterEach(() => {
        resetManager();
        restoreConsole();
    });

    it('MM-001 – getInstance returns the same instance', () => {
        const a = MaterialManager.getInstance();
        const b = MaterialManager.getInstance();
        expect(a).toBe(b);
    });

    it('MM-002 – destroy clears the singleton', () => {
        const mgr = MaterialManager.getInstance();
        MaterialManager.destroy();
        const mgr2 = MaterialManager.getInstance();
        expect(mgr2).not.toBe(mgr);
    });

    it('MM-003 – destroy is idempotent (no error when called twice)', () => {
        MaterialManager.getInstance();
        expect(() => {
            MaterialManager.destroy();
            MaterialManager.destroy();
        }).not.toThrow();
    });
});

describe('MaterialManager – Type Registry', () => {
    let mgr: MaterialManager;

    beforeEach(() => {
        resetManager();
        muteConsole();
        mgr = MaterialManager.getInstance();
    });

    afterEach(() => {
        resetManager();
        restoreConsole();
    });

    it('MM-010 – built-in types (STANDARD, PBR) are registered', () => {
        const types = mgr.getRegisteredTypes();
        expect(types).toContain(MaterialType.STANDARD);
        expect(types).toContain(MaterialType.PBR);
    });

    it('MM-011 – getTypeInfo returns info for registered type', () => {
        const info = mgr.getTypeInfo(MaterialType.STANDARD);
        expect(info).not.toBeNull();
        expect(info!.type).toBe(MaterialType.STANDARD);
    });

    it('MM-012 – getTypeInfo returns null for unregistered type', () => {
        const info = mgr.getTypeInfo(MaterialType.UNLIT);
        expect(info).toBeNull();
    });

    it('MM-013 – registerMaterialType registers a new type', () => {
        class CustomMaterial {
            id = 'custom';
            materialType = MaterialType.CUSTOM;
        }
        mgr.registerMaterialType(
            MaterialType.CUSTOM,
            CustomMaterial as any,
            { materialType: MaterialType.CUSTOM } as any,
            'Custom material'
        );
        expect(mgr.getRegisteredTypes()).toContain(MaterialType.CUSTOM);
    });

    it('MM-014 – unregisterMaterialType removes a type', () => {
        const result = mgr.unregisterMaterialType(MaterialType.STANDARD);
        expect(result).toBe(true);
        expect(mgr.getTypeInfo(MaterialType.STANDARD)).toBeNull();
    });

    it('MM-015 – unregisterMaterialType returns false for unregistered type', () => {
        const result = mgr.unregisterMaterialType(MaterialType.UNLIT);
        expect(result).toBe(false);
    });
});

describe('MaterialManager – Material CRUD', () => {
    let mgr: MaterialManager;

    beforeEach(() => {
        resetManager();
        muteConsole();
        mgr = MaterialManager.getInstance();
    });

    afterEach(() => {
        resetManager();
        restoreConsole();
    });

    it('MM-020 – createMaterial creates a material', () => {
        const mat = mgr.createMaterial(MaterialType.STANDARD);
        expect(mat).not.toBeNull();
        expect(mat!.materialType).toBe(MaterialType.STANDARD);
    });

    it('MM-021 – createMaterial with unknown type returns null', () => {
        const mat = mgr.createMaterial(MaterialType.UNLIT);
        expect(mat).toBeNull();
    });

    it('MM-022 – createMaterial with config merges defaults', () => {
        const mat = mgr.createMaterial(MaterialType.STANDARD, {
            blendMode: BlendMode.ADDITIVE,
        });
        expect(mat!.blendMode).toBe(BlendMode.ADDITIVE);
    });

    it('MM-023 – getMaterial retrieves by id', () => {
        const mat = mgr.createMaterial(MaterialType.STANDARD);
        const found = mgr.getMaterial(mat!.id);
        expect(found).toBe(mat);
    });

    it('MM-024 – getMaterial returns null for unknown id', () => {
        expect(mgr.getMaterial('nonexistent')).toBeNull();
    });

    it('MM-025 – getAllMaterials returns all created materials', () => {
        mgr.createMaterial(MaterialType.STANDARD);
        mgr.createMaterial(MaterialType.STANDARD);
        mgr.createMaterial(MaterialType.PBR);
        expect(mgr.getAllMaterials()).toHaveLength(3);
    });

    it('MM-026 – getMaterialsByType filters correctly', () => {
        mgr.createMaterial(MaterialType.STANDARD);
        mgr.createMaterial(MaterialType.PBR);
        mgr.createMaterial(MaterialType.STANDARD);
        expect(mgr.getMaterialsByType(MaterialType.STANDARD)).toHaveLength(2);
        expect(mgr.getMaterialsByType(MaterialType.PBR)).toHaveLength(1);
    });

    it('MM-027 – getMaterialsByType returns empty array for unused type', () => {
        expect(mgr.getMaterialsByType(MaterialType.PBR)).toHaveLength(0);
    });

    it('MM-028 – destroyMaterial removes material', () => {
        const mat = mgr.createMaterial(MaterialType.STANDARD);
        const id = mat!.id;
        const result = mgr.destroyMaterial(id);
        expect(result).toBe(true);
        expect(mgr.getMaterial(id)).toBeNull();
    });

    it('MM-029 – destroyMaterial returns false for unknown id', () => {
        expect(mgr.destroyMaterial('nonexistent')).toBe(false);
    });

    it('MM-030 – destroyMaterial removes from type tracking', () => {
        const mat = mgr.createMaterial(MaterialType.STANDARD);
        mgr.destroyMaterial(mat!.id);
        expect(mgr.getMaterialsByType(MaterialType.STANDARD)).toHaveLength(0);
    });
});

describe('MaterialManager – Cloning', () => {
    let mgr: MaterialManager;

    beforeEach(() => {
        resetManager();
        muteConsole();
        mgr = MaterialManager.getInstance();
    });

    afterEach(() => {
        resetManager();
        restoreConsole();
    });

    it('MM-040 – cloneMaterial creates a new material with same data', () => {
        const original = mgr.createMaterial(MaterialType.STANDARD, {
            blendMode: BlendMode.ADDITIVE,
        });
        const clone = mgr.cloneMaterial(original!.id);
        expect(clone).not.toBeNull();
        expect(clone).not.toBe(original);
        expect(clone!.blendMode).toBe(BlendMode.ADDITIVE);
    });

    it('MM-041 – cloneMaterial returns null for unknown id', () => {
        expect(mgr.cloneMaterial('nonexistent')).toBeNull();
    });

    it('MM-042 – cloned material is independently modifiable', () => {
        const original = mgr.createMaterial(MaterialType.STANDARD);
        const clone = mgr.cloneMaterial(original!.id);

        original!.blendMode = BlendMode.ALPHA_BLEND;
        expect(clone!.blendMode).toBe(BlendMode.OPAQUE);
    });

    it('MM-043 – cloneMaterial with newId', () => {
        const original = mgr.createMaterial(MaterialType.STANDARD);
        const clone = mgr.cloneMaterial(original!.id, 'custom-id');
        expect(clone!.id).toBe('custom-id');
    });
});

describe('MaterialManager – Reference Counting', () => {
    let mgr: MaterialManager;

    beforeEach(() => {
        resetManager();
        muteConsole();
        mgr = MaterialManager.getInstance();
    });

    afterEach(() => {
        resetManager();
        restoreConsole();
    });

    it('MM-050 – initial reference count is 1', () => {
        const mat = mgr.createMaterial(MaterialType.STANDARD);
        expect(mgr.getReferenceCount(mat!.id)).toBe(1);
    });

    it('MM-051 – addReference increments count', () => {
        const mat = mgr.createMaterial(MaterialType.STANDARD);
        mgr.addReference(mat!.id);
        expect(mgr.getReferenceCount(mat!.id)).toBe(2);
    });

    it('MM-052 – removeReference decrements count', () => {
        const mat = mgr.createMaterial(MaterialType.STANDARD);
        mgr.addReference(mat!.id);
        const result = mgr.removeReference(mat!.id);
        expect(result).toBe(true);
        expect(mgr.getReferenceCount(mat!.id)).toBe(1);
    });

    it('MM-053 – removeReference at 1 returns false (eligible for cleanup)', () => {
        const mat = mgr.createMaterial(MaterialType.STANDARD);
        const result = mgr.removeReference(mat!.id);
        expect(result).toBe(false);
        expect(mgr.getReferenceCount(mat!.id)).toBe(0);
    });

    it('MM-054 – addReference for non-existent returns false', () => {
        expect(mgr.addReference('nonexistent')).toBe(false);
    });

    it('MM-055 – removeReference for non-existent returns false', () => {
        expect(mgr.removeReference('nonexistent')).toBe(false);
    });

    it('MM-056 – getReferenceCount returns 0 for unknown id', () => {
        expect(mgr.getReferenceCount('nonexistent')).toBe(0);
    });
});

describe('MaterialManager – Event System', () => {
    let mgr: MaterialManager;

    beforeEach(() => {
        resetManager();
        muteConsole();
        mgr = MaterialManager.getInstance();
    });

    afterEach(() => {
        resetManager();
        restoreConsole();
    });

    it('MM-060 – materialCreated event fires on create', () => {
        const listener = vi.fn();
        mgr.addEventListener('materialCreated', listener);
        mgr.createMaterial(MaterialType.STANDARD);
        expect(listener).toHaveBeenCalledTimes(1);
        expect(listener.mock.calls[0][0].type).toBe(MaterialType.STANDARD);
    });

    it('MM-061 – materialDestroyed event fires on destroy', () => {
        const listener = vi.fn();
        const mat = mgr.createMaterial(MaterialType.STANDARD);
        mgr.addEventListener('materialDestroyed', listener);
        mgr.destroyMaterial(mat!.id);
        expect(listener).toHaveBeenCalledTimes(1);
    });

    it('MM-062 – materialCloned event fires on clone', () => {
        const listener = vi.fn();
        const mat = mgr.createMaterial(MaterialType.STANDARD);
        mgr.addEventListener('materialCloned', listener);
        mgr.cloneMaterial(mat!.id);
        expect(listener).toHaveBeenCalledTimes(1);
    });

    it('MM-063 – removeEventListener stops events', () => {
        const listener = vi.fn();
        mgr.addEventListener('materialCreated', listener);
        mgr.removeEventListener('materialCreated', listener);
        mgr.createMaterial(MaterialType.STANDARD);
        expect(listener).not.toHaveBeenCalled();
    });

    it('MM-064 – multiple listeners all fire', () => {
        const l1 = vi.fn();
        const l2 = vi.fn();
        mgr.addEventListener('materialCreated', l1);
        mgr.addEventListener('materialCreated', l2);
        mgr.createMaterial(MaterialType.STANDARD);
        expect(l1).toHaveBeenCalledTimes(1);
        expect(l2).toHaveBeenCalledTimes(1);
    });
});

describe('MaterialManager – Search Functions', () => {
    let mgr: MaterialManager;

    beforeEach(() => {
        resetManager();
        muteConsole();
        mgr = MaterialManager.getInstance();
    });

    afterEach(() => {
        resetManager();
        restoreConsole();
    });

    it('MM-070 – findMaterialsByProperty finds by custom property', () => {
        const mat = mgr.createMaterial(MaterialType.STANDARD);
        mat!.setProperty('_CustomProp', 'test');
        const results = mgr.findMaterialsByProperty('_CustomProp');
        expect(results.length).toBeGreaterThanOrEqual(1);
        expect(results).toContain(mat);
    });

    it('MM-071 – findMaterialsByProperty with value filter', () => {
        const mat = mgr.createMaterial(MaterialType.STANDARD);
        mat!.setProperty('_Custom', 42);
        const results = mgr.findMaterialsByProperty('_Custom', 42);
        expect(results).toContain(mat);
    });

    it('MM-072 – findMaterialsByProperty with wrong value returns empty', () => {
        mgr.createMaterial(MaterialType.STANDARD);
        const results = mgr.findMaterialsByProperty('_Color', 'wrong');
        expect(results).toHaveLength(0);
    });

    it('MM-073 – findMaterialsByKeyword finds by keyword', () => {
        const mat = mgr.createMaterial(MaterialType.STANDARD);
        mat!.enableKeyword('_TEST');
        const results = mgr.findMaterialsByKeyword('_TEST');
        expect(results).toContain(mat);
    });

    it('MM-074 – findMaterialsByKeyword returns empty for unused keyword', () => {
        const results = mgr.findMaterialsByKeyword('_NONEXISTENT');
        expect(results).toHaveLength(0);
    });
});

describe('MaterialManager – Statistics', () => {
    let mgr: MaterialManager;

    beforeEach(() => {
        resetManager();
        muteConsole();
        mgr = MaterialManager.getInstance();
    });

    afterEach(() => {
        resetManager();
        restoreConsole();
    });

    it('MM-080 – getStatistics returns correct totalMaterials', () => {
        mgr.createMaterial(MaterialType.STANDARD);
        mgr.createMaterial(MaterialType.PBR);
        const stats = mgr.getStatistics();
        expect(stats.totalMaterials).toBe(2);
    });

    it('MM-081 – getStatistics returns materialsByType', () => {
        mgr.createMaterial(MaterialType.STANDARD);
        mgr.createMaterial(MaterialType.STANDARD);
        const stats = mgr.getStatistics();
        expect(stats.materialsByType[MaterialType.STANDARD]).toBe(2);
    });

    it('MM-082 – getStatistics returns totalReferences', () => {
        mgr.createMaterial(MaterialType.STANDARD);
        mgr.createMaterial(MaterialType.STANDARD);
        const stats = mgr.getStatistics();
        expect(stats.totalReferences).toBe(2); // 1 ref each
    });

    it('MM-083 – getStatistics returns memoryUsage estimate', () => {
        mgr.createMaterial(MaterialType.STANDARD);
        const stats = mgr.getStatistics();
        expect(stats.memoryUsage).toBeGreaterThan(0);
    });
});

describe('MaterialManager – Convenience Functions', () => {
    beforeEach(() => {
        resetManager();
        muteConsole();
    });

    afterEach(() => {
        resetManager();
        restoreConsole();
    });

    it('MM-090 – standalone createMaterial works', async () => {
        const { createMaterial } = await import('../material/material-manager');
        const mat = createMaterial(MaterialType.STANDARD);
        expect(mat).not.toBeNull();
    });

    it('MM-091 – standalone getMaterial works', async () => {
        const { createMaterial, getMaterial } = await import('../material/material-manager');
        const mat = createMaterial(MaterialType.STANDARD);
        expect(getMaterial(mat!.id)).toBe(mat);
    });

    it('MM-092 – standalone destroyMaterial works', async () => {
        const { createMaterial, destroyMaterial, getMaterial } = await import('../material/material-manager');
        const mat = createMaterial(MaterialType.STANDARD);
        const id = mat!.id;
        expect(destroyMaterial(id)).toBe(true);
        expect(getMaterial(id)).toBeNull();
    });

    it('MM-093 – standalone cloneMaterial works', async () => {
        const { createMaterial, cloneMaterial } = await import('../material/material-manager');
        const mat = createMaterial(MaterialType.STANDARD);
        const clone = cloneMaterial(mat!.id);
        expect(clone).not.toBeNull();
        expect(clone).not.toBe(mat);
    });
});

describe('MaterialManager – Edge Cases', () => {
    let mgr: MaterialManager;

    beforeEach(() => {
        resetManager();
        muteConsole();
        mgr = MaterialManager.getInstance();
    });

    afterEach(() => {
        resetManager();
        restoreConsole();
    });

    it('MM-100 – createMaterial after destroy returns new instance', () => {
        const mat1 = mgr.createMaterial(MaterialType.STANDARD);
        mgr.destroyMaterial(mat1!.id);
        const mat2 = mgr.createMaterial(MaterialType.STANDARD);
        expect(mat2!.id).not.toBe(mat1!.id);
    });

    it('MM-101 – multiple materials can coexist of same type', () => {
        const m1 = mgr.createMaterial(MaterialType.STANDARD);
        const m2 = mgr.createMaterial(MaterialType.STANDARD);
        const m3 = mgr.createMaterial(MaterialType.STANDARD);
        expect(mgr.getAllMaterials()).toHaveLength(3);
        expect(new Set([m1!.id, m2!.id, m3!.id]).size).toBe(3);
    });

    it('MM-102 – destroying last material of type removes type tracking', () => {
        const mat = mgr.createMaterial(MaterialType.STANDARD);
        mgr.destroyMaterial(mat!.id);
        expect(mgr.getMaterialsByType(MaterialType.STANDARD)).toHaveLength(0);
    });
});