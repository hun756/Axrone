import { describe, expect, it } from 'vitest';
import { UIRuntime } from '../index';
import { createTestFontAsset } from './test-font';

describe('@axrone/ui widget translation', () => {
    it('stores translation as absolute offset without float drift', () => {
        const runtime = new UIRuntime({ width: 400, height: 200 });
        runtime.fonts.registerFace(createTestFontAsset());

        const widget = runtime.createWidget({
            layout: { width: 100, height: 50 },
        });
        runtime.appendChild(runtime.root, widget);
        runtime.commit();

        const originalBox = runtime.getLayoutBox(widget);

        // Translate 1000 times by (0.1, 0.1)
        for (let i = 0; i < 1000; i++) {
            runtime.translateWidgetBox(widget, 0.1, 0.1);
        }

        const translatedBox = runtime.getLayoutBox(widget);

        // Translate back 1000 times by (-0.1, -0.1)
        for (let i = 0; i < 1000; i++) {
            runtime.translateWidgetBox(widget, -0.1, -0.1);
        }

        const finalBox = runtime.getLayoutBox(widget);

        // The final box should be exactly the original (no float drift)
        // Using toBeCloseTo because 0.1 cannot be represented exactly in binary floating point
        expect(finalBox.x).toBeCloseTo(originalBox.x, 10);
        expect(finalBox.y).toBeCloseTo(originalBox.y, 10);
        expect(finalBox.width).toBe(originalBox.width);
        expect(finalBox.height).toBe(originalBox.height);
        expect(finalBox.contentX).toBeCloseTo(originalBox.contentX, 10);
        expect(finalBox.contentY).toBeCloseTo(originalBox.contentY, 10);

        // The translated box should be offset by (100, 100)
        expect(translatedBox.x).toBeCloseTo(originalBox.x + 100);
        expect(translatedBox.y).toBeCloseTo(originalBox.y + 100);
    });
});
