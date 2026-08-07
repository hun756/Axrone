import type { ComponentConstructor, ComponentRegistry } from '@axrone/ecs-runtime';
import type { SceneBuiltInRegistry, SceneRegistry } from './types';

export type SceneBuiltInComponentName = keyof SceneBuiltInRegistry;

export interface SceneBuiltInManifest<
    TBuiltIns extends readonly SceneBuiltInComponentName[] = readonly SceneBuiltInComponentName[],
> {
    readonly id: string;
    readonly builtIns: TBuiltIns;
}

export type SceneRegistryForBuiltIns<
    R extends ComponentRegistry,
    TBuiltIns extends readonly SceneBuiltInComponentName[],
> = R & Pick<SceneBuiltInRegistry, TBuiltIns[number]>;

export interface SceneRegistryBuilderOptions<
    R extends ComponentRegistry = Record<string, never>,
    TBuiltIns extends readonly SceneBuiltInComponentName[] | undefined = undefined,
> {
    readonly registry?: R;
    readonly builtIns?: TBuiltIns;
}

export interface SceneManifestRegistryBuilderOptions<
    R extends ComponentRegistry = Record<string, never>,
> {
    readonly registry?: R;
    readonly manifests?: readonly SceneBuiltInManifest[];
}

export type SceneBuiltInRegistrySource = Partial<
    Record<SceneBuiltInComponentName, ComponentConstructor>
>;

const resolveSourceBuiltIns = (
    source: SceneBuiltInRegistrySource
): readonly SceneBuiltInComponentName[] =>
    Object.freeze(Object.keys(source) as SceneBuiltInComponentName[]);

export const createSceneBuiltInManifest = <
    const TBuiltIns extends readonly SceneBuiltInComponentName[],
>(
    manifest: SceneBuiltInManifest<TBuiltIns>
): SceneBuiltInManifest<TBuiltIns> =>
    Object.freeze({
        id: manifest.id,
        builtIns: Object.freeze([...manifest.builtIns]) as TBuiltIns,
    });

export const resolveSceneBuiltInComponents = (
    manifests: readonly SceneBuiltInManifest[],
    fallbackBuiltIns: readonly SceneBuiltInComponentName[] = []
): readonly SceneBuiltInComponentName[] => {
    const resolved: SceneBuiltInComponentName[] = [];
    const included = new Set<SceneBuiltInComponentName>();
    const source = manifests.length > 0 ? manifests : [{ id: 'scene/fallback', builtIns: fallbackBuiltIns }];

    for (let manifestIndex = 0; manifestIndex < source.length; manifestIndex += 1) {
        const manifest = source[manifestIndex]!;
        for (let builtInIndex = 0; builtInIndex < manifest.builtIns.length; builtInIndex += 1) {
            const builtIn = manifest.builtIns[builtInIndex]!;
            if (included.has(builtIn)) {
                continue;
            }

            included.add(builtIn);
            resolved.push(builtIn);
        }
    }

    return Object.freeze(resolved);
};

export const createSceneRegistryWithSource = <
    R extends ComponentRegistry = Record<string, never>,
    TBuiltIns extends readonly SceneBuiltInComponentName[] | undefined = undefined,
>(
    source: SceneBuiltInRegistrySource,
    options: SceneRegistryBuilderOptions<R, TBuiltIns> = {}
): TBuiltIns extends readonly SceneBuiltInComponentName[]
    ? SceneRegistryForBuiltIns<R, TBuiltIns>
    : SceneRegistry<R> => {
    const registry: Record<string, ComponentConstructor> = {};
    const builtIns = (options.builtIns ?? resolveSourceBuiltIns(source)) as readonly SceneBuiltInComponentName[];

    for (const componentName of builtIns) {
        const component = source[componentName];
        if (!component) {
            continue;
        }

        registry[componentName] = component;
    }

    return {
        ...registry,
        ...(options.registry ?? {}),
    } as TBuiltIns extends readonly SceneBuiltInComponentName[]
        ? SceneRegistryForBuiltIns<R, TBuiltIns>
        : SceneRegistry<R>;
};

export const createSceneRegistryFromBuiltInManifestsWithSource = <
    R extends ComponentRegistry = Record<string, never>,
>(
    source: SceneBuiltInRegistrySource,
    options: SceneManifestRegistryBuilderOptions<R> = {},
    fallbackBuiltIns: readonly SceneBuiltInComponentName[] = resolveSourceBuiltIns(source)
): SceneRegistry<R> =>
    createSceneRegistryWithSource(source, {
        registry: options.registry,
        builtIns: resolveSceneBuiltInComponents(options.manifests ?? [], fallbackBuiltIns),
    });