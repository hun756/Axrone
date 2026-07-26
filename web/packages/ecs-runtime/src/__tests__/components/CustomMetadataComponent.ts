import { Component, script } from '@axrone/ecs-runtime';
import type { ComponentMetadata } from '@axrone/ecs-runtime';
import CustomDepComponent from './CustomDepComponent';

@script({
    dependencies: [CustomDepComponent],
    singleton: true,
})
export default class CustomMetadataComponent extends Component {
    static customMetadata: ComponentMetadata | null = null;

    static setComponentMetadata(target: any, metadata: ComponentMetadata) {
        CustomMetadataComponent.customMetadata = metadata;
    }

    getValue(): string {
        return 'custom';
    }
}
