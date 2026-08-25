import type { DeepReadonly } from './types';
import { assertSafeKey } from './errors';

const PATH_CACHE = new Map<string, readonly string[]>();

export function parsePath(path: string): readonly string[] {
    let segments = PATH_CACHE.get(path);
    if (segments !== undefined) {
        return segments;
    }

    segments = Object.freeze(path.split('.'));
    for (let i = 0; i < segments.length; i++) {
        assertSafeKey(segments[i]!);
    }

    if (PATH_CACHE.size > 2048) {
        PATH_CACHE.clear();
    }

    PATH_CACHE.set(path, segments);
    return segments;
}

export const DeltaKind = {
    DIRECT: 0,
    PATH: 1,
    MERGE: 2,
    MUTATION: 3,
} as const;

export type DeltaKind = (typeof DeltaKind)[keyof typeof DeltaKind];

export type DeltaRecord =
    | { readonly kind: 0; readonly key: PropertyKey; readonly value: unknown }
    | { readonly kind: 1; readonly segments: readonly string[]; readonly value: unknown }
    | { readonly kind: 2; readonly partial: Record<PropertyKey, unknown> }
    | { readonly kind: 3; readonly fn: (draft: Record<string, unknown>) => void };

export class StateNode {
    public readonly parent: StateNode | null;
    public readonly delta: DeltaRecord | null;
    public readonly height: number;

    constructor(parent: StateNode | null, delta: DeltaRecord | null) {
        this.parent = parent;
        this.delta = delta;
        this.height = parent === null ? 0 : parent.height + 1;
    }

    public materialize(seed: Record<string, unknown>): Record<string, unknown> {
        const chain: DeltaRecord[] = new Array(this.height);
        let curr: StateNode | null = this;
        let idx = this.height - 1;

        while (curr !== null && curr.delta !== null) {
            chain[idx--] = curr.delta;
            curr = curr.parent;
        }

        const out = Object.assign({}, seed);

        for (let i = 0; i < chain.length; i++) {
            const record = chain[i]!;
            switch (record.kind) {
                case DeltaKind.DIRECT: {
                    out[record.key as string] = record.value;
                    break;
                }
                case DeltaKind.PATH: {
                    setPathInTarget(out, record.segments, record.value);
                    break;
                }
                case DeltaKind.MERGE: {
                    Object.assign(out, record.partial);
                    break;
                }
                case DeltaKind.MUTATION: {
                    record.fn(out);
                    break;
                }
            }
        }

        return out;
    }
}

export function setPathInTarget(
    root: Record<string, unknown>,
    segments: readonly string[],
    value: unknown
): void {
    let curr: any = root;
    const lastIndex = segments.length - 1;

    for (let i = 0; i < lastIndex; i++) {
        const segment = segments[i]!;
        const nextSegment = segments[i + 1]!;
        const isNextNumeric = /^\d+$/.test(nextSegment);

        let next = curr[segment];
        if (next === null || typeof next !== 'object') {
            next = isNextNumeric ? [] : {};
            curr[segment] = next;
        } else if (Array.isArray(next)) {
            next = next.slice();
            curr[segment] = next;
        } else {
            next = Object.assign({}, next);
            curr[segment] = next;
        }
        curr = next;
    }

    const terminal = segments[lastIndex]!;
    if (Array.isArray(curr)) {
        const copy = curr.slice();
        copy[Number(terminal)] = value;
        curr = copy;
    } else {
        curr[terminal] = value;
    }
}

export function getPathInTarget(root: unknown, segments: readonly string[]): unknown {
    let curr: any = root;
    for (let i = 0; i < segments.length; i++) {
        if (curr === null || typeof curr !== 'object') {
            return undefined;
        }
        curr = curr[segments[i]!];
    }
    return curr;
}

export function fastDeepFreeze<T>(obj: T): DeepReadonly<T> {
    if (obj === null || typeof obj !== 'object' || Object.isFrozen(obj)) {
        return obj as DeepReadonly<T>;
    }

    Object.freeze(obj);

    const keys = Object.getOwnPropertyNames(obj);
    for (let i = 0; i < keys.length; i++) {
        const prop = (obj as Record<string, unknown>)[keys[i]!];
        if (prop !== null && (typeof prop === 'object' || typeof prop === 'function')) {
            fastDeepFreeze(prop);
        }
    }

    return obj as DeepReadonly<T>;
}
