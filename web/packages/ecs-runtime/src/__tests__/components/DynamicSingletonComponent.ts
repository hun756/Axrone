import { Component, script } from '@axrone/ecs-runtime';

@script({ singleton: true, scriptName: 'DynamicSingletonComponent' })
export default class DynamicSingletonComponent extends Component {
    data: string = 'dynamic';
}