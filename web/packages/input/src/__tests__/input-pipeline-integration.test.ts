import { describe, expect, it } from 'vitest';
import { createInputSystem } from '@axrone/input';

/**
 * Input Pipeline Integration Tests
 *
 * Validates the full input flow: DOM keyboard events -> InputSystem -> game logic response,
 * simulating how game scripts like CharacterMovement consume input.
 */

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Dispatch a synthetic keydown event on the document. */
const pressKey = (code: string): void => {
    const event = new Event('keydown') as KeyboardEvent;
    Object.defineProperty(event, 'code', { value: code });
    Object.defineProperty(event, 'repeat', { value: false });
    document.dispatchEvent(event);
};

/** Dispatch a synthetic keyup event on the document. */
const releaseKey = (code: string): void => {
    const event = new Event('keyup') as KeyboardEvent;
    Object.defineProperty(event, 'code', { value: code });
    Object.defineProperty(event, 'repeat', { value: false });
    document.dispatchEvent(event);
};

// ---------------------------------------------------------------------------
// 1. Keyboard -> Action Mapping
// ---------------------------------------------------------------------------

describe('Keyboard -> Action Mapping', () => {
    it('maps Space keydown to jump action reading true', () => {
        const input = createInputSystem({
            schema: { jump: { kind: 'button' } },
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

        pressKey('Space');
        input.update(1);

        expect(input.read('jump')).toBe(true);
        expect(input.isPressed('jump')).toBe(true);

        attachment.dispose();
    });

    it('maps Space keyup to jump action reading false', () => {
        const input = createInputSystem({
            schema: { jump: { kind: 'button' } },
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

        pressKey('Space');
        input.update(1);
        expect(input.read('jump')).toBe(true);

        releaseKey('Space');
        input.update(2);
        expect(input.read('jump')).toBe(false);
        expect(input.isPressed('jump')).toBe(false);

        attachment.dispose();
    });

    it('handles multiple keys pressed simultaneously', () => {
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
        const attachment = input.attach({ document, window });

        pressKey('Space');
        pressKey('KeyF');
        input.update(1);

        expect(input.read('jump')).toBe(true);
        expect(input.read('fire')).toBe(true);

        // Release only jump
        releaseKey('Space');
        input.update(2);

        expect(input.read('jump')).toBe(false);
        expect(input.read('fire')).toBe(true);

        attachment.dispose();
    });
});

// ---------------------------------------------------------------------------
// 2. Axis Input (WASD Movement)
// ---------------------------------------------------------------------------

describe('Axis Input (WASD Movement)', () => {
    it('reads positive moveX when D is pressed', () => {
        const input = createInputSystem({
            schema: { moveX: { kind: 'axis' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        moveX: [
                            {
                                type: 'axis',
                                negative: 'keyboard/KeyA',
                                positive: 'keyboard/KeyD',
                            },
                        ],
                    },
                },
            ],
        });
        const attachment = input.attach({ document, window });

        pressKey('KeyD');
        input.update(1);

        expect(input.read('moveX')).toBeGreaterThan(0);

        attachment.dispose();
    });

    it('reads negative moveX when A is pressed', () => {
        const input = createInputSystem({
            schema: { moveX: { kind: 'axis' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        moveX: [
                            {
                                type: 'axis',
                                negative: 'keyboard/KeyA',
                                positive: 'keyboard/KeyD',
                            },
                        ],
                    },
                },
            ],
        });
        const attachment = input.attach({ document, window });

        pressKey('KeyA');
        input.update(1);

        expect(input.read('moveX')).toBeLessThan(0);

        attachment.dispose();
    });

    it('reads positive and negative moveY for W and S', () => {
        const input = createInputSystem({
            schema: { moveY: { kind: 'axis' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        moveY: [
                            {
                                type: 'axis',
                                negative: 'keyboard/KeyS',
                                positive: 'keyboard/KeyW',
                            },
                        ],
                    },
                },
            ],
        });
        const attachment = input.attach({ document, window });

        pressKey('KeyW');
        input.update(1);
        const wValue = input.read('moveY');
        expect(wValue).toBeGreaterThan(0);

        releaseKey('KeyW');
        pressKey('KeyS');
        input.update(2);
        const sValue = input.read('moveY');
        expect(sValue).toBeLessThan(0);

        attachment.dispose();
    });

    it('returns axis to 0 when all keys are released', () => {
        const input = createInputSystem({
            schema: { moveX: { kind: 'axis' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        moveX: [
                            {
                                type: 'axis',
                                negative: 'keyboard/KeyA',
                                positive: 'keyboard/KeyD',
                            },
                        ],
                    },
                },
            ],
        });
        const attachment = input.attach({ document, window });

        pressKey('KeyD');
        input.update(1);
        expect(input.read('moveX')).not.toBe(0);

        releaseKey('KeyD');
        input.update(2);
        expect(input.read('moveX')).toBe(0);

        attachment.dispose();
    });

    it('cancels axis to 0 when opposing keys are both pressed', () => {
        const input = createInputSystem({
            schema: { moveX: { kind: 'axis' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        moveX: [
                            {
                                type: 'axis',
                                negative: 'keyboard/KeyA',
                                positive: 'keyboard/KeyD',
                            },
                        ],
                    },
                },
            ],
        });
        const attachment = input.attach({ document, window });

        pressKey('KeyA');
        pressKey('KeyD');
        input.update(1);

        // Opposing keys should cancel out
        expect(input.read('moveX')).toBe(0);

        attachment.dispose();
    });
});

