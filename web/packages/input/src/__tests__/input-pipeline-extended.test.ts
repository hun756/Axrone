import { describe, expect, it } from 'vitest';
import { createInputSystem } from '@axrone/input';
import type { InputSystem, InputActionSchema } from '@axrone/input';

/**
 * T-10 Extended: Input Pipeline Integration Tests
 *
 * Comprehensive coverage of mouse, touch, gamepad, rebinding, DOM attachment,
 * action events, context management, and snapshot/restore.
 */

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const dispatchKey = (
    input: InputSystem<InputActionSchema>,
    code: string,
    pressed: boolean,
): void => {
    input.dispatch({ type: 'keyboard', code, pressed });
};

// ===========================================================================
// 1. Mouse Button Input (3 tests)
// ===========================================================================

describe('T-10 Extended > Mouse Button Input', () => {
    it('left mouse button press and release drives a button action', () => {
        const input = createInputSystem({
            schema: {
                fire: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        fire: [{ type: 'control', control: 'mouse/button/0' }],
                    },
                },
            ],
        });

        // Press left mouse button (button 0)
        input.dispatch({ type: 'mouse-button', button: 0, pressed: true });
        input.update(1);
        expect(input.read('fire')).toBe(true);
        expect(input.isActive('fire')).toBe(true);
        expect(input.isPressed('fire')).toBe(true);

        // Release left mouse button
        input.dispatch({ type: 'mouse-button', button: 0, pressed: false });
        input.update(2);
        expect(input.read('fire')).toBe(false);
        expect(input.isReleased('fire')).toBe(true);

        input.dispose();
    });

    it('right and middle mouse buttons map to separate actions', () => {
        const input = createInputSystem({
            schema: {
                primaryFire: { kind: 'button' },
                secondaryFire: { kind: 'button' },
                middleClick: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        primaryFire: [{ type: 'control', control: 'mouse/button/0' }],
                        secondaryFire: [{ type: 'control', control: 'mouse/button/2' }],
                        middleClick: [{ type: 'control', control: 'mouse/button/1' }],
                    },
                },
            ],
        });

        // Press right button (2)
        input.dispatch({ type: 'mouse-button', button: 2, pressed: true });
        input.update(1);
        expect(input.read('secondaryFire')).toBe(true);
        expect(input.read('primaryFire')).toBe(false);

        // Press middle button (1)
        input.dispatch({ type: 'mouse-button', button: 1, pressed: true });
        input.update(2);
        expect(input.read('middleClick')).toBe(true);
        expect(input.read('secondaryFire')).toBe(true);

        // Release right button
        input.dispatch({ type: 'mouse-button', button: 2, pressed: false });
        input.update(3);
        expect(input.read('secondaryFire')).toBe(false);
        expect(input.read('middleClick')).toBe(true);

        input.dispose();
    });

    it('mouse button state transitions through press-hold-release cycle', () => {
        const input = createInputSystem({
            schema: {
                fire: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        fire: [{ type: 'control', control: 'mouse/button/0' }],
                    },
                },
            ],
        });

        // Frame 1: press
        input.dispatch({ type: 'mouse-button', button: 0, pressed: true });
        input.update(1);
        let state = input.state('fire');
        expect(state.kind).toBe('button');
        expect(state.pressed).toBe(true);
        expect(state.released).toBe(false);
        expect(state.active).toBe(true);

        // Frame 2: still held (no new event, just update)
        input.update(2);
        state = input.state('fire');
        expect(state.pressed).toBe(false);
        expect(state.released).toBe(false);
        expect(state.active).toBe(true);

        // Frame 3: release
        input.dispatch({ type: 'mouse-button', button: 0, pressed: false });
        input.update(3);
        state = input.state('fire');
        expect(state.pressed).toBe(false);
        expect(state.released).toBe(true);
        expect(state.active).toBe(false);

        input.dispose();
    });
});

// ===========================================================================
// 2. Mouse Movement (2 tests)
// ===========================================================================

