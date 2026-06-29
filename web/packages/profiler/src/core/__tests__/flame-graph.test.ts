import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { FlameGraphNode, FlameGraphBuilder } from '../flame-graph';

describe('FlameGraphNode', () => {
    let node: FlameGraphNode;

    beforeEach(() => {
        node = new FlameGraphNode('testFunc');
    });

    afterEach(async () => {
        await node[Symbol.asyncDispose]();
    });

    it('should create with a name', () => {
        expect(node.name).toBe('testFunc');
    });

    it('should accumulate duration', () => {
        node.addDuration(1000n);
        node.addDuration(2000n);
        expect(node.getDurationNs()).toBe(3000n);
    });

    it('should add unique signatures', () => {
        node.addSignature('sig1');
        node.addSignature('sig2');
        node.addSignature('sig1');
        const sigs = node.getUniqueSignatures();
        expect(sigs).toHaveLength(2);
        expect(sigs).toContain('sig1');
        expect(sigs).toContain('sig2');
    });

    it('should return empty signatures after dispose', async () => {
        node.addSignature('sig1');
        await node[Symbol.asyncDispose]();
        expect(node.getUniqueSignatures()).toHaveLength(0);
    });

    it('should not add duration after dispose', async () => {
        await node[Symbol.asyncDispose]();
        node.addDuration(1000n);
        expect(node.getDurationNs()).toBe(0n);
    });

    it('should not add signature after dispose', async () => {
        await node[Symbol.asyncDispose]();
        node.addSignature('sig1');
        expect(node.getUniqueSignatures()).toHaveLength(0);
    });
});

describe('FlameGraphBuilder', () => {
    let builder: FlameGraphBuilder;

    beforeEach(() => {
        builder = new FlameGraphBuilder();
    });

    afterEach(async () => {
        await builder[Symbol.asyncDispose]();
    });

    it('should create a node on frame record', () => {
        builder.recordFrame('frame1');
        builder.recordFrame('frame1', 100n);
        const roots = builder.getRoots();
        expect(roots).toHaveLength(1);
        expect(roots[0].name).toBe('frame1');
        expect(roots[0].getDurationNs()).toBe(100n);
    });

    it('should track multiple frames', () => {
        builder.recordFrame('frameA', 100n);
        builder.recordFrame('frameB', 200n);
        const roots = builder.getRoots();
        expect(roots).toHaveLength(2);
    });

    it('should add signature to a frame', () => {
        builder.recordSignature('frame1', 'sig-value');
        const roots = builder.getRoots();
        expect(roots[0].getUniqueSignatures()).toContain('sig-value');
    });

    it('should not add signature without a value', () => {
        builder.recordFrame('frame1');
        builder.recordSignature('frame1');
        const roots = builder.getRoots();
        expect(roots[0].getUniqueSignatures()).toHaveLength(0);
    });

    it('should not record frames after dispose', async () => {
        await builder[Symbol.asyncDispose]();
        builder.recordFrame('test', 100n);
        expect(builder.getRoots()).toHaveLength(0);
    });

    it('should return empty roots on fresh builder', () => {
        expect(builder.getRoots()).toHaveLength(0);
    });

    it('should not record new frames after dispose', async () => {
        builder.recordFrame('frame1');
        await builder[Symbol.asyncDispose]();
        builder.recordFrame('frame2', 100n);
        const roots = builder.getRoots();
        expect(roots).toHaveLength(1);
        expect(roots[0].name).toBe('frame1');
    });
});
