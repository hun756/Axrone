import { Component, script } from '@axrone/ecs-runtime';

@script({
    scriptName: 'Health',
})
export default class Health extends Component {
    current = 100;
    max = 100;

    damage(amount: number): void {
        this.current = Math.max(0, this.current - amount);
    }
}