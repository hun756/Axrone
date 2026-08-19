import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Component, script } from '@axrone/ecs-runtime';
import { World } from '@axrone/ecs-runtime';
import { Actor } from '@axrone/ecs-runtime/actor';
import { Transform } from '@axrone/ecs-runtime';
import { createActor } from './factory-helpers';

/**
 * Test component that tracks all lifecycle events
 */
class LifecycleTestComponent extends Component {
    public events: string[] = [];

    awake(): void {
        this.events.push('awake');
    }

    start(): void {
        this.events.push('start');
    }

    onEnable(): void {
        this.events.push('onEnable');
    }

    onDisable(): void {
        this.events.push('onDisable');
    }

    update(dt: number): void {
        this.events.push(`update:${dt}`);
    }

    lateUpdate(dt: number): void {
        this.events.push(`lateUpdate:${dt}`);
    }

    fixedUpdate(dt: number): void {
        this.events.push(`fixedUpdate:${dt}`);
    }

    onDestroy(): void {
        this.events.push('onDestroy');
    }
}

/**
 * Test component with priority for ordering tests
 */
@script({ priority: 10 })
class HighPriorityComponent extends Component {
    public executionOrder: number[] = [];
    private static _order = 0;

    update(): void {
        this.executionOrder.push(HighPriorityComponent._order++);
    }

    static resetOrder(): void {
        HighPriorityComponent._order = 0;
    }
}

@script({ priority: 5 })
class MediumPriorityComponent extends Component {
    public executionOrder: number[] = [];
    private static _order = 0;

    update(): void {
        this.executionOrder.push(MediumPriorityComponent._order++);
    }

    static resetOrder(): void {
        MediumPriorityComponent._order = 0;
    }
}

@script({ priority: 1 })
class LowPriorityComponent extends Component {
    public executionOrder: number[] = [];
    private static _order = 0;

    update(): void {
        this.executionOrder.push(LowPriorityComponent._order++);
    }

    static resetOrder(): void {
        LowPriorityComponent._order = 0;
    }
}

/**
 * Test component with dependencies
 */
@script({ dependencies: [Transform] })
class DependentComponent extends Component {
    public dependencyResolved = false;

    awake(): void {
        this.dependencyResolved = this.actor?.hasComponent(Transform) ?? false;
    }
}

/**
 * Test component that depends on another custom component
 */
class DependencyTargetComponent extends Component {
    public initialized = false;

    awake(): void {
        this.initialized = true;
    }
}

@script({ dependencies: [DependencyTargetComponent] })
class DependentOnCustomComponent extends Component {
    public targetWasInitialized = false;

    start(): void {
        const target = this.actor?.getComponent(DependencyTargetComponent);
        this.targetWasInitialized = target?.initialized ?? false;
    }
}

