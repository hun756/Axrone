import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { createInputSystem, InputDisposedError } from '@axrone/input';
import type {
    InputSystem,
    InputActionSchema,
} from '@axrone/input';

/**
 * T-10: Input Pipeline Integration Test
 *
 * Validates the full input pipeline: keyboard events -> input system -> game logic response.
 * Covers InputSystem initialization, keyboard processing, state query API,
 * axis mapping, game script integration (CharacterMovement pattern), and edge cases.
 */

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Dispatch a keyboard source event into the InputSystem. */
const dispatchKey = (
    input: InputSystem<InputActionSchema>,
    code: string,
    pressed: boolean,
): void => {
    input.dispatch({ type: 'keyboard', code, pressed });
};

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

/**
 * Minimal simulation of the CharacterMovement pressed-key set.
 * Mirrors the pattern in Assets/Scripts/CharacterMovement.ts where
 * a Set<string> tracks currently-held key codes.
 */
class MockPressedKeyTracker {
    private readonly _pressed = new Set<string>();

    onKeyDown(code: string): void {
        this._pressed.add(code);
    }

    onKeyUp(code: string): void {
        this._pressed.delete(code);
    }

    onBlur(): void {
        this._pressed.clear();
    }

    isPressed(code: string): boolean {
        return this._pressed.has(code);
    }

    /** Compute WASD movement direction matching CharacterMovement logic. */
    getMovement(): { moveX: number; moveZ: number; isMoving: boolean } {
        const moveX =
            (this._pressed.has('KeyA') ? 1 : 0) - (this._pressed.has('KeyD') ? 1 : 0);
        const moveZ =
            (this._pressed.has('KeyW') ? 1 : 0) - (this._pressed.has('KeyS') ? 1 : 0);
        return { moveX, moveZ, isMoving: moveX !== 0 || moveZ !== 0 };
    }

    clear(): void {
        this._pressed.clear();
    }
}

/**
 * Normalize a 2D vector (mirrors Vec3.normalize for x/z plane).
 */
const normalize2D = (x: number, z: number): { x: number; z: number } => {
    const mag = Math.sqrt(x * x + z * z);
    if (mag < 1e-9) return { x: 0, z: 0 };
    return { x: x / mag, z: z / mag };
};

// ---------------------------------------------------------------------------
// Shared schema for WASD movement tests
// ---------------------------------------------------------------------------

const wasdSchema = {
    move: {
        kind: 'vector2' as const,
        normalize: true,
    },
    moveHorizontal: { kind: 'axis' as const },
    moveVertical: { kind: 'axis' as const },
    jump: { kind: 'button' as const },
    sprint: { kind: 'button' as const },
};

const wasdContext = {
    id: 'gameplay',
    bindings: {
        move: [
            {
                type: 'vector2' as const,
                up: 'keyboard/KeyW',
                down: 'keyboard/KeyS',
                left: 'keyboard/KeyA',
                right: 'keyboard/KeyD',
                normalize: true,
            },
        ],
        moveHorizontal: [
            {
                type: 'axis' as const,
                negative: 'keyboard/KeyA',
                positive: 'keyboard/KeyD',
            },
        ],
        moveVertical: [
            {
                type: 'axis' as const,
                negative: 'keyboard/KeyS',
                positive: 'keyboard/KeyW',
            },
        ],
        jump: [{ type: 'control' as const, control: 'keyboard/Space' }],
        sprint: [{ type: 'control' as const, control: 'keyboard/ShiftLeft' }],
    },
};

// ===========================================================================
// 1. Input System Initialization (4 tests)
// ===========================================================================

