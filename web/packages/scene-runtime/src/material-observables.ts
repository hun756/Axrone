import {
    createSubject,
    type IObservableSubject,
} from '@axrone/observer';

export interface SceneMaterialLifecycleEvent {
    readonly materialId: string;
}

export interface SceneMaterialCloneEvent {
    readonly sourceId: string;
    readonly cloneId: string;
}

export interface SceneMaterialUniformChangeEvent {
    readonly materialId: string;
    readonly uniformName: string;
}

export interface SceneMaterialKeywordChangeEvent {
    readonly materialId: string;
    readonly keyword: string;
    readonly enabled: boolean;
}

export interface SceneMaterialTextureChangeEvent {
    readonly materialId: string;
    readonly slotName: string;
}

export class SceneMaterialObservables {
    readonly materialCreated: IObservableSubject<SceneMaterialLifecycleEvent>;
    readonly materialDeleted: IObservableSubject<SceneMaterialLifecycleEvent>;
    readonly materialCloned: IObservableSubject<SceneMaterialCloneEvent>;
    readonly uniformChanged: IObservableSubject<SceneMaterialUniformChangeEvent>;
    readonly keywordChanged: IObservableSubject<SceneMaterialKeywordChangeEvent>;
    readonly textureChanged: IObservableSubject<SceneMaterialTextureChangeEvent>;

    private _disposed = false;

    constructor() {
        this.materialCreated = createSubject<SceneMaterialLifecycleEvent>();
        this.materialDeleted = createSubject<SceneMaterialLifecycleEvent>();
        this.materialCloned = createSubject<SceneMaterialCloneEvent>();
        this.uniformChanged = createSubject<SceneMaterialUniformChangeEvent>();
        this.keywordChanged = createSubject<SceneMaterialKeywordChangeEvent>();
        this.textureChanged = createSubject<SceneMaterialTextureChangeEvent>();
    }

    dispose(): void {
        this._disposed = true;
        this.materialCreated.dispose();
        this.materialDeleted.dispose();
        this.materialCloned.dispose();
        this.uniformChanged.dispose();
        this.keywordChanged.dispose();
        this.textureChanged.dispose();
    }

    /** @internal */
    _notifyMaterialCreated(materialId: string): void {
        if (this._disposed) { return; }
        void this.materialCreated.notify({ materialId }).catch((error) => {
            console.error('Failed to notify materialCreated:', error);
        });
    }

    /** @internal */
    _notifyMaterialDeleted(materialId: string): void {
        if (this._disposed) { return; }
        void this.materialDeleted.notify({ materialId }).catch((error) => {
            console.error('Failed to notify materialDeleted:', error);
        });
    }

    /** @internal */
    _notifyMaterialCloned(sourceId: string, cloneId: string): void {
        if (this._disposed) { return; }
        void this.materialCloned.notify({ sourceId, cloneId }).catch((error) => {
            console.error('Failed to notify materialCloned:', error);
        });
    }

    /** @internal */
    _notifyUniformChanged(materialId: string, uniformName: string): void {
        if (this._disposed) { return; }
        void this.uniformChanged.notify({ materialId, uniformName }).catch((error) => {
            console.error('Failed to notify uniformChanged:', error);
        });
    }

    /** @internal */
    _notifyKeywordChanged(materialId: string, keyword: string, enabled: boolean): void {
        if (this._disposed) { return; }
        void this.keywordChanged
            .notify({ materialId, keyword, enabled })
            .catch((error) => {
                console.error('Failed to notify keywordChanged:', error);
            });
    }

    /** @internal */
    _notifyTextureChanged(materialId: string, slotName: string): void {
        if (this._disposed) { return; }
        void this.textureChanged.notify({ materialId, slotName }).catch((error) => {
            console.error('Failed to notify textureChanged:', error);
        });
    }
}
