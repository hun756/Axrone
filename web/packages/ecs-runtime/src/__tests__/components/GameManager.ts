import { Component, script } from '@axrone/ecs-runtime';

@script({
    scriptName: 'GameManager',
    singleton: true,
})
export default class GameManager extends Component {
    score = 0;
    level = 1;

    incrementScore(points: number): void {
        this.score += points;
    }

    nextLevel(): void {
        this.level++;
    }
}