import type { ILens, Path, PathValue } from './types';
import { parsePath, getPathInTarget, setPathInTarget } from './core';

export class Lens<TRoot extends object, TSub> implements ILens<TRoot, TSub> {
    constructor(
        private readonly getter: (root: TRoot) => TSub,
        private readonly setter: (sub: TSub, root: TRoot) => TRoot
    ) {}

    public get(root: TRoot): TSub {
        return this.getter(root);
    }

    public set(sub: TSub, root: TRoot): TRoot {
        return this.setter(sub, root);
    }

    public compose<TNested>(inner: ILens<TSub, TNested>): Lens<TRoot, TNested> {
        return new Lens<TRoot, TNested>(
            (root) => inner.get(this.getter(root)),
            (sub, root) => this.setter(inner.set(sub, this.getter(root)), root)
        );
    }

    public static fromPath<TRoot extends object, P extends Path<TRoot>>(
        path: P
    ): Lens<TRoot, PathValue<TRoot, P>> {
        const segments = parsePath(path);
        return new Lens<TRoot, PathValue<TRoot, P>>(
            (root) => getPathInTarget(root, segments) as PathValue<TRoot, P>,
            (sub, root) => {
                const copy = Object.assign({}, root);
                setPathInTarget(copy, segments, sub);
                return copy as TRoot;
            }
        );
    }
}
