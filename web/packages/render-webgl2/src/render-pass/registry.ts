import type { RenderPassKind, ReadonlyRenderResourceRegistry, RenderExecutionContext } from '@axrone/render-core/types';
import {
    defineWebGL2RenderPassExecutor,
    isWebGL2RenderPassExecutorDescriptor,
} from '../pipeline-contracts';
import type {
    WebGL2AnyRenderPassExecutorDescriptor,
    WebGL2AnyRenderPassExecutorRegistration,
    WebGL2RenderPassExecutorDescriptor,
} from '../pipeline-contracts';

interface RegisteredExecutor {
    readonly descriptor: WebGL2AnyRenderPassExecutorDescriptor;
    readonly priority: number;
    readonly sequence: number;
}

const normalizeExecutorRegistration = (
    registration: WebGL2AnyRenderPassExecutorRegistration
): WebGL2AnyRenderPassExecutorDescriptor => {
    const descriptor = isWebGL2RenderPassExecutorDescriptor(registration)
        ? registration
        : defineWebGL2RenderPassExecutor(registration);
    return Object.freeze({
        ...descriptor,
        priority: Number.isFinite(descriptor.priority) ? descriptor.priority : 0,
    });
};

export class WebGL2RenderPassExecutorRegistry {
    private readonly _byKind = new Map<RenderPassKind, readonly WebGL2AnyRenderPassExecutorDescriptor[]>();
    private readonly _all: readonly WebGL2AnyRenderPassExecutorDescriptor[];

    constructor(registrations: readonly WebGL2AnyRenderPassExecutorRegistration[]) {
        const grouped = new Map<RenderPassKind, RegisteredExecutor[]>();
        const all: WebGL2AnyRenderPassExecutorDescriptor[] = [];
        for (let index = 0; index < registrations.length; index += 1) {
            const descriptor = normalizeExecutorRegistration(registrations[index]!);
            const current = grouped.get(descriptor.kind) ?? [];
            current.push({ descriptor, priority: descriptor.priority, sequence: index });
            grouped.set(descriptor.kind, current);
            all.push(descriptor);
        }
        for (const [kind, entries] of grouped.entries()) {
            entries.sort((left, right) =>
                right.priority === left.priority ? left.sequence - right.sequence : right.priority - left.priority
            );
            this._byKind.set(kind, Object.freeze(entries.map((entry) => entry.descriptor)));
        }
        this._all = Object.freeze(all);
    }

    has(kind: RenderPassKind): boolean {
        return (this._byKind.get(kind)?.length ?? 0) > 0;
    }

    list(kind?: RenderPassKind): readonly WebGL2AnyRenderPassExecutorDescriptor[] {
        return kind ? this._byKind.get(kind) ?? [] : this._all;
    }

    resolve<K extends RenderPassKind>(
        pass: import('../pipeline-contracts').WebGL2RenderPassOf<K>,
        context: RenderExecutionContext<import('../pipeline-contracts').WebGL2RenderResourceHandle>,
        execution: import('../pipeline-contracts').WebGL2RenderPassExecutionContext
    ): WebGL2RenderPassExecutorDescriptor<K> | null {
        const descriptors = this._byKind.get(pass.kind);
        if (!descriptors) return null;
        for (let index = 0; index < descriptors.length; index += 1) {
            const descriptor = descriptors[index]!;
            if (descriptor.kind !== pass.kind) continue;
            const typedDescriptor = descriptor as unknown as WebGL2RenderPassExecutorDescriptor<K>;
            if (!typedDescriptor.matches || typedDescriptor.matches(pass, context, execution)) {
                return typedDescriptor;
            }
        }
        return null;
    }
}
