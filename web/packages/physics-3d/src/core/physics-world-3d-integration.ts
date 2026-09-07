import type { IVec3Like } from '@axrone/numeric';
import { PhysicsConstants } from '../types';
import { BODY_TYPE_DYNAMIC, BODY_TYPE_STATIC } from './physics-world-3d-shared';
import type { BodyManager3D } from './physics-managers-3d';

/**
 * Integrates accumulated forces into velocities (gravity, damping, velocity clamp).
 * Extracted from PhysicsWorld3D — operates on BodyManager3D via explicit parameters.
 *
 * @param maxVelocity - Maximum linear velocity in metres per second (m/s).
 *   Bodies exceeding this speed are scaled down preserving direction.
 *   Default: `PhysicsConstants.MAX_VELOCITY` (200 m/s). @see ADR 0004
 * @param maxAngularVelocity - Maximum angular velocity in radians per second (rad/s).
 *   Default: `PhysicsConstants.MAX_ANGULAR_VELOCITY` (250 rad/s). @see ADR 0004
 */
export function integrateVelocities(
    bodyManager: BodyManager3D,
    gravity: Readonly<IVec3Like>,
    dt: number,
    maxVelocity: number = PhysicsConstants.MAX_VELOCITY,
    maxAngularVelocity: number = PhysicsConstants.MAX_ANGULAR_VELOCITY
): void {
    // Integrate accumulated forces (F*dt*invMass → velocity)
    bodyManager.integrateForces(dt);

    const bodyIds = bodyManager.getBodyIds();
    const gravityX = gravity.x * dt;
    const gravityY = gravity.y * dt;
    const gravityZ = gravity.z * dt;

    for (const bodyId of bodyIds) {
        if (bodyManager.getBodyType(bodyId) !== BODY_TYPE_DYNAMIC) continue;
        if (!bodyManager.isEnabled(bodyId)) continue;
        if (!bodyManager.isAwake(bodyId)) continue;

        const gravityScale = bodyManager.getGravityScale(bodyId);
        const velocity = bodyManager.getLinearVelocity(bodyId);
        const angularVelocity = bodyManager.getAngularVelocity(bodyId);
        const linearDamping = Math.max(0, 1 - bodyManager.getLinearDamping(bodyId) * dt);
        const angularDamping = Math.max(0, 1 - bodyManager.getAngularDamping(bodyId) * dt);

        bodyManager.setLinearVelocity(bodyId, {
            x: (velocity.x + gravityX * gravityScale) * linearDamping,
            y: (velocity.y + gravityY * gravityScale) * linearDamping,
            z: (velocity.z + gravityZ * gravityScale) * linearDamping,
        });

        bodyManager.setAngularVelocity(bodyId, {
            x: bodyManager.isFixedRotation(bodyId) ? 0 : angularVelocity.x * angularDamping,
            y: bodyManager.isFixedRotation(bodyId) ? 0 : angularVelocity.y * angularDamping,
            z: bodyManager.isFixedRotation(bodyId) ? 0 : angularVelocity.z * angularDamping,
        });

        // Velocity clamp: prevent numerical explosion (ADR 0004 — metre-based)
        const lv = bodyManager.getLinearVelocity(bodyId);
        const lvSq = lv.x * lv.x + lv.y * lv.y + lv.z * lv.z;
        const maxV = maxVelocity;
        if (lvSq > maxV * maxV) {
            const scale = maxV / Math.sqrt(lvSq);
            bodyManager.setLinearVelocity(bodyId, {
                x: lv.x * scale, y: lv.y * scale, z: lv.z * scale,
            });
        }
        if (!bodyManager.isFixedRotation(bodyId)) {
            const av = bodyManager.getAngularVelocity(bodyId);
            const avSq = av.x * av.x + av.y * av.y + av.z * av.z;
            const maxAV = maxAngularVelocity;
            if (avSq > maxAV * maxAV) {
                const scale = maxAV / Math.sqrt(avSq);
                bodyManager.setAngularVelocity(bodyId, {
                    x: av.x * scale, y: av.y * scale, z: av.z * scale,
                });
            }
        }
    }
}

/**
 * Integrates velocities into positions (linear + angular via quaternion integration).
 * Optionally clears force accumulators when autoClearForces is enabled.
 */
export function integratePositions(
    bodyManager: BodyManager3D,
    dt: number,
    autoClearForces: boolean
): void {
    const bodyIds = bodyManager.getBodyIds();

    for (const bodyId of bodyIds) {
        if (bodyManager.getBodyType(bodyId) === BODY_TYPE_STATIC) continue;
        if (!bodyManager.isEnabled(bodyId)) continue;
        if (!bodyManager.isAwake(bodyId)) continue;

        const position = bodyManager.getPosition(bodyId);
        const velocity = bodyManager.getLinearVelocity(bodyId);
        const rotation = bodyManager.getRotation(bodyId);
        const angularVelocity = bodyManager.getAngularVelocity(bodyId);

        bodyManager.setPosition(bodyId, {
            x: position.x + velocity.x * dt,
            y: position.y + velocity.y * dt,
            z: position.z + velocity.z * dt,
        });

        const angularSpeed = Math.sqrt(
            angularVelocity.x * angularVelocity.x +
                angularVelocity.y * angularVelocity.y +
                angularVelocity.z * angularVelocity.z
        );

        if (angularSpeed > 1e-10 && !bodyManager.isFixedRotation(bodyId)) {
            const halfAngle = angularSpeed * dt * 0.5;
            const s = Math.sin(halfAngle) / angularSpeed;
            const c = Math.cos(halfAngle);

            const dqx = angularVelocity.x * s;
            const dqy = angularVelocity.y * s;
            const dqz = angularVelocity.z * s;
            const dqw = c;

            const newW =
                dqw * rotation.w -
                dqx * rotation.x -
                dqy * rotation.y -
                dqz * rotation.z;
            const newX =
                dqw * rotation.x +
                dqx * rotation.w +
                dqy * rotation.z -
                dqz * rotation.y;
            const newY =
                dqw * rotation.y -
                dqx * rotation.z +
                dqy * rotation.w +
                dqz * rotation.x;
            const newZ =
                dqw * rotation.z +
                dqx * rotation.y -
                dqy * rotation.x +
                dqz * rotation.w;

            const length = Math.sqrt(
                newX * newX + newY * newY + newZ * newZ + newW * newW
            );
            const inverseLength = length > 1e-10 ? 1 / length : 0;

            bodyManager.setRotation(bodyId, {
                x: newX * inverseLength,
                y: newY * inverseLength,
                z: newZ * inverseLength,
                w: newW * inverseLength,
            });
        }
    }

    if (autoClearForces) {
        bodyManager.clearForceAccumulators();
    }
}