describe('T-10 Extended > Mouse Movement', () => {
    it('mouse-move events drive a vector2 action via dual-axis binding', () => {
        const input = createInputSystem({
            schema: {
                look: { kind: 'vector2' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        look: [{ type: 'dual-axis', x: 'mouse/move/x', y: 'mouse/move/y' }],
                    },
                },
            ],
        });

        input.dispatch({
            type: 'mouse-move',
            x: 100,
            y: 200,
            deltaX: 5,
            deltaY: -3,
        });
        input.update(1);

        const look = input.read('look');
        expect(look.x).toBe(5);
        expect(look.y).toBe(-3);

        input.dispose();
    });

    it('mouse-move values reset to zero on the next frame without new events', () => {
        const input = createInputSystem({
            schema: {
                look: { kind: 'vector2' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        look: [{ type: 'dual-axis', x: 'mouse/move/x', y: 'mouse/move/y' }],
                    },
                },
            ],
        });

        input.dispatch({
            type: 'mouse-move',
            x: 50,
            y: 50,
            deltaX: 10,
            deltaY: 10,
        });
        input.update(1);
        expect(input.read('look')).toEqual({ x: 10, y: 10 });

        // Next frame with no new mouse-move event
        input.update(2);
        expect(input.read('look')).toEqual({ x: 0, y: 0 });

        input.dispose();
    });
});

// ===========================================================================
// 3. Mouse Wheel (2 tests)
// ===========================================================================

describe('T-10 Extended > Mouse Wheel', () => {
    it('mouse-wheel events drive an axis action', () => {
        const input = createInputSystem({
            schema: {
                zoom: { kind: 'axis' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        zoom: [{ type: 'control', control: 'mouse/wheel/y' }],
                    },
                },
            ],
        });

        input.dispatch({
            type: 'mouse-wheel',
            deltaX: 0,
            deltaY: 3,
        });
        input.update(1);

        // The wheel delta is scaled (by default 20x for line-mode)
        const zoomValue = input.read('zoom');
        expect(typeof zoomValue).toBe('number');
        expect(Number.isFinite(zoomValue)).toBe(true);
        expect(zoomValue).not.toBe(0);

        input.dispose();
    });

    it('mouse-wheel resets to zero on subsequent frames without new events', () => {
        const input = createInputSystem({
            schema: {
                zoom: { kind: 'axis' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        zoom: [{ type: 'control', control: 'mouse/wheel/y' }],
                    },
                },
            ],
        });

        input.dispatch({ type: 'mouse-wheel', deltaX: 0, deltaY: 2 });
        input.update(1);
        expect(input.read('zoom')).not.toBe(0);

        input.update(2);
        expect(input.read('zoom')).toBe(0);

        input.dispose();
    });
});

// ===========================================================================
// 4. Touch Input (3 tests)
// ===========================================================================

