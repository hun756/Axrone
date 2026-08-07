import type {
    FocusMoveDirection,
    LayoutBox,
    UIInputEvent,
    UIKeyEvent,
    UIPointerEvent,
    UITextInputEvent,
    WidgetId,
} from '../types';

/**
 * Input dispatch logic extracted from UIRuntime (refactor plan P2-4).
 *
 * The dispatch functions operate against a narrow host interface instead of
 * the full runtime, so the SoA arrays stay private to UIRuntime. Behavior is
 * intentionally identical to the previous private-method implementations.
 */
export interface UIInputDispatchHost {
    getPressed(): WidgetId | null;
    setPressed(widget: WidgetId | null): void;
    getFocused(): WidgetId | null;
    hitTest(x: number, y: number): WidgetId | null;
    updateHover(target: WidgetId | null, event: Readonly<UIPointerEvent>): void;
    bubbleEvent(index: number, event: Readonly<UIInputEvent>): boolean;
    isFocusable(index: number): boolean;
    setFocus(widget: WidgetId | null, reason: 'pointer'): boolean;
    moveFocus(direction: FocusMoveDirection): WidgetId | null;
}

export function dispatchPointerEvent(host: UIInputDispatchHost, event: Readonly<UIPointerEvent>): boolean {
    const target = host.hitTest(event.x, event.y);
    if (event.phase === 'move') {
        host.updateHover(target, event);
        const pressed = host.getPressed();
        if (pressed) {
            if (target && pressed !== target) {
                return host.bubbleEvent(pressed as number, event) || host.bubbleEvent(target as number, event);
            }
            return host.bubbleEvent((target ?? pressed) as number, event);
        }
        if (target) {
            return host.bubbleEvent(target as number, event);
        }
        return false;
    }
    if (event.phase === 'down') {
        host.setPressed(target);
        if (target) {
            if (host.isFocusable(target as number)) {
                host.setFocus(target, 'pointer');
            }
            return host.bubbleEvent(target as number, event);
        }
        return false;
    }
    if (event.phase === 'up') {
        const resolved = target ?? host.getPressed();
        host.setPressed(null);
        return resolved ? host.bubbleEvent(resolved as number, event) : false;
    }
    if (event.phase === 'wheel') {
        return target ? host.bubbleEvent(target as number, event) : false;
    }
    return false;
}

export function dispatchKeyEvent(host: UIInputDispatchHost, event: Readonly<UIKeyEvent>): boolean {
    const focused = host.getFocused();
    if (focused && host.bubbleEvent(focused as number, event)) {
        return true;
    }
    if (event.phase === 'down') {
        if (event.key === 'Tab') {
            const direction: FocusMoveDirection = event.shiftKey ? 'backward' : 'forward';
            return host.moveFocus(direction) !== null;
        }
        if (event.key === 'ArrowLeft') {
            return host.moveFocus('left') !== null;
        }
        if (event.key === 'ArrowRight') {
            return host.moveFocus('right') !== null;
        }
        if (event.key === 'ArrowUp') {
            return host.moveFocus('up') !== null;
        }
        if (event.key === 'ArrowDown') {
            return host.moveFocus('down') !== null;
        }
    }
    return false;
}

export function dispatchTextEvent(host: UIInputDispatchHost, event: Readonly<UITextInputEvent>): boolean {
    const focused = host.getFocused();
    return focused ? host.bubbleEvent(focused as number, event) : false;
}

export function moveFocusLinear(
    candidates: readonly WidgetId[],
    direction: FocusMoveDirection,
    focused: WidgetId | null,
    cycle: boolean
): WidgetId | null {
    if (candidates.length === 0) {
        return null;
    }
    if (!focused) {
        return direction === 'backward' ? candidates[candidates.length - 1] : candidates[0];
    }
    const currentIndex = candidates.findIndex((candidate) => candidate === focused);
    if (currentIndex === -1) {
        return direction === 'backward' ? candidates[candidates.length - 1] : candidates[0];
    }
    const nextIndex = direction === 'backward' ? currentIndex - 1 : currentIndex + 1;
    if (nextIndex >= 0 && nextIndex < candidates.length) {
        return candidates[nextIndex];
    }
    return cycle ? (direction === 'backward' ? candidates[candidates.length - 1] : candidates[0]) : null;
}

export function moveFocusDirectional(
    candidates: readonly WidgetId[],
    direction: Exclude<FocusMoveDirection, 'forward' | 'backward'>,
    focused: WidgetId | null,
    readBox: (index: number) => LayoutBox
): WidgetId | null {
    if (candidates.length === 0) {
        return null;
    }
    const current = focused ? (focused as number) : candidates[0] as number;
    const origin = readBox(current);
    const originCenterX = origin.x + origin.width / 2;
    const originCenterY = origin.y + origin.height / 2;
    let best: { id: WidgetId; score: number } | null = null;
    for (const candidate of candidates) {
        const index = candidate as number;
        if (index === current) {
            continue;
        }
        const box = readBox(index);
        const centerX = box.x + box.width / 2;
        const centerY = box.y + box.height / 2;
        const deltaX = centerX - originCenterX;
        const deltaY = centerY - originCenterY;
        if (direction === 'left' && deltaX >= 0) {
            continue;
        }
        if (direction === 'right' && deltaX <= 0) {
            continue;
        }
        if (direction === 'up' && deltaY >= 0) {
            continue;
        }
        if (direction === 'down' && deltaY <= 0) {
            continue;
        }
        const primary = direction === 'left' || direction === 'right' ? Math.abs(deltaX) : Math.abs(deltaY);
        const secondary = direction === 'left' || direction === 'right' ? Math.abs(deltaY) : Math.abs(deltaX);
        const score = primary * primary + secondary * secondary * 0.25;
        if (!best || score < best.score) {
            best = { id: candidate, score };
        }
    }
    return best?.id ?? null;
}
