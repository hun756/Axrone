import { describe, expect, it } from 'vitest';
import {
    asAssetId,
    asAssetKey,
    asAssetUri,
    asAssetFingerprint,
    asAssetRevision,
    asAssetLocale,
    asAssetImporterId,
    asAssetSourceIdentity,
    normalizeAssetUri,
    canonicalizeAssetKey,
    normalizeAssetLocale,
    normalizeAssetSourceIdentity,
    createAssetReference,
    createVersionedAssetReference,
    isAssetReferenceToken,
    isAssetVersionedReferenceToken,
    parseAssetReferenceToken,
    parseAssetVersionedReferenceToken,
    isAssetReference,
    isAssetVersionedReference,
} from '@axrone/asset-core';

describe('Asset Reference Utilities', () => {
    describe('Type branding casts', () => {
        it('asAssetId returns the same string value', () => {
            const id = 'test-id-123';
            expect(asAssetId(id)).toBe(id);
        });

        it('asAssetKey returns the same string value', () => {
            const key = 'test-key';
            expect(asAssetKey(key)).toBe(key);
        });

        it('asAssetUri returns the same string value', () => {
            const uri = 'assets/test.txt';
            expect(asAssetUri(uri)).toBe(uri);
        });

        it('asAssetFingerprint returns the same string value', () => {
            const fp = 'fp:abc123';
            expect(asAssetFingerprint(fp)).toBe(fp);
        });

        it('asAssetRevision returns the same number value', () => {
            const rev = 42;
            expect(asAssetRevision(rev)).toBe(rev);
        });

        it('asAssetLocale returns the same string value', () => {
            const locale = 'en-US';
            expect(asAssetLocale(locale)).toBe(locale);
        });

        it('asAssetImporterId returns the same string value', () => {
            const importerId = 'test.importer';
            expect(asAssetImporterId(importerId)).toBe(importerId);
        });

        it('asAssetSourceIdentity returns the same string value', () => {
            const identity = 'source:identity';
            expect(asAssetSourceIdentity(identity)).toBe(identity);
        });
    });

    describe('normalizeAssetUri', () => {
        it('returns undefined for undefined input', () => {
            expect(normalizeAssetUri(undefined)).toBeUndefined();
        });

        it('returns undefined for empty string', () => {
            expect(normalizeAssetUri('')).toBeUndefined();
        });

        it('returns undefined for whitespace-only string', () => {
            expect(normalizeAssetUri('   ')).toBeUndefined();
        });

        it('normalizes backslashes to forward slashes', () => {
            const result = normalizeAssetUri('assets\\folder\\file.txt');
            expect(result).toBe('assets/folder/file.txt');
        });

        it('trims whitespace', () => {
            const result = normalizeAssetUri('  assets/file.txt  ');
            expect(result).toBe('assets/file.txt');
        });

        it('canonicalizes URL with scheme', () => {
            const result = normalizeAssetUri('https://example.com/path/file.txt');
            expect(result).toBe('https://example.com/path/file.txt');
        });

        it('handles invalid URL gracefully', () => {
            const result = normalizeAssetUri('https://');
            expect(result).toBeDefined();
        });
    });

    describe('canonicalizeAssetKey', () => {
        it('normalizes path-like values', () => {
            const result = canonicalizeAssetKey('assets\\folder\\file.txt');
            expect(result).toBe('assets/folder/file.txt');
        });

        it('normalizes URI-like values', () => {
            const result = canonicalizeAssetKey('https://example.com/file.txt');
            expect(result).toBe('https://example.com/file.txt');
        });

        it('trims whitespace', () => {
            const result = canonicalizeAssetKey('  assets/file.txt  ');
            expect(result).toBe('assets/file.txt');
        });
    });

    describe('normalizeAssetLocale', () => {
        it('returns undefined for undefined input', () => {
            expect(normalizeAssetLocale(undefined)).toBeUndefined();
        });

        it('returns undefined for empty string', () => {
            expect(normalizeAssetLocale('')).toBeUndefined();
        });

        it('returns undefined for whitespace-only string', () => {
            expect(normalizeAssetLocale('   ')).toBeUndefined();
        });

        it('canonicalizes locale using Intl.getCanonicalLocales', () => {
            const result = normalizeAssetLocale('en-us');
            expect(result).toBe('en-US');
        });

        it('handles invalid locale gracefully', () => {
            const result = normalizeAssetLocale('invalid-locale');
            expect(result).toBeDefined();
        });
    });

    describe('normalizeAssetSourceIdentity', () => {
        it('returns undefined for undefined input', () => {
            expect(normalizeAssetSourceIdentity(undefined)).toBeUndefined();
        });

        it('returns undefined for empty string', () => {
            expect(normalizeAssetSourceIdentity('')).toBeUndefined();
        });

        it('returns undefined for whitespace-only string', () => {
            expect(normalizeAssetSourceIdentity('   ')).toBeUndefined();
        });

        it('trims whitespace', () => {
            const result = normalizeAssetSourceIdentity('  source:identity  ');
            expect(result).toBe('source:identity');
        });
    });

    describe('createAssetReference', () => {
        it('creates frozen reference with correct properties', () => {
            const id = asAssetId('test-id');
            const ref = createAssetReference('texture', id);

            expect(ref.kind).toBe('texture');
            expect(ref.id).toBe(id);
            expect(ref.token).toBe('asset:texture:test-id');
            expect(Object.isFrozen(ref)).toBe(true);
        });
    });

    describe('createVersionedAssetReference', () => {
        it('creates frozen versioned reference with revision', () => {
            const id = asAssetId('test-id');
            const revision = asAssetRevision(5);
            const ref = createVersionedAssetReference('texture', id, revision);

            expect(ref.kind).toBe('texture');
            expect(ref.id).toBe(id);
            expect(ref.revision).toBe(revision);
            expect(ref.token).toBe('asset:texture:test-id');
            expect(ref.versionedToken).toBe('asset:texture:test-id@5');
            expect(Object.isFrozen(ref)).toBe(true);
        });
    });

    describe('isAssetReferenceToken', () => {
        it('returns true for valid reference token', () => {
            expect(isAssetReferenceToken('asset:texture:test-id')).toBe(true);
        });

        it('returns false for versioned token', () => {
            expect(isAssetReferenceToken('asset:texture:test-id@5')).toBe(false);
        });

        it('returns false for invalid patterns', () => {
            expect(isAssetReferenceToken('not-a-token')).toBe(false);
            expect(isAssetReferenceToken('asset:')).toBe(false);
            expect(isAssetReferenceToken(123)).toBe(false);
            expect(isAssetReferenceToken(null)).toBe(false);
        });
    });

    describe('isAssetVersionedReferenceToken', () => {
        it('returns true for valid versioned token', () => {
            expect(isAssetVersionedReferenceToken('asset:texture:test-id@5')).toBe(true);
        });

        it('returns false for unversioned token', () => {
            expect(isAssetVersionedReferenceToken('asset:texture:test-id')).toBe(false);
        });

        it('returns false for invalid patterns', () => {
            expect(isAssetVersionedReferenceToken('asset:texture:test-id@')).toBe(false);
            expect(isAssetVersionedReferenceToken('asset:texture:test-id@abc')).toBe(false);
            expect(isAssetVersionedReferenceToken(123)).toBe(false);
        });
    });

    describe('parseAssetReferenceToken', () => {
        it('parses valid token into reference', () => {
            const ref = parseAssetReferenceToken('asset:texture:test-id');
            expect(ref).toBeDefined();
            expect(ref?.kind).toBe('texture');
            expect(ref?.id).toBe('test-id');
            expect(ref?.token).toBe('asset:texture:test-id');
        });

        it('returns undefined for invalid token', () => {
            expect(parseAssetReferenceToken('invalid')).toBeUndefined();
        });
    });

    describe('parseAssetVersionedReferenceToken', () => {
        it('parses valid versioned token', () => {
            const ref = parseAssetVersionedReferenceToken('asset:texture:test-id@5');
            expect(ref).toBeDefined();
            expect(ref?.kind).toBe('texture');
            expect(ref?.id).toBe('test-id');
            expect(ref?.revision).toBe(5);
            expect(ref?.versionedToken).toBe('asset:texture:test-id@5');
        });

        it('returns undefined for invalid token', () => {
            expect(parseAssetVersionedReferenceToken('invalid')).toBeUndefined();
        });
    });

    describe('isAssetReference', () => {
        it('returns true for valid reference object', () => {
            const id = asAssetId('test-id');
            const ref = createAssetReference('texture', id);
            expect(isAssetReference(ref)).toBe(true);
        });

        it('returns false for null', () => {
            expect(isAssetReference(null)).toBe(false);
        });

        it('returns false for primitives', () => {
            expect(isAssetReference('string')).toBe(false);
            expect(isAssetReference(123)).toBe(false);
        });

        it('returns false for incomplete objects', () => {
            expect(isAssetReference({ kind: 'texture' })).toBe(false);
            expect(isAssetReference({ kind: 'texture', id: 'test' })).toBe(false);
        });

        it('returns false for object with invalid token', () => {
            expect(isAssetReference({ kind: 'texture', id: 'test', token: 'invalid' })).toBe(false);
        });
    });

    describe('isAssetVersionedReference', () => {
        it('returns true for valid versioned reference', () => {
            const id = asAssetId('test-id');
            const revision = asAssetRevision(5);
            const ref = createVersionedAssetReference('texture', id, revision);
            expect(isAssetVersionedReference(ref)).toBe(true);
        });

        it('returns false for unversioned reference', () => {
            const id = asAssetId('test-id');
            const ref = createAssetReference('texture', id);
            expect(isAssetVersionedReference(ref)).toBe(false);
        });

        it('returns false for null', () => {
            expect(isAssetVersionedReference(null)).toBe(false);
        });
    });
});
