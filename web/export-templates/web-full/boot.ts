import {
    Component,
    getComponentMetadata,
    type ComponentConstructor,
} from '@axrone/ecs-runtime';
import { AudioListenerComponent, AudioSourceComponent } from '@axrone/audio';
import { BoxCollider3D, CapsuleCollider3D, HingeJoint3D, Rigidbody3D } from '@axrone/physics-3d';
import { Camera, Scene } from '@axrone/scene-3d/facade';
import { ParticleSystem, PrefabNodeBinding } from '@axrone/scene-3d/support';
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
    readonly onProgress?: (stage: string) => void;
};

export type AxroneRuntimeHandle = {
    readonly scene: Scene;
    readonly data: AxroneGameData;
    dispose(): void;
};

const ENGINE_COMPONENT_TYPES: readonly ComponentConstructor[] = Object.freeze([
    Rigidbody3D,
    BoxCollider3D,
    CapsuleCollider3D,
    HingeJoint3D,
    AudioSourceComponent,
    AudioListenerComponent,
    ParticleSystem,
]);

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
    scene: Scene,
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

const applyCameraSelection = (scene: Scene, cameraEntityId: string | null): void => {
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

export const bootAxroneRuntime = async (
    options: AxroneBootOptions = {}
): Promise<AxroneRuntimeHandle> => {
    const dataUrl = options.dataUrl ?? DEFAULT_DATA_URL;
    const container = resolveContainer(options.container);
    const reportProgress = (stage: string): void => {
        options.onProgress?.(stage);
    };

    reportProgress('loading-data');
    const data = await fetchGameData(dataUrl);

    reportProgress('creating-scene');
    const scene = new Scene({
        width: container.clientWidth || data.presentation.width,
        height: container.clientHeight || data.presentation.height,
        autoStart: true,
        parent: container,
        appendToDom: true,
        createCanvas: () => document.createElement('canvas'),
        clearColor: data.environment.clearColor as [number, number, number, number],
        ambientLight: data.environment.ambientLight as [number, number, number],
        skyLight: data.environment.skyLight as [number, number, number],
        groundLight: data.environment.groundLight as [number, number, number],
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
    const scriptComponentTypes = await loadScriptComponentTypes(dataUrl, data.scripts);

    for (const componentType of ENGINE_COMPONENT_TYPES) {
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
