import { AABB3D } from '@axrone/geometry';
import type { IVec3Like } from '@axrone/numeric';

interface TreeNode3D {
    id: number;
    aabb: AABB3D;
    userData: any;
    parent: number;
    child1: number;
    child2: number;
    height: number;
}

const NULL_NODE = -1;

/** Wraps IAABB union into AABB3D since getUnion returns the interface type */
function toAabb3D(union: { min: Readonly<IVec3Like>; max: Readonly<IVec3Like> }): AABB3D {
    return new AABB3D(union.min, union.max);
}

export class DynamicAABBTree3D {
    private _nodes: TreeNode3D[];
    private _root: number = NULL_NODE;
    private _freeList: number = 0;
    private _nodeCount: number = 0;
    private _nodeCapacity: number;
    private readonly _fatAabbMargin: number = 0.1;

    constructor(initialCapacity: number = 1024) {
        this._nodeCapacity = initialCapacity;
        this._nodes = new Array(initialCapacity);
        const empty = AABB3D.EMPTY;
        for (let i = 0; i < initialCapacity - 1; ++i) {
            const cloned = AABB3D.from(empty);
            this._nodes[i] = {
                id: i,
                aabb: cloned,
                userData: null,
                parent: i + 1,
                child1: NULL_NODE,
                child2: NULL_NODE,
                height: -1,
            };
        }
        this._nodes[initialCapacity - 1] = {
            id: initialCapacity - 1,
            aabb: AABB3D.from(empty),
            userData: null,
            parent: NULL_NODE,
            child1: NULL_NODE,
            child2: NULL_NODE,
            height: -1,
        };
    }

    createProxy(aabb: AABB3D, userData: any): number {
        const proxyId = this._allocateNode();
        const fatAabb = AABB3D.from(aabb);
        fatAabb.expand(this._fatAabbMargin);
        this._nodes[proxyId].aabb = fatAabb;
        this._nodes[proxyId].userData = userData;
        this._nodes[proxyId].height = 0;
        this._insertLeaf(proxyId);
        return proxyId;
    }

    destroyProxy(proxyId: number): void {
        this._removeLeaf(proxyId);
        this._freeNode(proxyId);
    }

    moveProxy(proxyId: number, aabb: AABB3D, displacement: IVec3Like): boolean {
        const node = this._nodes[proxyId];
        if (node.aabb.containsAABB(aabb)) {
            return false;
        }
        this._removeLeaf(proxyId);
        const fatAabb = AABB3D.from(aabb);
        fatAabb.expand(this._fatAabbMargin);
        const dx = displacement.x * 2.0;
        const dy = displacement.y * 2.0;
        const dz = displacement.z * 2.0;
        node.aabb = new AABB3D(
            {
                x: fatAabb.min.x + (dx < 0 ? dx : 0),
                y: fatAabb.min.y + (dy < 0 ? dy : 0),
                z: fatAabb.min.z + (dz < 0 ? dz : 0),
            },
            {
                x: fatAabb.max.x + (dx > 0 ? dx : 0),
                y: fatAabb.max.y + (dy > 0 ? dy : 0),
                z: fatAabb.max.z + (dz > 0 ? dz : 0),
            }
        );
        this._insertLeaf(proxyId);
        return true;
    }

    query(callback: (proxyId: number) => boolean, aabb: AABB3D): void {
        const stack: number[] = [this._root];
        while (stack.length > 0) {
            const nodeId = stack.pop()!;
            if (nodeId === NULL_NODE) continue;
            const node = this._nodes[nodeId];
            if (node.aabb.intersectsAABB(aabb)) {
                if (node.child1 === NULL_NODE) {
                    const proceed = callback(nodeId);
                    if (!proceed) return;
                } else {
                    stack.push(node.child1);
                    stack.push(node.child2);
                }
            }
        }
    }

    queryPairs(callback: (proxyIdA: number, proxyIdB: number) => boolean): void {
        if (this._root === NULL_NODE) return;
        const stack: number[] = [this._root];
        while (stack.length > 0) {
            const nodeId = stack.pop()!;
            if (nodeId === NULL_NODE) continue;
            const node = this._nodes[nodeId];
            if (node.child1 === NULL_NODE) continue;
            stack.push(node.child1);
            stack.push(node.child2);
            this._queryPairsInternal(node.child1, node.child2, callback);
        }
    }

