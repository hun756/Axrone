import {
    EPSILON,
    assertFiniteNumber,
    assertPositiveNumber,
    distanceSquared,
    isConvexPolygon,
    isSimplePolygon,
    normalizeContourOrientation,
    pointInConvexPolygon,
    polygonAbsoluteArea,
    polygonSignedArea,
    toPoint,
} from './common';
import { ShapeValidationError } from './errors';
import { createPaint, createStroke } from './paint';
import type {
    CircleShape,
    CircleShapeInput,
    EllipseShape,
    EllipseShapeInput,
    LineShape,
    LineShapeInput,
    PolygonRing,
    PolygonRingInput,
    PolygonShape,
    PolygonShapeInput,
    PolygonWinding,
    RectangleShape,
    RectangleShapeInput,
    Shape2D,
    ShapeAppearance,
    ShapeAppearanceInput,
    ShapeKind,
    TriangleShape,
    TriangleShapeInput,
} from './types';
import { clamp01, type IVec2Like } from "@axrone/numeric";

const normalizeAppearance = (
    input: ShapeAppearanceInput = {},
    allowFill: boolean = true
): ShapeAppearance => {
    const fill = input.fill === undefined || input.fill === null ? null : createPaint(input.fill);
    if (!allowFill && fill) {
        throw new ShapeValidationError('Line shapes do not support fill paint');
    }

    const stroke =
        input.stroke === undefined || input.stroke === null ? null : createStroke(input.stroke);
    const opacity = clamp01(input.opacity ?? 1);
    const visible = input.visible ?? true;
    const name = input.name?.trim() || undefined;

    return {
        fill,
        stroke,
        opacity,
        visible,
        name,
    };
};

export const createRectangleShape = (input: RectangleShapeInput): RectangleShape => {
    const width = assertPositiveNumber(input.width, 'rectangle.width');
    const height = assertPositiveNumber(input.height, 'rectangle.height');

    return Object.freeze({
        kind: 'rectangle',
        x: assertFiniteNumber(input.x, 'rectangle.x'),
        y: assertFiniteNumber(input.y, 'rectangle.y'),
        width,
        height,
        ...normalizeAppearance(input),
    });
};

export const createCircleShape = (input: CircleShapeInput): CircleShape =>
    Object.freeze({
        kind: 'circle',
        cx: assertFiniteNumber(input.cx, 'circle.cx'),
        cy: assertFiniteNumber(input.cy, 'circle.cy'),
        radius: assertPositiveNumber(input.radius, 'circle.radius'),
        ...normalizeAppearance(input),
    });

export const createEllipseShape = (input: EllipseShapeInput): EllipseShape =>
    Object.freeze({
        kind: 'ellipse',
        cx: assertFiniteNumber(input.cx, 'ellipse.cx'),
        cy: assertFiniteNumber(input.cy, 'ellipse.cy'),
        radiusX: assertPositiveNumber(input.radiusX, 'ellipse.radiusX'),
        radiusY: assertPositiveNumber(input.radiusY, 'ellipse.radiusY'),
        ...normalizeAppearance(input),
    });

export const createTriangleShape = (input: TriangleShapeInput): TriangleShape => {
    const a = toPoint(input.a, 'triangle.a');
    const b = toPoint(input.b, 'triangle.b');
    const c = toPoint(input.c, 'triangle.c');
    const doubledArea =
        a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y);

    if (Math.abs(doubledArea) <= EPSILON) {
        throw new ShapeValidationError('Triangle points must not be collinear');
    }

    return Object.freeze({
        kind: 'triangle',
        a,
        b,
        c,
        ...normalizeAppearance(input),
    });
};

export const createLineShape = (input: LineShapeInput): LineShape => {
    const start = toPoint(input.start, 'line.start');
    const end = toPoint(input.end, 'line.end');

    if (distanceSquared(start.x, start.y, end.x, end.y) <= EPSILON && !input.stroke) {
        throw new ShapeValidationError('Zero-length lines require a stroke');
    }

    return Object.freeze({
        kind: 'line',
        start,
        end,
        ...normalizeAppearance(input, false),
    });
};

