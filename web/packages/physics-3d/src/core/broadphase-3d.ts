import { AABB3D } from '@axrone/geometry';
import type { IVec3Like } from '@axrone/numeric';

/** Ray-AABB slab intersection. Returns fraction or -1 if miss. */
function _rayAabbSlab(
    ox: number, oy: number, oz: number,
    dx: number, dy: number, dz: number,
    minX: number, minY: number, minZ: number,
    maxX: number, maxY: number, maxZ: number,
    maxFrac: number
): number {
    let tmin = 0;
    let tmax = maxFrac;
    // X slab
    if (Math.abs(dx) < 1e-12) {
        if (ox < minX || ox > maxX) return -1;
    } else {
        const invD = 1.0 / dx;
        let t1 = (minX - ox) * invD;
        let t2 = (maxX - ox) * invD;
        if (t1 > t2) { const tmp = t1; t1 = t2; t2 = tmp; }
        if (t1 > tmin) tmin = t1;
        if (t2 < tmax) tmax = t2;
        if (tmin > tmax) return -1;
    }
    // Y slab
    if (Math.abs(dy) < 1e-12) {
        if (oy < minY || oy > maxY) return -1;
    } else {
        const invD = 1.0 / dy;
        let t1 = (minY - oy) * invD;
        let t2 = (maxY - oy) * invD;
        if (t1 > t2) { const tmp = t1; t1 = t2; t2 = tmp; }
        if (t1 > tmin) tmin = t1;
        if (t2 < tmax) tmax = t2;
        if (tmin > tmax) return -1;
    }
    // Z slab
    if (Math.abs(dz) < 1e-12) {
        if (oz < minZ || oz > maxZ) return -1;
    } else {
        const invD = 1.0 / dz;
        let t1 = (minZ - oz) * invD;
        let t2 = (maxZ - oz) * invD;
        if (t1 > t2) { const tmp = t1; t1 = t2; t2 = tmp; }
        if (t1 > tmin) tmin = t1;
        if (t2 < tmax) tmax = t2;
        if (tmin > tmax) return -1;
    }
    return tmin;
}

interface TreeNode3D<TUserData> {
    id: number;
    aabb: AABB3D;
    userData: TUserData | null;
    parent: number;
    child1: number;
    child2: number;
    height: number;
}

const NULL_NODE = -1;

function toAabb3D(union: { min: Readonly<IVec3Like>; max: Readonly<IVec3Like> }): AABB3D {
    return new AABB3D(union.min, union.max);
}

export class DynamicAABBTree3D<TUserData = unknown> {
    private _nodes: TreeNode3D<TUserData>[];
    private _root: number = NULL_NODE;
    private _freeList: number = 0;
    private _nodeCount: number = 0;
    private _nodeCapacity: number;
    private readonly _fatAabbMargin: number = 0.1;
    /** Number of active leaf (proxy) nodes. */
    private _leafCount: number = 0;

