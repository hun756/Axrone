import { Component, script } from '@axrone/ecs-runtime';
import { Transform } from '@axrone/ecs-runtime';
import TestDependencyComponent from './TestDependencyComponent';

@script({
    dependencies: [Transform, TestDependencyComponent],
    singleton: true,
    executeInEditMode: true,
    priority: 100,
})
export default class AdvancedScriptComponent extends Component {
    name: string = 'advanced';
}
