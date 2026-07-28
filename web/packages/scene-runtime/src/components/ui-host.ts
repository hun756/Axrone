import { Component, script } from '@axrone/ecs-runtime';

export type UIHostRenderMode = 'screen-overlay';

export interface UIHostConfig {
    readonly assetId?: string;
    readonly renderMode?: UIHostRenderMode;
    readonly receiveInput?: boolean;
}

const VALID_RENDER_MODES: ReadonlySet<UIHostRenderMode> = new Set<UIHostRenderMode>([
    'screen-overlay',
]);

/**
 * UIHost is a scene component that references a standalone `.ui.json` asset
 * for screen-space UI overlay rendering.
 *
 * This follows the Unreal/UMG "UI as independent asset" architecture:
 * - The UI asset is authored and stored independently from the scene.
 * - The scene only holds a reference (assetId) to the UI asset.
 * - At runtime, a bridge system loads the asset and creates the UI overlay.
 *
 * Properties:
 * - `assetId`: Path/ID of the `.ui.json` asset to load.
 * - `renderMode`: How the UI is rendered (currently only 'screen-overlay').
 * - `receiveInput`: Whether pointer/key events are forwarded to the UI runtime.
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

    constructor(config: UIHostConfig = {}) {
        super();
        this._assetId = config.assetId ?? '';
        this._renderMode = VALID_RENDER_MODES.has(config.renderMode ?? '' as UIHostRenderMode)
            ? config.renderMode!
            : 'screen-overlay';
        this._receiveInput = config.receiveInput ?? true;
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

    override serialize(): Record<string, unknown> {
        return {
            assetId: this._assetId,
            renderMode: this._renderMode,
            receiveInput: this._receiveInput,
        };
    }

    override deserialize(data: Record<string, unknown>): void {
        if (typeof data['assetId'] === 'string') {
            this._assetId = data['assetId'];
        }
        if (typeof data['renderMode'] === 'string' && VALID_RENDER_MODES.has(data['renderMode'] as UIHostRenderMode)) {
            this._renderMode = data['renderMode'] as UIHostRenderMode;
        }
        if (typeof data['receiveInput'] === 'boolean') {
            this._receiveInput = data['receiveInput'];
        }
    }
}
