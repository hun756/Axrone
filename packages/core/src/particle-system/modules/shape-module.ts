import { Vec3, IVec3Like } from '@axrone/numeric';
import { BaseModule } from './base-module';
import { IShapeModule, IParticleSOA } from '../interfaces';
import { EmitterShape } from '../types';

export class ShapeModule extends BaseModule implements IShapeModule {
    public shape: EmitterShape;
    public angle: number;
    public radius: number;
    public donutRadius: number;
    public length: number;
    public box: Vec3;
    public circle: {
        radius: number;
        arc: number;
        arcMode: number;
        arcSpread: number;
        thickness: number;
    };
    public hemisphere: { radius: number; emitFromShell: boolean };
    public cone: {
        angle: number;
        radius: number;
        length: number;
        emitFrom: number;
        randomizeDirection: number;
    };
    public donut: { radius: number; donutRadius: number; arc: number; arcMode: number };
    public mesh: {
        mesh: any;
        useMeshMaterialIndex: boolean;
        materialIndex: number;
        useMeshColors: boolean;
        normalOffset: number;
    };
    public sprite: { sprite: any; normalOffset: number };
    public spriteRenderer: { sprite: any; normalOffset: number };
    public skinnedMeshRenderer: {
        mesh: any;
        useMeshMaterialIndex: boolean;
        materialIndex: number;
        useMeshColors: boolean;
        normalOffset: number;
    };
    public rectangle: { x: number; y: number; z: number };
    public edge: { radius: number; radiusMode: number; arc: number; arcMode: number };
    public position: Vec3;
    public rotation: Vec3;
    public scale: Vec3;
    public alignToDirection: boolean;
    public randomDirectionAmount: number;
    public sphericalDirectionAmount: number;
    public randomPositionAmount: number;
    public biasType: number;
    public bias: number;
    public texture: any;
    public textureClipChannel: number;
    public textureClipThreshold: number;
    public textureColorAffectsParticles: boolean;
    public textureAlphaAffectsParticles: boolean;
    public textureBilinearFiltering: boolean;
    public textureUVChannel: number;

    constructor(config: Partial<IShapeModule> = {}) {
        super('ShapeModule', config.enabled ?? false);

        this.shape = config.shape ?? EmitterShape.Box;
        this.angle = config.angle ?? 25;
        this.radius = config.radius ?? 1;
        this.donutRadius = config.donutRadius ?? 0.2;
        this.length = config.length ?? 5;
        this.box = config.box ?? new Vec3(1, 1, 1);

        this.circle = config.circle ?? {
            radius: 1,
            arc: 360,
            arcMode: 0,
            arcSpread: 0,
            thickness: 1,
        };

        this.hemisphere = config.hemisphere ?? {
            radius: 1,
            emitFromShell: false,
        };

        this.cone = config.cone ?? {
            angle: 25,
            radius: 1,
            length: 5,
            emitFrom: 0,
            randomizeDirection: 0,
        };

        this.donut = config.donut ?? {
            radius: 1,
            donutRadius: 0.2,
            arc: 360,
            arcMode: 0,
        };

        this.mesh = config.mesh ?? {
            mesh: null,
            useMeshMaterialIndex: false,
            materialIndex: 0,
            useMeshColors: false,
            normalOffset: 0,
        };

        this.sprite = config.sprite ?? {
            sprite: null,
            normalOffset: 0,
        };

        this.spriteRenderer = config.spriteRenderer ?? {
            sprite: null,
            normalOffset: 0,
        };

        this.skinnedMeshRenderer = config.skinnedMeshRenderer ?? {
            mesh: null,
            useMeshMaterialIndex: false,
            materialIndex: 0,
            useMeshColors: false,
            normalOffset: 0,
        };

        this.rectangle = config.rectangle ?? { x: 1, y: 1, z: 0 };

        this.edge = config.edge ?? {
            radius: 1,
            radiusMode: 0,
            arc: 360,
            arcMode: 0,
        };

        this.position = config.position ?? new Vec3(0, 0, 0);
        this.rotation = config.rotation ?? new Vec3(0, 0, 0);
        this.scale = config.scale ?? new Vec3(1, 1, 1);

        this.alignToDirection = config.alignToDirection ?? false;
        this.randomDirectionAmount = config.randomDirectionAmount ?? 0;
        this.sphericalDirectionAmount = config.sphericalDirectionAmount ?? 0;
        this.randomPositionAmount = config.randomPositionAmount ?? 0;
        this.biasType = config.biasType ?? 0;
        this.bias = config.bias ?? 0;

        this.texture = config.texture ?? null;
        this.textureClipChannel = config.textureClipChannel ?? 0;
        this.textureClipThreshold = config.textureClipThreshold ?? 0;
        this.textureColorAffectsParticles = config.textureColorAffectsParticles ?? false;
        this.textureAlphaAffectsParticles = config.textureAlphaAffectsParticles ?? false;
        this.textureBilinearFiltering = config.textureBilinearFiltering ?? true;
        this.textureUVChannel = config.textureUVChannel ?? 0;
    }

    protected onInitialize(): void {}

