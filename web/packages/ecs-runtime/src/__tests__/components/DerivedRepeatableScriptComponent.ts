import { script, property } from '@axrone/ecs-runtime';
import RepeatableScriptComponent from './RepeatableScriptComponent';

@script()
export default class DerivedRepeatableScriptComponent extends RepeatableScriptComponent {
    @property({ label: 'Enabled', defaultValue: true })
    public enabledFlag: boolean = true;
}