describe('T-10 > Input System Initialization', () => {
    it('creates without errors when given a valid schema', () => {
        const input = createInputSystem({
            schema: { jump: { kind: 'button' } },
        });
        expect(input).toBeDefined();
        expect(input.isDisposed).toBe(false);
        input.dispose();
    });

    it('registers default key bindings through context definitions', () => {
        const input = createInputSystem({
            schema: wasdSchema,
            contexts: [wasdContext],
        });

        const bindings = input.bindings('gameplay', 'jump');
        expect(bindings).toHaveLength(1);
        expect(bindings[0]).toEqual(
            expect.objectContaining({
                type: 'control',
                control: 'keyboard/Space',
            }),
        );

        input.dispose();
    });

    it('has clean state (no pressed keys) at initialization', () => {
        const input = createInputSystem({
            schema: wasdSchema,
            contexts: [wasdContext],
        });

        // Before any dispatch+update, all button values should be false
        expect(input.read('jump')).toBe(false);
        expect(input.read('sprint')).toBe(false);

        // Axis values should be 0
        expect(input.read('moveHorizontal')).toBe(0);
        expect(input.read('moveVertical')).toBe(0);

        // Vector2 should be zero
        expect(input.read('move')).toEqual({ x: 0, y: 0 });

        input.dispose();
    });

    it('dispose cleans up and marks system as disposed', () => {
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

        expect(input.isDisposed).toBe(false);
        input.dispose();
        expect(input.isDisposed).toBe(true);

        // After dispose, operations should throw InputDisposedError
        expect(() => input.update(1)).toThrow(InputDisposedError);
        expect(() => input.dispatch({ type: 'keyboard', code: 'Space', pressed: true })).toThrow(
            InputDisposedError,
        );
    });
});

// ===========================================================================
// 2. Keyboard Input Processing (5 tests)
// ===========================================================================

describe('T-10 > Keyboard Input Processing', () => {
    it('keydown event registers key as pressed', () => {
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

        dispatchKey(input, 'Space', true);
        input.update(1);

        expect(input.read('jump')).toBe(true);
        expect(input.isActive('jump')).toBe(true);
        input.dispose();
    });

    it('keyup event registers key as released', () => {
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

        dispatchKey(input, 'Space', true);
        input.update(1);
        expect(input.read('jump')).toBe(true);

        dispatchKey(input, 'Space', false);
        input.update(2);
        expect(input.read('jump')).toBe(false);
        input.dispose();
    });

    it('maps key codes correctly (KeyW, KeyA, KeyS, KeyD)', () => {
        const input = createInputSystem({
            schema: wasdSchema,
            contexts: [wasdContext],
        });

        // Press W -> vertical should be +1
        dispatchKey(input, 'KeyW', true);
        input.update(1);
        expect(input.read('moveVertical')).toBe(1);

        // Also press S -> vertical should cancel to 0
        dispatchKey(input, 'KeyS', true);
        input.update(2);
        expect(input.read('moveVertical')).toBe(0);

        // Release W, only S held -> vertical should be -1
        dispatchKey(input, 'KeyW', false);
        input.update(3);
        expect(input.read('moveVertical')).toBe(-1);

        // Press A -> horizontal should be -1
        dispatchKey(input, 'KeyA', true);
        input.update(4);
        expect(input.read('moveHorizontal')).toBe(-1);

        // Also press D -> horizontal should cancel to 0
        dispatchKey(input, 'KeyD', true);
        input.update(5);
        expect(input.read('moveHorizontal')).toBe(0);

        input.dispose();
    });

    it('tracks multiple simultaneous keys', () => {
        const input = createInputSystem({
            schema: wasdSchema,
            contexts: [wasdContext],
        });

        // Press W + D simultaneously (diagonal)
        dispatchKey(input, 'KeyW', true);
        dispatchKey(input, 'KeyD', true);
        input.update(1);

        expect(input.read('moveVertical')).toBe(1);
        expect(input.read('moveHorizontal')).toBe(1);

        const move = input.read('move');
        expect(move.x).toBeGreaterThan(0);
        expect(move.y).toBeGreaterThan(0);

        // Press sprint too
        dispatchKey(input, 'ShiftLeft', true);
        input.update(2);
        expect(input.read('sprint')).toBe(true);

        // All three should be active
        expect(input.isActive('move')).toBe(true);
        expect(input.isActive('sprint')).toBe(true);

        input.dispose();
    });

    it('blur/focus-lost event clears all pressed keys', () => {
        const input = createInputSystem({
            schema: wasdSchema,
            contexts: [wasdContext],
        });

        // Press multiple keys
        dispatchKey(input, 'KeyW', true);
        dispatchKey(input, 'KeyD', true);
        dispatchKey(input, 'Space', true);
        input.update(1);

        expect(input.read('jump')).toBe(true);
        expect(input.isActive('move')).toBe(true);

        // Simulate focus loss
        input.dispatch({ type: 'focus', focused: false });
        input.update(2);

        // All values should reset
        expect(input.read('jump')).toBe(false);
        expect(input.read('sprint')).toBe(false);
        expect(input.read('moveHorizontal')).toBe(0);
        expect(input.read('moveVertical')).toBe(0);
        expect(input.read('move')).toEqual({ x: 0, y: 0 });

        input.dispose();
    });
});

