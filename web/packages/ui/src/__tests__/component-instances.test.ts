import { describe, expect, it } from 'vitest';
import {
    AXRONE_DEFAULT_UI_FONT_FAMILY,
    UIRuntime,
    deserializeUIAsset,
    getDropdownSelectedIndex,
    serializeUIAsset,
} from '../index';
import { buttonFeedbackController } from '../controls/button-controller';
import { dropdownController } from '../controls/dropdown-controller';
import { createTestFontAsset } from './test-font';
import type { UIAsset, WidgetId } from '../types';

const textBlock = (value: string, size = 14) => ({
    value,
    family: AXRONE_DEFAULT_UI_FONT_FAMILY,
    size,
    weight: '400',
    style: 'normal',
    lineHeight: size * 1.3,
    letterSpacing: 0,
    align: 'start',
    wrap: 'word',
    overflow: 'ellipsis',
});

const dropdownMaster = (key: string) => ({
    role: 'custom:dropdown',
    key,
    enabled: true,
    interactive: true,
    controller: 'dropdown-select',
    props: {
        selectedIndex: 0,
        options: ['A', 'B'],
        triggerKey: `${key}-label`,
        popupKey: `${key}-popup`,
        chevronKey: `${key}-chevron`,
        placeholder: 'Pick',
    },
    layout: { width: 200, height: 'content', display: 'stack', direction: 'column', gap: 4 },
    children: [
        {
            role: 'container',
            key: `${key}-trigger`,
            enabled: true,
            interactive: false,
            layout: { width: '100%', height: 36, direction: 'row', alignItems: 'center', justifyContent: 'space-between' },
            style: { background: '#1e293bff', radius: 6 },
            children: [
                {
                    role: 'text',
                    key: `${key}-label`,
                    enabled: true,
                    interactive: false,
                    layout: { width: 'content', height: 'content' },
                    style: { color: '#e2e8f0ff' },
                    text: textBlock('A'),
                    children: [],
                },
                {
                    role: 'text',
                    key: `${key}-chevron`,
                    enabled: true,
                    interactive: false,
                    layout: { width: 'content', height: 'content' },
                    style: { color: '#94a3b8ff' },
                    text: textBlock('▾', 12),
                    children: [],
                },
            ],
        },
        {
            role: 'container',
            key: `${key}-popup`,
            enabled: false,
            interactive: false,
            layout: { width: '100%', height: 'content', display: 'stack', direction: 'column', gap: 2, padding: 4 },
            style: { background: '#1e293bff', radius: 8 },
            children: [0, 1].map((index) => ({
                role: 'custom:dropdown-item',
                key: `${key}-item-${index}`,
                enabled: true,
                interactive: false,
                layout: { width: '100%', height: 32, padding: 8 },
                style: { background: '#00000000', radius: 4 },
                children: [
                    {
                        role: 'text',
                        key: `${key}-item-${index}-text`,
                        enabled: true,
                        interactive: false,
                        layout: { width: 'content', height: 'content' },
                        style: { color: '#e2e8f0ff' },
                        text: textBlock(index === 0 ? 'A' : 'B'),
                        children: [],
                    },
                ],
            })),
        },
    ],
});

const buildAsset = (
    instances: Record<string, unknown>[],
    components: Record<string, unknown> = {},
): UIAsset =>
    ({
        id: 'test-instances',
        name: 'test-instances',
        version: 1,
        canvas: { referenceWidth: 800, referenceHeight: 600, scaleMode: 'match-width-or-height', matchBias: 0.5 },
        bindings: { root: 'root', ...Object.fromEntries(instances.map((node) => [(node as { key: string }).key, (node as { key: string }).key])) },
        components,
        root: {
            role: 'root',
            key: 'root',
            enabled: true,
            interactive: false,
            layout: { display: 'overlay', width: 800, height: 600 },
            children: instances,
        },
    }) as unknown as UIAsset;

const instanceNode = (key: string, props: Record<string, unknown>) => ({
    role: 'custom:instance',
    key,
    enabled: true,
    interactive: false,
    props,
    layout: { width: 200, height: 'content' },
    children: [],
});

const prepareRuntime = () => {
    const runtime = new UIRuntime({ width: 800, height: 600 });
    runtime.fonts.registerFace(createTestFontAsset(AXRONE_DEFAULT_UI_FONT_FAMILY));
    runtime.registry.register(buttonFeedbackController);
    runtime.registry.register(dropdownController);
    return runtime;
};

const frameTexts = (runtime: UIRuntime) =>
    runtime
        .commit()
        .commands.filter((c) => c.kind === 'text')
        .map((c) => (c.kind === 'text' ? c.layout.text : ''));

const collectRoles = (node: { role: string; children?: { role: string; children?: never[] }[] }): string[] => [
    node.role,
    ...(node.children ?? []).flatMap((child) =>
        collectRoles(child as { role: string; children?: { role: string; children?: never[] }[] }),
    ),
];

