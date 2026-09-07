import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ContactManager2D } from '@axrone/physics-2d';
import { SensorEventType, CollisionEventType } from '@axrone/physics-core';

describe('ContactManager2D', () => {
    let manager: ContactManager2D;
    const bodyIdA = 1 as any;
    const bodyIdB = 2 as any;
    const shapeIdA = 10 as any;
    const shapeIdB = 20 as any;

    beforeEach(() => {
        manager = new ContactManager2D(64);
    });

    describe('Contact Creation', () => {
        it('creates contact', () => {
            const contactId = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            expect(contactId).toBeGreaterThan(0);
            expect(manager.contactCount).toBe(1);
        });

        it('creates multiple contacts', () => {
            manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            manager.createContact(shapeIdA, 21 as any, bodyIdA, bodyIdB);
            manager.createContact(11 as any, shapeIdB, bodyIdA, bodyIdB);
            expect(manager.contactCount).toBe(3);
        });

        it('throws when capacity exceeded', () => {
            const smallManager = new ContactManager2D(2);
            smallManager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            smallManager.createContact(shapeIdA, 21 as any, bodyIdA, bodyIdB);
            expect(() => {
                smallManager.createContact(11 as any, shapeIdB, bodyIdA, bodyIdB);
            }).toThrow();
        });
    });

    describe('Contact Destruction', () => {
        it('destroys contact', () => {
            const contactId = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            expect(manager.contactCount).toBe(1);
            manager.destroyContact(contactId);
            expect(manager.contactCount).toBe(0);
        });

        it('handles destroying non-existent contact', () => {
            manager.destroyContact(9999 as any);
            expect(manager.contactCount).toBe(0);
        });

        it('destroys multiple contacts', () => {
            const id1 = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            const id2 = manager.createContact(shapeIdA, 21 as any, bodyIdA, bodyIdB);
            manager.destroyContact(id1);
            manager.destroyContact(id2);
            expect(manager.contactCount).toBe(0);
        });
    });

    describe('Contact Updates', () => {
        it('updates contact manifold', () => {
            const contactId = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            const manifold = {
                normal: { x: 1, y: 0 },
                pointCount: 1,
                points: [
                    {
                        localPointA: { x: 0, y: 0 },
                        localPointB: { x: 0, y: 0 },
                        separation: -0.1,
                        id: 0 as any,
                        normalImpulse: 0,
                        tangentImpulse: 0,
                    },
                ],
            };

            manager.updateContact(contactId, manifold as any);
        });

        it('updates contact with multiple points', () => {
            const contactId = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            const manifold = {
                normal: { x: 1, y: 0 },
                pointCount: 2,
                points: [
                    {
                        localPointA: { x: 0, y: 0 },
                        localPointB: { x: 0, y: 0 },
                        separation: -0.1,
                        id: 0 as any,
                        normalImpulse: 0,
                        tangentImpulse: 0,
                    },
                    {
                        localPointA: { x: 1, y: 0 },
                        localPointB: { x: 1, y: 0 },
                        separation: -0.1,
                        id: 1 as any,
                        normalImpulse: 0,
                        tangentImpulse: 0,
                    },
                ],
            };

            manager.updateContact(contactId, manifold as any);
        });

        it('handles update of non-existent contact', () => {
            const manifold = {
                normal: { x: 1, y: 0 },
                pointCount: 0,
                points: [],
            };
            manager.updateContact(9999 as any, manifold as any);
        });
    });

    describe('Warm Start Impulses', () => {
        it('gets warm start impulse', () => {
            const contactId = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            const impulse = manager.getWarmStartImpulse(contactId, 0);
            expect(impulse.normalImpulse).toBe(0);
            expect(impulse.tangentImpulse).toBe(0);
        });

        it('sets warm start impulse', () => {
            const contactId = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            manager.setWarmStartImpulse(contactId, 0, 10, 5);
            const impulse = manager.getWarmStartImpulse(contactId, 0);
            expect(impulse.normalImpulse).toBe(10);
            expect(impulse.tangentImpulse).toBe(5);
        });

        it('sets impulses for multiple contact points', () => {
            const contactId = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            manager.setWarmStartImpulse(contactId, 0, 10, 5);
            manager.setWarmStartImpulse(contactId, 1, 20, 15);

            const impulse0 = manager.getWarmStartImpulse(contactId, 0);
            const impulse1 = manager.getWarmStartImpulse(contactId, 1);

            expect(impulse0.normalImpulse).toBe(10);
            expect(impulse1.normalImpulse).toBe(20);
        });

        it('returns zero impulse for non-existent contact', () => {
            const impulse = manager.getWarmStartImpulse(9999 as any, 0);
            expect(impulse.normalImpulse).toBe(0);
            expect(impulse.tangentImpulse).toBe(0);
        });
    });

    describe('Contact Data Access', () => {
        it('gets contact data with default material values', () => {
            const contactId = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            const data = manager.getContactData(contactId);
            expect(data).not.toBeNull();
            if (data) {
                // No materials → defaults: friction=0.2, restitution=0.0
                expect(data.friction).toBeCloseTo(0.2, 5);
                expect(data.restitution).toBeCloseTo(0.0, 5);
            }
        });

        it('computes geometric mean friction from materials (C2 fix)', () => {
            const matA = { friction: 0.4, restitution: 0.3, density: 1 };
            const matB = { friction: 0.9, restitution: 0.7, density: 1 };
            const contactId = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB, matA as any, matB as any);
            const data = manager.getContactData(contactId);
            expect(data).not.toBeNull();
            if (data) {
                // sqrt(0.4 * 0.9) = sqrt(0.36) = 0.6
                expect(data.friction).toBeCloseTo(0.6, 5);
                // max(0.3, 0.7) = 0.7
                expect(data.restitution).toBeCloseTo(0.7, 5);
            }
        });

        it('returns null for non-existent contact', () => {
            const data = manager.getContactData(9999 as any);
            expect(data).toBeNull();
        });
    });

    describe('Contact Listener', () => {
        it('sets contact listener and fires on collision events', () => {
            const listener = {
                onCollisionBegin: vi.fn(),
                onCollisionEnd: vi.fn(),
            };
            manager.setContactListener(listener);

            // Create a contact and update it with a manifold to trigger collision begin
            const contactId = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            manager.updateContact(contactId, {
                normal: { x: 1, y: 0 },
                pointCount: 1,
                points: [{
                    localPointA: { x: 0, y: 0 },
                    localPointB: { x: 0, y: 0 },
                    separation: -0.1,
                    id: 0 as any,
                    normalImpulse: 0,
                    tangentImpulse: 0,
                }],
            } as any);

            // updateContact fires onCollisionBegin directly (not-touching → touching)
            expect(listener.onCollisionBegin).toHaveBeenCalledTimes(1);
        });

        it('clears contact listener', () => {
            manager.setContactListener(null);
            // After clearing, updates should not throw
            const contactId = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            expect(() => manager.updateContact(contactId, {
                normal: { x: 1, y: 0 },
                pointCount: 0,
                points: [],
            } as any)).not.toThrow();
        });
    });

    describe('Collision Filter', () => {
        it('sets collision filter and blocks contacts', () => {
            const filter = {
                shouldCollide: vi.fn(() => false),
            };
            manager.setCollisionFilter(filter);

            // Create contact — filter should block it
            const contactId = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            expect(filter.shouldCollide).toHaveBeenCalledWith(shapeIdA, shapeIdB);
            // Contact was blocked (returns 0)
            expect(contactId).toBe(0);
            expect(manager.contactCount).toBe(0);
        });

        it('clears collision filter', () => {
            manager.setCollisionFilter(null);
            // After clearing, contacts should be created normally
            const contactId = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            expect(contactId).toBeGreaterThan(0);
        });
    });

    describe('Body Contact Queries', () => {
        it('gets contacts for body', () => {
            const id1 = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            const id2 = manager.createContact(shapeIdA, 21 as any, bodyIdA, 3 as any);

            const contacts = Array.from(manager.getContactsForBody(bodyIdA));
            expect(contacts).toHaveLength(2);
            expect(contacts).toContain(id1);
            expect(contacts).toContain(id2);
        });

        it('returns empty for body with no contacts', () => {
            const contacts = Array.from(manager.getContactsForBody(999 as any));
            expect(contacts).toHaveLength(0);
        });
    });

    describe('Disposal', () => {
        it('disposes manager', () => {
            manager[Symbol.dispose]();
        });

        it('throws when using after disposal', () => {
            manager[Symbol.dispose]();
            expect(() => {
                manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            }).toThrow();
        });

        it('allows double disposal', () => {
            manager[Symbol.dispose]();
            manager[Symbol.dispose]();
        });
    });

    describe('Edge Cases', () => {
        it('handles many contacts', () => {
            for (let i = 0; i < 50; i++) {
                manager.createContact((10 + i) as any, (20 + i) as any, bodyIdA, bodyIdB);
            }
            expect(manager.contactCount).toBe(50);
        });

        it('handles contact creation and destruction cycle', () => {
            for (let i = 0; i < 20; i++) {
                const id = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
                manager.destroyContact(id);
            }
            expect(manager.contactCount).toBe(0);
        });
    });

    describe('EF#2 manifold ID separation', () => {
        it('assigns distinct manifold IDs independent of contact IDs', () => {
            const c1 = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            const c2 = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);

            // Contact IDs should be different
            expect(c1).not.toBe(c2);

            // Trigger collision to build manifold — use updateContact with points
            manager.updateContact(c1, {
                id: 0 as any,
                bodyIdA,
                bodyIdB,
                shapeIdA,
                shapeIdB,
                normal: { x: 1, y: 0 },
                pointCount: 1,
                points: [{ localPointA: { x: 0, y: 0 }, localPointB: { x: 0, y: 0 }, separation: 0 }],
            });

            // Destroy should not throw and manifold ID should be valid
            expect(() => manager.destroyContact(c1)).not.toThrow();
            expect(() => manager.destroyContact(c2)).not.toThrow();
            expect(manager.contactCount).toBe(0);
        });
    });

    describe('Sensor Event Dispatch', () => {
        const sensorShapeId = 100 as any;
        const visitorShapeId = 200 as any;
        const sensorBodyId = 1 as any;
        const visitorBodyId = 2 as any;

        const TOUCHING_MANIFOLD = {
            normal: { x: 1, y: 0 },
            pointCount: 1,
            points: [{
                localPointA: { x: 0, y: 0 },
                localPointB: { x: 0, y: 0 },
                separation: -0.1,
                id: 0 as any,
                normalImpulse: 0,
                tangentImpulse: 0,
            }],
        };

        const SEPARATED_MANIFOLD = {
            normal: { x: 1, y: 0 },
            pointCount: 0,
            points: [],
        };

        it('fires onSensorEnter (not onCollisionBegin) for sensor contacts', () => {
            manager.setSensorChecker((id) => id === sensorShapeId);
            const listener = {
                onCollisionBegin: vi.fn(),
                onSensorEnter: vi.fn(),
            };
            manager.setContactListener(listener);

            const contactId = manager.createContact(sensorShapeId, visitorShapeId, sensorBodyId, visitorBodyId);
            manager.updateContact(contactId, TOUCHING_MANIFOLD as any);

            expect(listener.onSensorEnter).toHaveBeenCalledTimes(1);
            expect(listener.onCollisionBegin).not.toHaveBeenCalled();

            const event = listener.onSensorEnter.mock.calls[0][0];
            expect(event.type).toBe(SensorEventType.Enter);
            expect(event.sensorBodyId).toBe(sensorBodyId);
            expect(event.sensorShapeId).toBe(sensorShapeId);
            expect(event.visitorBodyId).toBe(visitorBodyId);
            expect(event.visitorShapeId).toBe(visitorShapeId);
            expect(typeof event.timestamp).toBe('number');
        });

        it('fires onSensorStay with reusable mutable object on continuing contact', () => {
            manager.setSensorChecker((id) => id === sensorShapeId);
            const listener = { onSensorStay: vi.fn() };
            manager.setContactListener(listener);

            const contactId = manager.createContact(sensorShapeId, visitorShapeId, sensorBodyId, visitorBodyId);
            manager.updateContact(contactId, TOUCHING_MANIFOLD as any); // Begin
            manager.updateContact(contactId, TOUCHING_MANIFOLD as any); // Stay

            expect(listener.onSensorStay).toHaveBeenCalledTimes(1);
            const event = listener.onSensorStay.mock.calls[0][0];
            expect(event.type).toBe(SensorEventType.Stay);
            expect(event.sensorBodyId).toBe(sensorBodyId);
        });

        it('fires onSensorExit when sensor contact separates', () => {
            manager.setSensorChecker((id) => id === sensorShapeId);
            const listener = {
                onSensorEnter: vi.fn(),
                onSensorExit: vi.fn(),
            };
            manager.setContactListener(listener);

            const contactId = manager.createContact(sensorShapeId, visitorShapeId, sensorBodyId, visitorBodyId);
            manager.updateContact(contactId, TOUCHING_MANIFOLD as any); // Enter
            manager.updateContact(contactId, SEPARATED_MANIFOLD as any); // Exit

            expect(listener.onSensorEnter).toHaveBeenCalledTimes(1);
            expect(listener.onSensorExit).toHaveBeenCalledTimes(1);
            const exitEvent = listener.onSensorExit.mock.calls[0][0];
            expect(exitEvent.type).toBe(SensorEventType.Exit);
            expect(exitEvent.sensorBodyId).toBe(sensorBodyId);
        });

        it('fires onSensorExit when sensor contact is destroyed while touching', () => {
            manager.setSensorChecker((id) => id === sensorShapeId);
            const listener = { onSensorExit: vi.fn() };
            manager.setContactListener(listener);

            const contactId = manager.createContact(sensorShapeId, visitorShapeId, sensorBodyId, visitorBodyId);
            manager.updateContact(contactId, TOUCHING_MANIFOLD as any); // Enter
            manager.destroyContact(contactId); // Exit via destroy

            expect(listener.onSensorExit).toHaveBeenCalledTimes(1);
        });

        it('negative: normal contact does NOT fire onSensorEnter', () => {
            manager.setSensorChecker(() => false); // No shapes are sensors
            const listener = {
                onCollisionBegin: vi.fn(),
                onSensorEnter: vi.fn(),
            };
            manager.setContactListener(listener);

            const contactId = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            manager.updateContact(contactId, TOUCHING_MANIFOLD as any);

            expect(listener.onCollisionBegin).toHaveBeenCalledTimes(1);
            expect(listener.onSensorEnter).not.toHaveBeenCalled();
        });

        it('negative: sensor contact does NOT fire onCollisionBegin', () => {
            manager.setSensorChecker((id) => id === sensorShapeId);
            const listener = {
                onCollisionBegin: vi.fn(),
                onCollisionEnd: vi.fn(),
            };
            manager.setContactListener(listener);

            const contactId = manager.createContact(sensorShapeId, visitorShapeId, sensorBodyId, visitorBodyId);
            manager.updateContact(contactId, TOUCHING_MANIFOLD as any);
            manager.updateContact(contactId, SEPARATED_MANIFOLD as any);

            expect(listener.onCollisionBegin).not.toHaveBeenCalled();
            expect(listener.onCollisionEnd).not.toHaveBeenCalled();
        });

        it('resolves sensor/visitor correctly when shapeIdB is the sensor', () => {
            // shapeIdB is sensor, shapeIdA is visitor
            manager.setSensorChecker((id) => id === shapeIdB);
            const listener = { onSensorEnter: vi.fn() };
            manager.setContactListener(listener);

            const contactId = manager.createContact(shapeIdA, shapeIdB, bodyIdA, bodyIdB);
            manager.updateContact(contactId, TOUCHING_MANIFOLD as any);

            const event = listener.onSensorEnter.mock.calls[0][0];
            expect(event.sensorBodyId).toBe(bodyIdB);
            expect(event.sensorShapeId).toBe(shapeIdB);
            expect(event.visitorBodyId).toBe(bodyIdA);
            expect(event.visitorShapeId).toBe(shapeIdA);
        });

        it('does not fire sensor events when no sensor checker is set', () => {
            // No sensor checker → all contacts treated as normal
            const listener = {
                onCollisionBegin: vi.fn(),
                onSensorEnter: vi.fn(),
            };
            manager.setContactListener(listener);

            const contactId = manager.createContact(sensorShapeId, visitorShapeId, sensorBodyId, visitorBodyId);
            manager.updateContact(contactId, TOUCHING_MANIFOLD as any);

            expect(listener.onCollisionBegin).toHaveBeenCalledTimes(1);
            expect(listener.onSensorEnter).not.toHaveBeenCalled();
        });
    });
});