// ---------------------------------------------------------------------------
// 3. DOM Event Forwarding
// ---------------------------------------------------------------------------

describe('DOM Event Forwarding', () => {
    it('forwards KeyboardEvent from document into InputSystem via attach()', () => {
        const input = createInputSystem({
            schema: { jump: { kind: 'button' } },
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

        // Dispatch raw DOM events
        const keyDown = new Event('keydown') as KeyboardEvent;
        Object.defineProperty(keyDown, 'code', { value: 'Space' });
        Object.defineProperty(keyDown, 'repeat', { value: false });
        document.dispatchEvent(keyDown);
        input.update(1);

        expect(input.read('jump')).toBe(true);

        attachment.dispose();
    });

    it('maps event.code to the correct control path', () => {
        const input = createInputSystem({
            schema: {
                action1: { kind: 'button' },
                action2: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        action1: [{ type: 'control', control: 'keyboard/KeyW' }],
                        action2: [{ type: 'control', control: 'keyboard/KeyS' }],
                    },
                },
            ],
        });
        const attachment = input.attach({ document, window });

        pressKey('KeyW');
        input.update(1);

        expect(input.read('action1')).toBe(true);
        expect(input.read('action2')).toBe(false);

        attachment.dispose();
    });

    it('stops forwarding events after attachment.dispose()', () => {
        const input = createInputSystem({
            schema: { jump: { kind: 'button' } },
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
        attachment.dispose();

        pressKey('Space');
        input.update(1);

        // Event should not have been captured
        expect(input.read('jump')).toBe(false);
    });
});

// ---------------------------------------------------------------------------
// 4. Action Lifecycle Events
// ---------------------------------------------------------------------------

describe('Action Lifecycle Events', () => {
    it('fires pressed event when key is first pressed', () => {
        const input = createInputSystem({
            schema: { jump: { kind: 'button' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        jump: [{ type: 'control', control: 'keyboard/Space' }],
                    },
                },
            ],
        });

        const phases: string[] = [];
        const subscription = input.subscribeAction('jump', (event) => {
            phases.push(event.phase);
        });

        input.dispatch({ type: 'keyboard', code: 'Space', pressed: true });
        input.update(1);

        expect(phases).toContain('started');
        expect(phases).toContain('performed');

        subscription.dispose();
    });

    it('fires canceled event when key is released', () => {
        const input = createInputSystem({
            schema: { jump: { kind: 'button' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        jump: [{ type: 'control', control: 'keyboard/Space' }],
                    },
                },
            ],
        });

        const phases: string[] = [];
        const subscription = input.subscribeAction('jump', (event) => {
            phases.push(event.phase);
        });

        input.dispatch({ type: 'keyboard', code: 'Space', pressed: true });
        input.update(1);

        input.dispatch({ type: 'keyboard', code: 'Space', pressed: false });
        input.update(2);

        expect(phases).toContain('canceled');

        subscription.dispose();
    });

    it('provides frozen state snapshots in lifecycle events', () => {
        const input = createInputSystem({
            schema: { jump: { kind: 'button' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        jump: [{ type: 'control', control: 'keyboard/Space' }],
                    },
                },
            ],
        });

        let capturedFrozen = false;
        const subscription = input.subscribeAction('jump', (event) => {
            capturedFrozen = Object.isFrozen(event) && Object.isFrozen(event.state);
        });

        input.dispatch({ type: 'keyboard', code: 'Space', pressed: true });
        input.update(1);

        expect(capturedFrozen).toBe(true);

        subscription.dispose();
    });

    it('stops receiving events after subscription is disposed', () => {
        const input = createInputSystem({
            schema: { jump: { kind: 'button' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        jump: [{ type: 'control', control: 'keyboard/Space' }],
                    },
                },
            ],
        });

        let callCount = 0;
        const subscription = input.subscribeAction('jump', () => {
            callCount++;
        });

        input.dispatch({ type: 'keyboard', code: 'Space', pressed: true });
        input.update(1);
        const countAfterPress = callCount;

        subscription.dispose();

        input.dispatch({ type: 'keyboard', code: 'Space', pressed: false });
        input.update(2);

        // No additional calls after dispose
        expect(callCount).toBe(countAfterPress);
    });
});

