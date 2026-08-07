import {
    Component,
    getComponentMetadata,
    type ComponentConstructor,
} from '@axrone/ecs-runtime';
import { Camera } from '@axrone/scene-runtime/scene-facade';
import { PrefabNodeBinding } from '@axrone/scene-prefab';
import type { SceneSnapshot } from '@axrone/scene-runtime';
import {
    decodeAxroneGameData,
    type AxroneGameData,
    type AxroneGameDataComponentState,
    type AxroneGameDataScriptEntry,
} from './game-data';

export type AxroneBootOptions = {
    readonly container?: HTMLElement | string | null;
    readonly dataUrl?: string;
    readonly data?: unknown;
    readonly scriptTypes?: readonly ComponentConstructor[];
    readonly onProgress?: (stage: string) => void;
};

export type BootSceneFacade = {
    readonly canvas: HTMLCanvasElement;
    readonly world: {
        getAllActors(): readonly {
            getComponent<T extends ComponentConstructor>(type: T): InstanceType<T> | null;
            getAllComponents(): readonly Component[];
        }[];
    };
    registerComponent(componentType: ComponentConstructor): unknown;
    loadScene(snapshot: SceneSnapshot, options?: { clearExisting?: boolean }): Promise<unknown>;
    resize(width: number, height: number, pixelRatio?: number): void;
    dispose(): void;
};

export type AxroneRuntimeHandle = {
    readonly scene: BootSceneFacade;
    readonly data: AxroneGameData;
    dispose(): void;
};

export type AxroneBootDependencies = {
    readonly createScene: (options: Record<string, unknown>) => BootSceneFacade;
    readonly engineComponentTypes: readonly ComponentConstructor[];
};

const DEFAULT_DATA_URL = './game.data.json';

const resolveContainer = (container: HTMLElement | string | null | undefined): HTMLElement => {
    if (container instanceof HTMLElement) {
        return container;
    }

    if (typeof container === 'string') {
        const resolved = document.querySelector(container);
        if (resolved instanceof HTMLElement) {
            return resolved;
        }

        throw new Error(`Axrone runtime cannot resolve container '${container}'.`);
    }

    return document.body;
};

const resolveModuleUrl = (dataUrl: string, modulePath: string): string =>
    new URL(modulePath, new URL(dataUrl, document.baseURI)).href;

const isScriptComponentType = (value: unknown): value is ComponentConstructor =>
    typeof value === 'function' &&
    (value as { prototype?: unknown }).prototype instanceof Component &&
    Boolean(getComponentMetadata(value as ComponentConstructor));

const resolveScriptComponentType = (
    moduleExports: Record<string, unknown>,
    entry: AxroneGameDataScriptEntry
): ComponentConstructor => {
    const candidates = Object.values(moduleExports).filter(isScriptComponentType);

    const directMatch = entry.className ? moduleExports[entry.className] : undefined;
    if (isScriptComponentType(directMatch)) {
        return directMatch;
    }

    const metadataMatch = candidates.find(
        (candidate) => getComponentMetadata(candidate)?.scriptName === entry.scriptName
    );
    if (metadataMatch) {
        return metadataMatch;
    }

    const classNameMatch = entry.className
        ? candidates.find((candidate) => candidate.name === entry.className)
        : undefined;
    if (classNameMatch) {
        return classNameMatch;
    }

    if (candidates.length === 1) {
        return candidates[0];
    }

    throw new Error(
        `Axrone runtime cannot resolve script component '${entry.className || entry.scriptName}' in '${entry.modulePath}'.`
    );
};

const loadScriptComponentTypes = async (
    dataUrl: string,
    scripts: readonly AxroneGameDataScriptEntry[]
): Promise<readonly ComponentConstructor[]> => {
    const componentTypes: ComponentConstructor[] = [];
    const moduleCache = new Map<string, Record<string, unknown>>();

    for (const entry of scripts) {
        const moduleUrl = resolveModuleUrl(dataUrl, entry.modulePath);
        let moduleExports = moduleCache.get(moduleUrl);
        if (!moduleExports) {
            moduleExports = (await import(/* @vite-ignore */ moduleUrl)) as Record<string, unknown>;
            moduleCache.set(moduleUrl, moduleExports);
        }

        componentTypes.push(resolveScriptComponentType(moduleExports, entry));
    }

    return componentTypes;
};

