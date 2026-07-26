import { Component, script, Transform } from '@axrone/ecs-runtime';
import { Quat, Vec3 } from '@axrone/numeric';

@script({ scriptName: 'CameraFollow' })
export default class CameraFollow extends Component {
    private _target?: Transform;
    public offset: Vec3;
    public damping: number;

    constructor(offset: Vec3 = new Vec3(0, 5, -10), damping: number = 5) {
        super();
        this.offset = offset;
        this.damping = damping;
    }

    setTarget(target: Transform): void {
        this._target = target;
    }

    lateUpdate(deltaTime: number): void {
        if (!this._target) return;

        const cameraTransform = this.transform as Transform | undefined;
        if (!cameraTransform) return;

        const desiredPosition = this._target.position.clone().add(this.offset);
        const deltaSeconds = deltaTime / 1000;
        const t = Math.min(1, this.damping * deltaSeconds);

        const currentPos = cameraTransform.position;
        const newPos = Vec3.lerp(currentPos, desiredPosition, t);
        cameraTransform.position = newPos;

        const lookAt = this._target.position.clone();
        cameraTransform.rotation = Quat.fromLookAt(
            newPos,
            lookAt,
            Vec3.UP,
            new Quat()
        );
    }
}