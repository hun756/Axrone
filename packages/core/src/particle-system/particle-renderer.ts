import { IVec4Array } from './aligned-arrays';
import { ParticleSOA } from './particle-soa';
import { SortMode } from './types';

export interface IRenderBatch {
    readonly startIndex: number;
    readonly count: number;
    readonly material: any;
    readonly texture: any;
    readonly blendMode: number;
    readonly sortMode: SortMode;
}

export class ParticleSystemRenderer {
    private readonly renderBatches: IRenderBatch[] = [];
    private readonly sortedIndices: Uint32Array;

    constructor(maxParticles: number) {
        this.sortedIndices = new Uint32Array(maxParticles);
    }

    sortParticles(particles: ParticleSOA, sortMode: SortMode, cameraPosition?: any): void {
        if (sortMode === SortMode.None) return;

        const count = particles.count;
        const sortKeys = (particles as any).sortKeys as Float32Array;
        const indices = (particles as any).indices as Uint32Array;

        for (let i = 0; i < count; i++) {
            indices[i] = i;

            switch (sortMode) {
                case SortMode.Distance:
                    if (cameraPosition) {
                        const dx = (particles.positions as any).x[i] - cameraPosition.x;
                        const dy = (particles.positions as any).y[i] - cameraPosition.y;
                        const dz = (particles.positions as any).z[i] - cameraPosition.z;
                        sortKeys[i] = dx * dx + dy * dy + dz * dz;
                    }
                    break;
                case SortMode.OldestFirst:
                    sortKeys[i] = ((particles as any).birthTime as Float32Array)[i];
                    break;
                case SortMode.YoungestFirst:
                    sortKeys[i] = -((particles as any).birthTime as Float32Array)[i];
                    break;
            }
        }

        this.radixSort(indices, sortKeys, count);
    }

    private radixSort(indices: Uint32Array, keys: Float32Array, count: number): void {
        const tempIndices = new Uint32Array(count);
        const histogram = new Uint32Array(256);
        const keyBytes = new Uint8Array(keys.buffer, keys.byteOffset, keys.byteLength);

        let isIndicesSource = true;

        for (let byte = 0; byte < 4; byte++) {
            histogram.fill(0);

            const sourceArray = isIndicesSource ? indices : tempIndices;
            const targetArray = isIndicesSource ? tempIndices : indices;

            for (let i = 0; i < count; i++) {
                const keyIndex = sourceArray[i];
                const byteValue = keyBytes[keyIndex * 4 + byte];
                histogram[byteValue]++;
            }

            let sum = 0;
            for (let i = 0; i < 256; i++) {
                const temp = histogram[i];
                histogram[i] = sum;
                sum += temp;
            }

            for (let i = 0; i < count; i++) {
                const keyIndex = sourceArray[i];
                const byteValue = keyBytes[keyIndex * 4 + byte];
                targetArray[histogram[byteValue]++] = sourceArray[i];
            }

            isIndicesSource = !isIndicesSource;
        }

        if (!isIndicesSource) {
            indices.set(tempIndices.subarray(0, count));
        }
    }

    createRenderBatches(
        particles: ParticleSOA,
        materials: readonly any[],
        maxBatchSize: number = 1000
    ): readonly IRenderBatch[] {
        this.renderBatches.length = 0;

        let currentMaterial: any = null;
        let batchStart = 0;
        let batchCount = 0;

        for (let i = 0; i < particles.count; i++) {
            if (!(particles as any).active[i]) continue;

            const particleMaterial = materials[(particles as any).emitterIndex[i]] || materials[0];

            if (currentMaterial !== particleMaterial || batchCount >= maxBatchSize) {
                if (batchCount > 0) {
                    this.renderBatches.push({
                        startIndex: batchStart,
                        count: batchCount,
                        material: currentMaterial,
                        texture: currentMaterial?.texture,
                        blendMode: currentMaterial?.blendMode || 0,
                        sortMode: currentMaterial?.sortMode || SortMode.None,
                    });
                }

                currentMaterial = particleMaterial;
                batchStart = i;
                batchCount = 0;
            }

            batchCount++;
        }

        if (batchCount > 0) {
            this.renderBatches.push({
                startIndex: batchStart,
                count: batchCount,
                material: currentMaterial,
                texture: currentMaterial?.texture,
                blendMode: currentMaterial?.blendMode || 0,
                sortMode: currentMaterial?.sortMode || SortMode.None,
            });
        }

        return this.renderBatches;
    }

