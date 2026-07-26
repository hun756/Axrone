import { Component, script } from '@axrone/ecs-runtime';

@script({
    scriptName: 'DeprecatedComponent',
    deprecated: true,
    deprecationMessage: 'Use NewComponent instead',
    version: '0.9.0',
})
export default class DeprecatedComponent extends Component {}