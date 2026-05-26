import { EMPTY_ARRAY } from './gltf-constants';
import { inferTextureFormat } from './gltf-texture';
import type { AssetImportDiagnostic, AssetWriteInput } from '../asset-contract';
import type { GltfTextureAsset } from '../types';
import type {
    GltfAssetSchema,
    GltfAssetSchemaLike,
    GltfTextureTranscodeRequest,
    GltfTextureTranscodeResult,
    GltfTextureTranscodeStageOptions,
    GltfTextureTranscoder,
    GltfTranscodeStage,
} from '../types';

export class GltfTextureTranscoderRegistry {
    private readonly _transcoders = new Map<string, GltfTextureTranscoder>();

    constructor(transcoders: readonly GltfTextureTranscoder[] = EMPTY_ARRAY) {
        for (const transcoder of transcoders) {
            this.register(transcoder);
        }
    }

    register(transcoder: GltfTextureTranscoder): this {
        this._transcoders.set(transcoder.id, transcoder);
        return this;
    }

    unregister(id: string): boolean {
        return this._transcoders.delete(id);
    }

    list(): readonly GltfTextureTranscoder[] {
        return Object.freeze(
            [...this._transcoders.values()].sort(
                (left, right) =>
                    (right.priority ?? 0) - (left.priority ?? 0) ||
                    left.id.localeCompare(right.id)
            )
        );
    }

    resolve(request: Readonly<GltfTextureTranscodeRequest>): GltfTextureTranscoder | undefined {
        return this.list().find((transcoder) => transcoder.canTranscode(request));
    }

    async transcode(
        request: Readonly<GltfTextureTranscodeRequest>
    ): Promise<GltfTextureTranscodeResult | undefined> {
        const transcoder = this.resolve(request);
        return transcoder ? transcoder.transcode(request) : undefined;
    }
}

export const isTextureWrite = <TSchema extends GltfAssetSchemaLike>(
    input: AssetWriteInput<TSchema>
): boolean => input.kind === 'gltf.texture';

export const applyTextureTranscode = <TSchema extends GltfAssetSchemaLike>(
    input: AssetWriteInput<TSchema>,
    result: GltfTextureTranscodeResult
): AssetWriteInput<TSchema> => {
    const data = input.data as unknown as GltfTextureAsset;
    const updated = Object.freeze({
        ...data,
        payload: result.payload ?? data.payload,
        runtimeFormat: result.runtimeFormat ?? data.runtimeFormat,
        transcode: result.state,
    }) as unknown as TSchema['gltf.texture'];

    return Object.freeze({
        ...input,
        data: updated,
    }) as unknown as AssetWriteInput<TSchema>;
};

export const createGltfTextureTranscodeStage = <
    TSchema extends GltfAssetSchemaLike = GltfAssetSchema,
>(
    options: GltfTextureTranscodeStageOptions<TSchema> = {}
): GltfTranscodeStage<TSchema> => {
    const registry = options.registry ?? new GltfTextureTranscoderRegistry();

    return {
        id: options.id ?? 'gltf.texture.transcode',
        phases: ['after-import'],
        run: async (context) => {
            if (context.phase !== 'after-import') {
                return {};
            }

            const { result, signal } = context;
            const diagnostics: AssetImportDiagnostic[] = [];
            let primary = result.primary;
            let primaryChanged = false;
            let additionalChanged = false;
            const additional = result.additional ? [...result.additional] : undefined;

            if (isTextureWrite(primary)) {
                const transcode = await registry.transcode({
                    texture: primary.data as unknown as GltfTextureAsset,
                    signal,
                });
                if (transcode) {
                    primary = applyTextureTranscode(primary, transcode);
                    primaryChanged = true;
                    if (transcode.diagnostics?.length) {
                        diagnostics.push(...transcode.diagnostics);
                    }
                }
            }

            if (additional) {
                for (let index = 0; index < additional.length; index += 1) {
                    const entry = additional[index]!;
                    if (!isTextureWrite(entry)) {
                        continue;
                    }

                    const transcode = await registry.transcode({
                        texture: entry.data as unknown as GltfTextureAsset,
                        signal,
                    });
                    if (!transcode) {
                        continue;
                    }

                    additional[index] = applyTextureTranscode(entry, transcode);
                    additionalChanged = true;
                    if (transcode.diagnostics?.length) {
                        diagnostics.push(...transcode.diagnostics);
                    }
                }
            }

            if (!primaryChanged && !additionalChanged && diagnostics.length === 0) {
                return {};
            }

            return {
                result: Object.freeze({
                    ...result,
                    primary,
                    ...(additional
                        ? {
                              additional: Object.freeze(additional),
                          }
                        : {}),
                    diagnostics:
                        diagnostics.length > 0
                            ? Object.freeze([
                                  ...(result.diagnostics ?? EMPTY_ARRAY),
                                  ...diagnostics,
                              ])
                            : result.diagnostics,
                }),
            };
        },
    };
};

export const createPassthroughGltfTextureTranscoder = (
    targetFormat?: import('@axrone/render-webgl2').TextureFormat
): GltfTextureTranscoder => ({
    id: 'gltf.texture.passthrough',
    priority: -100,
    canTranscode: () => true,
    transcode: ({ texture }) => ({
        runtimeFormat: texture.runtimeFormat ?? inferTextureFormat(texture.payload) ?? targetFormat,
        state: {
            status: 'source',
            transcoderId: 'gltf.texture.passthrough',
            targetFormat:
                texture.runtimeFormat ?? inferTextureFormat(texture.payload) ?? targetFormat,
        },
    }),
});
