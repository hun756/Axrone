import {
    createCircleShape,
    createEllipseShape,
    createLinearGradientPaint,
    createLineShape,
    createPolygonShape,
    createRectangleShape,
    createSolidPaint,
    createTriangleShape,
} from '../index';

describe('@axrone/shapes-2d primitives', () => {
    it('creates rectangle shapes with appearance data', () => {
        const shape = createRectangleShape({
            x: 10,
            y: 20,
            width: 100,
            height: 50,
            fill: '#ff0000',
            stroke: {
                paint: '#000000',
                width: 10,
            },
        });

        expect(shape.kind).toBe('rectangle');
        expect(shape.fill?.kind).toBe('solid');
        expect(shape.stroke?.paint.kind).toBe('solid');
        expect(shape.opacity).toBe(1);
    });

    it('creates circles and paint descriptors', () => {
        const fill = createLinearGradientPaint({
            start: [0, 0],
            end: [1, 0],
            stops: [
                { offset: 0, color: '#ff0000' },
                { offset: 1, color: '#0000ff' },
            ],
        });

        const shape = createCircleShape({
            cx: 0,
            cy: 0,
            radius: 24,
            fill,
            stroke: {
                paint: '#ffffff',
                width: 6,
            },
        });

        expect(shape.kind).toBe('circle');
        expect(shape.fill?.kind).toBe('linear-gradient');
        expect(shape.stroke?.paint.kind).toBe('solid');
        expect(createSolidPaint('#ffffff').kind).toBe('solid');
    });

    it('creates ellipse shapes with appearance', () => {
        const shape = createEllipseShape({
            cx: 50,
            cy: 50,
            radiusX: 30,
            radiusY: 20,
            fill: '#00ff00',
            opacity: 0.8,
        });

        expect(shape.kind).toBe('ellipse');
        expect(shape.cx).toBe(50);
        expect(shape.cy).toBe(50);
        expect(shape.radiusX).toBe(30);
        expect(shape.radiusY).toBe(20);
        expect(shape.fill?.kind).toBe('solid');
        expect(shape.opacity).toBeCloseTo(0.8, 4);
    });

    it('creates triangle shapes with appearance', () => {
        const shape = createTriangleShape({
            a: [0, 0],
            b: [10, 0],
            c: [5, 10],
            fill: '#0000ff',
            stroke: { paint: '#ffffff', width: 2, alignment: 'outside' },
        });

        expect(shape.kind).toBe('triangle');
        expect(shape.a).toEqual({ x: 0, y: 0 });
        expect(shape.b).toEqual({ x: 10, y: 0 });
        expect(shape.c).toEqual({ x: 5, y: 10 });
        expect(shape.stroke?.alignment).toBe('outside');
    });

    it('creates line shapes with stroke', () => {
        const shape = createLineShape({
            start: [0, 0],
            end: [100, 100],
            stroke: { paint: '#ff0000', width: 3, alignment: 'center' },
            name: 'connector',
        });

        expect(shape.kind).toBe('line');
        expect(shape.start).toEqual({ x: 0, y: 0 });
        expect(shape.end).toEqual({ x: 100, y: 100 });
        expect(shape.stroke?.alignment).toBe('center');
        expect(shape.fill).toBeNull();
        expect(shape.name).toBe('connector');
    });

    it('creates polygon shapes with outer ring', () => {
        const shape = createPolygonShape({
            outer: {
                points: [[0, 0], [10, 0], [5, 10]] as const,
            },
            fill: '#abcdef',
            visible: false,
        });

        expect(shape.kind).toBe('polygon');
        expect(shape.outer.points).toHaveLength(3);
        expect(shape.closed).toBe(true);
        expect(shape.convex).toBe(true);
        expect(shape.holes).toHaveLength(0);
        expect(shape.visible).toBe(false);
    });
});