    protected onUpdate(deltaTime: number, particles: IParticleSOA): void {
        // Shape module doesn't need per-frame updates
        // Particle emission positioning is handled during emission
    }

    protected onReset(): void {}

    public getEmissionPosition(): Vec3 {
        switch (this.shape) {
            case EmitterShape.Point:
                return this.position.clone();

            case EmitterShape.Box:
                return this.position.add(
                    new Vec3(
                        (Math.random() - 0.5) * this.box.x * this.scale.x,
                        (Math.random() - 0.5) * this.box.y * this.scale.y,
                        (Math.random() - 0.5) * this.box.z * this.scale.z
                    )
                );

            case EmitterShape.Sphere:
                return this.getRandomSpherePosition();

            case EmitterShape.Circle:
                return this.getRandomCirclePosition();

            case EmitterShape.Cone:
                return this.getRandomConePosition();

            case EmitterShape.Hemisphere:
                return this.getRandomHemispherePosition();

            case EmitterShape.Donut:
                return this.getRandomDonutPosition();

            default:
                return this.position.clone();
        }
    }

    public getEmissionDirection(): Vec3 {
        switch (this.shape) {
            case EmitterShape.Cone:
                return this.getRandomConeDirection();

            case EmitterShape.Sphere:
            case EmitterShape.Hemisphere:
                return this.getRandomSphericalDirection();

            default:
                const direction = new Vec3(0, 0, 1);
                if (this.randomDirectionAmount > 0) {
                    direction.x += (Math.random() - 0.5) * this.randomDirectionAmount;
                    direction.y += (Math.random() - 0.5) * this.randomDirectionAmount;
                    direction.z += (Math.random() - 0.5) * this.randomDirectionAmount;
                }
                return direction.normalize();
        }
    }

    private getRandomSpherePosition(): Vec3 {
        const phi = Math.random() * Math.PI * 2;
        const cosTheta = Math.random() * 2 - 1;
        const sinTheta = Math.sqrt(1 - cosTheta * cosTheta);
        const r = this.radius * Math.cbrt(Math.random());

        return this.position.add(
            new Vec3(
                r * sinTheta * Math.cos(phi) * this.scale.x,
                r * sinTheta * Math.sin(phi) * this.scale.y,
                r * cosTheta * this.scale.z
            )
        );
    }

    private getRandomCirclePosition(): Vec3 {
        const angle = Math.random() * Math.PI * 2;
        const r = Math.sqrt(Math.random()) * this.circle.radius;

        return this.position.add(
            new Vec3(r * Math.cos(angle) * this.scale.x, r * Math.sin(angle) * this.scale.y, 0)
        );
    }

    private getRandomConePosition(): Vec3 {
        const height = Math.random() * this.cone.length;
        const radiusAtHeight = (this.cone.radius * (this.cone.length - height)) / this.cone.length;
        const angle = Math.random() * Math.PI * 2;
        const r = Math.sqrt(Math.random()) * radiusAtHeight;

        return this.position.add(
            new Vec3(
                r * Math.cos(angle) * this.scale.x,
                r * Math.sin(angle) * this.scale.y,
                height * this.scale.z
            )
        );
    }

    private getRandomHemispherePosition(): Vec3 {
        const phi = Math.random() * Math.PI * 2;
        const cosTheta = Math.random();
        const sinTheta = Math.sqrt(1 - cosTheta * cosTheta);
        const r = this.hemisphere.emitFromShell
            ? this.radius
            : this.radius * Math.cbrt(Math.random());

        return this.position.add(
            new Vec3(
                r * sinTheta * Math.cos(phi) * this.scale.x,
                r * sinTheta * Math.sin(phi) * this.scale.y,
                r * cosTheta * this.scale.z
            )
        );
    }

    private getRandomDonutPosition(): Vec3 {
        const angle = Math.random() * Math.PI * 2;
        const torusAngle = Math.random() * Math.PI * 2;
        const majorRadius = this.donut.radius;
        const minorRadius = this.donut.donutRadius * Math.sqrt(Math.random());

        const x = (majorRadius + minorRadius * Math.cos(torusAngle)) * Math.cos(angle);
        const y = (majorRadius + minorRadius * Math.cos(torusAngle)) * Math.sin(angle);
        const z = minorRadius * Math.sin(torusAngle);

        return this.position.add(new Vec3(x * this.scale.x, y * this.scale.y, z * this.scale.z));
    }

    private getRandomConeDirection(): Vec3 {
        const halfAngle = (this.cone.angle * Math.PI) / 360;
        const cosAngle = Math.cos(halfAngle);
        const z = cosAngle + Math.random() * (1 - cosAngle);
        const sinTheta = Math.sqrt(1 - z * z);
        const phi = Math.random() * Math.PI * 2;

        return new Vec3(sinTheta * Math.cos(phi), sinTheta * Math.sin(phi), z).normalize();
    }

    private getRandomSphericalDirection(): Vec3 {
        const phi = Math.random() * Math.PI * 2;
        const cosTheta = Math.random() * 2 - 1;
        const sinTheta = Math.sqrt(1 - cosTheta * cosTheta);

        return new Vec3(sinTheta * Math.cos(phi), sinTheta * Math.sin(phi), cosTheta).normalize();
    }
}