describe('T-10 Extended > Touch Input', () => {
    it('touch pinch gesture drives an axis action', () => {
        const input = createInputSystem({
            schema: {
                zoom: { kind: 'axis' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        zoom: [{ type: 'control', control: 'touch/pinch' }],
                    },
                },
            ],
        });

        // Start two touches 10 units apart
        input.dispatch({
            type: 'touch',
            phase: 'start',
            touches: [
                { id: 1, x: 0, y: 0 },
                { id: 2, x: 10, y: 0 },
            ],
            changed: [
                { id: 1, x: 0, y: 0 },
                { id: 2, x: 10, y: 0 },
            ],
        });
        // Move touch 2 to 18 units apart (pinch out by 8)
        input.dispatch({
            type: 'touch',
            phase: 'move',
            touches: [
                { id: 1, x: 0, y: 0 },
                { id: 2, x: 18, y: 0 },
            ],
            changed: [{ id: 2, x: 18, y: 0 }],
        });
        input.update(1);

        expect(input.read('zoom')).toBe(8);

        input.dispose();
    });

    it('touch start/move/end lifecycle is handled cleanly', () => {
        const input = createInputSystem({
            schema: {
                zoom: { kind: 'axis' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        zoom: [{ type: 'control', control: 'touch/pinch' }],
                    },
                },
            ],
        });

        // Start
        input.dispatch({
            type: 'touch',
            phase: 'start',
            touches: [
                { id: 1, x: 0, y: 0 },
                { id: 2, x: 5, y: 0 },
            ],
            changed: [
                { id: 1, x: 0, y: 0 },
                { id: 2, x: 5, y: 0 },
            ],
        });
        input.update(1);
        expect(input.read('zoom')).toBe(0); // no movement yet

        // Move - increase distance
        input.dispatch({
            type: 'touch',
            phase: 'move',
            touches: [
                { id: 1, x: 0, y: 0 },
                { id: 2, x: 15, y: 0 },
            ],
            changed: [{ id: 2, x: 15, y: 0 }],
        });
        input.update(2);
        expect(input.read('zoom')).toBe(10);

        // End
        input.dispatch({
            type: 'touch',
            phase: 'end',
            touches: [],
            changed: [{ id: 1, x: 0, y: 0 }],
        });
        input.update(3);
        // After touch end, zoom should reset
        expect(input.read('zoom')).toBe(0);

        input.dispose();
    });

    it('multi-touch with more than two fingers is tracked', () => {
        const input = createInputSystem({
            schema: {
                zoom: { kind: 'axis' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        zoom: [{ type: 'control', control: 'touch/pinch' }],
                    },
                },
            ],
        });

        input.dispatch({
            type: 'touch',
            phase: 'start',
            touches: [
                { id: 1, x: 0, y: 0 },
                { id: 2, x: 10, y: 0 },
                { id: 3, x: 5, y: 5 },
            ],
            changed: [
                { id: 1, x: 0, y: 0 },
                { id: 2, x: 10, y: 0 },
                { id: 3, x: 5, y: 5 },
            ],
        });
        input.update(1);

        // Move touch 2 further
        input.dispatch({
            type: 'touch',
            phase: 'move',
            touches: [
                { id: 1, x: 0, y: 0 },
                { id: 2, x: 20, y: 0 },
                { id: 3, x: 5, y: 5 },
            ],
            changed: [{ id: 2, x: 20, y: 0 }],
        });
        input.update(2);

        // Pinch value should reflect movement
        expect(Number.isFinite(input.read('zoom'))).toBe(true);

        input.dispose();
    });
});

// ===========================================================================
// 5. Gamepad Input (3 tests)
// ===========================================================================

describe('T-10 Extended > Gamepad Input', () => {
    it('gamepad button press drives a button action', () => {
        const input = createInputSystem({
            schema: {
                fire: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        fire: [{ type: 'control', control: 'gamepad/0/button/0' }],
                    },
                },
            ],
        });

        input.dispatch({
            type: 'gamepad',
            gamepads: [
                {
                    index: 0,
                    connected: true,
                    buttons: [1],
                    axes: [0, 0],
                },
            ],
        });
        input.update(1);

        expect(input.read('fire')).toBe(true);
        expect(input.isActive('fire')).toBe(true);

        // Release button
        input.dispatch({
            type: 'gamepad',
            gamepads: [
                {
                    index: 0,
                    connected: true,
                    buttons: [0],
                    axes: [0, 0],
                },
            ],
        });
        input.update(2);
        expect(input.read('fire')).toBe(false);

        input.dispose();
    });

    it('gamepad axes drive a vector2 action via dual-axis binding', () => {
        const input = createInputSystem({
            schema: {
                move: { kind: 'vector2' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        move: [{ type: 'dual-axis', x: 'gamepad/0/axis/0', y: 'gamepad/0/axis/1' }],
                    },
                },
            ],
        });

        input.dispatch({
            type: 'gamepad',
            gamepads: [
                {
                    index: 0,
                    connected: true,
                    buttons: [],
                    axes: [0.5, -0.75],
                },
            ],
        });
        input.update(1);

        expect(input.read('move')).toEqual({ x: 0.5, y: -0.75 });

        input.dispose();
    });

    it('gamepad disconnect clears all associated action values', () => {
        const input = createInputSystem({
            schema: {
                move: { kind: 'vector2' },
                fire: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        move: [{ type: 'dual-axis', x: 'gamepad/0/axis/0', y: 'gamepad/0/axis/1' }],
                        fire: [{ type: 'control', control: 'gamepad/0/button/0' }],
                    },
                },
            ],
        });

        // Connect and activate
        input.dispatch({
            type: 'gamepad',
            gamepads: [
                {
                    index: 0,
                    connected: true,
                    buttons: [1],
                    axes: [0.8, -0.6],
                },
            ],
        });
        input.update(1);
        expect(input.read('fire')).toBe(true);
        expect(input.read('move')).toEqual({ x: 0.8, y: -0.6 });

        // Disconnect
        input.dispatch({
            type: 'gamepad',
            gamepads: [
                {
                    index: 0,
                    connected: false,
                    buttons: [0],
                    axes: [0, 0],
                },
            ],
        });
        input.update(2);
        expect(input.read('fire')).toBe(false);
        expect(input.read('move')).toEqual({ x: 0, y: 0 });

        input.dispose();
    });
});