const resolveComponentTypeName = (component: Component): string =>
    getComponentMetadata(component.constructor as ComponentConstructor)?.scriptName ??
    component.constructor.name;

const applyComponentStates = (
    scene: BootSceneFacade,
    statesByNodeId: ReadonlyMap<string, readonly AxroneGameDataComponentState[]>
): void => {
    for (const actor of scene.world.getAllActors()) {
        const nodeId = actor.getComponent(PrefabNodeBinding)?.nodeId;
        if (!nodeId) {
            continue;
        }

        const componentStates = statesByNodeId.get(nodeId);
        if (!componentStates || componentStates.length === 0) {
            continue;
        }

        const occurrences = new Map<string, number>();
        for (const component of actor.getAllComponents()) {
            const type = resolveComponentTypeName(component);
            const occurrenceIndex = occurrences.get(type) ?? 0;
            occurrences.set(type, occurrenceIndex + 1);

            const matchedState = componentStates.find(
                (candidate) =>
                    candidate.type === type && candidate.occurrenceIndex === occurrenceIndex
            );
            if (matchedState) {
                component.enabled = matchedState.enabled;
            }
        }
    }
};

const applyCameraSelection = (scene: BootSceneFacade, cameraEntityId: string | null): void => {
    let matched = false;
    let fallbackCamera: Camera | null = null;

    for (const actor of scene.world.getAllActors()) {
        const camera = actor.getComponent(Camera);
        if (!camera) {
            continue;
        }

        fallbackCamera ??= camera;
        const isMatch = Boolean(
            cameraEntityId && actor.getComponent(PrefabNodeBinding)?.nodeId === cameraEntityId
        );
        camera.primary = isMatch;
        matched ||= isMatch;
    }

    if (!matched && fallbackCamera) {
        fallbackCamera.primary = true;
    }
};

const fetchGameData = async (dataUrl: string): Promise<AxroneGameData> => {
    const response = await fetch(dataUrl, { cache: 'no-cache' });
    if (!response.ok) {
        throw new Error(
            `Axrone runtime cannot load game data from '${dataUrl}' (${response.status}).`
        );
    }

    return decodeAxroneGameData(await response.json());
};