// ---------------------------------------------------------------------------
// 5. Context Switching
// ---------------------------------------------------------------------------

describe('Context Switching', () => {
    it('activates gameplay context and deactivates on switch to ui', () => {
        const input = createInputSystem({
            schema: {
                jump: { kind: 'button' },
                confirm: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    priority: 0,
                    bindings: {
                        jump: [{ type: 'control', control: 'keyboard/Space' }],
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

        // Default: both contexts active, ui has higher priority and captures
        input.dispatch({ type: 'keyboard', code: 'Space', pressed: true });
        input.update(1);

        expect(input.read('confirm')).toBe(true);
        expect(input.read('jump')).toBe(false); // captured by ui

        // Deactivate ui context -> gameplay takes over
        input.deactivateContext('ui');
        input.update(2);

        expect(input.read('jump')).toBe(true);
        expect(input.read('confirm')).toBe(false);
    });

    it('re-activating a context restores its bindings', () => {
        const input = createInputSystem({
            schema: {
                jump: { kind: 'button' },
                confirm: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    priority: 0,
                    bindings: {
                        jump: [{ type: 'control', control: 'keyboard/Space' }],
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

        input.deactivateContext('ui');
        input.dispatch({ type: 'keyboard', code: 'Space', pressed: true });
        input.update(1);
        expect(input.read('jump')).toBe(true);

        // Re-activate ui
        input.activateContext('ui');
        input.update(2);
        expect(input.read('confirm')).toBe(true);
        expect(input.read('jump')).toBe(false);
    });

    it('actions from disabled context read as false', () => {
        const input = createInputSystem({
            schema: { jump: { kind: 'button' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        jump: [{ type: 'control', control: 'keyboard/Space' }],
                    },
                },
            ],
        });

        input.deactivateContext('gameplay');
        input.dispatch({ type: 'keyboard', code: 'Space', pressed: true });
        input.update(1);

        expect(input.read('jump')).toBe(false);
    });
});

// ---------------------------------------------------------------------------
// 6. Game Script Simulation (CharacterMovement)
// ---------------------------------------------------------------------------

describe('Game Script Simulation (CharacterMovement)', () => {
    it('simulates WASD diagonal movement with vector2 directional binding', () => {
        const input = createInputSystem({
            schema: { move: { kind: 'vector2' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
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
        const attachment = input.attach({ document, window });

        // Press W+D for diagonal movement (forward-right)
        pressKey('KeyW');
        pressKey('KeyD');
        input.update(1);

        const movement = input.read('move');
        expect(movement.x).toBeGreaterThan(0); // right
        expect(movement.y).toBeGreaterThan(0); // up

        attachment.dispose();
    });

    it('returns zero vector when no movement keys are pressed', () => {
        const input = createInputSystem({
            schema: { move: { kind: 'vector2' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
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
        const attachment = input.attach({ document, window });

        input.update(1);

        const movement = input.read('move');
        expect(movement.x).toBe(0);
        expect(movement.y).toBe(0);

        attachment.dispose();
    });

    it('stops movement on opposing keys (W+S or A+D)', () => {
        const input = createInputSystem({
            schema: { move: { kind: 'vector2' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
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
        const attachment = input.attach({ document, window });

        pressKey('KeyW');
        pressKey('KeyS');
        input.update(1);

        const movement = input.read('move');
        expect(movement.y).toBe(0); // opposing keys cancel

        attachment.dispose();
    });

    it('provides stable vector state objects (zero-allocation pattern)', () => {
        const input = createInputSystem({
            schema: { move: { kind: 'vector2' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
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

        input.dispatch({ type: 'keyboard', code: 'KeyW', pressed: true });
        input.update(1);

        const state1 = input.state('move');
        input.update(2);
        const state2 = input.state('move');

        // The value object should be frozen and structurally stable
        expect(Object.isFrozen(state1)).toBe(true);
        expect(Object.isFrozen(state1.value)).toBe(true);
        expect(Object.isFrozen(state2)).toBe(true);
        expect(Object.isFrozen(state2.value)).toBe(true);
    });
});

// ---------------------------------------------------------------------------
// 7. Input Buffering / Timing
// ---------------------------------------------------------------------------

describe('Input Buffering / Timing', () => {
    it('catches single-frame tap press via isPressed()', () => {
        const input = createInputSystem({
            schema: { jump: { kind: 'button' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        jump: [{ type: 'control', control: 'keyboard/Space' }],
                    },
                },
            ],
        });

        // Press and release within a single update frame
        input.dispatch({ type: 'keyboard', code: 'Space', pressed: true });
        input.update(1);

        expect(input.isPressed('jump')).toBe(true);

        input.dispatch({ type: 'keyboard', code: 'Space', pressed: false });
        input.update(2);

        expect(input.isPressed('jump')).toBe(false);
    });

    it('detects hold state after multiple frames', () => {
        const input = createInputSystem({
            schema: {
                charge: {
                    kind: 'button',
                    interactions: [{ type: 'hold', durationMs: 50 }],
                },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        charge: [{ type: 'control', control: 'keyboard/KeyQ' }],
                    },
                },
            ],
        });

        input.dispatch({ type: 'keyboard', code: 'KeyQ', pressed: true });
        input.update(0);

        // Not yet held long enough
        let state = input.state('charge');
        expect(state.holdTriggered).toBe(false);

        // Advance past hold threshold
        input.update(60);

        state = input.state('charge');
        expect(state.holdTriggered).toBe(true);
        expect(state.heldDurationMs).toBeGreaterThanOrEqual(50);
    });

    it('tracks repeat interactions across frames', () => {
        const input = createInputSystem({
            schema: {
                rapid: {
                    kind: 'button',
                    interactions: [
                        { type: 'repeat', delayMs: 20, intervalMs: 10 },
                    ],
                },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        rapid: [{ type: 'control', control: 'keyboard/KeyR' }],
                    },
                },
            ],
        });

        input.dispatch({ type: 'keyboard', code: 'KeyR', pressed: true });
        input.update(0);

        // First repeat after delay
        input.update(20);
        let state = input.state('rapid');
        expect(state.repeatTriggered).toBe(true);
        expect(state.repeatCount).toBeGreaterThanOrEqual(1);

        // More repeats
        input.update(30);
        state = input.state('rapid');
        expect(state.repeatCount).toBeGreaterThanOrEqual(2);
    });

    it('correctly resets interaction state on release', () => {
        const input = createInputSystem({
            schema: {
                charge: {
                    kind: 'button',
                    interactions: [{ type: 'hold', durationMs: 30 }],
                },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        charge: [{ type: 'control', control: 'keyboard/KeyQ' }],
                    },
                },
            ],
        });

        input.dispatch({ type: 'keyboard', code: 'KeyQ', pressed: true });
        input.update(0);
        input.update(40);

        expect(input.state('charge').holdTriggered).toBe(true);

        input.dispatch({ type: 'keyboard', code: 'KeyQ', pressed: false });
        input.update(50);

        const state = input.state('charge');
        expect(state.released).toBe(true);
        expect(state.heldDurationMs).toBe(50);
    });
});

// ---------------------------------------------------------------------------
// 8. Full Pipeline: DOM -> System -> Game Logic
// ---------------------------------------------------------------------------

describe('Full Pipeline: DOM -> System -> Game Logic', () => {
    it('simulates a complete character movement loop from DOM events', () => {
        const input = createInputSystem({
            schema: {
                move: { kind: 'vector2' },
                jump: { kind: 'button' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        move: [
                            {
                                type: 'vector2',
                                up: 'keyboard/KeyW',
                                down: 'keyboard/KeyS',
                                left: 'keyboard/KeyA',
                                right: 'keyboard/KeyD',
                            },
                        ],
                        jump: [{ type: 'control', control: 'keyboard/Space' }],
                    },
                },
            ],
        });
        const attachment = input.attach({ document, window });

        // Simulate a game frame: player presses W+D and Space
        pressKey('KeyW');
        pressKey('KeyD');
        pressKey('Space');
        input.update(1);

        const movement = input.read('move');
        const jumping = input.read('jump');

        // Game logic consumes input
        expect(movement.x).toBeGreaterThan(0);
        expect(movement.y).toBeGreaterThan(0);
        expect(jumping).toBe(true);

        // Next frame: player releases jump but keeps moving
        releaseKey('Space');
        input.update(2);

        expect(input.read('jump')).toBe(false);
        expect(input.read('move').x).toBeGreaterThan(0);
        expect(input.read('move').y).toBeGreaterThan(0);

        // Final frame: player stops
        releaseKey('KeyW');
        releaseKey('KeyD');
        input.update(3);

        const finalMovement = input.read('move');
        expect(finalMovement.x).toBe(0);
        expect(finalMovement.y).toBe(0);

        attachment.dispose();
    });

    it('game logic can subscribe to action events for reactive patterns', () => {
        const input = createInputSystem({
            schema: { jump: { kind: 'button' } },
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

        // Simulate a game script that reacts to jump events
        let jumpCount = 0;
        const subscription = input.subscribeAction('jump', (event) => {
            if (event.phase === 'performed' && event.state.value === true) {
                jumpCount++;
            }
        });

        pressKey('Space');
        input.update(1);
        expect(jumpCount).toBe(1);

        releaseKey('Space');
        input.update(2);

        pressKey('Space');
        input.update(3);
        expect(jumpCount).toBe(2);

        subscription.dispose();
        attachment.dispose();
    });
});
