import { Component, script } from '@axrone/ecs-runtime';
import { clamp } from '@axrone/numeric';
import {
    DEFAULT_TERRAIN_NOISE_OPTIONS,
    TERRAIN_MAX_FOLIAGE_LAYERS,
    TERRAIN_MAX_LAYERS,
    isTerrainResolution,
    isTerrainSplatResolution,
    type ResolvedTerrainNoiseOptions,
    type TerrainDescriptor,
    type TerrainFoliageLayer,
    type TerrainLayer,
    type TerrainNoiseOptions,
    type TerrainResolution,
    type TerrainSourceKind,
    type TerrainSplatResolution,
} from '@axrone/terrain';

export interface TerrainConfig {
    readonly source?: TerrainSourceKind;
    readonly heightmapAsset?: string;
    readonly heightData?: string;
    readonly width?: number;
    readonly length?: number;
    readonly maxHeight?: number;
    readonly resolution?: TerrainResolution;
    readonly noise?: TerrainNoiseOptions;
    readonly layers?: readonly Partial<TerrainLayer>[];
    readonly splatData?: string;
    readonly splatResolution?: TerrainSplatResolution;
    readonly foliageLayers?: readonly Partial<TerrainFoliageLayer>[];
    readonly foliageData?: readonly string[];
    readonly foliageSeed?: number;
    readonly materialId?: string | null;
    readonly castShadows?: boolean;
    readonly generateCollider?: boolean;
}

const DEFAULT_TERRAIN_WIDTH = 100;
const DEFAULT_TERRAIN_LENGTH = 100;
const DEFAULT_TERRAIN_MAX_HEIGHT = 30;
const DEFAULT_TERRAIN_RESOLUTION: TerrainResolution = 129;

const TERRAIN_SOURCE_KINDS: readonly TerrainSourceKind[] = ['flat', 'noise', 'heightmap', 'sculpted'];

const normalizeSource = (value: unknown, fallback: TerrainSourceKind): TerrainSourceKind =>
    TERRAIN_SOURCE_KINDS.includes(value as TerrainSourceKind)
        ? (value as TerrainSourceKind)
        : fallback;

const normalizeDimension = (value: unknown, fallback: number): number => {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
};

const normalizeMaxHeight = (value: unknown, fallback: number): number => {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed >= 0 ? parsed : fallback;
};

const normalizeResolution = (value: unknown, fallback: TerrainResolution): TerrainResolution => {
    const parsed = Number(value);
    return isTerrainResolution(parsed) ? parsed : fallback;
};

const normalizeFinite = (value: unknown, fallback: number): number => {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
};

/** Coerces arbitrary serialized noise data into a valid resolved option set. */
const normalizeNoise = (value: TerrainNoiseOptions | null | undefined): ResolvedTerrainNoiseOptions => {
    const defaults = DEFAULT_TERRAIN_NOISE_OPTIONS;
    if (!value || typeof value !== 'object') {
        return Object.freeze({ ...defaults });
    }

    return Object.freeze({
        seed: Math.trunc(normalizeFinite(value.seed, defaults.seed)),
        frequency: Math.max(normalizeFinite(value.frequency, defaults.frequency), 1e-4),
        octaves: clamp(Math.trunc(normalizeFinite(value.octaves, defaults.octaves)), 1, 8),
        persistence: clamp(normalizeFinite(value.persistence, defaults.persistence), 0.01, 1),
        lacunarity: Math.max(normalizeFinite(value.lacunarity, defaults.lacunarity), 1),
        offsetX: normalizeFinite(value.offsetX, defaults.offsetX),
        offsetZ: normalizeFinite(value.offsetZ, defaults.offsetZ),
    });
};

const areEqualNoise = (
    left: ResolvedTerrainNoiseOptions,
    right: ResolvedTerrainNoiseOptions
): boolean =>
    left.seed === right.seed &&
    left.frequency === right.frequency &&
    left.octaves === right.octaves &&
    left.persistence === right.persistence &&
    left.lacunarity === right.lacunarity &&
    left.offsetX === right.offsetX &&
    left.offsetZ === right.offsetZ;

