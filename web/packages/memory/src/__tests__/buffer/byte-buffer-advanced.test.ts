import { describe, expect, it } from 'vitest';
import { ByteBuffer, ByteOrder, SeekOrigin, BufferOverflowError, BufferUnderflowError } from '../../buffering';

describe('ByteBuffer Advanced', () => {
    describe('static concat()', () => {
        it('concatenates empty array', () => {
            const result = ByteBuffer.concat([]);
            expect(result.capacity).toBe(0);
            expect(result.remaining).toBe(0);
        });

        it('concatenates single buffer', () => {
            const b = ByteBuffer.alloc(16);
            b.putAll([1, 2, 3, 4]);
            b.flip();
            const result = ByteBuffer.concat([b]);
            expect(result.remaining).toBe(4);
            expect(result.getUint8()).toBe(1);
        });

        it('concatenates multiple buffers', () => {
            const b1 = ByteBuffer.alloc(8);
            b1.putAll([1, 2]);
            b1.flip();
            const b2 = ByteBuffer.alloc(8);
            b2.putAll([3, 4]);
            b2.flip();
            const result = ByteBuffer.concat([b1, b2]);
            expect(result.remaining).toBe(4);
            expect(result.getUint8()).toBe(1);
            expect(result.getUint8()).toBe(2);
            expect(result.getUint8()).toBe(3);
            expect(result.getUint8()).toBe(4);
        });
    });

    describe('static copyOf()', () => {
        it('copies with default capacity', () => {
            const b = ByteBuffer.alloc(16);
            b.putAll([1, 2, 3]);
            b.flip();
            const copy = ByteBuffer.copyOf(b);
            expect(copy.remaining).toBe(3);
            expect(copy.getUint8()).toBe(1);
        });

        it('copies with custom capacity', () => {
            const b = ByteBuffer.alloc(16);
            b.putAll([1, 2, 3]);
            b.flip();
            const copy = ByteBuffer.copyOf(b, 32);
            expect(copy.capacity).toBeGreaterThanOrEqual(32);
            expect(copy.remaining).toBe(3);
        });

        it('throws when new capacity is too small', () => {
            const b = ByteBuffer.alloc(16);
            b.putAll([1, 2, 3, 4, 5]);
            b.flip();
            expect(() => ByteBuffer.copyOf(b, 2)).toThrow(RangeError);
        });
    });

    describe('growth', () => {
        it('preserves writes across buffer growth', () => {
            const b = ByteBuffer.alloc(8);
            const values: number[] = [];
            for (let i = 0; i < 100; i++) {
                values.push(i & 0xff);
                b.putUint8(i & 0xff);
            }
            expect(b.position).toBe(100);
            b.flip();
            for (let i = 0; i < 100; i++) {
                expect(b.getUint8()).toBe(values[i]);
            }
        });

        it('keeps an explicit limit below the grown capacity', () => {
            const b = ByteBuffer.alloc(8);
            b.setLimit(4);
            b.putAll([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
            expect(b.position).toBe(4);
        });
    });

    describe('putString() / getString()', () => {
        it('roundtrips utf8 strings', () => {
            const b = ByteBuffer.alloc(64);
            b.putString('hello world');
            b.rewind();
            expect(b.getString()).toBe('hello world');
        });

        it('honors utf16 encoding', () => {
            const b = ByteBuffer.alloc(64);
            b.putString('héllo wörld', 'utf16');
            b.rewind();
            expect(b.getString('utf16')).toBe('héllo wörld');
        });

        it('decodes utf16 from an odd byte offset', () => {
            const b = ByteBuffer.alloc(64);
            b.putUint8(0xaa);
            b.putString('héllo', 'utf16');
            b.rewind();
            expect(b.getUint8()).toBe(0xaa);
            expect(b.getString('utf16')).toBe('héllo');
        });
    });

    describe('putJson() / getJson()', () => {
        it('roundtrips simple object', () => {
            const b = ByteBuffer.alloc(256);
            const obj = { name: 'test', value: 42 };
            b.putJson(obj);
            b.rewind();
            const result = b.getJson<typeof obj>();
            expect(result).toEqual(obj);
        });

        it('roundtrips nested object', () => {
            const b = ByteBuffer.alloc(512);
            const obj = { a: { b: { c: [1, 2, 3] } } };
            b.putJson(obj);
            b.rewind();
            expect(b.getJson()).toEqual(obj);
        });

        it('roundtrips array', () => {
            const b = ByteBuffer.alloc(256);
            const arr = [1, 2, 3, 4, 5];
            b.putJson(arr);
            b.rewind();
            expect(b.getJson()).toEqual(arr);
        });
    });

    describe('hash() / crc32()', () => {
        it('hash is deterministic', () => {
            const b1 = ByteBuffer.alloc(16);
            b1.putAll([1, 2, 3, 4]);
            const b2 = ByteBuffer.alloc(16);
            b2.putAll([1, 2, 3, 4]);
            expect(b1.hash()).toBe(b2.hash());
        });

        it('different data produces different hashes', () => {
            const b1 = ByteBuffer.alloc(16);
            b1.putAll([1, 2, 3, 4]);
            b1.rewind();
            const b2 = ByteBuffer.alloc(16);
            b2.putAll([5, 6, 7, 8]);
            b2.rewind();
            expect(b1.hash()).not.toBe(b2.hash());
        });

        it('crc32 is deterministic', () => {
            const b1 = ByteBuffer.alloc(16);
            b1.putAll([1, 2, 3, 4]);
            const b2 = ByteBuffer.alloc(16);
            b2.putAll([1, 2, 3, 4]);
            expect(b1.crc32()).toBe(b2.crc32());
        });
    });

    describe('fillBytes()', () => {
        it('fills with zero (fast path)', () => {
            const b = ByteBuffer.alloc(16);
            b.fillBytes(0, 8);
            b.rewind();
            for (let i = 0; i < 8; i++) {
                expect(b.getUint8()).toBe(0);
            }
        });

        it('fills with non-zero value (slow path)', () => {
            const b = ByteBuffer.alloc(16);
            b.fillBytes(0xab, 8);
            b.rewind();
            for (let i = 0; i < 8; i++) {
                expect(b.getUint8()).toBe(0xab);
            }
        });

        it('advances position', () => {
            const b = ByteBuffer.alloc(16);
            b.fillBytes(0, 4);
            expect(b.position).toBe(4);
        });
    });

    describe('seek()', () => {
        it('seeks from Begin', () => {
            const b = ByteBuffer.alloc(16);
            b.seek(5, SeekOrigin.Begin);
            expect(b.position).toBe(5);
        });

        it('seeks from Current', () => {
            const b = ByteBuffer.alloc(16);
            b.seek(5, SeekOrigin.Begin);
            b.seek(3, SeekOrigin.Current);
            expect(b.position).toBe(8);
        });

        it('seeks from End', () => {
            const b = ByteBuffer.alloc(16);
            b.putAll([1, 2, 3, 4]);
            b.flip(); // limit=4, pos=0
            b.seek(-2, SeekOrigin.End);
            expect(b.position).toBe(2);
        });

        it('throws on out of bounds', () => {
            const b = ByteBuffer.alloc(16);
            expect(() => b.seek(-1, SeekOrigin.Begin)).toThrow(RangeError);
            expect(() => b.seek(100, SeekOrigin.Begin)).toThrow(RangeError);
        });
    });

    describe('slice()', () => {
        it('slices with bounds', () => {
            const b = ByteBuffer.alloc(16);
            b.putAll([1, 2, 3, 4, 5, 6]);
            const slice = b.slice(1, 4);
            expect(slice.remaining).toBe(3);
            expect(slice.getUint8()).toBe(2);
        });

        it('propagates readOnly', () => {
            const b = ByteBuffer.alloc(16);
            b.putAll([1, 2, 3]);
            const ro = b.asReadOnlyBuffer() as ByteBuffer;
            const slice = ro.slice(0, 2) as ByteBuffer;
            expect(slice.isReadOnly).toBe(true);
        });

        it('throws on invalid bounds', () => {
            const b = ByteBuffer.alloc(16);
            b.putAll([1, 2, 3]);
            expect(() => b.slice(-1, 2)).toThrow(RangeError);
            expect(() => b.slice(2, 1)).toThrow(RangeError);
        });
    });

    describe('put() / putBuffer()', () => {
        it('put from Uint8Array', () => {
            const b = ByteBuffer.alloc(16);
            b.put(new Uint8Array([1, 2, 3]));
            b.rewind();
            expect(b.getUint8()).toBe(1);
            expect(b.getUint8()).toBe(2);
            expect(b.getUint8()).toBe(3);
        });

        it('put from ArrayBuffer', () => {
            const b = ByteBuffer.alloc(16);
            const ab = new Uint8Array([4, 5, 6]).buffer;
            b.put(ab);
            b.rewind();
            expect(b.getUint8()).toBe(4);
        });

        it('put from number array', () => {
            const b = ByteBuffer.alloc(16);
            b.put([7, 8, 9]);
            b.rewind();
            expect(b.getUint8()).toBe(7);
        });

        it('putBuffer from ByteBuffer', () => {
            const src = ByteBuffer.alloc(8);
            src.putAll([10, 11, 12]);
            src.flip();
            const dst = ByteBuffer.alloc(16);
            dst.putBuffer(src);
            dst.rewind();
            expect(dst.getUint8()).toBe(10);
        });
    });

    describe('putAll()', () => {
        it('puts number array', () => {
            const b = ByteBuffer.alloc(16);
            b.putAll([1, 2, 3, 4]);
            b.seek(0);
            const result: number[] = [];
            for (let i = 0; i < 4; i++) result.push(b.getUint8());
            expect(result).toEqual([1, 2, 3, 4]);
        });

        it('puts TypedArray', () => {
            const b = ByteBuffer.alloc(16);
            b.putAll(new Uint8Array([5, 6, 7]));
            b.seek(0);
            const result: number[] = [];
            for (let i = 0; i < 3; i++) result.push(b.getUint8());
            expect(result).toEqual([5, 6, 7]);
        });
    });

    describe('setLimit()', () => {
        it('sets valid limit', () => {
            const b = ByteBuffer.alloc(16);
            b.setLimit(8);
            expect(b.limit).toBe(8);
        });

        it('adjusts position if beyond new limit', () => {
            const b = ByteBuffer.alloc(16);
            b.seek(10);
            b.setLimit(5);
            expect(b.position).toBe(5);
        });

        it('invalidates mark if beyond new limit', () => {
            const b = ByteBuffer.alloc(16);
            b.seek(5);
            b.mark();
            b.setLimit(3);
            expect(() => b.reset()).toThrow();
        });

        it('throws on out of bounds', () => {
            const b = ByteBuffer.alloc(16);
            expect(() => b.setLimit(-1)).toThrow(RangeError);
            expect(() => b.setLimit(100)).toThrow(RangeError);
        });
    });

    describe('hasAvailable() / remaining / hasRemaining', () => {
        it('hasAvailable checks space', () => {
            const b = ByteBuffer.alloc(16);
            // Fill most of the buffer
            b.putAll(new Uint8Array(28));
            expect(b.hasAvailable(4)).toBe(true);
            expect(b.hasAvailable(10)).toBe(false);
        });

        it('remaining calculates correctly', () => {
            const b = ByteBuffer.alloc(16);
            b.putAll([1, 2, 3]);
            b.flip();
            expect(b.remaining).toBe(3);
        });

        it('hasRemaining returns boolean', () => {
            const b = ByteBuffer.alloc(16);
            expect(b.hasRemaining).toBe(true);
            b.setLimit(0);
            expect(b.hasRemaining).toBe(false);
        });
    });

    describe('release()', () => {
        it('releases pooled buffer', () => {
            const b = ByteBuffer.alloc(16, ByteOrder.Big, true);
            expect(b.isPooled).toBe(true);
            b.release();
            expect(() => b.position).toThrow();
        });

        it('non-pooled buffer release is no-op', () => {
            const b = ByteBuffer.alloc(16, ByteOrder.Big, false);
            expect(b.isPooled).toBe(false);
            b.release();
            expect(b.position).toBe(0);
        });

        it('double release is idempotent', () => {
            const b = ByteBuffer.alloc(16, ByteOrder.Big, true);
            b.release();
            b.release();
            expect(() => b.position).toThrow();
        });
    });

    describe('asTypedView()', () => {
        it('returns cached view', () => {
            const b = ByteBuffer.alloc(16);
            const view1 = b.asTypedView('uint8');
            const view2 = b.asTypedView('uint8');
            expect(view1).toBe(view2);
        });

        it('returns different views for different types', () => {
            const b = ByteBuffer.alloc(16);
            const u8 = b.asTypedView('uint8');
            const u16 = b.asTypedView('uint16');
            expect(u8).not.toBe(u16);
        });
    });

    describe('auto-expansion', () => {
        it('expands when capacity exceeded', () => {
            const b = ByteBuffer.alloc(4);
            // Write more than the power-of-2 rounded capacity to force expansion
            const data = new Uint8Array(64);
            b.putAll(data);
            expect(b.capacity).toBeGreaterThanOrEqual(64);
        });

        it('preserves data after expansion', () => {
            const b = ByteBuffer.alloc(4);
            b.putAll([1, 2, 3, 4, 5, 6]);
            b.rewind();
            expect(b.getUint8()).toBe(1);
            expect(b.getUint8()).toBe(2);
            expect(b.getUint8()).toBe(3);
            expect(b.getUint8()).toBe(4);
            expect(b.getUint8()).toBe(5);
            expect(b.getUint8()).toBe(6);
        });
    });

    describe('state transitions', () => {
        it('clear() resets position and limit', () => {
            const b = ByteBuffer.alloc(16);
            b.putAll([1, 2, 3]);
            b.clear();
            expect(b.position).toBe(0);
            expect(b.limit).toBe(b.capacity);
        });

        it('flip() sets limit to position, position to 0', () => {
            const b = ByteBuffer.alloc(16);
            b.putAll([1, 2, 3]);
            b.flip();
            expect(b.position).toBe(0);
            expect(b.limit).toBe(3);
        });

        it('rewind() sets position to 0', () => {
            const b = ByteBuffer.alloc(16);
            b.putAll([1, 2, 3]);
            b.rewind();
            expect(b.position).toBe(0);
        });
    });

    describe('directBuffer()', () => {
        it('creates non-pooled buffer', () => {
            const b = ByteBuffer.directBuffer(16);
            expect(b.isPooled).toBe(false);
            expect(b.capacity).toBeGreaterThanOrEqual(16);
        });
    });
});