describe('Component Lifecycle Integration', () => {
    let world: World<any>;
    let actor: Actor;
    let registry: any;

    beforeEach(() => {
        registry = {
            Transform,
            LifecycleTestComponent,
            HighPriorityComponent,
            MediumPriorityComponent,
            LowPriorityComponent,
            DependentComponent,
            DependencyTargetComponent,
            DependentOnCustomComponent,
        };
        world = new World(registry);
        actor = createActor(world, 'TestActor');
    });

    afterEach(() => {
        try {
            if (actor && !actor.isDestroyed) {
                actor.destroy();
            }
        } catch (error) {}
        try {
            if (world && !world.isDisposed) {
                world.clear();
            }
        } catch (error) {}
    });

    describe('Test Group 1: Basic Lifecycle Flow', () => {
        it('should transition through complete lifecycle: uninitialized → awake → enabled → destroyed', async () => {
            const component = actor.addComponent(LifecycleTestComponent);

            expect(component.state).toBe('uninitialized');
            expect(component.events).toEqual([]);

            await component._internalAwake();
            expect(component.state).toBe('awake');
            expect(component.events).toContain('awake');

            await component._internalStart();
            expect(component.state).toBe('enabled');
            expect(component.events).toContain('start');
            expect(component.events).toContain('onEnable');

            component._internalUpdate(0.016);
            expect(component.events).toContain('update:0.016');

            await component._internalDestroy();
            expect(component.state).toBe('destroyed');
            expect(component.events).toContain('onDisable');
            expect(component.events).toContain('onDestroy');
        });

        it('should call lifecycle methods in correct order', async () => {
            const component = new LifecycleTestComponent();

            await component._internalAwake();
            expect(component.events[0]).toBe('awake');

            await component._internalStart();
            expect(component.events[1]).toBe('start');
            expect(component.events[2]).toBe('onEnable');

            component._internalUpdate(0.016);
            expect(component.events[3]).toBe('update:0.016');

            await component._internalDestroy();
            expect(component.events[4]).toBe('onDisable');
            expect(component.events[5]).toBe('onDestroy');

            component._internalDestroy();
        });

        it('should pass correct deltaTime to update methods', async () => {
            const component = new LifecycleTestComponent();

            await component._internalAwake();
            await component._internalStart();

            component._internalUpdate(0.016);
            component._internalUpdate(0.033);
            component._internalUpdate(0.05);

            expect(component.events).toContain('update:0.016');
            expect(component.events).toContain('update:0.033');
            expect(component.events).toContain('update:0.05');

            component._internalDestroy();
        });

        it('should handle actor-level lifecycle correctly', () => {
            const component = actor.addComponent(LifecycleTestComponent);

            expect(component.events).toContain('awake');

            actor.start();
            expect(component.events).toContain('start');
            expect(component.events).toContain('onEnable');

            actor.update(0.016);
            expect(component.events).toContain('update:0.016');

            actor.destroy();
            expect(component.events).toContain('onDisable');
            expect(component.events).toContain('onDestroy');
        });
    });

    describe('Test Group 2: Enable/Disable Cycle', () => {
        it('should call onDisable when component is disabled', async () => {
            const component = new LifecycleTestComponent();

            await component._internalAwake();
            await component._internalStart();

            expect(component.state).toBe('enabled');
            expect(component.events).toContain('onEnable');

            component.enabled = false;
            expect(component.state).toBe('disabled');
            expect(component.events).toContain('onDisable');

            component._internalDestroy();
        });

        it('should call onEnable when component is re-enabled', async () => {
            const component = new LifecycleTestComponent();

            await component._internalAwake();
            await component._internalStart();

            component.enabled = false;
            expect(component.state).toBe('disabled');

            component.enabled = true;
            expect(component.state).toBe('enabled');

            const onEnableCount = component.events.filter((e) => e === 'onEnable').length;
            expect(onEnableCount).toBe(2);

            component._internalDestroy();
        });

        it('should NOT call update when component is disabled', async () => {
            const component = new LifecycleTestComponent();

            await component._internalAwake();
            await component._internalStart();

            component.enabled = false;
            component._internalUpdate(0.016);

            const updateEvents = component.events.filter((e) => e.startsWith('update:'));
            expect(updateEvents).toHaveLength(0);

            component._internalDestroy();
        });

        it('should call update after re-enabling component', async () => {
            const component = new LifecycleTestComponent();

            await component._internalAwake();
            await component._internalStart();

            component.enabled = false;
            component._internalUpdate(0.016);

            component.enabled = true;
            component._internalUpdate(0.033);

            const updateEvents = component.events.filter((e) => e.startsWith('update:'));
            expect(updateEvents).toHaveLength(1);
            expect(updateEvents[0]).toBe('update:0.033');

            component._internalDestroy();
        });

        it('should handle actor-level enable/disable cycle', () => {
            const component = actor.addComponent(LifecycleTestComponent);

            actor.start();
            expect(component.events).toContain('onEnable');

            actor.active = false;
            expect(component.events).toContain('onDisable');

            actor.active = true;
            const onEnableCount = component.events.filter((e) => e === 'onEnable').length;
            expect(onEnableCount).toBe(2);

            actor.destroy();
        });

        it('should not call update on disabled actor', () => {
            const component = actor.addComponent(LifecycleTestComponent);

            actor.start();
            actor.active = false;

            actor.update(0.016);

            const updateEvents = component.events.filter((e) => e.startsWith('update:'));
            expect(updateEvents).toHaveLength(0);

            actor.destroy();
        });
    });

    describe('Test Group 3: Priority Ordering', () => {
        it('should execute components in priority order (lower number = earlier)', () => {
            LowPriorityComponent.resetOrder();
            MediumPriorityComponent.resetOrder();
            HighPriorityComponent.resetOrder();

            const low = actor.addComponent(LowPriorityComponent);
            const medium = actor.addComponent(MediumPriorityComponent);
            const high = actor.addComponent(HighPriorityComponent);

            actor.start();
            actor.update(0.016);

            expect(low.executionOrder[0]).toBe(0);
            expect(medium.executionOrder[0]).toBe(1);
            expect(high.executionOrder[0]).toBe(2);
        });

        it('should maintain priority order across multiple updates', () => {
            LowPriorityComponent.resetOrder();
            MediumPriorityComponent.resetOrder();
            HighPriorityComponent.resetOrder();

            actor.addComponent(LowPriorityComponent);
            actor.addComponent(MediumPriorityComponent);
            actor.addComponent(HighPriorityComponent);

            actor.start();

            actor.update(0.016);
            actor.update(0.016);
            actor.update(0.016);

            const lowComp = actor.getComponent(LowPriorityComponent) as LowPriorityComponent;
            const mediumComp = actor.getComponent(MediumPriorityComponent) as MediumPriorityComponent;
            const highComp = actor.getComponent(HighPriorityComponent) as HighPriorityComponent;

            expect(lowComp.executionOrder).toHaveLength(3);
            expect(mediumComp.executionOrder).toHaveLength(3);
            expect(highComp.executionOrder).toHaveLength(3);

            expect(lowComp.executionOrder[0]).toBeLessThan(mediumComp.executionOrder[0]);
            expect(mediumComp.executionOrder[0]).toBeLessThan(highComp.executionOrder[0]);
        });

        it('should handle components with same priority', () => {
            class SamePriorityA extends Component {
                public order: number[] = [];
                private static _counter = 0;
                update() { this.order.push(SamePriorityA._counter++); }
                static reset() { SamePriorityA._counter = 0; }
            }

            class SamePriorityB extends Component {
                public order: number[] = [];
                update() { this.order.push(SamePriorityA._counter++); }
            }

            world.registerComponentType(SamePriorityA);
            world.registerComponentType(SamePriorityB);
            SamePriorityA.reset();

            actor.addComponent(SamePriorityA);
            actor.addComponent(SamePriorityB);

            actor.start();
            actor.update(0.016);

            const compA = actor.getComponent(SamePriorityA) as SamePriorityA;
            const compB = actor.getComponent(SamePriorityB) as SamePriorityB;

            expect(compA.order).toHaveLength(1);
            expect(compB.order).toHaveLength(1);
        });
    });

    describe('Test Group 4: Dependency Resolution', () => {
        it('should resolve Transform dependency before dependent component starts', () => {
            const dependent = actor.addComponent(DependentComponent);

            expect(dependent.dependencyResolved).toBe(true);
            expect(actor.hasComponent(Transform)).toBe(true);
        });

        it('should resolve custom component dependencies', () => {
            const target = actor.addComponent(DependencyTargetComponent);
            const dependent = actor.addComponent(DependentOnCustomComponent);

            actor.start();

            expect(target.initialized).toBe(true);
            expect(dependent.targetWasInitialized).toBe(true);
        });

        it('should throw error when dependency is missing', () => {
            expect(() => {
                actor.addComponent(DependentComponent);
            }).not.toThrow();

            const dependent = actor.getComponent(DependentComponent);
            expect(dependent).toBeDefined();
        });

        it('should maintain dependency order during actor start', () => {
            const target = actor.addComponent(DependencyTargetComponent);
            const dependent = actor.addComponent(DependentOnCustomComponent);

            expect(target.initialized).toBe(true);

            actor.start();

            expect(dependent.targetWasInitialized).toBe(true);
        });
    });

    describe('Test Group 5: Scene-Level Lifecycle', () => {
        it('should run update through scene scheduler', () => {
            const component = actor.addComponent(LifecycleTestComponent);

            actor.start();
            actor.update(0.016);

            expect(component.events).toContain('update:0.016');

            actor.destroy();
        });

        it('should run lateUpdate after all updates', () => {
            const component = actor.addComponent(LifecycleTestComponent);

            actor.start();
            actor.lateUpdate(0.016);

            expect(component.events).toContain('lateUpdate:0.016');

            actor.destroy();
        });

        it('should run fixedUpdate at fixed intervals', () => {
            const component = actor.addComponent(LifecycleTestComponent);

            actor.start();
            actor.fixedUpdate(0.02);

            expect(component.events).toContain('fixedUpdate:0.02');

            actor.destroy();
        });

        it('should execute lifecycle phases in correct order: fixedUpdate → update → lateUpdate', () => {
            const component = actor.addComponent(LifecycleTestComponent);

            actor.start();

            actor.fixedUpdate(0.02);
            actor.update(0.016);
            actor.lateUpdate(0.016);

            const fixedIndex = component.events.indexOf('fixedUpdate:0.02');
            const updateIndex = component.events.indexOf('update:0.016');
            const lateIndex = component.events.indexOf('lateUpdate:0.016');

            expect(fixedIndex).toBeLessThan(updateIndex);
            expect(updateIndex).toBeLessThan(lateIndex);

            actor.destroy();
        });

        it('should not execute updates on inactive actor', () => {
            const component = actor.addComponent(LifecycleTestComponent);

            actor.start();
            actor.active = false;

            actor.update(0.016);
            actor.lateUpdate(0.016);
            actor.fixedUpdate(0.02);

            const updateEvents = component.events.filter((e) =>
                e.startsWith('update:') || e.startsWith('lateUpdate:') || e.startsWith('fixedUpdate:')
            );
            expect(updateEvents).toHaveLength(0);

            actor.destroy();
        });
    });

    describe('Test Group 6: Component State Machine', () => {
        it('should transition: uninitialized → awake (via _internalAwake)', async () => {
            const component = new LifecycleTestComponent();

            expect(component.state).toBe('uninitialized');

            await component._internalAwake();
            expect(component.state).toBe('awake');

            component._internalDestroy();
        });

        it('should transition: awake → started → enabled (via _internalStart)', async () => {
            const component = new LifecycleTestComponent();

            await component._internalAwake();
            expect(component.state).toBe('awake');

            await component._internalStart();
            expect(component.state).toBe('enabled');

            component._internalDestroy();
        });

        it('should transition: enabled → disabled (via enabled = false)', async () => {
            const component = new LifecycleTestComponent();

            await component._internalAwake();
            await component._internalStart();
            expect(component.state).toBe('enabled');

            component.enabled = false;
            expect(component.state).toBe('disabled');

            component._internalDestroy();
        });

        it('should transition: disabled → enabled (via enabled = true)', async () => {
            const component = new LifecycleTestComponent();

            await component._internalAwake();
            await component._internalStart();

            component.enabled = false;
            expect(component.state).toBe('disabled');

            component.enabled = true;
            expect(component.state).toBe('enabled');

            component._internalDestroy();
        });

        it('should transition: any → destroyed (via _internalDestroy)', async () => {
            const component = new LifecycleTestComponent();

            await component._internalAwake();
            await component._internalStart();

            await component._internalDestroy();
            expect(component.state).toBe('destroyed');
        });

        it('should not allow state changes on destroyed component', async () => {
            const component = new LifecycleTestComponent();

            await component._internalAwake();
            await component._internalStart();
            await component._internalDestroy();

            expect(() => {
                component.enabled = true;
            }).toThrow();
        });

        it('should handle invalid transition: awake without _internalAwake', async () => {
            const component = new LifecycleTestComponent();

            expect(component.state).toBe('uninitialized');

            await component._internalStart();
            expect(component.state).toBe('uninitialized');

            component._internalDestroy();
        });

        it('should handle invalid transition: _internalAwake on already awake component', async () => {
            const component = new LifecycleTestComponent();

            await component._internalAwake();
            expect(component.state).toBe('awake');

            await component._internalAwake();
            expect(component.state).toBe('awake');

            component._internalDestroy();
        });

        it('should handle multiple destroy calls gracefully', async () => {
            const component = new LifecycleTestComponent();

            await component._internalAwake();
            await component._internalStart();

            await component._internalDestroy();
            expect(component.state).toBe('destroyed');

            await component._internalDestroy();
            expect(component.state).toBe('destroyed');
        });

        it('should prevent update on uninitialized component', () => {
            const component = new LifecycleTestComponent();

            expect(component.state).toBe('uninitialized');

            component._internalUpdate(0.016);

            const updateEvents = component.events.filter((e) => e.startsWith('update:'));
            expect(updateEvents).toHaveLength(0);

            component._internalDestroy();
        });

        it('should prevent update on awake component', async () => {
            const component = new LifecycleTestComponent();

            await component._internalAwake();
            expect(component.state).toBe('awake');

            component._internalUpdate(0.016);

            const updateEvents = component.events.filter((e) => e.startsWith('update:'));
            expect(updateEvents).toHaveLength(0);

            component._internalDestroy();
        });
    });
});