const DEFAULT_TERRAIN_SPLAT_RESOLUTION: TerrainSplatResolution = 128;

const normalizeSplatResolution = (
    value: unknown,
    fallback: TerrainSplatResolution
): TerrainSplatResolution => {
    const parsed = Number(value);
    return isTerrainSplatResolution(parsed) ? parsed : fallback;
};

let terrainLayerIdCounter = 0;

const createTerrainLayerId = (): string => {
    terrainLayerIdCounter += 1;
    return `terrain_layer_${terrainLayerIdCounter}`;
};

/** Coerces arbitrary serialized layer data into a valid, capped layer list. */
const normalizeLayers = (
    value: readonly Partial<TerrainLayer>[] | null | undefined
): readonly TerrainLayer[] => {
    if (!Array.isArray(value)) {
        return Object.freeze([]);
    }

    const layers: TerrainLayer[] = [];
    for (const entry of value.slice(0, TERRAIN_MAX_LAYERS)) {
        if (!entry || typeof entry !== 'object') {
            continue;
        }

        const tiling = Number(entry.tiling);
        layers.push(
            Object.freeze({
                id:
                    typeof entry.id === 'string' && entry.id.length > 0
                        ? entry.id
                        : createTerrainLayerId(),
                name: typeof entry.name === 'string' ? entry.name : `Layer ${layers.length + 1}`,
                textureAsset: typeof entry.textureAsset === 'string' ? entry.textureAsset : '',
                tiling: Number.isFinite(tiling) && tiling > 0 ? tiling : 8,
            })
        );
    }

    return Object.freeze(layers);
};

const areEqualLayers = (
    left: readonly TerrainLayer[],
    right: readonly TerrainLayer[]
): boolean =>
    left.length === right.length &&
    left.every(
        (layer, index) =>
            layer.id === right[index]!.id &&
            layer.name === right[index]!.name &&
            layer.textureAsset === right[index]!.textureAsset &&
            layer.tiling === right[index]!.tiling
    );

let terrainFoliageLayerIdCounter = 0;

const createTerrainFoliageLayerId = (): string => {
    terrainFoliageLayerIdCounter += 1;
    return `terrain_foliage_${terrainFoliageLayerIdCounter}`;
};

const DEFAULT_TERRAIN_FOLIAGE_SEED = 4242;

const normalizeUnit = (value: unknown, fallback: number): number =>
    clamp(normalizeFinite(value, fallback), 0, 1);

/** Coerces serialized foliage layer data into a valid, capped layer list. */
const normalizeFoliageLayers = (
    value: readonly Partial<TerrainFoliageLayer>[] | null | undefined
): readonly TerrainFoliageLayer[] => {
    if (!Array.isArray(value)) {
        return Object.freeze([]);
    }

    const layers: TerrainFoliageLayer[] = [];
    for (const entry of value.slice(0, TERRAIN_MAX_FOLIAGE_LAYERS)) {
        if (!entry || typeof entry !== 'object') {
            continue;
        }

        const minScale = Math.max(normalizeFinite(entry.minScale, 0.8), 0.01);
        const maxScale = Math.max(normalizeFinite(entry.maxScale, 1.4), minScale);
        layers.push(
            Object.freeze({
                id:
                    typeof entry.id === 'string' && entry.id.length > 0
                        ? entry.id
                        : createTerrainFoliageLayerId(),
                name:
                    typeof entry.name === 'string'
                        ? entry.name
                        : `Foliage ${layers.length + 1}`,
                meshAsset: typeof entry.meshAsset === 'string' ? entry.meshAsset : '',
                density: normalizeUnit(entry.density, 0.5),
                minScale,
                maxScale,
                alignToNormal: Boolean(entry.alignToNormal),
                maxSlopeDeg: clamp(normalizeFinite(entry.maxSlopeDeg, 45), 0, 90),
            })
        );
    }

    return Object.freeze(layers);
};

