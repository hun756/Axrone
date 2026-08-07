import { describe, expect, it } from 'vitest';
import {
    createWebGL2RenderPassExecutorId,
    isWebGL2RenderPassExecutorDescriptor,
    defineWebGL2RenderPassExecutor,
} from '../pipeline-contracts';
import type {
    WebGL2RenderPassExecutorDefinition,
    WebGL2AnyRenderPassExecutorDefinition,
} from '../pipeline-contracts';

describe('createWebGL2RenderPassExecutorId', () => {
    it('creates a branded id string from kind and name', () => {
        const id = createWebGL2RenderPassExecutorId('opaque', 'default');
        expect(id).toBe('opaque:default');
    });

    it('works with different pass kinds', () => {
        expect(createWebGL2RenderPassExecutorId('shadow', 'csm')).toBe('shadow:csm');
        expect(createWebGL2RenderPassExecutorId('post-process', 'bloom')).toBe('post-process:bloom');
        expect(createWebGL2RenderPassExecutorId('present', 'main')).toBe('present:main');
    });
});

describe('isWebGL2RenderPassExecutorDescriptor', () => {
    it('returns true for descriptor (has id)', () => {
        const descriptor = {
            kind: 'opaque' as const,
            name: 'test',
            id: createWebGL2RenderPassExecutorId('opaque', 'test'),
            priority: 0,
            execute: () => {},
        };
        expect(isWebGL2RenderPassExecutorDescriptor(descriptor)).toBe(true);
    });

    it('returns false for definition (no id)', () => {
        const definition = {
            kind: 'opaque' as const,
            name: 'test',
            execute: () => {},
        };
        expect(isWebGL2RenderPassExecutorDescriptor(definition)).toBe(false);
    });
});

describe('defineWebGL2RenderPassExecutor', () => {
    it('creates a descriptor with id and default priority', () => {
        const definition: WebGL2AnyRenderPassExecutorDefinition = {
            kind: 'opaque',
            name: 'default',
            execute: () => {},
        };

        const descriptor = defineWebGL2RenderPassExecutor(definition);
        expect(descriptor.id).toBe('opaque:default');
        expect(descriptor.priority).toBe(0);
        expect(descriptor.kind).toBe('opaque');
        expect(descriptor.name).toBe('default');
    });

    it('preserves custom priority', () => {
        const definition: WebGL2AnyRenderPassExecutorDefinition = {
            kind: 'shadow',
            name: 'csm',
            priority: 10,
            execute: () => {},
        };

        const descriptor = defineWebGL2RenderPassExecutor(definition);
        expect(descriptor.priority).toBe(10);
    });

    it('freezes the result', () => {
        const definition: WebGL2AnyRenderPassExecutorDefinition = {
            kind: 'tonemap',
            name: 'aces',
            execute: () => {},
        };

        const descriptor = defineWebGL2RenderPassExecutor(definition);
        expect(Object.isFrozen(descriptor)).toBe(true);
    });

    it('preserves matches function', () => {
        const matches = () => true;
        const definition = {
            kind: 'post-process' as const,
            name: 'bloom',
            matches,
            execute: () => {},
        };

        const descriptor = defineWebGL2RenderPassExecutor(definition);
        expect(descriptor.matches).toBe(matches);
    });
});
