/**
 * Structural seam for `@property({ type: 'ui-widget' })` script references.
 *
 * scene-runtime never imports UI packages (see ui-boundary architecture
 * tests), so widget resolution is injected from the outside: the WebGL2 UI
 * bridge (@axrone/ui-webgl2) installs a resolver when UIHosts are bound to a
 * scene, and hydrated script properties resolve through it lazily on first
 * use. The interface is intentionally UI-type free — only primitives and
 * plain records cross the boundary.
 */

/** Script-facing widget handle contract fulfilled by @axrone/ui-webgl2's UIWidgetRef. */
export interface SceneUIWidgetRef {
    readonly widgetKey: string;
    isValid(): boolean;
    setText(value: string): boolean;
    setStyle(patch: Record<string, unknown>): boolean;
    setLayout(patch: Record<string, unknown>): boolean;
    setHandlers(handlers: Record<string, unknown>): boolean;
    setEnabled(enabled: boolean): boolean;
}

/** Resolves a live widget handle for a UIHost component instance and widget key. */
export type SceneUIWidgetRefResolver = (
    host: unknown,
    widgetKey: string
) => SceneUIWidgetRef | null;

let activeResolver: SceneUIWidgetRefResolver | null = null;

export const setSceneUIWidgetRefResolver = (
    resolver: SceneUIWidgetRefResolver | null
): void => {
    activeResolver = resolver;
};

export const getSceneUIWidgetRefResolver = (): SceneUIWidgetRefResolver | null => activeResolver;

/**
 * Lazy delegating ref assigned to `@property('ui-widget')` script fields
 * during hydration. UIHost bindings are typically established after scripts
 * are instantiated, so every call re-resolves until a valid handle exists;
 * before that, mutations are no-ops returning false.
 */
export const createLazySceneUIWidgetRef = (
    resolveHost: () => unknown | null,
    widgetKey: string
): SceneUIWidgetRef => {
    let resolved: SceneUIWidgetRef | null = null;

    const ref = (): SceneUIWidgetRef | null => {
        if (resolved && resolved.isValid()) {
            return resolved;
        }
        const resolver = getSceneUIWidgetRefResolver();
        const host = resolveHost();
        resolved = resolver && host ? resolver(host, widgetKey) : null;
        return resolved;
    };

    return {
        widgetKey,
        isValid: () => ref()?.isValid() ?? false,
        setText: (value) => ref()?.setText(value) ?? false,
        setStyle: (patch) => ref()?.setStyle(patch) ?? false,
        setLayout: (patch) => ref()?.setLayout(patch) ?? false,
        setHandlers: (handlers) => ref()?.setHandlers(handlers) ?? false,
        setEnabled: (enabled) => ref()?.setEnabled(enabled) ?? false,
    };
};
