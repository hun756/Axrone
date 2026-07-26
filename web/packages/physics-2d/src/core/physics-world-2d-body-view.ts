import type { IVec2Like } from '@axrone/numeric';
import type {
    BodyId,
    BodyFlags,
    IPhysicsBody2D,
    IMassData2D,
    Inertia,
    Mass,
} from '../types';

import { BodyManager2D } from './body-manager';
import { ShapeManager2D } from './shape-manager';
import {
    cloneVec2,
    inverseRotateVec2,
    rotateVec2,
    subtractVec2,
    transformPoint2D,
} from './physics-world-2d-helpers';

interface IPhysicsBody2DViewDeps {
    readonly bodyManager: BodyManager2D;
    readonly shapeManager: ShapeManager2D;
    readonly getBodyWorldCenter: (bodyId: BodyId) => IVec2Like;
    readonly resetBodyMassData: (bodyId: BodyId) => void;
}

export function createPhysicsBody2DView(
    bodyId: BodyId,
    deps: IPhysicsBody2DViewDeps
): IPhysicsBody2D {
    return {
        get id(): BodyId {
            return bodyId;
        },
        get type() {
            return deps.bodyManager.getBodyType(bodyId);
        },
        get transform() {
            return this.getTransform();
        },
        get velocity() {
            return {
                linear: cloneVec2(deps.bodyManager.getLinearVelocity(bodyId)),
                angular: deps.bodyManager.getAngularVelocity(bodyId),
            };
        },
        get massData() {
            return this.getMassData();
        },
        get shapes() {
            return deps.shapeManager.getShapesForBody(bodyId);
        },
        get flags(): BodyFlags {
            return deps.bodyManager.getFlags(bodyId);
        },
        get gravityScale(): number {
            return deps.bodyManager.getGravityScale(bodyId);
        },
        get linearDamping(): number {
            return deps.bodyManager.getLinearDamping(bodyId);
        },
        get angularDamping(): number {
            return deps.bodyManager.getAngularDamping(bodyId);
        },
        get sleepTime(): number {
            return deps.bodyManager.getSleepTime(bodyId);
        },
        get userData(): unknown {
            return deps.bodyManager.getUserData(bodyId);
        },
        applyForce(force: Readonly<IVec2Like>, point?: Readonly<IVec2Like>): void {
            deps.bodyManager.applyForce(bodyId, force, point);
        },
        applyForceToCenter(force: Readonly<IVec2Like>): void {
            deps.bodyManager.applyForceToCenter(bodyId, force);
        },
        applyTorque(torque: number): void {
            deps.bodyManager.applyTorque(bodyId, torque);
        },
        applyImpulse(impulse: Readonly<IVec2Like>, point?: Readonly<IVec2Like>): void {
            deps.bodyManager.applyImpulse(bodyId, impulse, point);
        },
        applyImpulseToCenter(impulse: Readonly<IVec2Like>): void {
            deps.bodyManager.applyImpulseToCenter(bodyId, impulse);
        },
        applyAngularImpulse(impulse: number): void {
            deps.bodyManager.applyAngularImpulse(bodyId, impulse);
        },
        getPosition(): Readonly<IVec2Like> {
            return cloneVec2(deps.bodyManager.getPosition(bodyId));
        },
        setPosition(position: Readonly<IVec2Like>): void {
            deps.bodyManager.setPosition(bodyId, position);
        },
        getRotation(): number {
            return deps.bodyManager.getRotation(bodyId);
        },
        setRotation(angle: number): void {
            deps.bodyManager.setRotation(bodyId, angle);
        },
        getTransform() {
            return {
                position: cloneVec2(deps.bodyManager.getPosition(bodyId)),
                rotation: deps.bodyManager.getRotation(bodyId),
            };
        },
        setTransform(position: Readonly<IVec2Like>, angle: number): void {
            deps.bodyManager.setPosition(bodyId, position);
            deps.bodyManager.setRotation(bodyId, angle);
        },
        getLinearVelocity(): Readonly<IVec2Like> {
            return cloneVec2(deps.bodyManager.getLinearVelocity(bodyId));
        },
        setLinearVelocity(velocity: Readonly<IVec2Like>): void {
            deps.bodyManager.setLinearVelocity(bodyId, velocity);
        },
        getAngularVelocity(): number {
            return deps.bodyManager.getAngularVelocity(bodyId);
        },
        setAngularVelocity(velocity: number): void {
            deps.bodyManager.setAngularVelocity(bodyId, velocity);
        },
        getLocalPoint(worldPoint: Readonly<IVec2Like>): IVec2Like {
            const transform = deps.bodyManager.getPosition(bodyId);
            const rotation = deps.bodyManager.getRotation(bodyId);
            return inverseRotateVec2(subtractVec2(worldPoint, transform), rotation);
        },
        getWorldPoint(localPoint: Readonly<IVec2Like>): IVec2Like {
            const position = deps.bodyManager.getPosition(bodyId);
            const rotation = deps.bodyManager.getRotation(bodyId);
            return transformPoint2D(position, rotation, localPoint);
        },
        getLocalVector(worldVector: Readonly<IVec2Like>): IVec2Like {
            return inverseRotateVec2(worldVector, deps.bodyManager.getRotation(bodyId));
        },
        getWorldVector(localVector: Readonly<IVec2Like>): IVec2Like {
            return rotateVec2(localVector, deps.bodyManager.getRotation(bodyId));
        },
        getLinearVelocityAtPoint(point: Readonly<IVec2Like>): IVec2Like {
            const linearVelocity = deps.bodyManager.getLinearVelocity(bodyId);
            const angularVelocity = deps.bodyManager.getAngularVelocity(bodyId);
            const center = deps.getBodyWorldCenter(bodyId);
            const offset = subtractVec2(point, center);
            return {
                x: linearVelocity.x - angularVelocity * offset.y,
                y: linearVelocity.y + angularVelocity * offset.x,
            };
        },
        getMass(): Mass {
            return deps.bodyManager.getMass(bodyId);
        },
        getInertia(): Inertia {
            return deps.bodyManager.getInertia(bodyId);
        },
        getMassData(): IMassData2D {
            const localCenter = deps.bodyManager.getLocalCenter(bodyId);
            const mass = deps.bodyManager.getMass(bodyId);
            const inertia = deps.bodyManager.getInertia(bodyId);
            const inverseMass = deps.bodyManager.getInverseMass(bodyId);
            const inverseInertia = deps.bodyManager.getInverseInertia(bodyId);
            return {
                mass,
                inverseMass,
                inertia,
                inverseInertia,
                center: cloneVec2(localCenter),
            };
        },
        setMassData(massData: IMassData2D): void {
            deps.bodyManager.setMassData(bodyId, massData.mass, massData.inertia, massData.center);
        },
        resetMassData(): void {
            deps.resetBodyMassData(bodyId);
        },
        isSleeping(): boolean {
            return !deps.bodyManager.isAwake(bodyId);
        },
        setSleeping(sleeping: boolean): void {
            deps.bodyManager.setAwake(bodyId, !sleeping);
        },
        isAwake(): boolean {
            return deps.bodyManager.isAwake(bodyId);
        },
        setAwake(awake: boolean): void {
            deps.bodyManager.setAwake(bodyId, awake);
        },
        isEnabled(): boolean {
            return deps.bodyManager.isEnabled(bodyId);
        },
        setEnabled(enabled: boolean): void {
            deps.bodyManager.setEnabled(bodyId, enabled);
        },
        isFixedRotation(): boolean {
            return deps.bodyManager.isFixedRotation(bodyId);
        },
        setFixedRotation(fixed: boolean): void {
            deps.bodyManager.setFixedRotation(bodyId, fixed);
        },
        isBullet(): boolean {
            return deps.bodyManager.isBullet(bodyId);
        },
        setBullet(bullet: boolean): void {
            deps.bodyManager.setBullet(bodyId, bullet);
        },
        getWorldCenter(): Readonly<IVec2Like> {
            return deps.getBodyWorldCenter(bodyId);
        },
        getLocalCenter(): Readonly<IVec2Like> {
            return cloneVec2(deps.bodyManager.getLocalCenter(bodyId));
        },
    };
}