    constructor(initialCapacity: number = 1024) {
        this._nodeCapacity = initialCapacity;
        this._nodes = new Array(initialCapacity);
        const empty = AABB3D.EMPTY;
        for (let i = 0; i < initialCapacity - 1; ++i) {
            this._nodes[i] = {
                id: i,
                aabb: AABB3D.from(empty),
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

    createProxy(aabb: AABB3D, userData: TUserData): number {
        const proxyId = this._allocateNode();
        const fatAabb = AABB3D.from(aabb);
        fatAabb.expand(this._fatAabbMargin);
        this._nodes[proxyId].aabb = fatAabb;
        this._nodes[proxyId].userData = userData;
        this._nodes[proxyId].height = 0;
        this._insertLeaf(proxyId);
        this._leafCount++;
        return proxyId;
    }

    destroyProxy(proxyId: number): void {
        this._removeLeaf(proxyId);
        this._freeNode(proxyId);
        this._leafCount--;
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

    getUserData(proxyId: number): TUserData | null {
        return this._nodes[proxyId].userData;
    }

    getAABB(proxyId: number): AABB3D {
        return this._nodes[proxyId].aabb;
    }

    getHeight(): number {
        if (this._root === NULL_NODE) return 0;
        return this._nodes[this._root].height;
    }

    get leafCount(): number {
        return this._leafCount;
    }

    /**
     * Quality ratio: actualHeight / max(1, ceil(log2(leafCount+1))).
     * 1.0 = perfectly balanced; higher = more degenerate.
     */
    getTreeQuality(): number {
        if (this._root === NULL_NODE || this._leafCount <= 1) return 1.0;
        const optimal = Math.ceil(Math.log2(this._leafCount + 1));
        return this._nodes[this._root].height / Math.max(1, optimal);
    }

    /**
     * Balance metric in [0, 1]: averages per-node min/max child-height ratios
     * across all internal nodes. 1.0 = every subtree pair has equal height.
     */
    getTreeBalance(): number {
        if (this._root === NULL_NODE || this._leafCount <= 1) return 1.0;
        const acc = this._sumBalanceRatios(this._root);
        return acc.n === 0 ? 1.0 : acc.sum / acc.n;
    }

    private _sumBalanceRatios(nid: number): { sum: number; n: number } {
        const nd = this._nodes[nid];
        if (nd.child1 === NULL_NODE) return { sum: 0, n: 0 };
        const a = this._nodes[nd.child1].height;
        const b = this._nodes[nd.child2].height;
        const hi = Math.max(a, b);
        const lo = Math.min(a, b);
        const l = this._sumBalanceRatios(nd.child1);
        const r = this._sumBalanceRatios(nd.child2);
        return { sum: (hi > 0 ? lo / hi : 1.0) + l.sum + r.sum, n: 1 + l.n + r.n };
    }

    get nodeCount(): number {
        return this._nodeCount;
    }

    /**
     * BVH-backed ray cast. Traverses the tree testing ray against node AABBs.
     * Callback receives leaf userData and the hit fraction; return new max fraction
     * to clip further tests, or -1 to abort.
     */
    rayCast(
        origin: Readonly<IVec3Like>, direction: Readonly<IVec3Like>, maxDistance: number,
        callback: (userData: TUserData, fraction: number) => number
    ): void {
        if (this._root === NULL_NODE) return;
        const stack: number[] = [this._root];
        let currentMax = maxDistance;
        while (stack.length > 0) {
            const nodeId = stack.pop()!;
            if (nodeId === NULL_NODE) continue;
            const node = this._nodes[nodeId];
            const frac = _rayAabbSlab(
                origin.x, origin.y, origin.z,
                direction.x, direction.y, direction.z,
                node.aabb.min.x, node.aabb.min.y, node.aabb.min.z,
                node.aabb.max.x, node.aabb.max.y, node.aabb.max.z,
                currentMax
            );
            if (frac < 0) continue;
            if (node.child1 === NULL_NODE) {
                // Leaf
                if (node.userData !== null) {
                    const newMax = callback(node.userData, frac);
                    if (newMax < 0) return;
                    currentMax = newMax;
                }
            } else {
                stack.push(node.child1);
                stack.push(node.child2);
            }
        }
    }

    /**
     * BVH-backed AABB query. Returns all leaves whose fat AABB overlaps the query AABB.
     */
    queryAABBAll(aabb: AABB3D, callback: (userData: TUserData) => boolean): void {
        this.query((proxyId) => {
            const ud = this._nodes[proxyId].userData;
            if (ud !== null) return callback(ud);
            return true;
        }, aabb);
    }

    /**
     * BVH-backed point query. Tests if a point lies within any leaf's fat AABB.
     */
    queryPointAll(point: Readonly<IVec3Like>, callback: (userData: TUserData) => boolean): void {
        // Create a degenerate AABB at the point
        const ptAabb = new AABB3D(point, point);
        this.queryAABBAll(ptAabb, callback);
    }

    private _allocateNode(): number {
        if (this._freeList === NULL_NODE) {
            const oldCapacity = this._nodeCapacity;
            this._nodeCapacity *= 2;
            const newNodes = new Array<TreeNode3D<TUserData>>(this._nodeCapacity);
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