    // Return interleaved customData (customData1.x..w, customData2.x..w) for active particles
    getCustomDataArray(particles: ParticleSOA): Float32Array {
        const activeCount = particles.count;
        const customData = new Float32Array(activeCount * 8);

        const custom1 = (particles as any).customData1 as Float32Array;
        const custom2 = (particles as any).customData2 as Float32Array;
        const active = (particles as any).active as Uint8Array | undefined;

        let dst = 0;
        if (active) {
            for (let i = 0; i < activeCount; i++) {
                if (!active[i]) continue;
                const src4 = i * 4;
                customData[dst++] = custom1[src4];
                customData[dst++] = custom1[src4 + 1];
                customData[dst++] = custom1[src4 + 2];
                customData[dst++] = custom1[src4 + 3];
                customData[dst++] = custom2[src4];
                customData[dst++] = custom2[src4 + 1];
                customData[dst++] = custom2[src4 + 2];
                customData[dst++] = custom2[src4 + 3];
            }
        } else {
            for (let i = 0; i < activeCount; i++) {
                const src4 = i * 4;
                customData[dst++] = custom1[src4];
                customData[dst++] = custom1[src4 + 1];
                customData[dst++] = custom1[src4 + 2];
                customData[dst++] = custom1[src4 + 3];
                customData[dst++] = custom2[src4];
                customData[dst++] = custom2[src4 + 1];
                customData[dst++] = custom2[src4 + 2];
                customData[dst++] = custom2[src4 + 3];
            }
        }

        return customData;
    }

    // Return interleaved light data from a ParticleSystem instance if provided.
    // If `system` is provided and has `getLightData`, it will be used; otherwise return an empty array.
    getLightDataArray(particles: ParticleSOA, system?: any, maxLights?: number): Float32Array {
        if (!system || typeof system.getLightData !== 'function') return new Float32Array(0);
        return system.getLightData(maxLights);
    }

    // Return positions for active particles as [x,y,z,...]
    getPositionArray(particles: ParticleSOA): Float32Array {
        const active = particles.getActiveIndices();
        const out = new Float32Array(active.length * 3);
        const src = (particles as any).positions as Float32Array;
        for (let i = 0; i < active.length; i++) {
            const idx = active[i] * 3;
            const dst = i * 3;
            out[dst] = src[idx];
            out[dst + 1] = src[idx + 1];
            out[dst + 2] = src[idx + 2];
        }
        return out;
    }

    // Return colors for active particles as [r,g,b,a,...]
    getColorArray(particles: ParticleSOA): Float32Array {
        const active = particles.getActiveIndices();
        const out = new Float32Array(active.length * 4);
        const src = (particles as any).colors as Float32Array;
        for (let i = 0; i < active.length; i++) {
            const idx = active[i] * 4;
            const dst = i * 4;
            out[dst] = src[idx];
            out[dst + 1] = src[idx + 1];
            out[dst + 2] = src[idx + 2];
            out[dst + 3] = src[idx + 3];
        }
        return out;
    }

    // Return sizes for active particles as [size,...] (particles store sizes as vec3 but we return single scalar)
    getSizeArray(particles: ParticleSOA): Float32Array {
        const active = particles.getActiveIndices();
        const out = new Float32Array(active.length);
        const src = (particles as any).sizes as Float32Array;
        for (let i = 0; i < active.length; i++) {
            const idx = active[i] * 3;
            out[i] = src[idx];
        }
        return out;
    }

    // Return active particle slot indices as a Uint32Array
    getIndexArray(particles: ParticleSOA): Uint32Array {
        const active = particles.getActiveIndices();
        const out = new Uint32Array(active.length);
        for (let i = 0; i < active.length; i++) {
            out[i] = active[i];
        }
        return out;
    }
}
