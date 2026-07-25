import { Component, script, property, Transform } from '@axrone/ecs-runtime';

@script({ allowMultiple: true })
export default class RepeatableScriptComponent extends Component {
    @property({ label: 'Speed', defaultValue: 5, min: 0, step: 0.5 })
    public speed: number = 5;

    @property({ type: Transform, description: 'Target transform reference' })
    public targetTransform: Transform | null = null;
}