const normalizePolygonRing = (
    ring: PolygonRingInput,
    name: string,
    requireCcw: boolean
): PolygonRing => {
    if (!Array.isArray(ring.points) || ring.points.length < 3) {
        throw new ShapeValidationError(`${name} must contain at least 3 points`);
    }
    const points: Readonly<IVec2Like>[] = ring.points.map((p, idx) =>
        toPoint(p, `${name}.points[${idx}]`)
    );
    const flat = new Float32Array(points.length * 2);
    for (let i = 0; i < points.length; i++) {
        flat[i * 2] = (points[i] as Readonly<IVec2Like>).x;
        flat[i * 2 + 1] = (points[i] as Readonly<IVec2Like>).y;
    }
    const signedArea = polygonSignedArea(flat);
    const detectedCcw = signedArea > 0;
    const winding: PolygonWinding = ring.winding ?? (detectedCcw ? 'ccw' : 'cw');

    if (polygonAbsoluteArea(flat) <= EPSILON) {
        throw new ShapeValidationError(`${name} has zero signed area; ring is degenerate`);
    }
    if (!isSimplePolygon(flat)) {
        throw new ShapeValidationError(`${name} is not a simple polygon (edges self-intersect)`);
    }

    if (requireCcw && winding !== 'ccw') {
        const reversed = normalizeContourOrientation(flat, true);
        for (let i = 0; i < points.length; i++) {
            (points[i] as { x: number; y: number }).x = reversed[i * 2] as number;
            (points[i] as { x: number; y: number }).y = reversed[i * 2 + 1] as number;
        }
    }

    return Object.freeze({
        points: Object.freeze(points.slice()) as readonly Readonly<IVec2Like>[],
        winding,
    });
};

export const createPolygonShape = (input: PolygonShapeInput): PolygonShape => {
    let outerInput: PolygonRingInput | null = null;
    if (input.outer) {
        outerInput = input.outer;
    } else if (Array.isArray(input.points)) {
        outerInput = { points: input.points };
    }

    if (!outerInput) {
        throw new ShapeValidationError(
            'Polygon shape requires either "outer" ring or "points" array'
        );
    }

    const outer = normalizePolygonRing(outerInput, 'polygon.outer', true);
    const outerFlat = new Float32Array(outer.points.length * 2);
    for (let i = 0; i < outer.points.length; i++) {
        outerFlat[i * 2] = (outer.points[i] as Readonly<IVec2Like>).x;
        outerFlat[i * 2 + 1] = (outer.points[i] as Readonly<IVec2Like>).y;
    }
    const convex = isConvexPolygon(outerFlat);

    const holes: PolygonRing[] = [];
    if (input.holes) {
        for (let i = 0; i < input.holes.length; i++) {
            const hole = normalizePolygonRing(
                input.holes[i] as PolygonRingInput,
                `polygon.holes[${i}]`,
                false
            );
            if (hole.winding !== 'cw') {
                throw new ShapeValidationError(
                    `polygon.holes[${i}] must be wound clockwise; got "${hole.winding}"`
                );
            }
            holes.push(hole);
        }
    }

    if (input.winding && input.winding !== outer.winding) {
        throw new ShapeValidationError(
            `Polygon declared winding "${input.winding}" but outer ring detected "${outer.winding}"`
        );
    }

    const closed = input.closed ?? true;
    if (!closed) {
        throw new ShapeValidationError('Open polygons are not supported by createPolygonShape');
    }

    void pointInConvexPolygon;

    return Object.freeze({
        kind: 'polygon',
        outer,
        holes: Object.freeze(holes) as readonly PolygonRing[],
        closed,
        convex,
        ...normalizeAppearance(input),
    });
};

export const isPolygonShape = (value: unknown): value is PolygonShape =>
    !!value && typeof value === 'object' && 'kind' in value && value.kind === 'polygon';

export const isRectangleShape = (value: unknown): value is RectangleShape =>
    !!value && typeof value === 'object' && 'kind' in value && value.kind === 'rectangle';

export const isCircleShape = (value: unknown): value is CircleShape =>
    !!value && typeof value === 'object' && 'kind' in value && value.kind === 'circle';

export const isEllipseShape = (value: unknown): value is EllipseShape =>
    !!value && typeof value === 'object' && 'kind' in value && value.kind === 'ellipse';

export const isTriangleShape = (value: unknown): value is TriangleShape =>
    !!value && typeof value === 'object' && 'kind' in value && value.kind === 'triangle';

export const isLineShape = (value: unknown): value is LineShape =>
    !!value && typeof value === 'object' && 'kind' in value && value.kind === 'line';

export const isShape2D = (value: unknown): value is Shape2D =>
    isRectangleShape(value) ||
    isCircleShape(value) ||
    isEllipseShape(value) ||
    isTriangleShape(value) ||
    isLineShape(value) ||
    isPolygonShape(value);

export const matchShape = <TResult>(
    shape: Shape2D,
    matcher: {
        readonly rectangle: (shape: RectangleShape) => TResult;
        readonly circle: (shape: CircleShape) => TResult;
        readonly ellipse: (shape: EllipseShape) => TResult;
        readonly triangle: (shape: TriangleShape) => TResult;
        readonly line: (shape: LineShape) => TResult;
        readonly polygon: (shape: PolygonShape) => TResult;
    }
): TResult => {
    switch (shape.kind) {
        case 'rectangle':
            return matcher.rectangle(shape);
        case 'circle':
            return matcher.circle(shape);
        case 'ellipse':
            return matcher.ellipse(shape);
        case 'triangle':
            return matcher.triangle(shape);
        case 'line':
            return matcher.line(shape);
        case 'polygon':
            return matcher.polygon(shape);
    }
};