const areEqualFoliageLayers = (
    left: readonly TerrainFoliageLayer[],
    right: readonly TerrainFoliageLayer[]
): boolean =>
    left.length === right.length &&
    left.every(
        (layer, index) =>
            layer.id === right[index]!.id &&
            layer.name === right[index]!.name &&
            layer.meshAsset === right[index]!.meshAsset &&
            layer.density === right[index]!.density &&
            layer.minScale === right[index]!.minScale &&
            layer.maxScale === right[index]!.maxScale &&
            layer.alignToNormal === right[index]!.alignToNormal &&
            layer.maxSlopeDeg === right[index]!.maxSlopeDeg
    );

/** Per-layer density payload list, trimmed to the foliage layer cap. */
const normalizeFoliageData = (
    value: readonly string[] | null | undefined
): readonly string[] => {
    if (!Array.isArray(value)) {
        return Object.freeze([]);
    }

    return Object.freeze(
        value
            .slice(0, TERRAIN_MAX_FOLIAGE_LAYERS)
            .map((entry) => (typeof entry === 'string' ? entry : ''))
    );
};

const areEqualStringLists = (left: readonly string[], right: readonly string[]): boolean =>
    left.length === right.length && left.every((entry, index) => entry === right[index]);

@script({
    scriptName: 'Terrain',
    priority: 100,
    executeInEditMode: true,
    singleton: false,
    trackInstances: false,
})
export class Terrain extends Component {
    private _source: TerrainSourceKind;
    private _heightmapAsset: string;
    private _heightData: string;
    private _width: number;
    private _length: number;
    private _maxHeight: number;
    private _resolution: TerrainResolution;
    private _noise: ResolvedTerrainNoiseOptions;
    private _layers: readonly TerrainLayer[];
    private _splatData: string;
    private _splatResolution: TerrainSplatResolution;
    private _foliageLayers: readonly TerrainFoliageLayer[];
    private _foliageData: readonly string[];
    private _foliageSeed: number;
    private _materialId: string | null;
    private _castShadows: boolean;
    private _generateCollider: boolean;
    private _dataVersion = 0;

    constructor(config: TerrainConfig = {}) {
        super();
        this._source = normalizeSource(config.source, 'noise');
        this._heightmapAsset = typeof config.heightmapAsset === 'string' ? config.heightmapAsset : '';
        this._heightData = typeof config.heightData === 'string' ? config.heightData : '';
        this._width = normalizeDimension(config.width, DEFAULT_TERRAIN_WIDTH);
        this._length = normalizeDimension(config.length, DEFAULT_TERRAIN_LENGTH);
        this._maxHeight = normalizeMaxHeight(config.maxHeight, DEFAULT_TERRAIN_MAX_HEIGHT);
        this._resolution = normalizeResolution(config.resolution, DEFAULT_TERRAIN_RESOLUTION);
        this._noise = normalizeNoise(config.noise);
        this._layers = normalizeLayers(config.layers);
        this._splatData = typeof config.splatData === 'string' ? config.splatData : '';
        this._splatResolution = normalizeSplatResolution(
            config.splatResolution,
            DEFAULT_TERRAIN_SPLAT_RESOLUTION
        );
        this._foliageLayers = normalizeFoliageLayers(config.foliageLayers);
        this._foliageData = normalizeFoliageData(config.foliageData);
        this._foliageSeed = Math.trunc(
            normalizeFinite(config.foliageSeed, DEFAULT_TERRAIN_FOLIAGE_SEED)
        );
        this._materialId = config.materialId ?? null;
        this._castShadows = config.castShadows ?? true;
        this._generateCollider = config.generateCollider ?? true;
    }

    /** Monotonic counter bumped whenever terrain geometry inputs change. */
    get dataVersion(): number {
        return this._dataVersion;
    }

    get source(): TerrainSourceKind {
        return this._source;
    }