// ===========================================================================
// 6. Runtime Rebinding (2 tests)
// ===========================================================================

describe('T-10 Extended > Runtime Rebinding', () => {
    it('beginRebinding updates a binding to a new key', () => {
        const input = createInputSystem({
            schema: {
                jump: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        jump: [{ type: 'control', control: 'keyboard/Space' }],
                    },
                },
            ],
        });

        // Original binding: Space
        dispatchKey(input, 'Space', true);
        input.update(1);
        expect(input.read('jump')).toBe(true);

        dispatchKey(input, 'Space', false);
        input.update(2);

        // Rebind to KeyF
        input.beginRebinding({
            context: 'gameplay',
            action: 'jump',
            index: 0,
        });

        dispatchKey(input, 'KeyF', true);
        input.update(3);

        // Now KeyF should trigger jump
        expect(input.read('jump')).toBe(true);
        expect(input.bindings('gameplay', 'jump')).toEqual([
            expect.objectContaining({
                type: 'control',
                control: 'keyboard/KeyF',
            }),
        ]);

        // Space should no longer trigger jump
        dispatchKey(input, 'KeyF', false);
        input.update(4);
        dispatchKey(input, 'Space', true);
        input.update(5);
        expect(input.read('jump')).toBe(false);

        input.dispose();
    });

    it('rebinding survives snapshot and restore', () => {
        const input = createInputSystem({
            schema: {
                fire: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        fire: [{ type: 'control', control: 'mouse/button/0' }],
                    },
                },
            ],
        });

        // Rebind fire from mouse button to keyboard
        input.beginRebinding({
            context: 'gameplay',
            action: 'fire',
            index: 0,
        });
        dispatchKey(input, 'KeyR', true);
        input.update(1);

        expect(input.read('fire')).toBe(true);

        const snapshot = input.snapshot();

        // Create a fresh system and restore the snapshot
        const restored = createInputSystem({
            schema: {
                fire: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        fire: [{ type: 'control', control: 'mouse/button/0' }],
                    },
                },
            ],
        });
        restored.restore(snapshot);

        // The restored system should use the rebound key
        dispatchKey(restored, 'KeyR', true);
        restored.update(2);
        expect(restored.read('fire')).toBe(true);

        input.dispose();
        restored.dispose();
    });
});

// ===========================================================================
// 7. DOM Attachment (2 tests)
// ===========================================================================

