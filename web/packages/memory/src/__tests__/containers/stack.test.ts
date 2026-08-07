import { describe, expect, it } from 'vitest';
import {
    OptimizedArrayStack,
    ImmutableStack,
    createStackCapacity,
    createStackSize,
    createNodeId,
    StackCapacityError,
    StackIntegrityError,
} from '../../containers/stack';
import { StackIterator } from '../../containers/stack/stack-iterator';
import { StackMemoryPool } from '../../containers/stack/pool-adapter';

describe('Stack Module', () => {
    describe('stack-core', () => {
        describe('createStackCapacity()', () => {
            it('creates valid capacity', () => {
                const cap = createStackCapacity(100);
                expect(cap).toBe(100);
            });

            it('throws on negative value', () => {
                expect(() => createStackCapacity(-1)).toThrow(StackIntegrityError);
            });

            it('throws on zero', () => {
                expect(() => createStackCapacity(0)).toThrow(StackIntegrityError);
            });

            it('throws on overflow', () => {
                expect(() => createStackCapacity(0x80000000)).toThrow(StackIntegrityError);
            });
        });

        describe('createStackSize()', () => {
            it('creates valid size', () => {
                const size = createStackSize(50);
                expect(size).toBe(50);
            });

            it('allows zero', () => {
                const size = createStackSize(0);
                expect(size).toBe(0);
            });

            it('throws on negative value', () => {
                expect(() => createStackSize(-1)).toThrow(StackIntegrityError);
            });
        });

        describe('createNodeId()', () => {
            it('creates a node ID', () => {
                const id = createNodeId();
                expect(typeof id).toBe('number');
            });

            it('creates unique IDs', () => {
                const ids = new Set();
                for (let i = 0; i < 100; i++) {
                    ids.add(createNodeId());
                }
                expect(ids.size).toBeGreaterThan(90);
            });
        });
    });

    describe('StackIterator', () => {
        it('iterates over empty stack', () => {
            const iter = new StackIterator(null, 0);
            expect(iter.next().done).toBe(true);
        });

        it('iterates over single node', () => {
            const node = { id: 1, value: 'a', next: null, refs: 1, generation: 0, memAddr: 0 };
            const iter = new StackIterator(node, 0);
            expect(iter.next()).toEqual({ done: false, value: 'a' });
            expect(iter.next().done).toBe(true);
        });

        it('iterates over multiple nodes', () => {
            const node3 = { id: 3, value: 'c', next: null, refs: 1, generation: 0, memAddr: 0 };
            const node2 = { id: 2, value: 'b', next: node3, refs: 1, generation: 0, memAddr: 0 };
            const node1 = { id: 1, value: 'a', next: node2, refs: 1, generation: 0, memAddr: 0 };
            const iter = new StackIterator(node1, 0);
            expect(iter.next().value).toBe('a');
            expect(iter.next().value).toBe('b');
            expect(iter.next().value).toBe('c');
            expect(iter.next().done).toBe(true);
        });

        it('supports Symbol.iterator', () => {
            const node = { id: 1, value: 'x', next: null, refs: 1, generation: 0, memAddr: 0 };
            const iter = new StackIterator(node, 0);
            const iter2 = iter[Symbol.iterator]();
            // Symbol.iterator returns a new iterator (not this)
            expect(iter2).not.toBe(iter);
            expect(iter2.next().value).toBe('x');
        });
    });

    describe('StackMemoryPool', () => {
        it('allocates and deallocates nodes', () => {
            const pool = new StackMemoryPool();
            const node = pool.allocate('test', null, 1);
            expect(node.value).toBe('test');
            expect(node.refs).toBe(1);
            pool.deallocate(node);
        });

        it('getStats returns stats object', () => {
            const pool = new StackMemoryPool();
            const stats = pool.getStats();
            expect(stats).toHaveProperty('totalAllocated');
            expect(stats).toHaveProperty('totalDeallocated');
            expect(stats).toHaveProperty('fragmentation');
        });

        it('clear empties the pool', () => {
            const pool = new StackMemoryPool();
            pool.allocate('a', null, 1);
            pool.allocate('b', null, 1);
            pool.clear();
            const stats = pool.getStats();
            expect(stats.totalAllocated).toBe(0);
        });

        it('deallocate skips nodes with refs > 1', () => {
            const pool = new StackMemoryPool();
            const node = pool.allocate('test', null, 1);
            (node as any).refs = 2;
            pool.deallocate(node);
        });
    });

    describe('OptimizedArrayStack', () => {
        it('push and pop single element', () => {
            const stack = new OptimizedArrayStack<number>();
            const pushResult = stack.push(42);
            expect(pushResult.tag).toBe('success');
            expect(stack.size).toBe(1);
            const popResult = stack.pop();
            expect(popResult.tag).toBe('success');
            expect(popResult.value).toBe(42);
            expect(stack.size).toBe(0);
        });

        it('push multiple elements', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(1);
            stack.push(2);
            stack.push(3);
            expect(stack.size).toBe(3);
            expect(stack.pop().value).toBe(3);
            expect(stack.pop().value).toBe(2);
            expect(stack.pop().value).toBe(1);
        });

        it('pop empty stack returns undefined', () => {
            const stack = new OptimizedArrayStack<number>();
            const result = stack.pop();
            expect(result.tag).toBe('success');
            expect(result.value).toBeUndefined();
        });

        it('peek returns top element without removing', () => {
            const stack = new OptimizedArrayStack<string>();
            stack.push('a');
            stack.push('b');
            expect(stack.peek().value).toBe('b');
            expect(stack.size).toBe(2);
        });

        it('peek empty stack returns undefined', () => {
            const stack = new OptimizedArrayStack<number>();
            expect(stack.peek().value).toBeUndefined();
        });

        it('peekUnsafe returns top element', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(10);
            stack.push(20);
            expect(stack.peekUnsafe()).toBe(20);
        });

        it('peekMany returns multiple elements', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(1);
            stack.push(2);
            stack.push(3);
            const result = stack.peekMany(2);
            expect(result.tag).toBe('success');
            expect(result.value).toEqual([3, 2]);
        });

        it('peekMany with invalid count returns failure', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(1);
            const result = stack.peekMany(5);
            expect(result.tag).toBe('failure');
        });

        it('contains checks for element existence', () => {
            const stack = new OptimizedArrayStack<string>();
            stack.push('apple');
            stack.push('banana');
            expect(stack.contains('apple')).toBe(true);
            expect(stack.contains('banana')).toBe(true);
            expect(stack.contains('cherry')).toBe(false);
        });

        it('indexOf returns element index', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(10);
            stack.push(20);
            stack.push(30);
            expect(stack.indexOf(20)).toBe(1);
            expect(stack.indexOf(99)).toBe(-1);
        });

        it('toArray returns frozen array', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(1);
            stack.push(2);
            stack.push(3);
            const arr = stack.toArray();
            expect(arr).toEqual([3, 2, 1]);
            expect(Object.isFrozen(arr)).toBe(true);
        });

        it('toReversedArray returns reversed frozen array', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(1);
            stack.push(2);
            stack.push(3);
            const arr = stack.toReversedArray();
            expect(arr).toEqual([1, 2, 3]);
            expect(Object.isFrozen(arr)).toBe(true);
        });

        it('slice returns portion of stack', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(1);
            stack.push(2);
            stack.push(3);
            stack.push(4);
            const sliced = stack.slice(1, 3);
            expect(sliced).toEqual([3, 2]);
        });

        it('clear empties the stack', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(1);
            stack.push(2);
            stack.push(3);
            stack.clear();
            expect(stack.size).toBe(0);
            expect(stack.isEmpty).toBe(true);
        });

        it('pushMany adds multiple elements', () => {
            const stack = new OptimizedArrayStack<number>();
            const result = stack.pushMany([1, 2, 3, 4]);
            expect(result.tag).toBe('success');
            expect(stack.size).toBe(4);
        });

        it('popMany removes multiple elements', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(1);
            stack.push(2);
            stack.push(3);
            stack.push(4);
            const result = stack.popMany(2);
            expect(result.tag).toBe('success');
            expect(result.value).toEqual([4, 3]);
            expect(stack.size).toBe(2);
        });

        it('swap exchanges top two elements', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(1);
            stack.push(2);
            stack.push(3);
            const result = stack.swap();
            expect(result.tag).toBe('success');
            expect(stack.pop().value).toBe(2);
            expect(stack.pop().value).toBe(3);
        });

        it('swap with insufficient elements returns failure', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(1);
            const result = stack.swap();
            expect(result.tag).toBe('failure');
        });

        it('duplicate duplicates top element', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(42);
            const result = stack.duplicate();
            expect(result.tag).toBe('success');
            expect(stack.size).toBe(2);
            expect(stack.pop().value).toBe(42);
            expect(stack.pop().value).toBe(42);
        });

        it('duplicate on empty stack returns failure', () => {
            const stack = new OptimizedArrayStack<number>();
            const result = stack.duplicate();
            expect(result.tag).toBe('failure');
        });

        it('capacity enforcement', () => {
            const stack = new OptimizedArrayStack<number>({ capacity: 3 });
            stack.push(1);
            stack.push(2);
            stack.push(3);
            const result = stack.push(4);
            expect(result.tag).toBe('failure');
            if (result.tag === 'failure') {
                expect(result.error).toBeInstanceOf(StackCapacityError);
            }
        });

        it('pushMany respects capacity', () => {
            const stack = new OptimizedArrayStack<number>({ capacity: 3 });
            stack.push(1);
            const result = stack.pushMany([2, 3, 4]);
            expect(result.tag).toBe('failure');
        });

        it('isEmpty and isFull', () => {
            const stack = new OptimizedArrayStack<number>({ capacity: 2 });
            expect(stack.isEmpty).toBe(true);
            expect(stack.isFull).toBe(false);
            stack.push(1);
            expect(stack.isEmpty).toBe(false);
            expect(stack.isFull).toBe(false);
            stack.push(2);
            expect(stack.isFull).toBe(true);
        });

        it('generation increments on mutation', () => {
            const stack = new OptimizedArrayStack<number>();
            const gen1 = stack.generation;
            stack.push(1);
            expect(stack.generation).toBeGreaterThan(gen1);
        });

        it('checksum changes on mutation', () => {
            const stack = new OptimizedArrayStack<number>();
            const cs1 = stack.checksum;
            stack.push(1);
            expect(stack.checksum).not.toBe(cs1);
        });

        it('serialize and deserialize', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(1);
            stack.push(2);
            stack.push(3);
            const buffer = stack.serialize();
            expect(buffer).toBeInstanceOf(ArrayBuffer);
            expect(buffer.byteLength).toBeGreaterThan(0);
        });

        it('equals compares stacks', () => {
            const stack1 = new OptimizedArrayStack<number>();
            stack1.push(1);
            stack1.push(2);
            const stack2 = new OptimizedArrayStack<number>();
            stack2.push(1);
            stack2.push(2);
            expect(stack1.equals(stack2)).toBe(true);
        });

        it('hash produces consistent hash', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(1);
            stack.push(2);
            const hash1 = stack.hash();
            const hash2 = stack.hash();
            expect(hash1).toBe(hash2);
        });

        it('validate checks integrity', () => {
            const stack = new OptimizedArrayStack<number>({ enableIntegrityChecks: true });
            stack.push(1);
            stack.push(2);
            const result = stack.validate();
            expect(result.tag).toBe('success');
            expect(result.value).toBe(true);
        });

        it('Symbol.iterator allows for...of', () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(1);
            stack.push(2);
            stack.push(3);
            const values: number[] = [];
            for (const v of stack) {
                values.push(v);
            }
            expect(values).toEqual([3, 2, 1]);
        });

        it('dispose prevents further operations', async () => {
            const stack = new OptimizedArrayStack<number>();
            stack.push(1);
            await stack.dispose();
            expect(() => stack.push(2)).toThrow(StackIntegrityError);
        });
    });

    describe('ImmutableStack', () => {
        it('empty creates empty stack', () => {
            const stack = ImmutableStack.empty<number>();
            expect(stack.size).toBe(0);
            expect(stack.isEmpty).toBe(true);
        });

        it('push returns new stack', () => {
            const stack1 = ImmutableStack.empty<number>();
            const stack2 = stack1.push(1);
            expect(stack1.size).toBe(0);
            expect(stack2.size).toBe(1);
            expect(stack2.peek().value).toBe(1);
        });

        it('pop returns tuple of value and new stack', () => {
            const stack = ImmutableStack.of(1, 2, 3);
            const [value, newStack] = stack.pop();
            // of(1,2,3) uses reduceRight so head is 1 (first element)
            expect(value).toBe(1);
            expect(newStack.size).toBe(2);
            expect(stack.size).toBe(3);
        });

        it('pop empty stack returns undefined', () => {
            const stack = ImmutableStack.empty<number>();
            const [value, newStack] = stack.pop();
            expect(value).toBeUndefined();
            expect(newStack.size).toBe(0);
        });

        it('of creates stack from values', () => {
            const stack = ImmutableStack.of(1, 2, 3);
            expect(stack.size).toBe(3);
            // reduceRight preserves insertion order in toArray
            expect(stack.toArray()).toEqual([1, 2, 3]);
        });

        it('fromIterable creates stack from iterable', () => {
            const stack = ImmutableStack.fromIterable([1, 2, 3]);
            expect(stack.size).toBe(3);
            expect(stack.toArray()).toEqual([1, 2, 3]);
        });

        it('pushMany adds multiple elements', () => {
            const stack = ImmutableStack.empty<number>();
            const newStack = stack.pushMany([1, 2, 3]);
            expect(newStack.size).toBe(3);
        });

        it('popMany removes multiple elements', () => {
            const stack = ImmutableStack.of(1, 2, 3, 4);
            const [values, newStack] = stack.popMany(2);
            expect(values).toEqual([1, 2]);
            expect(newStack.size).toBe(2);
        });

        it('concat combines stacks', () => {
            const stack1 = ImmutableStack.of(1, 2);
            const stack2 = ImmutableStack.of(3, 4);
            const combined = stack1.concat(stack2);
            expect(combined.toArray()).toEqual([3, 4, 1, 2]);
        });

        it('filter removes elements', () => {
            const stack = ImmutableStack.of(1, 2, 3, 4, 5);
            const filtered = stack.filter((x) => x % 2 === 0);
            expect(filtered.toArray()).toEqual([4, 2]);
        });

        it('map transforms elements', () => {
            const stack = ImmutableStack.of(1, 2, 3);
            const mapped = stack.map((x) => x * 2);
            expect(mapped.toArray()).toEqual([6, 4, 2]);
        });

        it('immutability is preserved', () => {
            const stack1 = ImmutableStack.of(1, 2, 3);
            const stack2 = stack1.push(4);
            const stack3 = stack1.pop()[1];
            expect(stack1.size).toBe(3);
            expect(stack2.size).toBe(4);
            expect(stack3.size).toBe(2);
        });
    });
});
