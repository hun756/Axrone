import { Component, script } from '@axrone/ecs-runtime';

@script({ singleton: false })
export default class DynamicSingletonComponent extends Component {
    data: string = 'dynamic';
}