import { Component, script } from '@axrone/ecs-runtime';

export type UIHostRenderMode = 'screen-overlay' | 'world-space';

/** How a world-space UI quad orients itself relative to the camera. */
export type UIHostBillboardMode = 'none' | 'camera-facing';

export interface UIHostConfig {
    readonly assetId?: string;
    readonly renderMode?: UIHostRenderMode;
    readonly receiveInput?: boolean;
    /** World-space quad width in world units. */
    readonly worldWidth?: number;
    /** World-space quad height in world units. */
    readonly worldHeight?: number;
    readonly billboard?: UIHostBillboardMode;
    /** Offscreen texture resolution per world unit. */
    readonly textureScale?: number;
}

const VALID_RENDER_MODES: ReadonlySet<UIHostRenderMode> = new Set<UIHostRenderMode>([
    'screen-overlay',
    'world-space',
]);

const VALID_BILLBOARD_MODES: ReadonlySet<UIHostBillboardMode> = new Set<UIHostBillboardMode>([
    'none',
    'camera-facing',
]);

const DEFAULT_WORLD_WIDTH = 1;
const DEFAULT_WORLD_HEIGHT = 0.5;
const DEFAULT_TEXTURE_SCALE = 512;
const MIN_WORLD_SIZE = 0.01;
const MAX_WORLD_SIZE = 1000;
const MIN_TEXTURE_SCALE = 64;
const MAX_TEXTURE_SCALE = 4096;

const clampNumber = (value: unknown, fallback: number, min: number, max: number): number => {
    if (typeof value !== 'number' || !Number.isFinite(value)) {
        return fallback;
    }
    return Math.max(min, Math.min(max, value));
};

/**
 * UIHost is a scene component that references a standalone `.ui.json` asset and
 * decides where that asset is rendered.
 *
 * This follows the Unreal/UMG "UI as independent asset" architecture:
 * - The UI asset is authored and stored independently from the scene.
 * - The scene only holds a reference (assetId) to the UI asset.
 * - At runtime, a bridge system loads the asset and creates the UI overlay.
 *
 * Properties:
 * - `assetId`: Path/ID of the `.ui.json` asset to load.
 * - `renderMode`: `screen-overlay` draws over the viewport; `world-space` renders
 *   the asset into an offscreen texture and displays it on a quad inside the 3D
 *   scene (the Unity World Space Canvas equivalent).
 * - `receiveInput`: Whether pointer/key events are forwarded to the UI runtime.
 * - `worldWidth` / `worldHeight`: World-space quad extents in world units.
 * - `billboard`: Whether a world-space quad turns to face the camera.
 * - `textureScale`: Offscreen texture pixels per world unit; drives UI sharpness.
 */
@script({
    scriptName: 'UIHost',
    priority: 500,
    executeInEditMode: true,
    singleton: false,
})
export class UIHost extends Component {
    private _assetId: string;
    private _renderMode: UIHostRenderMode;
    private _receiveInput: boolean;
    private _worldWidth: number;
    private _worldHeight: number;
    private _billboard: UIHostBillboardMode;
    private _textureScale: number;

    constructor(config: UIHostConfig = {}) {
        super();
        this._assetId = config.assetId ?? '';
        this._renderMode = VALID_RENDER_MODES.has(config.renderMode ?? ('' as UIHostRenderMode))
            ? config.renderMode!
            : 'screen-overlay';
        this._receiveInput = config.receiveInput ?? true;
        this._worldWidth = clampNumber(
            config.worldWidth,
            DEFAULT_WORLD_WIDTH,
            MIN_WORLD_SIZE,
            MAX_WORLD_SIZE
        );
        this._worldHeight = clampNumber(
            config.worldHeight,
            DEFAULT_WORLD_HEIGHT,
            MIN_WORLD_SIZE,
            MAX_WORLD_SIZE
        );
        this._billboard = VALID_BILLBOARD_MODES.has(
            config.billboard ?? ('' as UIHostBillboardMode)
        )
            ? config.billboard!
            : 'none';
        this._textureScale = clampNumber(
            config.textureScale,
            DEFAULT_TEXTURE_SCALE,
            MIN_TEXTURE_SCALE,
            MAX_TEXTURE_SCALE
        );
    }

    get assetId(): string {
        return this._assetId;
    }

    set assetId(value: string) {
        this._assetId = value;
    }

    get renderMode(): UIHostRenderMode {
        return this._renderMode;
    }

    set renderMode(value: UIHostRenderMode) {
        if (VALID_RENDER_MODES.has(value)) {
            this._renderMode = value;
        }
    }

    get receiveInput(): boolean {
        return this._receiveInput;
    }

    set receiveInput(value: boolean) {
        this._receiveInput = value;
    }

    get worldWidth(): number {
        return this._worldWidth;
    }

    set worldWidth(value: number) {
        this._worldWidth = clampNumber(
            value,
            this._worldWidth,
            MIN_WORLD_SIZE,
            MAX_WORLD_SIZE
        );
    }

    get worldHeight(): number {
        return this._worldHeight;
    }

    set worldHeight(value: number) {
        this._worldHeight = clampNumber(
            value,
            this._worldHeight,
            MIN_WORLD_SIZE,
            MAX_WORLD_SIZE
        );
    }

    get billboard(): UIHostBillboardMode {
        return this._billboard;
    }

    set billboard(value: UIHostBillboardMode) {
        if (VALID_BILLBOARD_MODES.has(value)) {
            this._billboard = value;
        }
    }

    get textureScale(): number {
        return this._textureScale;
    }

    set textureScale(value: number) {
        this._textureScale = clampNumber(
            value,
            this._textureScale,
            MIN_TEXTURE_SCALE,
            MAX_TEXTURE_SCALE
        );
    }

    override serialize(): Record<string, unknown> {
        return {
            assetId: this._assetId,
            renderMode: this._renderMode,
            receiveInput: this._receiveInput,
            worldWidth: this._worldWidth,
            worldHeight: this._worldHeight,
            billboard: this._billboard,
            textureScale: this._textureScale,
        };
    }

    override deserialize(data: Record<string, unknown>): void {
        if (typeof data['assetId'] === 'string') {
            this._assetId = data['assetId'];
        }
        if (
            typeof data['renderMode'] === 'string' &&
            VALID_RENDER_MODES.has(data['renderMode'] as UIHostRenderMode)
        ) {
            this._renderMode = data['renderMode'] as UIHostRenderMode;
        }
        if (typeof data['receiveInput'] === 'boolean') {
            this._receiveInput = data['receiveInput'];
        }
        if (data['worldWidth'] !== undefined) {
            this.worldWidth = data['worldWidth'] as number;
        }
        if (data['worldHeight'] !== undefined) {
            this.worldHeight = data['worldHeight'] as number;
        }
        if (
            typeof data['billboard'] === 'string' &&
            VALID_BILLBOARD_MODES.has(data['billboard'] as UIHostBillboardMode)
        ) {
            this._billboard = data['billboard'] as UIHostBillboardMode;
        }
        if (data['textureScale'] !== undefined) {
            this.textureScale = data['textureScale'] as number;
        }
    }
}