describe('T-10 Extended > DOM Attachment', () => {
    it('attach() captures keyboard events from the document', () => {
        const input = createInputSystem({
            schema: {
                jump: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        jump: [{ type: 'control', control: 'keyboard/Space' }],
                    },
                },
            ],
        });

        const attachment = input.attach({ document, window });

        // Dispatch keydown via DOM
        const keyDown = new Event('keydown') as KeyboardEvent;
        Object.defineProperty(keyDown, 'code', { value: 'Space' });
        Object.defineProperty(keyDown, 'repeat', { value: false });
        document.dispatchEvent(keyDown);
        input.update(1);

        expect(input.read('jump')).toBe(true);

        // Dispatch keyup via DOM
        const keyUp = new Event('keyup') as KeyboardEvent;
        Object.defineProperty(keyUp, 'code', { value: 'Space' });
        Object.defineProperty(keyUp, 'repeat', { value: false });
        document.dispatchEvent(keyUp);
        input.update(2);

        expect(input.read('jump')).toBe(false);

        attachment.dispose();
        input.dispose();
    });

    it('attach() captures mousemove events from the window', () => {
        const input = createInputSystem({
            schema: {
                look: { kind: 'vector2' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        look: [{ type: 'dual-axis', x: 'mouse/move/x', y: 'mouse/move/y' }],
                    },
                },
            ],
        });

        const attachment = input.attach({ document, window });

        const move = new Event('mousemove') as MouseEvent;
        Object.defineProperty(move, 'clientX', { value: 42 });
        Object.defineProperty(move, 'clientY', { value: 17 });
        window.dispatchEvent(move);
        input.update(1);

        expect(input.read('look')).toEqual({ x: 42, y: 17 });

        attachment.dispose();
        input.dispose();
    });
});

// ===========================================================================
// 8. Action Events (2 tests)
// ===========================================================================

describe('T-10 Extended > Action Events', () => {
    it('subscribeAction fires lifecycle events for button press and release', () => {
        const input = createInputSystem({
            schema: {
                fire: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        fire: [{ type: 'control', control: 'keyboard/KeyF' }],
                    },
                },
            ],
        });

        const phases: string[] = [];
        const subscription = input.subscribeAction('fire', (event) => {
            phases.push(`${event.phase}:${event.trigger}`);
        });

        dispatchKey(input, 'KeyF', true);
        input.update(1);

        dispatchKey(input, 'KeyF', false);
        input.update(2);

        expect(phases).toContain('started:press');
        expect(phases).toContain('canceled:release');
        expect(phases.length).toBeGreaterThanOrEqual(2);

        subscription.dispose();
        input.dispose();
    });

    it('subscribe fires for all actions with phase filtering', () => {
        const input = createInputSystem({
            schema: {
                jump: { kind: 'button' },
                fire: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        jump: [{ type: 'control', control: 'keyboard/Space' }],
                        fire: [{ type: 'control', control: 'keyboard/KeyF' }],
                    },
                },
            ],
        });

        const events: string[] = [];
        const subscription = input.subscribe(
            (event) => {
                events.push(`${event.action}:${event.phase}`);
            },
            { phases: ['started'] },
        );

        dispatchKey(input, 'Space', true);
        input.update(1);

        dispatchKey(input, 'KeyF', true);
        input.update(2);

        // Only 'started' events should be captured
        expect(events).toEqual(['jump:started', 'fire:started']);

        subscription.dispose();
        input.dispose();
    });
});

// ===========================================================================
// 9. Context Management (2 tests)
// ===========================================================================

