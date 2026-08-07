import { Component, script } from '@axrone/ecs-runtime';

@script({
    singleton: false,
    priority: -50,
})
export default class NegativePriorityComponent extends Component {
    data: string = '';
}