describe('component instance expansion', () => {
    it('expands instances with remapped bindings and working controllers', () => {
        const runtime = prepareRuntime();
        runtime.loadFromAsset(
            buildAsset(
                [instanceNode('inst1', { componentId: 'card' })],
                { card: { name: 'Card', root: dropdownMaster('card') } },
            ),
        );

        expect(runtime.getBoundWidget('inst1__card-label')).not.toBeNull();
        expect(runtime.getBoundWidget('inst1__card-popup')).not.toBeNull();

        const texts = frameTexts(runtime);
        expect(texts).toContain('A');
        expect(texts).not.toContain('B');

        const dropdown = runtime.getBoundWidget('inst1__card')!;
        const trigger = runtime.getBoundWidget('inst1__card-trigger')!;
        const box = runtime.getLayoutBox(trigger);
        runtime.dispatchInput({ type: 'pointer', phase: 'down', x: box.x + 4, y: box.y + 4 });
        expect(frameTexts(runtime)).toContain('B');

        const item = runtime.getBoundWidget('inst1__card-item-1')!;
        const itemBox = runtime.getLayoutBox(item);
        runtime.dispatchInput({ type: 'pointer', phase: 'move', x: itemBox.x + 4, y: itemBox.y + 4 });
        runtime.dispatchInput({ type: 'pointer', phase: 'up', x: itemBox.x + 4, y: itemBox.y + 4 });
        expect(getDropdownSelectedIndex(runtime, dropdown)).toBe(1);
    });

    it('applies text and prop overrides', () => {
        const runtime = prepareRuntime();
        runtime.loadFromAsset(
            buildAsset(
                [
                    instanceNode('inst1', {
                        componentId: 'card',
                        textOverrides: { 'card-item-0-text': 'Alpha' },
                        propOverrides: { selectedIndex: 1 },
                    }),
                ],
                { card: { name: 'Card', root: dropdownMaster('card') } },
            ),
        );

        const dropdown = runtime.getBoundWidget('inst1__card')!;
        expect(getDropdownSelectedIndex(runtime, dropdown)).toBe(1);
        expect(frameTexts(runtime)).toContain('B');

        const trigger = runtime.getBoundWidget('inst1__card-trigger')!;
        const box = runtime.getLayoutBox(trigger);
        runtime.dispatchInput({ type: 'pointer', phase: 'down', x: box.x + 4, y: box.y + 4 });
        expect(frameTexts(runtime)).toContain('Alpha');
    });

    it('expands nested instances and leaves missing masters alone', () => {
        const runtime = prepareRuntime();
        runtime.loadFromAsset(
            buildAsset(
                [
                    instanceNode('outer-inst', { componentId: 'outer' }),
                    instanceNode('ghost-inst', { componentId: 'ghost' }),
                ],
                {
                    inner: {
                        name: 'Inner',
                        root: {
                            role: 'text',
                            key: 'deep-text',
                            enabled: true,
                            interactive: false,
                            layout: { width: 'content', height: 'content' },
                            style: { color: '#e2e8f0ff' },
                            text: textBlock('deep'),
                            children: [],
                        },
                    },
                    outer: {
                        name: 'Outer',
                        root: {
                            role: 'container',
                            key: 'outer-box',
                            enabled: true,
                            interactive: false,
                            layout: { width: 200, height: 'content' },
                            children: [instanceNode('nested', { componentId: 'inner' })],
                        },
                    },
                },
            ),
        );

        expect(frameTexts(runtime)).toContain('deep');
        expect(runtime.getBoundWidget('ghost-inst')).not.toBeNull();
    });

    it('flattens instances in snapshots and round-trips components through io', () => {
        const asset = buildAsset(
            [instanceNode('inst1', { componentId: 'card' })],
            { card: { name: 'Card', root: dropdownMaster('card') } },
        );
        const revived = deserializeUIAsset(serializeUIAsset(asset));
        expect(revived.components?.['card']?.name).toBe('Card');

        const runtime = prepareRuntime();
        runtime.loadFromAsset(revived);
        const roles = collectRoles(runtime.snapshot().root as unknown as { role: string; children?: never[] });
        expect(roles).not.toContain('custom:instance');
        expect(roles).toContain('custom:dropdown');
    });

    it('keeps sibling order when expanding mid-tree instances', () => {
        const runtime = prepareRuntime();
        const first = {
            role: 'text',
            key: 'first-text',
            enabled: true,
            interactive: false,
            layout: { width: 'content', height: 'content' },
            style: { color: '#e2e8f0ff' },
            text: textBlock('first'),
            children: [],
        };
        runtime.loadFromAsset(
            buildAsset(
                [first, instanceNode('inst1', { componentId: 'tag' })],
                {
                    tag: {
                        name: 'Tag',
                        root: {
                            role: 'text',
                            key: 'tag-text',
                            enabled: true,
                            interactive: false,
                            layout: { width: 'content', height: 'content' },
                            style: { color: '#e2e8f0ff' },
                            text: textBlock('tag'),
                            children: [],
                        },
                    },
                },
            ),
        );
        const texts = frameTexts(runtime).filter((text) => text === 'first' || text === 'tag');
        expect(texts).toEqual(['first', 'tag']);
    });
});