describe('T-10 Extended > Context Management', () => {
    it('registerContext adds a new context at runtime', () => {
        const input = createInputSystem({
            schema: {
                jump: { kind: 'button' },
                interact: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        jump: [{ type: 'control', control: 'keyboard/Space' }],
                    },
                },
            ],
        });

        // Register a new context
        input.registerContext({
            id: 'ui',
            bindings: {
                interact: [{ type: 'control', control: 'keyboard/KeyE' }],
            },
        });

        dispatchKey(input, 'KeyE', true);
        input.update(1);
        expect(input.read('interact')).toBe(true);

        input.dispose();
    });

    it('context priority gates which context processes a shared key', () => {
        const input = createInputSystem({
            schema: {
                action: { kind: 'button' },
                confirm: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    priority: 0,
                    bindings: {
                        action: [{ type: 'control', control: 'keyboard/Space' }],
                    },
                },
                {
                    id: 'ui',
                    priority: 10,
                    capture: 'used',
                    bindings: {
                        confirm: [{ type: 'control', control: 'keyboard/Space' }],
                    },
                },
            ],
        });

        // UI context has higher priority and captures
        dispatchKey(input, 'Space', true);
        input.update(1);

        expect(input.read('confirm')).toBe(true);
        expect(input.read('action')).toBe(false);

        // Deactivate UI context -> gameplay takes over
        input.deactivateContext('ui');
        input.update(2);

        expect(input.read('confirm')).toBe(false);
        expect(input.read('action')).toBe(true);

        input.dispose();
    });
});

// ===========================================================================
// 10. Snapshot / Restore (2 tests)
// ===========================================================================

describe('T-10 Extended > Snapshot / Restore', () => {
    it('snapshot captures and restore replays context bindings', () => {
        const input = createInputSystem({
            schema: {
                jump: { kind: 'button' },
                move: { kind: 'vector2' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        jump: [{ type: 'control', control: 'keyboard/Space' }],
                        move: [
                            {
                                type: 'vector2',
                                up: 'keyboard/KeyW',
                                down: 'keyboard/KeyS',
                                left: 'keyboard/KeyA',
                                right: 'keyboard/KeyD',
                            },
                        ],
                    },
                },
            ],
        });

        const snapshot = input.snapshot();
        expect(snapshot).toBeDefined();

        // Create a new system with different bindings and restore
        const restored = createInputSystem({
            schema: {
                jump: { kind: 'button' },
                move: { kind: 'vector2' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        jump: [{ type: 'control', control: 'keyboard/KeyJ' }],
                        move: [
                            {
                                type: 'vector2',
                                up: 'keyboard/KeyI',
                                down: 'keyboard/KeyK',
                                left: 'keyboard/KeyJ',
                                right: 'keyboard/KeyL',
                            },
                        ],
                    },
                },
            ],
        });

        restored.restore(snapshot);

        // After restore, original bindings should be active
        dispatchKey(restored, 'Space', true);
        restored.update(1);
        expect(restored.read('jump')).toBe(true);

        dispatchKey(restored, 'KeyW', true);
        restored.update(2);
        expect(restored.read('move').y).toBeCloseTo(1, 5);

        input.dispose();
        restored.dispose();
    });

    it('snapshot captures rebound bindings and restores them to a fresh system', () => {
        const input = createInputSystem({
            schema: {
                fire: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        fire: [{ type: 'control', control: 'mouse/button/0' }],
                    },
                },
            ],
        });

        // Rebind fire to KeyX
        input.beginRebinding({
            context: 'gameplay',
            action: 'fire',
            index: 0,
        });
        dispatchKey(input, 'KeyX', true);
        input.update(1);
        expect(input.read('fire')).toBe(true);

        const snapshot = input.snapshot();

        // Fresh system with default binding
        const fresh = createInputSystem({
            schema: {
                fire: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        fire: [{ type: 'control', control: 'mouse/button/0' }],
                    },
                },
            ],
        });
        fresh.restore(snapshot);

        // Rebound binding should be in effect
        dispatchKey(fresh, 'KeyX', true);
        fresh.update(2);
        expect(fresh.read('fire')).toBe(true);

        // Original mouse binding should no longer trigger fire
        fresh.dispatch({ type: 'mouse-button', button: 0, pressed: true });
        fresh.update(3);
        // fire is already true from KeyX, but let's check the binding list
        expect(fresh.bindings('gameplay', 'fire')).toEqual([
            expect.objectContaining({
                type: 'control',
                control: 'keyboard/KeyX',
            }),
        ]);

        input.dispose();
        fresh.dispose();
    });
});