    set source(value: TerrainSourceKind) {
        const normalized = normalizeSource(value, this._source);
        if (normalized === this._source) {
            return;
        }

        this._source = normalized;
        this._dataVersion += 1;
    }

    get heightmapAsset(): string {
        return this._heightmapAsset;
    }

    set heightmapAsset(value: string) {
        const normalized = typeof value === 'string' ? value : '';
        if (normalized === this._heightmapAsset) {
            return;
        }

        this._heightmapAsset = normalized;
        this._dataVersion += 1;
    }

    /** Serialized sculpted-height payload (`@axrone/terrain` codec format). */
    get heightData(): string {
        return this._heightData;
    }

    set heightData(value: string) {
        const normalized = typeof value === 'string' ? value : '';
        if (normalized === this._heightData) {
            return;
        }

        this._heightData = normalized;
        this._dataVersion += 1;
    }

    get width(): number {
        return this._width;
    }

    set width(value: number) {
        const normalized = normalizeDimension(value, this._width);
        if (normalized === this._width) {
            return;
        }

        this._width = normalized;
        this._dataVersion += 1;
    }

    get length(): number {
        return this._length;
    }

    set length(value: number) {
        const normalized = normalizeDimension(value, this._length);
        if (normalized === this._length) {
            return;
        }

        this._length = normalized;
        this._dataVersion += 1;
    }

    get maxHeight(): number {
        return this._maxHeight;
    }

    set maxHeight(value: number) {
        const normalized = normalizeMaxHeight(value, this._maxHeight);
        if (normalized === this._maxHeight) {
            return;
        }

        this._maxHeight = normalized;
        this._dataVersion += 1;
    }

    get resolution(): TerrainResolution {
        return this._resolution;
    }

    set resolution(value: TerrainResolution) {
        const normalized = normalizeResolution(value, this._resolution);
        if (normalized === this._resolution) {
            return;
        }

        this._resolution = normalized;
        this._dataVersion += 1;
    }

    get noise(): ResolvedTerrainNoiseOptions {
        return this._noise;
    }

    set noise(value: TerrainNoiseOptions | null) {
        const normalized = normalizeNoise(value);
        if (areEqualNoise(this._noise, normalized)) {
            return;
        }

        this._noise = normalized;
        this._dataVersion += 1;
    }

    get layers(): readonly TerrainLayer[] {
        return this._layers;
    }

    set layers(value: readonly Partial<TerrainLayer>[] | null) {
        const normalized = normalizeLayers(value);
        if (areEqualLayers(this._layers, normalized)) {
            return;
        }

        this._layers = normalized;
        this._dataVersion += 1;
    }

    /** Serialized splat weight payload (`@axrone/terrain` codec format). */
    get splatData(): string {
        return this._splatData;
    }

    set splatData(value: string) {
        const normalized = typeof value === 'string' ? value : '';
        if (normalized === this._splatData) {
            return;
        }

        this._splatData = normalized;
        this._dataVersion += 1;
    }

    get splatResolution(): TerrainSplatResolution {
        return this._splatResolution;
    }

    set splatResolution(value: TerrainSplatResolution) {
        const normalized = normalizeSplatResolution(value, this._splatResolution);
        if (normalized === this._splatResolution) {
            return;
        }

        this._splatResolution = normalized;
        this._dataVersion += 1;
    }

    get foliageLayers(): readonly TerrainFoliageLayer[] {
        return this._foliageLayers;
    }

    set foliageLayers(value: readonly Partial<TerrainFoliageLayer>[] | null) {
        const normalized = normalizeFoliageLayers(value);
        if (areEqualFoliageLayers(this._foliageLayers, normalized)) {
            return;
        }

        this._foliageLayers = normalized;
        this._dataVersion += 1;
    }

    /** Per-layer density payloads (`@axrone/terrain` foliage codec format). */
    get foliageData(): readonly string[] {
        return this._foliageData;
    }

