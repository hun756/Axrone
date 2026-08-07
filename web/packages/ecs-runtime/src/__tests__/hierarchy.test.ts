import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Hierarchy } from '../component-system/components/hierarchy';

/**
 * Helper: assign a fake entity id to a Hierarchy instance so that
 * _linkChild / _unlinkChild guards pass (they check `child.entity`).
 */
const assignEntity = (h: Hierarchy, entity: number): void => {
    (h as any).entity = entity;
};

/** Helper: assign fake actor/world so _emitHierarchyChanged can fire. */
const assignActorContext = (
    h: Hierarchy,
    actor: any,
    world: any
): void => {
    (h as any).actor = actor;
    (h as any).world = world;
};

describe('Hierarchy', () => {
    let root: Hierarchy;
    let childA: Hierarchy;
    let childB: Hierarchy;

    beforeEach(() => {
        root = new Hierarchy();
        childA = new Hierarchy();
        childB = new Hierarchy();

        assignEntity(root, 1);
        assignEntity(childA, 2);
        assignEntity(childB, 3);
    });

    // ─── Parent-child linking ───────────────────────────────────────

    describe('setParent / parent-child linking', () => {
        it('establishes parent-child relationship', () => {
            childA.setParent(root);

            expect(childA.parent).toBe(root);
            expect(root.children).toContain(childA);
            expect(root.childCount).toBe(1);
        });

        it('increments version on both parent and child when linking', () => {
            const versionBefore = root.version;
            childA.setParent(root);

            expect(root.version).toBeGreaterThan(versionBefore);
            expect(childA.version).toBeGreaterThan(0);
        });

        it('is a no-op when setting the same parent again', () => {
            childA.setParent(root);
            const versionAfterFirst = childA.version;

            childA.setParent(root);
            expect(childA.version).toBe(versionAfterFirst);
        });

        it('is a no-op when setting self as parent', () => {
            root.setParent(root);
            expect(root.parent).toBeUndefined();
            expect(root.childCount).toBe(0);
        });
    });

    // ─── Cycle detection ────────────────────────────────────────────

    describe('cycle detection', () => {
        it('prevents setting parent to a direct child', () => {
            childA.setParent(root);
            root.setParent(childA);

            expect(root.parent).toBeUndefined();
        });

        it('prevents setting parent to a deep descendant', () => {
            childA.setParent(root);
            childB.setParent(childA);

            root.setParent(childB);
            expect(root.parent).toBeUndefined();
        });
    });

    // ─── Unlinking ──────────────────────────────────────────────────

    describe('unlinking', () => {
        it('removes child from parent when setting parent to undefined', () => {
            childA.setParent(root);
            expect(root.childCount).toBe(1);

            childA.setParent(undefined);
            expect(childA.parent).toBeUndefined();
            expect(root.childCount).toBe(0);
        });

        it('unlinks from old parent when re-parenting', () => {
            const newParent = new Hierarchy();
            assignEntity(newParent, 10);

            childA.setParent(root);
            expect(root.childCount).toBe(1);

            childA.setParent(newParent);
            expect(root.childCount).toBe(0);
            expect(newParent.childCount).toBe(1);
            expect(childA.parent).toBe(newParent);
        });
    });

    // ─── isAncestorOf / isDescendantOf ──────────────────────────────

    describe('isAncestorOf / isDescendantOf', () => {
        it('returns true for direct ancestor', () => {
            childA.setParent(root);
            expect(root.isAncestorOf(childA)).toBe(true);
        });

        it('returns true for deep ancestor', () => {
            childA.setParent(root);
            childB.setParent(childA);

            expect(root.isAncestorOf(childB)).toBe(true);
        });

        it('returns false for non-ancestor', () => {
            childA.setParent(root);
            expect(childB.isAncestorOf(childA)).toBe(false);
        });

        it('returns false for self', () => {
            expect(root.isAncestorOf(root)).toBe(false);
        });

        it('isDescendantOf is the inverse of isAncestorOf', () => {
            childA.setParent(root);
            expect(childA.isDescendantOf(root)).toBe(true);
            expect(root.isDescendantOf(childA)).toBe(false);
        });
    });

    // ─── getRoot ────────────────────────────────────────────────────

    describe('getRoot', () => {
        it('returns self when already root', () => {
            expect(root.getRoot()).toBe(root);
        });

        it('returns topmost ancestor', () => {
            childA.setParent(root);
            childB.setParent(childA);

            expect(childB.getRoot()).toBe(root);
            expect(childA.getRoot()).toBe(root);
        });
    });

    // ─── getDepth ───────────────────────────────────────────────────

    describe('getDepth', () => {
        it('returns 0 for root', () => {
            expect(root.getDepth()).toBe(0);
        });

        it('returns correct depth for nested hierarchy', () => {
            childA.setParent(root);
            childB.setParent(childA);

            expect(childA.getDepth()).toBe(1);
            expect(childB.getDepth()).toBe(2);
        });
    });

    // ─── getAllDescendants ──────────────────────────────────────────

    describe('getAllDescendants', () => {
        it('returns empty array for leaf node', () => {
            expect(childA.getAllDescendants()).toEqual([]);
        });

        it('returns all descendants in BFS order', () => {
            childA.setParent(root);
            childB.setParent(root);

            const grandChild = new Hierarchy();
            assignEntity(grandChild, 4);
            grandChild.setParent(childA);

            const descendants = root.getAllDescendants();
            expect(descendants).toHaveLength(3);
            expect(descendants).toContain(childA);
            expect(descendants).toContain(childB);
            expect(descendants).toContain(grandChild);

            // BFS order: direct children before grandchildren
            const idxA = descendants.indexOf(childA);
            const idxB = descendants.indexOf(childB);
            const idxGC = descendants.indexOf(grandChild);
            expect(idxA).toBeLessThan(idxGC);
            expect(idxB).toBeLessThan(idxGC);
        });
    });

    // ─── children lazy cache ────────────────────────────────────────

    describe('children lazy cache', () => {
        it('returns same array reference when not dirty', () => {
            childA.setParent(root);
            const first = root.children;
            const second = root.children;

            expect(first).toBe(second);
        });

        it('rebuilds array content after child is added', () => {
            childA.setParent(root);
            const first = root.children;
            expect(first).toHaveLength(1);

            childB.setParent(root);
            // The same array reference is mutated in-place
            expect(root.children).toHaveLength(2);
            expect(root.children).toContain(childA);
            expect(root.children).toContain(childB);
        });
    });

    // ─── childActors lazy cache ─────────────────────────────────────

    describe('childActors lazy cache', () => {
        it('returns empty array when children have no actors', () => {
            childA.setParent(root);
            expect(root.childActors).toEqual([]);
        });

        it('collects actors from children', () => {
            const mockActor = { id: 'actor-a' } as any;
            (childA as any).actor = mockActor;

            childA.setParent(root);
            const actors = root.childActors;

            expect(actors).toContain(mockActor);
        });

        it('rebuilds when version changes', () => {
            childA.setParent(root);
            const first = root.childActors;
            expect(first).toHaveLength(0);

            // Adding a child with an actor should cause rebuild
            const mockActor = { id: 'actor-b' } as any;
            (childB as any).actor = mockActor;
            childB.setParent(root);

            const second = root.childActors;
            // Same array reference, mutated in-place
            expect(second).toHaveLength(1);
            expect(second).toContain(mockActor);
        });
    });

    // ─── childCount ─────────────────────────────────────────────────

    describe('childCount', () => {
        it('starts at 0', () => {
            expect(root.childCount).toBe(0);
        });

        it('increments on link, decrements on unlink', () => {
            childA.setParent(root);
            expect(root.childCount).toBe(1);

            childB.setParent(root);
            expect(root.childCount).toBe(2);

            childA.setParent(undefined);
            expect(root.childCount).toBe(1);
        });
    });

    // ─── parentActor ────────────────────────────────────────────────

    describe('parentActor', () => {
        it('returns undefined when no parent', () => {
            expect(root.parentActor).toBeUndefined();
        });

        it('returns parent actor when parent has one', () => {
            const mockActor = { id: 'parent-actor' } as any;
            (root as any).actor = mockActor;

            childA.setParent(root);
            expect(childA.parentActor).toBe(mockActor);
        });
    });

    // ─── onDestroy ──────────────────────────────────────────────────

    describe('onDestroy', () => {
        it('orphans all children', () => {
            childA.setParent(root);
            childB.setParent(root);

            root.onDestroy();

            expect(childA.parent).toBeUndefined();
            expect(childB.parent).toBeUndefined();
        });

        it('unlinks from parent', () => {
            childA.setParent(root);
            root.onDestroy();

            // childA was orphaned above, but root's parent was undefined anyway
            expect(root.parent).toBeUndefined();
        });

        it('clears internal state', () => {
            childA.setParent(root);
            root.onDestroy();

            expect(root.childCount).toBe(0);
            expect(root.children).toEqual([]);
            expect(root.version).toBe(0);
        });

        it('unlinks from its own parent', () => {
            childA.setParent(root);
            childA.onDestroy();

            expect(root.childCount).toBe(0);
            expect(childA.parent).toBeUndefined();
        });
    });

    // ─── _linkChild guards ──────────────────────────────────────────

    describe('_linkChild guards', () => {
        it('skips linking child without entity', () => {
            const noEntityChild = new Hierarchy();
            // entity is undefined by default

            root.setParent(undefined); // ensure clean state
            // Directly call setParent which triggers _linkChild on root
            noEntityChild.setParent(root);

            // _linkChild checks child.entity; since it's undefined, child is NOT linked
            expect(root.childCount).toBe(0);
        });

        it('skips duplicate link for same entity+child', () => {
            childA.setParent(root);
            const versionAfterFirst = root.version;

            // Try linking same child again (same entity)
            // setParent would be no-op since parent is already root, so we test via _linkChild
            (root as any)._linkChild(childA);
            expect(root.version).toBe(versionAfterFirst);
        });
    });

    // ─── _unlinkChild guards ────────────────────────────────────────

    describe('_unlinkChild guards', () => {
        it('is no-op for child without entity', () => {
            const noEntityChild = new Hierarchy();
            const versionBefore = root.version;

            (root as any)._unlinkChild(noEntityChild);
            expect(root.version).toBe(versionBefore);
        });

        it('is no-op for unknown entity', () => {
            const unknown = new Hierarchy();
            assignEntity(unknown, 999);

            const versionBefore = root.version;
            (root as any)._unlinkChild(unknown);
            expect(root.version).toBe(versionBefore);
        });
    });

    // ─── _emitHierarchyChanged ──────────────────────────────────────

    describe('_emitHierarchyChanged', () => {
        it('skips when world is not set', () => {
            // No world/actor/entity set, should not throw
            expect(() => childA.setParent(root)).not.toThrow();
        });

        it('emits TransformHierarchyChanged when context is available', () => {
            const mockWorld = { emitSync: vi.fn(), registry: {} };
            const mockActor = { id: 'test-actor', getComponent: () => undefined };

            assignActorContext(childA, mockActor, mockWorld);

            childA.setParent(root);

            expect(mockWorld.emitSync).toHaveBeenCalledWith(
                'TransformHierarchyChanged',
                expect.objectContaining({
                    entity: childA.entity,
                    actor: mockActor,
                })
            );
        });

        it('uses self as component fallback when no Transform', () => {
            const mockWorld = { emitSync: vi.fn(), registry: {} };
            const mockActor = { id: 'test-actor', getComponent: () => undefined };

            assignActorContext(childA, mockActor, mockWorld);

            childA.setParent(root);

            const emittedData = mockWorld.emitSync.mock.calls[0]?.[1];
            // When _getTransform returns undefined, component falls back to `this` (childA)
            expect(emittedData?.component).toBe(childA);
        });
    });

    // ─── _markSpatialTreeDirty ──────────────────────────────────────

    describe('_markSpatialTreeDirty', () => {
        it('calls markHierarchyDirty on transform when available', () => {
            const mockTransform = { markHierarchyDirty: vi.fn() };
            const mockActor = {
                id: 'child-actor',
                getComponent: () => mockTransform,
            };
            const mockWorld = { emitSync: vi.fn(), registry: { Transform: class {} } };

            // _markSpatialTreeDirty is called on `this` (the hierarchy calling setParent)
            assignActorContext(childA, mockActor, mockWorld);

            childA.setParent(root);

            expect(mockTransform.markHierarchyDirty).toHaveBeenCalled();
        });

        it('does not throw when transform has no markHierarchyDirty', () => {
            const mockTransform = {}; // no markHierarchyDirty
            const mockActor = {
                id: 'test-actor',
                getComponent: () => mockTransform,
            };
            const mockWorld = { emitSync: vi.fn(), registry: { Transform: class {} } };

            assignActorContext(root, mockActor, mockWorld);

            expect(() => childA.setParent(root)).not.toThrow();
        });
    });

    // ─── parent setter ──────────────────────────────────────────────

    describe('parent setter', () => {
        it('delegates to setParent', () => {
            childA.parent = root;
            expect(childA.parent).toBe(root);
            expect(root.children).toContain(childA);
        });

        it('can clear parent via undefined', () => {
            childA.parent = root;
            childA.parent = undefined;
            expect(childA.parent).toBeUndefined();
            expect(root.childCount).toBe(0);
        });
    });
});
