import { BaseModule } from './base-module';
import { IEmissionModule, IParticleSOA } from '../interfaces';
import { Curve, Burst } from '../types';

export class EmissionModule extends BaseModule implements IEmissionModule {
    public rateOverTime: Curve;
    public rateOverDistance: Curve;
    public burstList: Burst[];

    private _time: number = 0;
    private _lastPosition: { x: number; y: number; z: number } = { x: 0, y: 0, z: 0 };
    private _accumulatedEmission: number = 0;
    private _burstIndex: number = 0;

    constructor(
        config: {
            enabled?: boolean;
            rateOverTime?: Curve;
            rateOverDistance?: Curve;
            burstList?: Burst[];
        } = {}
    ) {
        super('EmissionModule', config.enabled ?? true);

        this.rateOverTime = config.rateOverTime ?? {
            mode: 0,
            constant: 10,
            constantMin: 0,
            constantMax: 0,
            curveMultiplier: 1,
        };

        this.rateOverDistance = config.rateOverDistance ?? {
            mode: 0,
            constant: 0,
            constantMin: 0,
            constantMax: 0,
            curveMultiplier: 1,
        };

        this.burstList = config.burstList ?? [];
    }

    protected onInitialize(): void {
        this._time = 0;
        this._accumulatedEmission = 0;
        this._burstIndex = 0;
    }

    protected onUpdate(deltaTime: number, particles: IParticleSOA): void {
        this._time += deltaTime;

        if (this.rateOverTime.constant > 0) {
            this._accumulatedEmission += this.rateOverTime.constant * deltaTime;

            const particlesToEmit = Math.floor(this._accumulatedEmission);
            if (particlesToEmit > 0) {
                this._accumulatedEmission -= particlesToEmit;
                this.system.emit(particlesToEmit);
            }
        }

        this.handleBursts();
    }

    protected onReset(): void {
        this._time = 0;
        this._accumulatedEmission = 0;
        this._burstIndex = 0;
    }

    private handleBursts(): void {
        while (this._burstIndex < this.burstList.length) {
            const burst = this.burstList[this._burstIndex];

            if (this._time >= burst.time) {
                if (Math.random() <= burst.probability) {
                    const count = Math.max(
                        0,
                        burst.count.value + (Math.random() - 0.5) * burst.count.variance
                    );
                    this.system.emit(Math.floor(count));
                }
                this._burstIndex++;
            } else {
                break;
            }
        }
    }

    public setRateOverTime(rate: number): void {
        this.rateOverTime.constant = rate;
    }

    public addBurst(time: number, count: number, probability: number = 1.0): void {
        this.burstList.push({
            time,
            count: { value: count, variance: 0 },
            cycles: 1,
            interval: 0,
            probability,
        });

        this.burstList.sort((a, b) => a.time - b.time);
    }

    public clearBursts(): void {
        this.burstList.length = 0;
        this._burstIndex = 0;
    }
}
