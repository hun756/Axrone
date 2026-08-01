const CORE_MODULES = [
    '@axrone/ecs-runtime',
    '@axrone/ecs-runtime/query',
    '@axrone/ecs-runtime/storage',
    '@axrone/ecs-runtime/support',
    '@axrone/event',
    '@axrone/game-loop',
    '@axrone/numeric',
    '@axrone/observer',
    '@axrone/random',
    '@axrone/utility',
    '@axrone/render-core',
    '@axrone/render-webgl2',
    '@axrone/scene-runtime',
    '@axrone/scene-runtime/prefab',
    '@axrone/scene-prefab',
    '@axrone/animation',
    '@axrone/input',
    '@axrone/tween',
    '@axrone/geometry',
    '@axrone/raycast',
    '@axrone/runtime-profile-core',
];

const MODULES_2D = [
    '@axrone/scene-2d',
    '@axrone/asset-2d',
    '@axrone/render-2d',
    '@axrone/physics-core',
    '@axrone/physics-2d',
    '@axrone/shapes-2d',
    '@axrone/ui',
    '@axrone/runtime-profile-2d',
];

const MODULES_3D = [
    '@axrone/scene-3d',
    '@axrone/scene-3d/facade',
    '@axrone/scene-3d/support',
    '@axrone/asset-core',
    '@axrone/asset-gltf',
    '@axrone/asset-shader',
    '@axrone/asset-ui',
    '@axrone/render-3d',
    '@axrone/physics-core',
    '@axrone/physics-3d',
    '@axrone/lighting',
    '@axrone/terrain',
    '@axrone/ui-webgl2',
    '@axrone/audio',
    '@axrone/particle-system',
    '@axrone/scene-runtime-gltf',
    '@axrone/runtime-profile-3d',
];

const dedupe = (modules) => [...new Set(modules)];

export const PROFILE_MODULE_CATALOG = Object.freeze({
    '2d': dedupe([...CORE_MODULES, ...MODULES_2D]),
    '3d': dedupe([...CORE_MODULES, ...MODULES_3D]),
    full: dedupe([...CORE_MODULES, ...MODULES_2D, ...MODULES_3D, '@axrone/runtime-profile-full']),
});

export const PROFILE_IDS = Object.freeze({
    '2d': 'scene/2d-default',
    '3d': 'scene/3d-default',
    full: 'scene/3d-full',
});

export const TEMPLATE_DEFINITIONS = Object.freeze([
    {
        id: 'web-2d',
        profile: '2d',
        sceneFacadeModule: '@axrone/scene-2d',
        sceneFacadeExport: 'Scene2D',
        engineComponents: [
            { module: '@axrone/physics-2d', exports: ['Rigidbody2D', 'BoxCollider2D', 'CircleCollider2D', 'CapsuleCollider2D'] },
        ],
    },
    {
        id: 'web-3d',
        profile: '3d',
        sceneFacadeModule: '@axrone/scene-3d/facade',
        sceneFacadeExport: 'Scene',
        engineComponents: [
            { module: '@axrone/physics-3d', exports: ['Rigidbody3D', 'BoxCollider3D', 'CapsuleCollider3D', 'HingeJoint3D'] },
            { module: '@axrone/audio', exports: ['AudioSourceComponent', 'AudioListenerComponent'] },
            { module: '@axrone/scene-3d/support', exports: ['ParticleSystem'] },
        ],
    },
    {
        id: 'web-full',
        profile: 'full',
        sceneFacadeModule: '@axrone/scene-3d/facade',
        sceneFacadeExport: 'Scene',
        engineComponents: [
            { module: '@axrone/physics-3d', exports: ['Rigidbody3D', 'BoxCollider3D', 'CapsuleCollider3D', 'HingeJoint3D'] },
            { module: '@axrone/audio', exports: ['AudioSourceComponent', 'AudioListenerComponent'] },
            { module: '@axrone/scene-3d/support', exports: ['ParticleSystem'] },
        ],
    },
]);
