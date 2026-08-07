import { Component, script } from '@axrone/ecs-runtime';

@script({
    scriptName: 'TestComponent',
    priority: 100,
    version: '1.0.0',
    author: 'Test Team',
    description: 'A test component for unit testing',
    tags: ['test', 'unit'],
    dependencies: [],
    singleton: false,
    executeInEditMode: true,
    validateDependencies: true,
    enableMetrics: true,
})
export default class TestComponent extends Component {
    public value: number = 42;

    constructor(value: number = 42) {
        super();
        this.value = value;
    }
}