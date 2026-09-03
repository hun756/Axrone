/**
 * Lightweight property animation for UI widgets.
 * Uses requestAnimationFrame for frame updates.
 * Supports: color interpolation, number interpolation (opacity, position, size).
 *
 * This module is self-contained — it does NOT depend on @axrone/tween.
 */

import { Color } from '@axrone/numeric';
import type { UIRuntime } from '../runtime';
import type { WidgetId } from '../types';

// ─── Easing functions ────────────────────────────────────────────────────────

export type EasingFunction = (t: number) => number;

export const Easing = {
    linear: (t: number) => t,
    easeOutQuad: (t: number) => t * (2 - t),
    easeInOutQuad: (t: number) => (t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t),
    easeOutCubic: (t: number) => (--t) * t * t + 1,
    easeInOutCubic: (t: number) =>
        t < 0.5 ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1,
} as const;

// ─── Animation target & config ───────────────────────────────────────────────

export type AnimationTarget = {
    /** Widget to animate */
    readonly widget: WidgetId;
    /** Property path: 'style.opacity', 'style.background', 'layout.zIndex', etc. */
    readonly property: string;
    /** Starting value (optional — if omitted, uses sensible defaults). */
    readonly from?: number | `#${string}`;
    /** Target value (number for opacity/position, hex string for color) */
    readonly to: number | `#${string}`;
};

export interface UIAnimationConfig {
    readonly targets: readonly AnimationTarget[];
    readonly duration: number; // seconds
    readonly easing?: EasingFunction;
    readonly onComplete?: () => void;
}

export interface UIAnimationHandle {
    cancel(): void;
    readonly isComplete: boolean;
}

// ─── Color helpers ───────────────────────────────────────────────────────────

interface RGBA {
    r: number;
    g: number;
    b: number;
    a: number;
}

const TRANSPARENT: RGBA = { r: 0, g: 0, b: 0, a: 0 };

/**
 * Parses a hex color string (#RGB, #RGBA, #RRGGBB, #RRGGBBAA) into RGBA.
 * Returns null when the format is unrecognized.
 */
const parseHexColor = (hex: string): RGBA | null => {
    try {
        const c = Color.fromHex(hex);
        return { r: c.r * 255, g: c.g * 255, b: c.b * 255, a: c.a * 255 };
    } catch {
        return null;
    }
};

/**
 * Formats RGBA into an 8-char hex string (#RRGGBBAA).
 */
const formatHexColor = (color: RGBA): `#${string}` => {
    const toHex = (value: number): string => {
        const clamped = Math.max(0, Math.min(255, Math.round(value)));
        return clamped.toString(16).padStart(2, '0');
    };
    return `#${toHex(color.r)}${toHex(color.g)}${toHex(color.b)}${toHex(color.a)}` as `#${string}`;
};

/**
 * Linearly interpolates between two RGBA colors.
 */
const lerpColor = (from: RGBA, to: RGBA, t: number): RGBA => ({
    r: from.r + (to.r - from.r) * t,
    g: from.g + (to.g - from.g) * t,
    b: from.b + (to.b - from.b) * t,
    a: from.a + (to.a - from.a) * t,
});

// ─── Start value resolution ──────────────────────────────────────────────────

interface ResolvedStartValue {
    readonly kind: 'number' | 'color';
    readonly numeric: number;
    readonly color: RGBA;
}

/**
 * Resolves the starting value for an animation target.
 * If `from` is provided in the target, uses it.
 * Otherwise, uses sensible defaults based on the property path.
 */
const resolveStartValue = (target: AnimationTarget): ResolvedStartValue => {
    const { from, property } = target;

    if (from !== undefined) {
        if (typeof from === 'number') {
            return { kind: 'number', numeric: from, color: TRANSPARENT };
        }
        const parsed = parseHexColor(from);
        return { kind: 'color', numeric: 0, color: parsed ?? TRANSPARENT };
    }

    // Default start values based on property.
    switch (property) {
        case 'style.opacity':
            return { kind: 'number', numeric: 1, color: TRANSPARENT };
        case 'style.background':
        case 'style.borderColor':
        case 'style.color':
            return { kind: 'color', numeric: 0, color: TRANSPARENT };
        case 'layout.zIndex':
            return { kind: 'number', numeric: 0, color: TRANSPARENT };
        default:
            return { kind: 'number', numeric: 0, color: TRANSPARENT };
    }
};

// ─── Value writing ───────────────────────────────────────────────────────────

/**
 * Writes an interpolated value to the widget via runtime.updateWidget.
 */
