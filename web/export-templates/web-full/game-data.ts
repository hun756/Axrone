export type AxroneGameDataEnvironment = {
    readonly clearColor: readonly [number, number, number, number];
    readonly ambientLight: readonly [number, number, number];
    readonly skyLight: readonly [number, number, number];
    readonly groundLight: readonly [number, number, number];
};

export type AxroneGameDataScriptEntry = {
    readonly modulePath: string;
    readonly className: string;
    readonly scriptName: string;
};

export type AxroneGameDataComponentState = {
    readonly type: string;
    readonly occurrenceIndex: number;
    readonly enabled: boolean;
};

export type AxroneGameDataComponentStates = {
    readonly nodeId: string;
    readonly states: readonly AxroneGameDataComponentState[];
};

export type AxroneGameData = {
    readonly schemaVersion: 1;
    readonly engine: {
        readonly version: string;
        readonly profileId: string;
    };
    readonly project: {
        readonly name: string;
        readonly sceneName: string;
    };
    readonly presentation: {
        readonly backgroundColor: string;
        readonly width: number;
        readonly height: number;
    };
    readonly environment: AxroneGameDataEnvironment;
    readonly snapshot: unknown;
    readonly componentStates: readonly AxroneGameDataComponentStates[];
    readonly cameraEntityId: string | null;
    readonly scripts: readonly AxroneGameDataScriptEntry[];
    readonly warnings: readonly string[];
};

type BinaryValueDescriptor = {
    readonly $binary: {
        readonly type: string;
        readonly base64: string;
    };
};

type TypedArrayConstructor = {
    new (buffer: ArrayBufferLike): ArrayBufferView;
};

const typedArrayConstructors: Readonly<Record<string, TypedArrayConstructor>> = {
    Int8Array,
    Uint8Array,
    Uint8ClampedArray,
    Int16Array,
    Uint16Array,
    Int32Array,
    Uint32Array,
    Float32Array,
    Float64Array,
};

const isBinaryValueDescriptor = (value: unknown): value is BinaryValueDescriptor => {
    if (!value || typeof value !== 'object' || Array.isArray(value)) {
        return false;
    }

    const descriptor = (value as { $binary?: unknown }).$binary;
    if (!descriptor || typeof descriptor !== 'object') {
        return false;
    }

    const { type, base64 } = descriptor as { type?: unknown; base64?: unknown };
    return typeof type === 'string' && typeof base64 === 'string';
};

const decodeBase64ToBuffer = (base64: string): ArrayBuffer => {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; index += 1) {
        bytes[index] = binary.charCodeAt(index);
    }

    return bytes.buffer;
};

const decodeBinaryValue = (descriptor: BinaryValueDescriptor): unknown => {
    const buffer = decodeBase64ToBuffer(descriptor.$binary.base64);
    if (descriptor.$binary.type === 'ArrayBuffer') {
        return buffer;
    }

    const typedArrayConstructor = typedArrayConstructors[descriptor.$binary.type];
    if (!typedArrayConstructor) {
        throw new Error(
            `Axrone runtime cannot decode binary value of type '${descriptor.$binary.type}'.`
        );
    }

    return new typedArrayConstructor(buffer);
};

const decodeBinaryValues = (value: unknown): unknown => {
    if (isBinaryValueDescriptor(value)) {
        return decodeBinaryValue(value);
    }

    if (Array.isArray(value)) {
        return value.map((entry) => decodeBinaryValues(entry));
    }

    if (value && typeof value === 'object') {
        const decoded: Record<string, unknown> = {};
        for (const [key, entry] of Object.entries(value as Record<string, unknown>)) {
            decoded[key] = decodeBinaryValues(entry);
        }

        return decoded;
    }

    return value;
};

export const decodeAxroneGameData = (value: unknown): AxroneGameData => {
    const decoded = decodeBinaryValues(value) as AxroneGameData;
    if (!decoded || typeof decoded !== 'object') {
        throw new Error('Axrone runtime received an invalid game data document.');
    }

    if (decoded.schemaVersion !== 1) {
        throw new Error(
            `Axrone runtime cannot load game data schema version ${String(decoded.schemaVersion)}.`
        );
    }

    return decoded;
};