// ===========================================================================
// 3. Input State Query API (5 tests)
// ===========================================================================

describe('T-10 > Input State Query API', () => {
    it('isPressed returns true on the first frame a button is held', () => {
        const input = createInputSystem({
            schema: { fire: { kind: 'button' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        fire: [{ type: 'control', control: 'keyboard/KeyF' }],
                    },
                },
            ],
        });

        dispatchKey(input, 'KeyF', true);
        input.update(1);

        expect(input.isPressed('fire')).toBe(true);
        expect(input.read('fire')).toBe(true);
        input.dispose();
    });

    it('isReleased returns true on the frame a button is let go', () => {
        const input = createInputSystem({
            schema: { fire: { kind: 'button' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        fire: [{ type: 'control', control: 'keyboard/KeyF' }],
                    },
                },
            ],
        });

        dispatchKey(input, 'KeyF', true);
        input.update(1);
        expect(input.isReleased('fire')).toBe(false);

        dispatchKey(input, 'KeyF', false);
        input.update(2);
        expect(input.isReleased('fire')).toBe(true);
        expect(input.read('fire')).toBe(false);
        input.dispose();
    });

    it('state() returns full button state with pressed/released/active flags', () => {
        const input = createInputSystem({
            schema: { fire: { kind: 'button' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        fire: [{ type: 'control', control: 'keyboard/KeyF' }],
                    },
                },
            ],
        });

        // Initial state
        let state = input.state('fire');
        expect(state.kind).toBe('button');
        expect(state.active).toBe(false);
        expect(state.value).toBe(false);

        // After press
        dispatchKey(input, 'KeyF', true);
        input.update(1);
        state = input.state('fire');
        expect(state.active).toBe(true);
        expect(state.value).toBe(true);
        expect(state.pressed).toBe(true);
        expect(state.released).toBe(false);

        // After release
        dispatchKey(input, 'KeyF', false);
        input.update(2);
        state = input.state('fire');
        expect(state.active).toBe(false);
        expect(state.value).toBe(false);
        expect(state.pressed).toBe(false);
        expect(state.released).toBe(true);

        input.dispose();
    });

    it('unknown action names throw an error', () => {
        const input = createInputSystem({
            schema: { fire: { kind: 'button' } },
        });

        expect(() => input.read('nonexistent' as never)).toThrow();
        expect(() => input.state('nonexistent' as never)).toThrow();
        expect(() => input.isPressed('nonexistent' as never)).toThrow();
        input.dispose();
    });

    it('axis state returns numeric value with delta information', () => {
        const input = createInputSystem({
            schema: {
                throttle: { kind: 'axis' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        throttle: [
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

        dispatchKey(input, 'KeyW', true);
        input.update(1);

        const state = input.state('throttle');
        expect(state.kind).toBe('axis');
        if (state.kind === 'axis') {
            expect(state.value).toBe(1);
            expect(typeof state.delta).toBe('number');
            expect(Number.isFinite(state.value)).toBe(true);
        }

        input.dispose();
    });
});

// ===========================================================================
// 4. Input Axis Mapping (5 tests)
// ===========================================================================

describe('T-10 > Input Axis Mapping', () => {
    it('WASD maps to movement vector correctly via directional binding', () => {
        const input = createInputSystem({
            schema: wasdSchema,
            contexts: [wasdContext],
        });

        // W only -> up
        dispatchKey(input, 'KeyW', true);
        input.update(1);
        let move = input.read('move');
        expect(move.y).toBeCloseTo(1, 5);
        expect(move.x).toBeCloseTo(0, 5);

        // Release W, press S -> down
        dispatchKey(input, 'KeyW', false);
        dispatchKey(input, 'KeyS', true);
        input.update(2);
        move = input.read('move');
        expect(move.y).toBeCloseTo(-1, 5);
        expect(move.x).toBeCloseTo(0, 5);

        // Release S, press A -> left
        dispatchKey(input, 'KeyS', false);
        dispatchKey(input, 'KeyA', true);
        input.update(3);
        move = input.read('move');
        expect(move.x).toBeCloseTo(-1, 5);
        expect(move.y).toBeCloseTo(0, 5);

        // Release A, press D -> right
        dispatchKey(input, 'KeyA', false);
        dispatchKey(input, 'KeyD', true);
        input.update(4);
        move = input.read('move');
        expect(move.x).toBeCloseTo(1, 5);
        expect(move.y).toBeCloseTo(0, 5);

        input.dispose();
    });

    it('horizontal axis: A=-1, D=+1, neither=0', () => {
        const input = createInputSystem({
            schema: wasdSchema,
            contexts: [wasdContext],
        });

        // Neither pressed
        input.update(1);
        expect(input.read('moveHorizontal')).toBe(0);

        // A pressed
        dispatchKey(input, 'KeyA', true);
        input.update(2);
        expect(input.read('moveHorizontal')).toBe(-1);

        // Both A and D -> cancel to 0
        dispatchKey(input, 'KeyD', true);
        input.update(3);
        expect(input.read('moveHorizontal')).toBe(0);

        // Release A, only D -> +1
        dispatchKey(input, 'KeyA', false);
        input.update(4);
        expect(input.read('moveHorizontal')).toBe(1);

        input.dispose();
    });

    it('vertical axis: W=+1, S=-1, neither=0', () => {
        const input = createInputSystem({
            schema: wasdSchema,
            contexts: [wasdContext],
        });

        // Neither pressed
        input.update(1);
        expect(input.read('moveVertical')).toBe(0);

        // W pressed
        dispatchKey(input, 'KeyW', true);
        input.update(2);
        expect(input.read('moveVertical')).toBe(1);

        // Both W and S -> cancel to 0
        dispatchKey(input, 'KeyS', true);
        input.update(3);
        expect(input.read('moveVertical')).toBe(0);

        // Release W, only S -> -1
        dispatchKey(input, 'KeyW', false);
        input.update(4);
        expect(input.read('moveVertical')).toBe(-1);

        input.dispose();
    });

    it('diagonal input is normalized to unit length', () => {
        const input = createInputSystem({
            schema: wasdSchema,
            contexts: [wasdContext],
        });

        // W + D -> diagonal, should be normalized
        dispatchKey(input, 'KeyW', true);
        dispatchKey(input, 'KeyD', true);
        input.update(1);

        const move = input.read('move');
        const magnitude = Math.sqrt(move.x * move.x + move.y * move.y);
        expect(magnitude).toBeCloseTo(1.0, 5);

        // Expected: (sqrt(2)/2, sqrt(2)/2)
        const expected = Math.SQRT1_2;
        expect(move.x).toBeCloseTo(expected, 5);
        expect(move.y).toBeCloseTo(expected, 5);

        input.dispose();
    });

    it('axis values are always finite numbers (not NaN)', () => {
        const input = createInputSystem({
            schema: wasdSchema,
            contexts: [wasdContext],
        });

        // No keys pressed
        input.update(1);
        expect(Number.isFinite(input.read('moveHorizontal'))).toBe(true);
        expect(Number.isFinite(input.read('moveVertical'))).toBe(true);

        const move = input.read('move');
        expect(Number.isFinite(move.x)).toBe(true);
        expect(Number.isFinite(move.y)).toBe(true);

        // With keys pressed
        dispatchKey(input, 'KeyW', true);
        dispatchKey(input, 'KeyA', true);
        input.update(2);

        expect(Number.isFinite(input.read('moveHorizontal'))).toBe(true);
        expect(Number.isFinite(input.read('moveVertical'))).toBe(true);

        const move2 = input.read('move');
        expect(Number.isFinite(move2.x)).toBe(true);
        expect(Number.isFinite(move2.y)).toBe(true);

        input.dispose();
    });
});

// ===========================================================================
// 5. Game Script Integration (3 tests)
// ===========================================================================

describe('T-10 > Game Script Integration (CharacterMovement pattern)', () => {
    let tracker: MockPressedKeyTracker;

    beforeEach(() => {
        tracker = new MockPressedKeyTracker();
    });

    afterEach(() => {
        tracker.clear();
    });

    it('CharacterMovement responds to WASD input with correct movement direction', () => {
        // Simulate pressing W (forward)
        tracker.onKeyDown('KeyW');
        let movement = tracker.getMovement();
        expect(movement.isMoving).toBe(true);
        expect(movement.moveZ).toBe(1);
        expect(movement.moveX).toBe(0);

        // Normalize (matching CharacterMovement's Vec3.normalize)
        const normalized = normalize2D(movement.moveX, movement.moveZ);
        expect(normalized.z).toBeCloseTo(1, 5);
        expect(normalized.x).toBeCloseTo(0, 5);

        // Simulate pressing W + D (diagonal forward-right)
        tracker.onKeyDown('KeyD');
        movement = tracker.getMovement();
        expect(movement.isMoving).toBe(true);
        expect(movement.moveX).toBe(-1); // D maps to -X in CharacterMovement
        expect(movement.moveZ).toBe(1);

        const diagNormalized = normalize2D(movement.moveX, movement.moveZ);
        const expectedMag = Math.sqrt(
            diagNormalized.x * diagNormalized.x + diagNormalized.z * diagNormalized.z,
        );
        expect(expectedMag).toBeCloseTo(1.0, 5);
    });

    it('CharacterMovement stops on key release', () => {
        tracker.onKeyDown('KeyW');
        tracker.onKeyDown('KeyD');
        expect(tracker.getMovement().isMoving).toBe(true);

        // Release both keys
        tracker.onKeyUp('KeyW');
        tracker.onKeyUp('KeyD');

        const movement = tracker.getMovement();
        expect(movement.isMoving).toBe(false);
        expect(movement.moveX).toBe(0);
        expect(movement.moveZ).toBe(0);
    });

    it('CharacterMovement handles blur (all keys released)', () => {
        // Press multiple movement keys
        tracker.onKeyDown('KeyW');
        tracker.onKeyDown('KeyA');
        tracker.onKeyDown('ShiftLeft');
        expect(tracker.getMovement().isMoving).toBe(true);

        // Simulate window blur
        tracker.onBlur();

        const movement = tracker.getMovement();
        expect(movement.isMoving).toBe(false);
        expect(movement.moveX).toBe(0);
        expect(movement.moveZ).toBe(0);
        expect(tracker.isPressed('ShiftLeft')).toBe(false);
    });
});

// ===========================================================================
// 6. Edge Cases & Error Handling (5 tests)
// ===========================================================================

describe('T-10 > Edge Cases & Error Handling', () => {
    it('rapid key press/release (key chatter) is handled without errors', () => {
        const input = createInputSystem({
            schema: { fire: { kind: 'button' } },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        fire: [{ type: 'control', control: 'keyboard/KeyF' }],
                    },
                },
            ],
        });

        // Simulate rapid chatter: press/release cycle 10 times
        for (let i = 0; i < 10; i++) {
            dispatchKey(input, 'KeyF', true);
            input.update(i * 2 + 1);
            dispatchKey(input, 'KeyF', false);
            input.update(i * 2 + 2);
        }

        // After all chatter, key should be released
        expect(input.read('fire')).toBe(false);
        expect(() => input.update(100)).not.toThrow();

        input.dispose();
    });

    it('focus loss clears input state completely', () => {
        const input = createInputSystem({
            schema: wasdSchema,
            contexts: [wasdContext],
        });

        // Press several keys
        dispatchKey(input, 'KeyW', true);
        dispatchKey(input, 'KeyD', true);
        dispatchKey(input, 'Space', true);
        dispatchKey(input, 'ShiftLeft', true);
        input.update(1);

        expect(input.read('jump')).toBe(true);
        expect(input.read('sprint')).toBe(true);
        expect(input.read('moveHorizontal')).toBe(1);
        expect(input.read('moveVertical')).toBe(1);

        // Focus lost
        input.dispatch({ type: 'focus', focused: false });
        input.update(2);

        // Everything should be cleared
        expect(input.read('jump')).toBe(false);
        expect(input.read('sprint')).toBe(false);
        expect(input.read('moveHorizontal')).toBe(0);
        expect(input.read('moveVertical')).toBe(0);
        expect(input.read('move')).toEqual({ x: 0, y: 0 });
        expect(input.isActive('move')).toBe(false);

        input.dispose();
    });

    it('input operations after dispose throw InputDisposedError', () => {
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

        input.dispose();

        // Methods that call _assertNotDisposed() should throw
        expect(() => input.update(1)).toThrow(InputDisposedError);
        expect(() =>
            input.dispatch({ type: 'keyboard', code: 'Space', pressed: true }),
        ).toThrow(InputDisposedError);
        expect(() => input.subscribe(() => {})).toThrow(InputDisposedError);
        expect(() => input.subscribeAction('jump', () => {})).toThrow(InputDisposedError);
        expect(() => input.attach()).toThrow(InputDisposedError);
        expect(() => input.registerContext({ id: 'test' })).toThrow(InputDisposedError);
        expect(() => input.snapshot()).toThrow(InputDisposedError);

        // isDisposed flag is set
        expect(input.isDisposed).toBe(true);
    });

    it('double dispose does not throw', () => {
        const input = createInputSystem({
            schema: { jump: { kind: 'button' } },
        });

        input.dispose();
        expect(() => input.dispose()).not.toThrow();
        expect(input.isDisposed).toBe(true);
    });

    it('DOM attachment forwards keyboard events and cleans up on dispose', () => {
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

        const attachment = input.attach({ document, window });

        // Simulate keydown via DOM
        pressKey('KeyW');
        input.update(1);

        const move = input.read('move');
        expect(move.y).toBeGreaterThan(0);

        // Simulate keyup via DOM
        releaseKey('KeyW');
        input.update(2);

        expect(input.read('move')).toEqual({ x: 0, y: 0 });

        // Dispose attachment - events should no longer be captured
        attachment.dispose();
        expect(attachment.isDisposed).toBe(true);

        pressKey('Space');
        input.update(3);

        // Should not have captured the event
        expect(input.read('jump')).toBe(false);

        input.dispose();
    });
});

