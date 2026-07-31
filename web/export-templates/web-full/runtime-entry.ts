import * as animation from '@axrone/animation';
import * as asset2d from '@axrone/asset-2d';
import * as assetCore from '@axrone/asset-core';
import * as assetGltf from '@axrone/asset-gltf';
import * as assetShader from '@axrone/asset-shader';
import * as audio from '@axrone/audio';
import * as ecsQuery from '@axrone/ecs-runtime/query';
import * as ecsRuntime from '@axrone/ecs-runtime';
import * as ecsStorage from '@axrone/ecs-runtime/storage';
import * as ecsWorldSupport from '@axrone/ecs-runtime/support';
import * as event from '@axrone/event';
import * as gameLoop from '@axrone/game-loop';
import * as geometry from '@axrone/geometry';
import * as input from '@axrone/input';
import * as inputCore from '@axrone/input-core';
import * as numeric from '@axrone/numeric';
import * as observer from '@axrone/observer';
import * as particleSystem from '@axrone/particle-system';
import * as physics from '@axrone/physics';
import * as physics2d from '@axrone/physics-2d';
import * as physics3d from '@axrone/physics-3d';
import * as physicsCore from '@axrone/physics-core';
import * as random from '@axrone/random';
import * as raycast from '@axrone/raycast';
import * as render2d from '@axrone/render-2d';
import * as render3d from '@axrone/render-3d';
import * as renderCore from '@axrone/render-core';
import * as renderWebgl2 from '@axrone/render-webgl2';
import * as runtimeProfile2d from '@axrone/runtime-profile-2d';
import * as runtimeProfile3d from '@axrone/runtime-profile-3d';
import * as runtimeProfileCore from '@axrone/runtime-profile-core';
import * as runtimeProfileFull from '@axrone/runtime-profile-full';
import * as scene2d from '@axrone/scene-2d';
import * as scene3d from '@axrone/scene-3d';
import * as scenePrefab from '@axrone/scene-prefab';
import * as sceneRuntime from '@axrone/scene-runtime';
import * as sceneRuntimeGltf from '@axrone/scene-runtime-gltf';
import * as sceneRuntimePrefab from '@axrone/scene-runtime/prefab';
import * as shapes2d from '@axrone/shapes-2d';
import * as tween from '@axrone/tween';
import * as ui from '@axrone/ui';
import * as uiWebgl2 from '@axrone/ui-webgl2';
import * as utility from '@axrone/utility';
import { bootAxroneRuntime, type AxroneBootOptions, type AxroneRuntimeHandle } from './boot';

declare const __AXRONE_TEMPLATE_VERSION__: string;
declare const __AXRONE_TEMPLATE_PROFILE_ID__: string;

export type AxroneRuntimeGlobal = {
    readonly version: string;
    readonly profileId: string;
    readonly modules: Readonly<Record<string, unknown>>;
    readonly boot: (options?: AxroneBootOptions) => Promise<AxroneRuntimeHandle>;
};

const modules: Readonly<Record<string, unknown>> = Object.freeze({
    '@axrone/animation': animation,
    '@axrone/asset-2d': asset2d,
    '@axrone/asset-core': assetCore,
    '@axrone/asset-gltf': assetGltf,
    '@axrone/asset-shader': assetShader,
    '@axrone/audio': audio,
    '@axrone/ecs-query': ecsQuery,
    '@axrone/ecs-runtime': ecsRuntime,
    '@axrone/ecs-storage': ecsStorage,
    '@axrone/ecs-world-support': ecsWorldSupport,
    '@axrone/event': event,
    '@axrone/game-loop': gameLoop,
    '@axrone/geometry': geometry,
    '@axrone/input': input,
    '@axrone/input-core': inputCore,
    '@axrone/numeric': numeric,
    '@axrone/observer': observer,
    '@axrone/particle-system': particleSystem,
    '@axrone/physics': physics,
    '@axrone/physics-2d': physics2d,
    '@axrone/physics-3d': physics3d,
    '@axrone/physics-core': physicsCore,
    '@axrone/random': random,
    '@axrone/raycast': raycast,
    '@axrone/render-2d': render2d,
    '@axrone/render-3d': render3d,
    '@axrone/render-core': renderCore,
    '@axrone/render-webgl2': renderWebgl2,
    '@axrone/runtime-profile-2d': runtimeProfile2d,
    '@axrone/runtime-profile-3d': runtimeProfile3d,
    '@axrone/runtime-profile-core': runtimeProfileCore,
    '@axrone/runtime-profile-full': runtimeProfileFull,
    '@axrone/scene-2d': scene2d,
    '@axrone/scene-3d': scene3d,
    '@axrone/scene-prefab': scenePrefab,
    '@axrone/scene-runtime': sceneRuntime,
    '@axrone/scene-runtime-gltf': sceneRuntimeGltf,
    '@axrone/scene-runtime/prefab': sceneRuntimePrefab,
    '@axrone/shapes-2d': shapes2d,
    '@axrone/tween': tween,
    '@axrone/ui': ui,
    '@axrone/ui-webgl2': uiWebgl2,
    '@axrone/utility': utility,
});

const runtime: AxroneRuntimeGlobal = Object.freeze({
    version: __AXRONE_TEMPLATE_VERSION__,
    profileId: __AXRONE_TEMPLATE_PROFILE_ID__,
    modules,
    boot: bootAxroneRuntime,
});

Reflect.set(globalThis, '__AXRONE_RUNTIME__', runtime);

export default runtime;
