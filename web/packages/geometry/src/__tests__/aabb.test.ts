import { describe, it, expect, beforeEach } from 'vitest';
import { Vec2, Vec3, Mat4 } from '@axrone/numeric';
import { AABB2D, AABB3D, AABB, AABBError } from '@axrone/geometry';

describe('AABB Core Functionality', () => {
    describe('AABB2D Basic Operations', () => {
        describe('Constructor and Properties', () => {
            it('should create an AABB2D with default values', () => {
                const aabb = new AABB2D();
                expect(aabb.min.x).toBe(0);
                expect(aabb.min.y).toBe(0);
                expect(aabb.max.x).toBe(0);
                expect(aabb.max.y).toBe(0);
                expect(aabb.center.x).toBe(0);
                expect(aabb.center.y).toBe(0);

                expect(aabb.isEmpty).toBe(false);
            });

            it('should create an AABB2D with specified min and max', () => {
                const min = Vec2.create(1, 2);
                const max = Vec2.create(3, 4);
                const aabb = new AABB2D(min, max);

                expect(aabb.min.x).toBe(1);
                expect(aabb.min.y).toBe(2);
                expect(aabb.max.x).toBe(3);
                expect(aabb.max.y).toBe(4);
                expect(aabb.center.x).toBe(2);
                expect(aabb.center.y).toBe(3);
                expect(aabb.extents.x).toBe(1);
                expect(aabb.extents.y).toBe(1);
                expect(aabb.isEmpty).toBe(false);
            });

            it('should report as empty when min > max', () => {
                const min = Vec2.create(3, 4);
                const max = Vec2.create(1, 2);
                const aabb = new AABB2D(min, max);
                expect(aabb.isEmpty).toBe(true);
            });

            it('should calculate size correctly', () => {
                const aabb = new AABB2D(Vec2.create(1, 2), Vec2.create(4, 6));
                const size = aabb.size;
                expect(size.x).toBe(3);
                expect(size.y).toBe(4);
            });

            it('should calculate volume correctly', () => {
                const aabb = new AABB2D(Vec2.create(0, 0), Vec2.create(3, 4));
                expect(aabb.volume).toBe(12);
            });

            it('should calculate surface area correctly', () => {
                const aabb = new AABB2D(Vec2.create(0, 0), Vec2.create(3, 4));
                expect(aabb.surfaceArea).toBe(14);
            });

            it('should have correct dimensions', () => {
                const aabb = new AABB2D();
                expect(aabb.dimensions).toBe(2);
            });
        });

        describe('Point and AABB Operations', () => {
            it('should correctly check point containment', () => {
                const aabb = new AABB2D(Vec2.create(0, 0), Vec2.create(4, 4));

                expect(aabb.containsPoint(Vec2.create(2, 2))).toBe(true);
                expect(aabb.containsPoint(Vec2.create(0, 0))).toBe(true);
                expect(aabb.containsPoint(Vec2.create(4, 4))).toBe(true);
                expect(aabb.containsPoint(Vec2.create(-1, 2))).toBe(false);
                expect(aabb.containsPoint(Vec2.create(5, 2))).toBe(false);
            });

            it('should correctly check AABB containment', () => {
                const aabb1 = new AABB2D(Vec2.create(0, 0), Vec2.create(4, 4));
                const aabb2 = new AABB2D(Vec2.create(1, 1), Vec2.create(3, 3));
                const aabb3 = new AABB2D(Vec2.create(2, 2), Vec2.create(6, 6));

                expect(aabb1.containsAABB(aabb2)).toBe(true);
                expect(aabb1.containsAABB(aabb3)).toBe(false);
                expect(aabb2.containsAABB(aabb1)).toBe(false);
            });

            it('should correctly check AABB intersections', () => {
                const aabb1 = new AABB2D(Vec2.create(0, 0), Vec2.create(4, 4));
                const aabb2 = new AABB2D(Vec2.create(2, 2), Vec2.create(6, 6));
                const aabb3 = new AABB2D(Vec2.create(5, 5), Vec2.create(7, 7));

                expect(aabb1.intersectsAABB(aabb2)).toBe(true);
                expect(aabb1.intersectsAABB(aabb3)).toBe(false);
            });
        });

        describe('Copy and Clone Operations', () => {
            it('should clone correctly', () => {
                const original = new AABB2D(Vec2.create(1, 2), Vec2.create(3, 4));
                const clone = original.clone();

                expect(clone).not.toBe(original);
                expect(clone.min.x).toBe(original.min.x);
                expect(clone.min.y).toBe(original.min.y);
                expect(clone.max.x).toBe(original.max.x);
                expect(clone.max.y).toBe(original.max.y);
            });

            it('should copy correctly', () => {
                const source = new AABB2D(Vec2.create(1, 2), Vec2.create(3, 4));
                const target = new AABB2D();

                target.copy(source);

                expect(target.min.x).toBe(source.min.x);
                expect(target.min.y).toBe(source.min.y);
                expect(target.max.x).toBe(source.max.x);
                expect(target.max.y).toBe(source.max.y);
            });
        });
    });

    describe('AABB3D Basic Operations', () => {
        describe('Constructor and Properties', () => {
            it('should create an AABB3D with default values', () => {
                const aabb = new AABB3D();
                expect(aabb.min.x).toBe(0);
                expect(aabb.min.y).toBe(0);
                expect(aabb.min.z).toBe(0);
                expect(aabb.max.x).toBe(0);
                expect(aabb.max.y).toBe(0);
                expect(aabb.max.z).toBe(0);
                expect(aabb.isEmpty).toBe(false);
            });

            it('should create an AABB3D with specified min and max', () => {
                const min = Vec3.create(1, 2, 3);
                const max = Vec3.create(4, 5, 6);
                const aabb = new AABB3D(min, max);

                expect(aabb.min.x).toBe(1);
                expect(aabb.min.y).toBe(2);
                expect(aabb.min.z).toBe(3);
                expect(aabb.max.x).toBe(4);
                expect(aabb.max.y).toBe(5);
                expect(aabb.max.z).toBe(6);
                expect(aabb.center.x).toBe(2.5);
                expect(aabb.center.y).toBe(3.5);
                expect(aabb.center.z).toBe(4.5);
                expect(aabb.isEmpty).toBe(false);
            });

            it('should calculate volume correctly', () => {
                const aabb = new AABB3D(Vec3.create(0, 0, 0), Vec3.create(2, 3, 4));
                expect(aabb.volume).toBe(24);
            });

            it('should have correct dimensions', () => {
                const aabb = new AABB3D();
                expect(aabb.dimensions).toBe(3);
            });
        });
    });

    describe('Factory Functions', () => {
        describe('AABB.create2D and AABB.create3D', () => {
            it('should create 2D AABB from min/max', () => {
                const min = Vec2.create(1, 2);
                const max = Vec2.create(3, 4);
                const aabb = AABB.create2D(min, max);

                expect(aabb.min.x).toBe(1);
                expect(aabb.min.y).toBe(2);
                expect(aabb.max.x).toBe(3);
                expect(aabb.max.y).toBe(4);
            });

            it('should create 3D AABB from min/max', () => {
                const min = Vec3.create(1, 2, 3);
                const max = Vec3.create(4, 5, 6);
                const aabb = AABB.create3D(min, max);

                expect(aabb.min.x).toBe(1);
                expect(aabb.min.y).toBe(2);
                expect((aabb.min as any).z).toBe(3);
                expect(aabb.max.x).toBe(4);
                expect(aabb.max.y).toBe(5);
                expect((aabb.max as any).z).toBe(6);
            });

            it('should handle swapped min/max correctly', () => {
                const min = Vec2.create(3, 4);
                const max = Vec2.create(1, 2);
                const aabb = AABB.create2D(min, max);

                expect(aabb.min.x).toBe(3);
                expect(aabb.max.x).toBe(1);
                expect(aabb.isEmpty).toBe(true);
            });
        });

        describe('AABB.fromCenterAndExtents', () => {
            it('should create AABB from center and extents', () => {
                const center = Vec2.create(2, 3);
                const extents = Vec2.create(2, 3); // half size
                const aabb = AABB.fromCenterAndExtents2D(center, extents);

                expect(aabb.min.x).toBe(0);
                expect(aabb.min.y).toBe(0);
                expect(aabb.max.x).toBe(4);
                expect(aabb.max.y).toBe(6);
                expect(aabb.center.x).toBe(2);
                expect(aabb.center.y).toBe(3);
            });
        });

        describe('AABB.fromPoints', () => {
            it('should create AABB from array of 2D points', () => {
                const points = [
                    Vec2.create(1, 1),
                    Vec2.create(3, 2),
                    Vec2.create(0, 4),
                    Vec2.create(2, 0),
                ];
                const aabb = AABB.fromPoints2D(points);

                expect(aabb.min.x).toBe(0);
                expect(aabb.min.y).toBe(0);
                expect(aabb.max.x).toBe(3);
                expect(aabb.max.y).toBe(4);
            });

            it('should create AABB from array of 3D points', () => {
                const points = [
                    Vec3.create(1, 1, 1),
                    Vec3.create(3, 2, 0),
                    Vec3.create(0, 4, 3),
                    Vec3.create(2, 0, 2),
                ];
                const aabb = AABB.fromPoints3D(points);

                expect(aabb.min.x).toBe(0);
                expect(aabb.min.y).toBe(0);
                expect(aabb.min.z).toBe(0);
                expect(aabb.max.x).toBe(3);
                expect(aabb.max.y).toBe(4);
                expect(aabb.max.z).toBe(3);
            });
        });
    });

    describe('Error Handling', () => {
        it('should handle AABBError correctly', () => {
            expect(() => {
                throw new AABBError('Test error');
            }).toThrow('Test error');

            expect(() => {
                throw new AABBError('Test error');
            }).toThrow(AABBError);
        });

        it('should throw AABBError from empty points array', () => {
            expect(() => AABB2D.fromPoints([])).toThrow(AABBError);
            expect(() => AABB3D.fromPoints([])).toThrow(AABBError);
            expect(() => AABB.fromPoints([])).toThrow(AABBError);
        });

        it('AABBError has correct name and prototype', () => {
            const err = new AABBError('test');
            expect(err.name).toBe('AABBError');
            expect(err).toBeInstanceOf(Error);
            expect(err).toBeInstanceOf(AABBError);
        });
    });

    describe('AABB2D Advanced Operations', () => {
        describe('EMPTY static instance', () => {
            it('should be frozen and empty', () => {
                expect(Object.isFrozen(AABB2D.EMPTY)).toBe(true);
                expect(AABB2D.EMPTY.isEmpty).toBe(true);
                expect(AABB2D.EMPTY.volume).toBe(0);
                expect(AABB2D.EMPTY.surfaceArea).toBe(0);
            });
        });

        describe('Static factories', () => {
            it('AABB2D.from copies another AABB', () => {
                const source = new AABB2D(Vec2.create(1, 2), Vec2.create(5, 6));
                const copy = AABB2D.from(source);
                expect(copy.min.x).toBe(1);
                expect(copy.min.y).toBe(2);
                expect(copy.max.x).toBe(5);
                expect(copy.max.y).toBe(6);
                expect(copy).not.toBe(source);
            });

            it('AABB2D.fromCenterAndExtents creates correct AABB', () => {
                const aabb = AABB2D.fromCenterAndExtents(
                    Vec2.create(5, 5),
                    Vec2.create(2, 3)
                );
                expect(aabb.min.x).toBe(3);
                expect(aabb.min.y).toBe(2);
                expect(aabb.max.x).toBe(7);
                expect(aabb.max.y).toBe(8);
            });
        });

        describe('Empty AABB properties', () => {
            it('volume is 0 for empty AABB', () => {
                const aabb = new AABB2D(Vec2.create(5, 5), Vec2.create(1, 1));
                expect(aabb.isEmpty).toBe(true);
                expect(aabb.volume).toBe(0);
            });

            it('surfaceArea is 0 for empty AABB', () => {
                const aabb = new AABB2D(Vec2.create(5, 5), Vec2.create(1, 1));
                expect(aabb.isEmpty).toBe(true);
                expect(aabb.surfaceArea).toBe(0);
            });
        });

        describe('getIntersection', () => {
            it('returns overlapping region', () => {
                const a = new AABB2D(Vec2.create(0, 0), Vec2.create(4, 4));
                const b = new AABB2D(Vec2.create(2, 2), Vec2.create(6, 6));
                const result = a.getIntersection(b);
                expect(result).not.toBeNull();
                expect(result!.min.x).toBe(2);
                expect(result!.min.y).toBe(2);
                expect(result!.max.x).toBe(4);
                expect(result!.max.y).toBe(4);
            });

            it('returns null for non-overlapping AABBs', () => {
                const a = new AABB2D(Vec2.create(0, 0), Vec2.create(1, 1));
                const b = new AABB2D(Vec2.create(5, 5), Vec2.create(6, 6));
                expect(a.getIntersection(b)).toBeNull();
            });

            it('writes into out parameter', () => {
                const a = new AABB2D(Vec2.create(0, 0), Vec2.create(4, 4));
                const b = new AABB2D(Vec2.create(2, 2), Vec2.create(6, 6));
                const out = new AABB2D();
                const result = a.getIntersection(b, out);
                expect(result).toBe(out);
                expect(out.min.x).toBe(2);
                expect(out.min.y).toBe(2);
            });
        });

        describe('getUnion', () => {
            it('merges two AABBs', () => {
                const a = new AABB2D(Vec2.create(0, 0), Vec2.create(2, 2));
                const b = new AABB2D(Vec2.create(3, 3), Vec2.create(5, 5));
                const result = a.getUnion(b);
                expect(result.min.x).toBe(0);
                expect(result.min.y).toBe(0);
                expect(result.max.x).toBe(5);
                expect(result.max.y).toBe(5);
            });

            it('writes into out parameter', () => {
                const a = new AABB2D(Vec2.create(0, 0), Vec2.create(2, 2));
                const b = new AABB2D(Vec2.create(3, 3), Vec2.create(5, 5));
                const out = new AABB2D();
                const result = a.getUnion(b, out);
                expect(result).toBe(out);
                expect(out.max.x).toBe(5);
            });
        });

        describe('expand', () => {
            it('expands by scalar amount', () => {
                const aabb = new AABB2D(Vec2.create(2, 2), Vec2.create(4, 4));
                const expanded = aabb.expand(1);
                expect(expanded.min.x).toBe(1);
                expect(expanded.min.y).toBe(1);
                expect(expanded.max.x).toBe(5);
                expect(expanded.max.y).toBe(5);
            });

            it('expands by vector amount', () => {
                const aabb = new AABB2D(Vec2.create(2, 2), Vec2.create(4, 4));
                const expanded = aabb.expand(Vec2.create(1, 3));
                expect(expanded.min.x).toBe(1);
                expect(expanded.min.y).toBe(-1);
                expect(expanded.max.x).toBe(5);
                expect(expanded.max.y).toBe(7);
            });

            it('returns new instance when no out given', () => {
                const aabb = new AABB2D(Vec2.create(0, 0), Vec2.create(1, 1));
                const expanded = aabb.expand(1);
                expect(expanded).not.toBe(aabb);
            });
        });

        describe('transform', () => {
            it('identity matrix preserves AABB', () => {
                const aabb = new AABB2D(Vec2.create(1, 2), Vec2.create(3, 4));
                const identity = new Mat4();
                const result = aabb.transform(identity);
                expect(result.min.x).toBeCloseTo(1);
                expect(result.min.y).toBeCloseTo(2);
                expect(result.max.x).toBeCloseTo(3);
                expect(result.max.y).toBeCloseTo(4);
            });

            it('translation shifts AABB', () => {
                const aabb = new AABB2D(Vec2.create(0, 0), Vec2.create(1, 1));
                const mat = Mat4.translate(Vec3.create(5, 10, 0));
                const result = aabb.transform(mat);
                expect(result.min.x).toBeCloseTo(5);
                expect(result.min.y).toBeCloseTo(10);
                expect(result.max.x).toBeCloseTo(6);
                expect(result.max.y).toBeCloseTo(11);
            });
        });

        describe('closestPoint', () => {
            it('clamps outside point to nearest boundary', () => {
                const aabb = new AABB2D(Vec2.create(0, 0), Vec2.create(4, 4));
                const out = Vec2.create(0, 0);
                aabb.closestPoint(Vec2.create(10, 10), out);
                expect(out.x).toBe(4);
                expect(out.y).toBe(4);
            });

            it('returns same point when inside', () => {
                const aabb = new AABB2D(Vec2.create(0, 0), Vec2.create(4, 4));
                const out = Vec2.create(0, 0);
                aabb.closestPoint(Vec2.create(2, 3), out);
                expect(out.x).toBe(2);
                expect(out.y).toBe(3);
            });

            it('clamps negative outside point', () => {
                const aabb = new AABB2D(Vec2.create(0, 0), Vec2.create(4, 4));
                const out = Vec2.create(0, 0);
                aabb.closestPoint(Vec2.create(-5, -3), out);
                expect(out.x).toBe(0);
                expect(out.y).toBe(0);
            });
        });

        describe('distanceToPoint / squaredDistanceToPoint', () => {
            it('returns 0 for point inside', () => {
                const aabb = new AABB2D(Vec2.create(0, 0), Vec2.create(4, 4));
                expect(aabb.squaredDistanceToPoint(Vec2.create(2, 2))).toBe(0);
                expect(aabb.distanceToPoint(Vec2.create(2, 2))).toBe(0);
            });

            it('computes correct distance for outside point', () => {
                const aabb = new AABB2D(Vec2.create(0, 0), Vec2.create(4, 4));
                expect(aabb.squaredDistanceToPoint(Vec2.create(7, 4))).toBe(9);
                expect(aabb.distanceToPoint(Vec2.create(7, 4))).toBeCloseTo(3);
            });

            it('computes correct distance for corner', () => {
                const aabb = new AABB2D(Vec2.create(0, 0), Vec2.create(1, 1));
                expect(aabb.squaredDistanceToPoint(Vec2.create(4, 5))).toBe(25);
                expect(aabb.distanceToPoint(Vec2.create(4, 5))).toBeCloseTo(5);
            });
        });

        describe('equals', () => {
            it('returns true for equal AABBs', () => {
                const a = new AABB2D(Vec2.create(1, 2), Vec2.create(3, 4));
                const b = new AABB2D(Vec2.create(1, 2), Vec2.create(3, 4));
                expect(a.equals(b)).toBe(true);
            });

            it('returns false for different AABBs', () => {
                const a = new AABB2D(Vec2.create(1, 2), Vec2.create(3, 4));
                const b = new AABB2D(Vec2.create(1, 2), Vec2.create(3, 5));
                expect(a.equals(b)).toBe(false);
            });

            it('returns false for non-AABB2D objects', () => {
                const a = new AABB2D(Vec2.create(1, 2), Vec2.create(3, 4));
                expect(a.equals('not an aabb' as unknown)).toBe(false);
                expect(a.equals(null)).toBe(false);
            });
        });

        describe('getHashCode', () => {
            it('returns deterministic hash', () => {
                const a = new AABB2D(Vec2.create(1, 2), Vec2.create(3, 4));
                const b = new AABB2D(Vec2.create(1, 2), Vec2.create(3, 4));
                expect(a.getHashCode()).toBe(b.getHashCode());
            });

            it('returns different hash for different AABBs', () => {
                const a = new AABB2D(Vec2.create(1, 2), Vec2.create(3, 4));
                const b = new AABB2D(Vec2.create(5, 6), Vec2.create(7, 8));
                expect(a.getHashCode()).not.toBe(b.getHashCode());
            });

            it('returns unsigned 32-bit integer', () => {
                const aabb = new AABB2D(Vec2.create(1, 2), Vec2.create(3, 4));
                const hash = aabb.getHashCode();
                expect(hash).toBeGreaterThanOrEqual(0);
                expect(hash).toBeLessThanOrEqual(0xffffffff);
                expect(Number.isInteger(hash)).toBe(true);
            });
        });

        describe('toString', () => {
            it('returns formatted string', () => {
                const aabb = new AABB2D(Vec2.create(1, 2), Vec2.create(3, 4));
                const str = aabb.toString();
                expect(str).toContain('AABB2D');
                expect(str).toContain('1.000');
                expect(str).toContain('2.000');
                expect(str).toContain('3.000');
                expect(str).toContain('4.000');
            });
        });

        describe('clear', () => {
            it('makes AABB empty', () => {
                const aabb = new AABB2D(Vec2.create(1, 2), Vec2.create(3, 4));
                expect(aabb.isEmpty).toBe(false);
                aabb.clear();
                expect(aabb.isEmpty).toBe(true);
            });
        });
    });

    describe('AABB3D Advanced Operations', () => {
        describe('EMPTY static instance', () => {
            it('should be frozen and empty', () => {
                expect(Object.isFrozen(AABB3D.EMPTY)).toBe(true);
                expect(AABB3D.EMPTY.isEmpty).toBe(true);
                expect(AABB3D.EMPTY.volume).toBe(0);
                expect(AABB3D.EMPTY.surfaceArea).toBe(0);
            });
        });

        describe('Static factories', () => {
            it('AABB3D.from copies another AABB', () => {
                const source = new AABB3D(Vec3.create(1, 2, 3), Vec3.create(4, 5, 6));
                const copy = AABB3D.from(source);
                expect(copy.min.x).toBe(1);
                expect(copy.min.z).toBe(3);
                expect(copy.max.x).toBe(4);
                expect(copy).not.toBe(source);
            });

            it('AABB3D.fromCenterAndExtents creates correct AABB', () => {
                const aabb = AABB3D.fromCenterAndExtents(
                    Vec3.create(5, 5, 5),
                    Vec3.create(2, 3, 4)
                );
                expect(aabb.min.x).toBe(3);
                expect(aabb.min.y).toBe(2);
                expect(aabb.min.z).toBe(1);
                expect(aabb.max.x).toBe(7);
                expect(aabb.max.y).toBe(8);
                expect(aabb.max.z).toBe(9);
            });
        });

        describe('surfaceArea', () => {
            it('calculates surface area correctly', () => {
                const aabb = new AABB3D(Vec3.create(0, 0, 0), Vec3.create(2, 3, 4));
                expect(aabb.surfaceArea).toBe(2 * (2 * 3 + 3 * 4 + 4 * 2));
            });

            it('returns 0 for empty AABB', () => {
                const aabb = new AABB3D(Vec3.create(5, 5, 5), Vec3.create(1, 1, 1));
                expect(aabb.isEmpty).toBe(true);
                expect(aabb.surfaceArea).toBe(0);
            });
        });

        describe('Point and AABB Operations', () => {
            it('containsPoint checks all 3 axes', () => {
                const aabb = new AABB3D(Vec3.create(0, 0, 0), Vec3.create(4, 4, 4));
                expect(aabb.containsPoint(Vec3.create(2, 2, 2))).toBe(true);
                expect(aabb.containsPoint(Vec3.create(0, 0, 0))).toBe(true);
                expect(aabb.containsPoint(Vec3.create(5, 2, 2))).toBe(false);
                expect(aabb.containsPoint(Vec3.create(2, 5, 2))).toBe(false);
                expect(aabb.containsPoint(Vec3.create(2, 2, 5))).toBe(false);
            });

            it('containsAABB checks all 3 axes', () => {
                const outer = new AABB3D(Vec3.create(0, 0, 0), Vec3.create(10, 10, 10));
                const inner = new AABB3D(Vec3.create(1, 1, 1), Vec3.create(9, 9, 9));
                const partial = new AABB3D(Vec3.create(5, 5, 5), Vec3.create(15, 15, 15));
                expect(outer.containsAABB(inner)).toBe(true);
                expect(outer.containsAABB(partial)).toBe(false);
            });

            it('intersectsAABB checks all 3 axes', () => {
                const a = new AABB3D(Vec3.create(0, 0, 0), Vec3.create(4, 4, 4));
                const b = new AABB3D(Vec3.create(2, 2, 2), Vec3.create(6, 6, 6));
                const c = new AABB3D(Vec3.create(5, 5, 5), Vec3.create(8, 8, 8));
                expect(a.intersectsAABB(b)).toBe(true);
                expect(a.intersectsAABB(c)).toBe(false);
            });
        });

        describe('getIntersection', () => {
            it('returns overlapping region', () => {
                const a = new AABB3D(Vec3.create(0, 0, 0), Vec3.create(4, 4, 4));
                const b = new AABB3D(Vec3.create(2, 2, 2), Vec3.create(6, 6, 6));
                const result = a.getIntersection(b);
                expect(result).not.toBeNull();
                expect(result!.min.x).toBe(2);
                expect(result!.min.z).toBe(2);
                expect(result!.max.x).toBe(4);
                expect(result!.max.z).toBe(4);
            });

            it('returns null for non-overlapping', () => {
                const a = new AABB3D(Vec3.create(0, 0, 0), Vec3.create(1, 1, 1));
                const b = new AABB3D(Vec3.create(5, 5, 5), Vec3.create(6, 6, 6));
                expect(a.getIntersection(b)).toBeNull();
            });
        });

        describe('getUnion', () => {
            it('merges two 3D AABBs', () => {
                const a = new AABB3D(Vec3.create(0, 0, 0), Vec3.create(2, 2, 2));
                const b = new AABB3D(Vec3.create(3, 3, 3), Vec3.create(5, 5, 5));
                const result = a.getUnion(b);
                expect(result.min.x).toBe(0);
                expect(result.max.x).toBe(5);
                expect(result.max.z).toBe(5);
            });
        });

        describe('expand', () => {
            it('expands by scalar', () => {
                const aabb = new AABB3D(Vec3.create(2, 2, 2), Vec3.create(4, 4, 4));
                const expanded = aabb.expand(1);
                expect(expanded.min.x).toBe(1);
                expect(expanded.min.z).toBe(1);
                expect(expanded.max.x).toBe(5);
                expect(expanded.max.z).toBe(5);
            });

            it('expands by vector', () => {
                const aabb = new AABB3D(Vec3.create(2, 2, 2), Vec3.create(4, 4, 4));
                const expanded = aabb.expand(Vec3.create(1, 2, 3));
                expect(expanded.min.x).toBe(1);
                expect(expanded.min.y).toBe(0);
                expect(expanded.min.z).toBe(-1);
                expect(expanded.max.x).toBe(5);
                expect(expanded.max.y).toBe(6);
                expect(expanded.max.z).toBe(7);
            });
        });

        describe('transform', () => {
            it('identity matrix preserves AABB', () => {
                const aabb = new AABB3D(Vec3.create(1, 2, 3), Vec3.create(4, 5, 6));
                const identity = new Mat4();
                const result = aabb.transform(identity);
                expect(result.min.x).toBeCloseTo(1);
                expect(result.min.z).toBeCloseTo(3);
                expect(result.max.x).toBeCloseTo(4);
                expect(result.max.z).toBeCloseTo(6);
            });

            it('translation shifts AABB', () => {
                const aabb = new AABB3D(Vec3.create(0, 0, 0), Vec3.create(1, 1, 1));
                const mat = Mat4.translate(Vec3.create(10, 20, 30));
                const result = aabb.transform(mat);
                expect(result.min.x).toBeCloseTo(10);
                expect(result.min.y).toBeCloseTo(20);
                expect(result.min.z).toBeCloseTo(30);
                expect(result.max.x).toBeCloseTo(11);
                expect(result.max.y).toBeCloseTo(21);
                expect(result.max.z).toBeCloseTo(31);
            });
        });

        describe('closestPoint', () => {
            it('clamps outside point to nearest boundary', () => {
                const aabb = new AABB3D(Vec3.create(0, 0, 0), Vec3.create(4, 4, 4));
                const out = Vec3.create(0, 0, 0);
                aabb.closestPoint(Vec3.create(10, 10, 10), out);
                expect(out.x).toBe(4);
                expect(out.y).toBe(4);
                expect(out.z).toBe(4);
            });

            it('returns same point when inside', () => {
                const aabb = new AABB3D(Vec3.create(0, 0, 0), Vec3.create(4, 4, 4));
                const out = Vec3.create(0, 0, 0);
                aabb.closestPoint(Vec3.create(2, 3, 1), out);
                expect(out.x).toBe(2);
                expect(out.y).toBe(3);
                expect(out.z).toBe(1);
            });
        });

        describe('distanceToPoint / squaredDistanceToPoint', () => {
            it('returns 0 for point inside', () => {
                const aabb = new AABB3D(Vec3.create(0, 0, 0), Vec3.create(4, 4, 4));
                expect(aabb.squaredDistanceToPoint(Vec3.create(2, 2, 2))).toBe(0);
                expect(aabb.distanceToPoint(Vec3.create(2, 2, 2))).toBe(0);
            });

            it('computes correct distance along single axis', () => {
                const aabb = new AABB3D(Vec3.create(0, 0, 0), Vec3.create(1, 1, 1));
                expect(aabb.squaredDistanceToPoint(Vec3.create(4, 0.5, 0.5))).toBe(9);
                expect(aabb.distanceToPoint(Vec3.create(4, 0.5, 0.5))).toBeCloseTo(3);
            });

            it('computes correct distance along diagonal', () => {
                const aabb = new AABB3D(Vec3.create(0, 0, 0), Vec3.create(1, 1, 1));
                expect(aabb.squaredDistanceToPoint(Vec3.create(3, 5, 5))).toBe(36);
            });
        });

        describe('clone / copy / equals', () => {
            it('clone produces independent copy', () => {
                const original = new AABB3D(Vec3.create(1, 2, 3), Vec3.create(4, 5, 6));
                const clone = original.clone();
                expect(clone).not.toBe(original);
                expect(clone.min.x).toBe(1);
                expect(clone.min.z).toBe(3);
                expect(clone.max.z).toBe(6);
            });

            it('copy transfers state', () => {
                const source = new AABB3D(Vec3.create(1, 2, 3), Vec3.create(4, 5, 6));
                const target = new AABB3D();
                target.copy(source);
                expect(target.min.x).toBe(1);
                expect(target.max.z).toBe(6);
            });

            it('equals compares all 6 components', () => {
                const a = new AABB3D(Vec3.create(1, 2, 3), Vec3.create(4, 5, 6));
                const b = new AABB3D(Vec3.create(1, 2, 3), Vec3.create(4, 5, 6));
                const c = new AABB3D(Vec3.create(1, 2, 3), Vec3.create(4, 5, 7));
                expect(a.equals(b)).toBe(true);
                expect(a.equals(c)).toBe(false);
                expect(a.equals('nope' as unknown)).toBe(false);
            });
        });

        describe('getHashCode', () => {
            it('is deterministic', () => {
                const a = new AABB3D(Vec3.create(1, 2, 3), Vec3.create(4, 5, 6));
                const b = new AABB3D(Vec3.create(1, 2, 3), Vec3.create(4, 5, 6));
                expect(a.getHashCode()).toBe(b.getHashCode());
            });

            it('differs for different AABBs', () => {
                const a = new AABB3D(Vec3.create(1, 2, 3), Vec3.create(4, 5, 6));
                const b = new AABB3D(Vec3.create(7, 8, 9), Vec3.create(10, 11, 12));
                expect(a.getHashCode()).not.toBe(b.getHashCode());
            });
        });

        describe('toString', () => {
            it('returns formatted string with 3D coordinates', () => {
                const aabb = new AABB3D(Vec3.create(1, 2, 3), Vec3.create(4, 5, 6));
                const str = aabb.toString();
                expect(str).toContain('AABB3D');
                expect(str).toContain('1.000');
                expect(str).toContain('6.000');
            });
        });

        describe('clear', () => {
            it('makes AABB3D empty', () => {
                const aabb = new AABB3D(Vec3.create(1, 2, 3), Vec3.create(4, 5, 6));
                aabb.clear();
                expect(aabb.isEmpty).toBe(true);
            });
        });
    });

    describe('AABB Namespace Advanced', () => {
        describe('unionAll2D', () => {
            it('computes union of multiple AABBs', () => {
                const a = new AABB2D(Vec2.create(0, 0), Vec2.create(1, 1));
                const b = new AABB2D(Vec2.create(2, 2), Vec2.create(3, 3));
                const c = new AABB2D(Vec2.create(-1, -1), Vec2.create(0.5, 0.5));
                const result = AABB.unionAll2D([a, b, c]);
                expect(result.min.x).toBe(-1);
                expect(result.min.y).toBe(-1);
                expect(result.max.x).toBe(3);
                expect(result.max.y).toBe(3);
            });

            it('works with single AABB', () => {
                const a = new AABB2D(Vec2.create(1, 2), Vec2.create(3, 4));
                const result = AABB.unionAll2D([a]);
                expect(result.min.x).toBe(1);
                expect(result.max.y).toBe(4);
            });

            it('throws for empty array', () => {
                expect(() => AABB.unionAll2D([])).toThrow(AABBError);
            });
        });

        describe('unionAll3D', () => {
            it('computes union of multiple 3D AABBs', () => {
                const a = new AABB3D(Vec3.create(0, 0, 0), Vec3.create(1, 1, 1));
                const b = new AABB3D(Vec3.create(2, 2, 2), Vec3.create(3, 3, 3));
                const result = AABB.unionAll3D([a, b]);
                expect(result.min.x).toBe(0);
                expect(result.max.z).toBe(3);
            });

            it('throws for empty array', () => {
                expect(() => AABB.unionAll3D([])).toThrow(AABBError);
            });
        });

        describe('fromPoints overloaded', () => {
            it('auto-detects 2D points', () => {
                const points = [Vec2.create(0, 0), Vec2.create(5, 5)];
                const result = AABB.fromPoints(points);
                expect(result).toBeInstanceOf(AABB2D);
                expect(result.dimensions).toBe(2);
            });

            it('auto-detects 3D points', () => {
                const points = [Vec3.create(0, 0, 0), Vec3.create(5, 5, 5)];
                const result = AABB.fromPoints(points);
                expect(result).toBeInstanceOf(AABB3D);
                expect(result.dimensions).toBe(3);
            });

            it('throws for empty array', () => {
                expect(() => AABB.fromPoints([])).toThrow(AABBError);
            });
        });
    });
});

