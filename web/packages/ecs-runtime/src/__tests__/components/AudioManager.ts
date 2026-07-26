import { Component, script } from '@axrone/ecs-runtime';

@script({
    scriptName: 'AudioManager',
    singleton: true,
})
export default class AudioManager extends Component {
    volume = 1.0;
    muted = false;

    setVolume(vol: number): void {
        this.volume = Math.max(0, Math.min(1, vol));
    }
}