// ===========================================================================
// 7. Full Pipeline Integration (additional robustness tests)
// ===========================================================================

describe('T-10 > Full Pipeline Integration', () => {
    it('complete WASD movement cycle through the InputSystem pipeline', () => {
        const input = createInputSystem({
            schema: wasdSchema,
            contexts: [wasdContext],
        });

        // Frame 1: Press W -> move forward
        dispatchKey(input, 'KeyW', true);
        input.update(1);
        expect(input.read('moveVertical')).toBe(1);
        expect(input.read('move').y).toBeCloseTo(1, 5);

        // Frame 2: Add D -> diagonal
        dispatchKey(input, 'KeyD', true);
        input.update(2);
        const moveDiag = input.read('move');
        expect(moveDiag.x).toBeCloseTo(Math.SQRT1_2, 5);
        expect(moveDiag.y).toBeCloseTo(Math.SQRT1_2, 5);

        // Frame 3: Release W -> only D (right)
        dispatchKey(input, 'KeyW', false);
        input.update(3);
        const moveRight = input.read('move');
        expect(moveRight.x).toBeCloseTo(1, 5);
        expect(moveRight.y).toBeCloseTo(0, 5);

        // Frame 4: Release D -> idle
        dispatchKey(input, 'KeyD', false);
        input.update(4);
        expect(input.read('move')).toEqual({ x: 0, y: 0 });
        expect(input.isActive('move')).toBe(false);

        input.dispose();
    });

    it('action events fire through the full pipeline on key press and release', () => {
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

        const events: string[] = [];
        input.subscribeAction('jump', (event) => {
            events.push(`${event.phase}:${event.trigger}`);
        });

        dispatchKey(input, 'Space', true);
        input.update(1);

        dispatchKey(input, 'Space', false);
        input.update(2);

        // Should have lifecycle events
        expect(events.length).toBeGreaterThan(0);
        expect(events).toContain('started:press');
        expect(events).toContain('canceled:release');

        input.dispose();
    });

    it('context activation/deactivation gates input processing', () => {
        const input = createInputSystem({
            schema: wasdSchema,
            contexts: [wasdContext],
        });

        // Press W -> should register
        dispatchKey(input, 'KeyW', true);
        input.update(1);
        expect(input.read('moveVertical')).toBe(1);

        // Deactivate gameplay context
        input.deactivateContext('gameplay');
        input.update(2);

        // Values should be zeroed because context is inactive
        expect(input.read('moveVertical')).toBe(0);
        expect(input.read('move')).toEqual({ x: 0, y: 0 });

        // Re-activate
        input.activateContext('gameplay');
        input.update(3);

        // W is still physically held, should register again
        expect(input.read('moveVertical')).toBe(1);

        input.dispose();
    });

    it('frame counter increments on each update call', () => {
        const input = createInputSystem({
            schema: { jump: { kind: 'button' } },
        });

        expect(input.frame).toBe(0);
        input.update(1);
        expect(input.frame).toBe(1);
        input.update(2);
        expect(input.frame).toBe(2);
        input.update(100);
        expect(input.frame).toBe(3);

        input.dispose();
    });

    it('timestamp is updated on each update call', () => {
        const input = createInputSystem({
            schema: { jump: { kind: 'button' } },
            now: () => 0,
        });

        input.update(10);
        expect(input.timestamp).toBe(10);
        input.update(20);
        expect(input.timestamp).toBe(20);

        input.dispose();
    });

    it('vector2 state view is frozen and immutable', () => {
        const input = createInputSystem({
            schema: wasdSchema,
            contexts: [wasdContext],
        });

        dispatchKey(input, 'KeyW', true);
        dispatchKey(input, 'KeyD', true);
        input.update(1);

        const state = input.state('move');
        expect(Object.isFrozen(state)).toBe(true);
        if (state.kind === 'vector2') {
            expect(Object.isFrozen(state.value)).toBe(true);
            expect(() => {
                (state.value as { x: number }).x = 999;
            }).toThrow();
        }

        // Original value should be unchanged
        const move = input.read('move');
        expect(move.x).toBeCloseTo(Math.SQRT1_2, 5);

        input.dispose();
    });
});