export const createAxroneBoot =
    (dependencies: AxroneBootDependencies) =>
    async (options: AxroneBootOptions = {}): Promise<AxroneRuntimeHandle> => {
        const dataUrl = options.dataUrl ?? DEFAULT_DATA_URL;
        const container = resolveContainer(options.container);
        const reportProgress = (stage: string): void => {
            options.onProgress?.(stage);
        };

        reportProgress('loading-data');
        const data = options.data
            ? decodeAxroneGameData(options.data)
            : await fetchGameData(dataUrl);

        reportProgress('creating-scene');
        const scene = dependencies.createScene({
            width: container.clientWidth || data.presentation.width,
            height: container.clientHeight || data.presentation.height,
            autoStart: true,
            parent: container,
            appendToDom: true,
            createCanvas: () => document.createElement('canvas'),
            clearColor: data.environment.clearColor,
            ambientLight: data.environment.ambientLight,
            skyLight: data.environment.skyLight,
            groundLight: data.environment.groundLight,
        });

        scene.canvas.style.display = 'block';
        scene.canvas.style.width = '100%';
        scene.canvas.style.height = '100%';

        const resizeScene = (): void => {
            scene.resize(
                container.clientWidth || data.presentation.width,
                container.clientHeight || data.presentation.height,
                window.devicePixelRatio || 1
            );
        };

        const resizeObserver = new ResizeObserver(() => {
            resizeScene();
        });
        resizeObserver.observe(container);
        resizeScene();

        reportProgress('loading-scripts');
        const scriptComponentTypes = options.scriptTypes && options.scriptTypes.length > 0
            ? [...options.scriptTypes]
            : await loadScriptComponentTypes(dataUrl, data.scripts);

        for (const componentType of dependencies.engineComponentTypes) {
            scene.registerComponent(componentType);
        }
        for (const componentType of scriptComponentTypes) {
            scene.registerComponent(componentType);
        }

        reportProgress('loading-scene');
        await scene.loadScene(data.snapshot as SceneSnapshot, { clearExisting: false });

        applyComponentStates(
            scene,
            new Map(data.componentStates.map((entry) => [entry.nodeId, entry.states]))
        );
        applyCameraSelection(scene, data.cameraEntityId);

        // UIHost initialization: resolve the scene-host binding module from the
        // __AXRONE_RUNTIME__ global (set by the generated export template entry)
        // and wire every UIHost component to its .ui.json asset so the UI overlay
        // renders on top of the 3D scene.
        const uiAssetContent = (data.uiAssets ?? {}) as Record<string, unknown>;
        const uiAssetKeys = Object.keys(uiAssetContent);
        console.log(`[Axrone Boot] UI assets: ${uiAssetKeys.length} found`, uiAssetKeys);

        if (uiAssetKeys.length > 0) {
            const axroneRuntime = (globalThis as Record<string, unknown>).__AXRONE_RUNTIME__ as
                | { readonly modules?: Record<string, unknown> }
                | undefined;
            console.log(`[Axrone Boot] __AXRONE_RUNTIME__:`, axroneRuntime ? 'present' : 'MISSING');

            const uiHostModule = axroneRuntime?.modules?.['@axrone/ui-webgl2/scene-host'] as
                | Record<string, unknown>
                | undefined;
            console.log(`[Axrone Boot] ui-webgl2/scene-host module:`, uiHostModule ? 'found' : 'MISSING');

            const bindUIHosts = uiHostModule?.['bindUIHostsToScene'];
            console.log(`[Axrone Boot] bindUIHostsToScene:`, typeof bindUIHosts === 'function' ? 'found' : 'MISSING');

            if (typeof bindUIHosts === 'function') {
                const resolveAssetJson = (assetId: string): string | null => {
                    const asset = uiAssetContent[assetId];
                    return asset ? JSON.stringify(asset) : null;
                };

                const bindingOptions = {
                    scene,
                    resolveAssetJson,
                    input: {
                        target: container,
                        keyboard: true,
                    },
                };

                let bindingSucceeded = false;
                try {
                    const result = (bindUIHosts as (options: Record<string, unknown>) => { handles?: readonly unknown[] })(bindingOptions);
                    console.log(`[Axrone Boot] bindUIHostsToScene result:`, result?.handles?.length ?? 0, 'handle(s)');
                    bindingSucceeded = (result?.handles?.length ?? 0) > 0;
                } catch (error) {
                    console.error(`[Axrone Boot] bindUIHostsToScene threw:`, error);
                }

                // Fallback: if bindUIHostsToScene bound zero hosts (e.g., getAllInstances
                // returned empty due to class identity mismatch in the bundle), walk actors
                // directly and collect UIHost components via getComponent.
                const UIHostClass = uiHostModule?.['UIHost'];
                if (typeof UIHostClass === 'function') {
                    const allInstances = (UIHostClass as { getAllInstances?: () => unknown[] }).getAllInstances?.() ?? [];
                    console.log(`[Axrone Boot] UIHost.getAllInstances(): ${allInstances.length} instance(s)`);

                    if (allInstances.length === 0 || !bindingSucceeded) {
                        const hosts: unknown[] = [];
                        for (const actor of scene.world.getAllActors()) {
                            const uiHost = actor.getComponent(UIHostClass as never);
                            if (uiHost) {
                                hosts.push(uiHost);
                            }
                        }
                        console.log(`[Axrone Boot] Fallback: found ${hosts.length} UIHost(s) via actor walk`);

                        if (hosts.length > 0) {
                            try {
                                (bindUIHosts as (options: Record<string, unknown>) => void)({
                                    ...bindingOptions,
                                    hosts,
                                });
                                console.log(`[Axrone Boot] Fallback binding succeeded`);
                            } catch (fallbackError) {
                                console.error(`[Axrone Boot] Fallback binding also failed:`, fallbackError);
                            }
                        }
                    }
                }
            }
        }

        reportProgress('ready');

        return {
            scene,
            data,
            dispose() {
                resizeObserver.disconnect();
                scene.dispose();
            },
        };
    };