    private _queryPairsInternal(
        nodeIdA: number,
        nodeIdB: number,
        callback: (proxyIdA: number, proxyIdB: number) => boolean
    ): void {
        const nodeA = this._nodes[nodeIdA];
        const nodeB = this._nodes[nodeIdB];
        if (!nodeA.aabb.intersectsAABB(nodeB.aabb)) return;
        const isLeafA = nodeA.child1 === NULL_NODE;
        const isLeafB = nodeB.child1 === NULL_NODE;
        if (isLeafA && isLeafB) {
            if (!callback(nodeIdA, nodeIdB)) return;
            return;
        }
        if (isLeafA) {
            this._queryPairsInternal(nodeIdA, nodeB.child1, callback);
            this._queryPairsInternal(nodeIdA, nodeB.child2, callback);
            return;
        }
        if (isLeafB) {
            this._queryPairsInternal(nodeA.child1, nodeIdB, callback);
            this._queryPairsInternal(nodeA.child2, nodeIdB, callback);
            return;
        }
        const areaA = nodeA.aabb.surfaceArea;
        const areaB = nodeB.aabb.surfaceArea;
        if (areaA > areaB) {
            this._queryPairsInternal(nodeA.child1, nodeIdB, callback);
            this._queryPairsInternal(nodeA.child2, nodeIdB, callback);
        } else {
            this._queryPairsInternal(nodeIdA, nodeB.child1, callback);
            this._queryPairsInternal(nodeIdA, nodeB.child2, callback);
        }
    }

    getUserData(proxyId: number): any {
        return this._nodes[proxyId].userData;
    }

    getAABB(proxyId: number): AABB3D {
        return this._nodes[proxyId].aabb;
    }

    getHeight(): number {
        if (this._root === NULL_NODE) return 0;
        return this._nodes[this._root].height;
    }

    get nodeCount(): number {
        return this._nodeCount;
    }

    private _allocateNode(): number {
        if (this._freeList === NULL_NODE) {
            const oldCapacity = this._nodeCapacity;
            this._nodeCapacity *= 2;
            const newNodes = new Array(this._nodeCapacity);
            for (let i = 0; i < oldCapacity; i++) {
                newNodes[i] = this._nodes[i];
            }
            const empty = AABB3D.EMPTY;
            for (let i = oldCapacity; i < this._nodeCapacity - 1; i++) {
                newNodes[i] = {
                    id: i,
                    aabb: AABB3D.from(empty),
                    userData: null,
                    parent: i + 1,
                    child1: NULL_NODE,
                    child2: NULL_NODE,
                    height: -1,
                };
            }
            newNodes[this._nodeCapacity - 1] = {
                id: this._nodeCapacity - 1,
                aabb: AABB3D.from(empty),
                userData: null,
                parent: NULL_NODE,
                child1: NULL_NODE,
                child2: NULL_NODE,
                height: -1,
            };
            this._nodes = newNodes;
            this._freeList = oldCapacity;
        }
        const nodeId = this._freeList;
        this._freeList = this._nodes[nodeId].parent;
        this._nodes[nodeId].parent = NULL_NODE;
        this._nodes[nodeId].child1 = NULL_NODE;
        this._nodes[nodeId].child2 = NULL_NODE;
        this._nodes[nodeId].height = 0;
        this._nodes[nodeId].userData = null;
        this._nodeCount++;
        return nodeId;
    }

    private _freeNode(nodeId: number): void {
        this._nodes[nodeId].parent = this._freeList;
        this._nodes[nodeId].height = -1;
        this._freeList = nodeId;
        this._nodeCount--;
    }

