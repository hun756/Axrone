import { Component, script } from '@axrone/ecs-runtime';

@script()
export default class DefaultScriptComponent extends Component {
    value: number = 0;
}
