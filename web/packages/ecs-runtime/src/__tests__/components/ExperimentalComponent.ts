import { Component, script } from '@axrone/ecs-runtime';

@script({
    scriptName: 'ExperimentalComponent',
    experimental: true,
    version: '2.0.0',
    tags: ['experimental', 'alpha'],
})
export default class ExperimentalComponent extends Component {}