    private _insertLeaf(leaf: number): void {
        if (this._root === NULL_NODE) {
            this._root = leaf;
            this._nodes[this._root].parent = NULL_NODE;
            return;
        }
        const leafAabb = this._nodes[leaf].aabb;
        let index = this._root;
        while (this._nodes[index].child1 !== NULL_NODE) {
            const node = this._nodes[index];
            const child1 = node.child1;
            const child2 = node.child2;
            const area = node.aabb.surfaceArea;
            const combinedAabb = toAabb3D(node.aabb.getUnion(leafAabb));
            const combinedArea = combinedAabb.surfaceArea;
            const cost = 2.0 * combinedArea;
            const inheritanceCost = 2.0 * (combinedArea - area);
            let cost1: number;
            const combinedAabb1 = toAabb3D(this._nodes[child1].aabb.getUnion(leafAabb));
            if (this._nodes[child1].child1 === NULL_NODE) {
                cost1 = combinedAabb1.surfaceArea + inheritanceCost;
            } else {
                cost1 = combinedAabb1.surfaceArea - this._nodes[child1].aabb.surfaceArea + inheritanceCost;
            }
            let cost2: number;
            const combinedAabb2 = toAabb3D(this._nodes[child2].aabb.getUnion(leafAabb));
            if (this._nodes[child2].child1 === NULL_NODE) {
                cost2 = combinedAabb2.surfaceArea + inheritanceCost;
            } else {
                cost2 = combinedAabb2.surfaceArea - this._nodes[child2].aabb.surfaceArea + inheritanceCost;
            }
            if (cost < cost1 && cost < cost2) {
                break;
            }
            if (cost1 < cost2) {
                index = child1;
            } else {
                index = child2;
            }
        }
        const sibling = index;
        const oldParent = this._nodes[sibling].parent;
        const newParent = this._allocateNode();
        this._nodes[newParent].parent = oldParent;
        this._nodes[newParent].userData = null;
        this._nodes[newParent].aabb = toAabb3D(leafAabb.getUnion(this._nodes[sibling].aabb));
        this._nodes[newParent].height = this._nodes[sibling].height + 1;
        if (oldParent !== NULL_NODE) {
            if (this._nodes[oldParent].child1 === sibling) {
                this._nodes[oldParent].child1 = newParent;
            } else {
                this._nodes[oldParent].child2 = newParent;
            }
            this._nodes[newParent].child1 = sibling;
            this._nodes[newParent].child2 = leaf;
            this._nodes[sibling].parent = newParent;
            this._nodes[leaf].parent = newParent;
        } else {
            this._nodes[newParent].child1 = sibling;
            this._nodes[newParent].child2 = leaf;
            this._nodes[sibling].parent = newParent;
            this._nodes[leaf].parent = newParent;
            this._root = newParent;
        }
        let walkIndex = this._nodes[leaf].parent;
        while (walkIndex !== NULL_NODE) {
            const walkNode = this._nodes[walkIndex];
            const child1 = walkNode.child1;
            const child2 = walkNode.child2;
            walkNode.height = 1 + Math.max(this._nodes[child1].height, this._nodes[child2].height);
            walkNode.aabb = toAabb3D(this._nodes[child1].aabb.getUnion(this._nodes[child2].aabb));
            walkIndex = walkNode.parent;
        }
    }

    private _removeLeaf(leaf: number): void {
        if (leaf === this._root) {
            this._root = NULL_NODE;
            return;
        }
        const parent = this._nodes[leaf].parent;
        const grandParent = this._nodes[parent].parent;
        const sibling =
            this._nodes[parent].child1 === leaf
                ? this._nodes[parent].child2
                : this._nodes[parent].child1;
        if (grandParent !== NULL_NODE) {
            if (this._nodes[grandParent].child1 === parent) {
                this._nodes[grandParent].child1 = sibling;
            } else {
                this._nodes[grandParent].child2 = sibling;
            }
            this._nodes[sibling].parent = grandParent;
            this._freeNode(parent);
            let index = grandParent;
            while (index !== NULL_NODE) {
                const node = this._nodes[index];
                const child1 = node.child1;
                const child2 = node.child2;
                node.aabb = toAabb3D(this._nodes[child1].aabb.getUnion(this._nodes[child2].aabb));
                node.height = 1 + Math.max(this._nodes[child1].height, this._nodes[child2].height);
                index = node.parent;
            }
        } else {
            this._root = sibling;
            this._nodes[sibling].parent = NULL_NODE;
            this._freeNode(parent);
        }
    }
}