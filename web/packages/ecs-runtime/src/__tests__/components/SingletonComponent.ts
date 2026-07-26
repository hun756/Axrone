import { Component, script } from '@axrone/ecs-runtime';

@script({ singleton: true })
export default class SingletonComponent extends Component {
    value: number = 0;
}