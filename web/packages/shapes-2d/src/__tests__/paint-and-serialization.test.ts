import {
    createCircleShape,
    createEllipseShape,
    createLinearGradientPaint,
    createLineShape,
    createPolygonShape,
    createRadialGradientPaint,
    createRectangleShape,
    createSolidPaint,
    createTriangleShape,
    deserializeShape,
    deserializePaint,
    sampleShapePaint,
    serializePaint,
    serializeShape,
    stringifyPaint,
    stringifyShape,
} from '../index';

describe('@axrone/shapes-2d paint and serialization', () => {
    it('samples linear gradients relative to shape bounds', () => {
        const fill = createLinearGradientPaint({
            start: [0, 0.5],
            end: [1, 0.5],
            units: 'shape-bounds',
            stops: [
                { offset: 0, color: '#ff0000' },
                { offset: 1, color: '#0000ff' },
            ],
        });

        const shape = createRectangleShape({
            x: 0,
            y: 0,
            width: 100,
            height: 100,
            fill,
        });

        const left = sampleShapePaint(shape, 'fill', [0, 50]);
        const center = sampleShapePaint(shape, 'fill', [50, 50]);
        const right = sampleShapePaint(shape, 'fill', [100, 50]);

        expect(left?.r ?? 0).toBeGreaterThan(0.9);
        expect(right?.b ?? 0).toBeGreaterThan(0.9);
        expect(center?.r ?? 0).toBeCloseTo(center?.b ?? 0, 1);
    });

    it('round-trips serialized shapes', () => {
        const shape = createRectangleShape({
            x: 4,
            y: 8,
            width: 32,
            height: 16,
            fill: '#00ff00',
            stroke: {
                paint: '#000000',
                width: 2,
                alignment: 'inside',
            },
            opacity: 0.75,
            name: 'hud-card',
        });

        const serialized = serializeShape(shape);
        const restored = deserializeShape(serialized);
        const restoredSerialized = serializeShape(restored);

        expect(restoredSerialized).toEqual(serialized);
    });

    it('round-trips circle serialization', () => {
        const shape = createCircleShape({ cx: 50, cy: 50, radius: 25, fill: '#ff0000' });
        const serialized = serializeShape(shape);
        const restored = deserializeShape(serialized);
        expect(serializeShape(restored)).toEqual(serialized);
    });

    it('round-trips ellipse serialization', () => {
        const shape = createEllipseShape({ cx: 0, cy: 0, radiusX: 20, radiusY: 10, fill: '#00ff00' });
        const serialized = serializeShape(shape);
        const restored = deserializeShape(serialized);
        expect(serializeShape(restored)).toEqual(serialized);
    });

    it('round-trips triangle serialization', () => {
        const shape = createTriangleShape({ a: [0, 0], b: [10, 0], c: [5, 10], fill: '#0000ff' });
        const serialized = serializeShape(shape);
        const restored = deserializeShape(serialized);
        expect(serializeShape(restored)).toEqual(serialized);
    });

    it('round-trips line serialization', () => {
        const shape = createLineShape({
            start: [0, 0],
            end: [100, 100],
            stroke: { paint: '#333', width: 3 },
        });
        const serialized = serializeShape(shape);
        const restored = deserializeShape(serialized);
        expect(serializeShape(restored)).toEqual(serialized);
    });

    it('round-trips polygon serialization', () => {
        const shape = createPolygonShape({
            outer: { points: [[0, 0], [10, 0], [5, 10]] as readonly [number, number][] },
            fill: '#abcdef',
        });
        const serialized = serializeShape(shape);
        const restored = deserializeShape(serialized);
        expect(serializeShape(restored)).toEqual(serialized);
    });

    it('round-trips gradient paint serialization', () => {
        const linear = createLinearGradientPaint({
            start: [0, 0],
            end: [1, 1],
            stops: [
                { offset: 0, color: '#ff0000' },
                { offset: 0.5, color: '#00ff00' },
                { offset: 1, color: '#0000ff' },
            ],
            spread: 'repeat',
            colorSpace: 'hsl',
        });
        const serialized = serializePaint(linear);
        const restored = deserializePaint(serialized);
        expect(restored.kind).toBe('linear-gradient');

        const radial = createRadialGradientPaint({
            center: [0.5, 0.5],
            radius: 10,
            stops: [{ offset: 0, color: '#fff' }, { offset: 1, color: '#000' }],
        });
        const radialSerialized = serializePaint(radial);
        const radialRestored = deserializePaint(radialSerialized);
        expect(radialRestored.kind).toBe('radial-gradient');
    });

    it('stringifyShape and stringifyPaint produce valid JSON', () => {
        const shape = createRectangleShape({ x: 0, y: 0, width: 10, height: 5, fill: '#f00' });
        const json = stringifyShape(shape);
        expect(() => JSON.parse(json)).not.toThrow();

        const paint = createSolidPaint('#00ff00');
        const paintJson = stringifyPaint(paint);
        expect(() => JSON.parse(paintJson)).not.toThrow();
    });
});