    set foliageData(value: readonly string[] | null) {
        const normalized = normalizeFoliageData(value);
        if (areEqualStringLists(this._foliageData, normalized)) {
            return;
        }

        this._foliageData = normalized;
        this._dataVersion += 1;
    }

    /** Seed for the deterministic foliage scatter of all layers. */
    get foliageSeed(): number {
        return this._foliageSeed;
    }

    set foliageSeed(value: number) {
        const normalized = Math.trunc(normalizeFinite(value, this._foliageSeed));
        if (normalized === this._foliageSeed) {
            return;
        }

        this._foliageSeed = normalized;
        this._dataVersion += 1;
    }

    get materialId(): string | null {
        return this._materialId;
    }

    set materialId(value: string | null) {
        this._materialId = value;
    }

    get castShadows(): boolean {
        return this._castShadows;
    }

    set castShadows(value: boolean) {
        this._castShadows = Boolean(value);
    }

    get generateCollider(): boolean {
        return this._generateCollider;
    }

    set generateCollider(value: boolean) {
        this._generateCollider = Boolean(value);
    }

    /** Descriptor consumed by the @axrone/terrain builders. */
    get descriptor(): TerrainDescriptor {
        return {
            width: this._width,
            length: this._length,
            maxHeight: this._maxHeight,
            resolution: this._resolution,
        };
    }

    override serialize(): Record<string, unknown> {
        return {
            source: this._source,
            heightmapAsset: this._heightmapAsset,
            heightData: this._heightData,
            width: this._width,
            length: this._length,
            maxHeight: this._maxHeight,
            resolution: this._resolution,
            noise: { ...this._noise },
            layers: this._layers.map((layer) => ({ ...layer })),
            splatData: this._splatData,
            splatResolution: this._splatResolution,
            foliageLayers: this._foliageLayers.map((layer) => ({ ...layer })),
            foliageData: [...this._foliageData],
            foliageSeed: this._foliageSeed,
            materialId: this._materialId,
            castShadows: this._castShadows,
            generateCollider: this._generateCollider,
        };
    }

    override deserialize(data: Record<string, any>): void {
        if (typeof data.source === 'string') {
            this.source = data.source as TerrainSourceKind;
        }
        if (typeof data.heightmapAsset === 'string') {
            this.heightmapAsset = data.heightmapAsset;
        }
        if (typeof data.heightData === 'string') {
            this.heightData = data.heightData;
        }
        if (typeof data.width === 'number') {
            this.width = data.width;
        }
        if (typeof data.length === 'number') {
            this.length = data.length;
        }
        if (typeof data.maxHeight === 'number') {
            this.maxHeight = data.maxHeight;
        }
        if (typeof data.resolution === 'number') {
            this.resolution = data.resolution as TerrainResolution;
        }
        if (typeof data.noise === 'object' && data.noise !== null && !Array.isArray(data.noise)) {
            this.noise = data.noise as TerrainNoiseOptions;
        }
        if (Array.isArray(data.layers)) {
            this.layers = data.layers as readonly Partial<TerrainLayer>[];
        }
        if (typeof data.splatData === 'string') {
            this.splatData = data.splatData;
        }
        if (typeof data.splatResolution === 'number') {
            this.splatResolution = data.splatResolution as TerrainSplatResolution;
        }
        if (Array.isArray(data.foliageLayers)) {
            this.foliageLayers = data.foliageLayers as readonly Partial<TerrainFoliageLayer>[];
        }
        if (Array.isArray(data.foliageData)) {
            this.foliageData = data.foliageData as readonly string[];
        }
        if (typeof data.foliageSeed === 'number') {
            this.foliageSeed = data.foliageSeed;
        }
        if (typeof data.materialId === 'string' || data.materialId === null) {
            this._materialId = data.materialId;
        }
        if (typeof data.castShadows === 'boolean') {
            this._castShadows = data.castShadows;
        }
        if (typeof data.generateCollider === 'boolean') {
            this._generateCollider = data.generateCollider;
        }
    }
}
