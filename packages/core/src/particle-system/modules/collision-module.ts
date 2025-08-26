import { Vec3 } from '@axrone/numeric';
import { BaseModule } from './base-module';
import { ICollisionModule, IParticleSOA } from '../interfaces';
import { Curve } from '../types';

export class CollisionModule extends BaseModule implements ICollisionModule {
    public type: number = 0;
    public mode: number = 0;
    public dampen: Curve;
    public bounce: Curve;
    public lifetimeLoss: Curve;
    public minKillSpeed: number = 0;
    public maxKillSpeed: number = Number.POSITIVE_INFINITY;
    public radiusScale: number = 1;
    public planes: { normal: Vec3; distance: number }[] = [];
    public visualization: number = 0;
    public visualizeBounds: boolean = false;
    public enableDynamicColliders: boolean = false;
    public maxCollisionShapes: number = 64;
    public quality: number = 0;
    public voxelSize: number = 1;
    public collidesWith: number = 0xffffffff;
    public collidesWithDynamic: boolean = false;
    public interiorCollisions: boolean = false;
    public sendCollisionMessages: boolean = false;
    public multiplyColliderForceByCollisionAngle: boolean = false;
    public multiplyColliderForceByParticleSpeed: boolean = false;
    public multiplyColliderForceByParticleSize: boolean = false;

    constructor(config: Partial<ICollisionModule> = {}) {
        super('CollisionModule', config.enabled ?? false);
        const defaultCurve: Curve = {
            mode: 0,
            constant: 0,
            constantMin: 0,
            constantMax: 0,
            curveMultiplier: 1,
        };
        this.dampen = config.dampen ?? defaultCurve;
        this.bounce = config.bounce ?? defaultCurve;
        this.lifetimeLoss = config.lifetimeLoss ?? defaultCurve;
        this.planes = config.planes ?? [];
        this.minKillSpeed = config.minKillSpeed ?? 0;
        this.maxKillSpeed = config.maxKillSpeed ?? Number.POSITIVE_INFINITY;
        Object.assign(this, config);
    }

    protected onInitialize(): void {
        // nothing
    }

    protected onUpdate(deltaTime: number, particles: IParticleSOA): void {
        if (!this.enabled) return;

        const positions = particles.positions;
        const velocities = particles.velocities;
        const active = particles.getActiveIndices();

        for (const idx of active) {
            const off = idx * 3;
            const px = positions[off];
            const py = positions[off + 1];
            const pz = positions[off + 2];

            for (const plane of this.planes) {
                const n = plane.normal;
                // Plane equation: dot(n, p) + d = 0, assume distance = d
                const dist = n.x * px + n.y * py + n.z * pz + plane.distance;
                if (dist < 0) {
                    // particle is past the plane (inside collision)
                    // reflect velocity
                    const vx = velocities[off];
                    const vy = velocities[off + 1];
                    const vz = velocities[off + 2];
                    const vDotN = vx * n.x + vy * n.y + vz * n.z;

                    // reflect
                    velocities[off] = vx - 2 * vDotN * n.x;
                    velocities[off + 1] = vy - 2 * vDotN * n.y;
                    velocities[off + 2] = vz - 2 * vDotN * n.z;

                    // apply dampening and bounce coefficients (simple)
                    const bounceFactor = this.bounce.constant ?? 0;
                    velocities[off] *= bounceFactor;
                    velocities[off + 1] *= bounceFactor;
                    velocities[off + 2] *= bounceFactor;

                    // optionally reduce lifetime
                    if (this.lifetimeLoss.constant > 0) {
                        // shorten lifetime by a fraction
                        particles.lifetimes[idx] = Math.max(
                            0,
                            particles.lifetimes[idx] - this.lifetimeLoss.constant
                        );
                    }
                }
            }
        }
    }

    protected onReset(): void {
        // --
    }

    public addPlane(normal: Vec3, distance: number): void {
        this.planes.push({ normal: normal.clone(), distance });
    }

    public clearPlanes(): void {
        this.planes.length = 0;
    }
}
