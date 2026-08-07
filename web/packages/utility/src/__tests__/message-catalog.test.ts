import { describe, it, expect } from 'vitest';
import { createMessageCatalog, resolveFromCatalog } from '../message-catalog';

describe('createMessageCatalog', () => {
    it('returns a frozen object', () => {
        const catalog = createMessageCatalog({
            'err.timeout': 'Operation timed out',
            'err.not-found': 'Resource not found',
        });
        expect(Object.isFrozen(catalog)).toBe(true);
    });

    it('preserves all entries', () => {
        const catalog = createMessageCatalog({
            'err.timeout': 'Operation timed out',
            'err.not-found': 'Resource not found',
        });
        expect(catalog['err.timeout']).toBe('Operation timed out');
        expect(catalog['err.not-found']).toBe('Resource not found');
    });

    it('does not share reference with input', () => {
        const input = { 'err.x': 'original' };
        const catalog = createMessageCatalog(input);
        input['err.x'] = 'mutated';
        expect(catalog['err.x']).toBe('original');
    });
});

describe('resolveFromCatalog', () => {
    const catalog = createMessageCatalog({
        'err.timeout': 'Operation timed out',
        'err.auth': 'Authentication failed',
    });

    it('returns message for known code', () => {
        expect(resolveFromCatalog(catalog, 'err.timeout')).toBe('Operation timed out');
        expect(resolveFromCatalog(catalog, 'err.auth')).toBe('Authentication failed');
    });

    it('returns fallback string for unknown code', () => {
        const result = resolveFromCatalog(catalog, 'err.unknown' as never);
        expect(result).toContain('Unknown message code');
        expect(result).toContain('err.unknown');
    });
});