const writeInterpolatedValue = (
    runtime: UIRuntime,
    widget: WidgetId,
    property: string,
    start: ResolvedStartValue,
    end: number | `#${string}`,
    t: number
): void => {
    if (start.kind === 'color') {
        const endColor = typeof end === 'string' ? parseHexColor(end) : null;
        if (!endColor) {
            return;
        }
        const interpolated = lerpColor(start.color, endColor, t);
        const hex = formatHexColor(interpolated);

        switch (property) {
            case 'style.background':
                runtime.updateWidget(widget, { style: { background: hex } });
                break;
            case 'style.borderColor':
                runtime.updateWidget(widget, { style: { borderColor: hex } });
                break;
            case 'style.color':
                runtime.updateWidget(widget, { style: { color: hex } });
                break;
            default:
                break;
        }
    } else {
        const endNum = typeof end === 'number' ? end : start.numeric;
        const value = start.numeric + (endNum - start.numeric) * t;

        switch (property) {
            case 'style.opacity':
                runtime.updateWidget(widget, { style: { opacity: value } });
                break;
            case 'layout.zIndex':
                runtime.updateWidget(widget, { layout: { zIndex: Math.round(value) } });
                break;
            default:
                break;
        }
    }
};

// ─── Core animation function ─────────────────────────────────────────────────

/**
 * Animates one or more widget properties from start values to target values.
 * Uses requestAnimationFrame for frame updates.
 * Returns a handle that can be used to cancel the animation.
 *
 * If a target does not specify `from`, sensible defaults are used:
 *   - style.opacity → 1
 *   - style.background / borderColor / color → transparent
 *   - layout.zIndex → 0
 *
 * For reliable "from current value" behavior, use the convenience functions
 * (animateWidgetOpacity, animateWidgetColor) which set the start value explicitly.
 */
export const animate = (runtime: UIRuntime, config: UIAnimationConfig): UIAnimationHandle => {
    const easing = config.easing ?? Easing.linear;
    const durationMs = Math.max(0, config.duration * 1000);
    const startValues: ResolvedStartValue[] = [];

    // Resolve start values for each target.
    for (const target of config.targets) {
        startValues.push(resolveStartValue(target));
    }

    let animationId: number | null = null;
    let startTime: number | null = null;
    let completed = false;

    const tick = (timestamp: number): void => {
        if (completed) {
            return;
        }
        if (startTime === null) {
            startTime = timestamp;
        }

        const elapsed = timestamp - startTime;
        const rawProgress = durationMs > 0 ? Math.min(1, elapsed / durationMs) : 1;
        const easedProgress = easing(rawProgress);

        // Apply interpolated values.
        try {
            for (let i = 0; i < config.targets.length; i++) {
                const target = config.targets[i];
                const start = startValues[i];
                writeInterpolatedValue(runtime, target.widget, target.property, start, target.to, easedProgress);
            }
        } catch {
            // A target widget was disposed mid-animation — cancel silently.
            if (animationId !== null) {
                cancelAnimationFrame(animationId);
                animationId = null;
            }
            completed = true;
            return;
        }

        if (rawProgress >= 1) {
            completed = true;
            animationId = null;
            config.onComplete?.();
            return;
        }

        animationId = requestAnimationFrame(tick);
    };

    animationId = requestAnimationFrame(tick);

    return {
        cancel(): void {
            if (animationId !== null) {
                cancelAnimationFrame(animationId);
                animationId = null;
            }
            completed = true;
        },
        get isComplete(): boolean {
            return completed;
        },
    };
};

// ─── Convenience functions ───────────────────────────────────────────────────

/**
 * Animates a widget's opacity from one value to another.
 * Sets the starting opacity immediately, then animates to the target.
 */
export const animateWidgetOpacity = (
    runtime: UIRuntime,
    widget: WidgetId,
    from: number,
    to: number,
    duration: number,
    easing?: EasingFunction
): UIAnimationHandle => {
    // Set the starting opacity immediately.
    runtime.updateWidget(widget, { style: { opacity: from } });

    return animate(runtime, {
        targets: [
            {
                widget,
                property: 'style.opacity',
                from,
                to,
            },
        ],
        duration,
        easing,
    });
};

/**
 * Animates a widget's color property (background, borderColor, or color)
 * from one hex value to another.
 * Sets the starting color immediately, then animates to the target.
 */
export const animateWidgetColor = (
    runtime: UIRuntime,
    widget: WidgetId,
    property: 'style.background' | 'style.borderColor' | 'style.color',
    from: `#${string}`,
    to: `#${string}`,
    duration: number,
    easing?: EasingFunction
): UIAnimationHandle => {
    // Set the starting color immediately.
    switch (property) {
        case 'style.background':
            runtime.updateWidget(widget, { style: { background: from } });
            break;
        case 'style.borderColor':
            runtime.updateWidget(widget, { style: { borderColor: from } });
            break;
        case 'style.color':
            runtime.updateWidget(widget, { style: { color: from } });
            break;
    }

    return animate(runtime, {
        targets: [
            {
                widget,
                property,
                from,
                to,
            },
        ],
        duration,
        easing,
    });
};
