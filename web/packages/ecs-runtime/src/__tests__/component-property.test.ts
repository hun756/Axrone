import {
    Component,
    Transform,
    getComponentMetadata,
    getComponentPropertyMetadata,
    getComponentPropertyMetadataByKey,
} from '@axrone/ecs-runtime';
import { describe, expect, test } from 'vitest';
import RepeatableScriptComponent from './components/RepeatableScriptComponent';
import DerivedRepeatableScriptComponent from './components/DerivedRepeatableScriptComponent';

describe('Component Property Decorator', () => {
    test('surfaces allowMultiple through script metadata', () => {
        const metadata = getComponentMetadata(RepeatableScriptComponent);

        expect(metadata).toBeDefined();
        expect(metadata?.scriptName).toBe('RepeatableScriptComponent');
        expect(metadata?.allowMultiple).toBe(true);
    });

    test('collects inherited property metadata in declaration order', () => {
        const metadata = getComponentPropertyMetadata(DerivedRepeatableScriptComponent);

        expect(metadata.map((entry) => entry.propertyKey)).toEqual([
            'speed',
            'targetTransform',
            'enabledFlag',
        ]);
        expect(metadata[0]).toMatchObject({
            propertyKey: 'speed',
            label: 'Speed',
            defaultValue: 5,
            min: 0,
            step: 0.5,
            serializable: true,
            visible: true,
        });
        expect(metadata[1]).toMatchObject({
            propertyKey: 'targetTransform',
            description: 'Target transform reference',
            type: Transform,
        });
        expect(metadata[2]).toMatchObject({
            propertyKey: 'enabledFlag',
            label: 'Enabled',
            defaultValue: true,
        });
    });

    test('resolves individual property metadata by key', () => {
        const metadata = getComponentPropertyMetadataByKey(
            RepeatableScriptComponent,
            'targetTransform',
        );

        expect(metadata).toMatchObject({
            propertyKey: 'targetTransform',
            type: Transform,
            description: 'Target transform reference',
        });
    });
});
