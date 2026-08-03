import { describe, expect, it } from 'vitest';
import {
    Render2DCapacityError,
    Render2DError,
    Render2DValidationError,
} from '../errors';
import {
    asRender2DMaterialReference,
    asRender2DTextureReference,
    getRender2DSpriteSourceKey,
    isRender2DSpriteMaterialSource,
    isRender2DSpriteTextureSource,
} from '../types';

describe('Render2DError', () => {
    it('has correct name, code, message, and optional cause', () => {
        const cause = new Error('root');
        const err = new Render2DError('something broke', 'MY_CODE', cause);
        expect(err).toBeInstanceOf(Error);
        expect(err).toBeInstanceOf(Render2DError);
        expect(err.name).toBe('Render2DError');
        expect(err.message).toBe('something broke');
        expect(err.code).toBe('MY_CODE');
        expect(err.cause).toBe(cause);
    });

    it('works without cause', () => {
        const err = new Render2DError('plain', 'CODE');
        expect(err.cause).toBeUndefined();
    });
});

describe('Render2DValidationError', () => {
    it('extends Render2DError with RENDER_2D_VALIDATION_ERROR code', () => {
        const err = new Render2DValidationError('bad input');
        expect(err).toBeInstanceOf(Render2DError);
        expect(err.name).toBe('Render2DValidationError');
        expect(err.code).toBe('RENDER_2D_VALIDATION_ERROR');
        expect(err.message).toBe('bad input');
    });

    it('supports optional cause', () => {
        const cause = new Error('inner');
        const err = new Render2DValidationError('bad', cause);
        expect(err.cause).toBe(cause);
    });
});

describe('Render2DCapacityError', () => {
    it('extends Render2DError with RENDER_2D_CAPACITY_ERROR code', () => {
        const err = new Render2DCapacityError('too many quads');
        expect(err).toBeInstanceOf(Render2DError);
        expect(err.name).toBe('Render2DCapacityError');
        expect(err.code).toBe('RENDER_2D_CAPACITY_ERROR');
        expect(err.message).toBe('too many quads');
    });
});

describe('type guards', () => {
    it('isRender2DSpriteTextureSource returns true for texture sources', () => {
        expect(isRender2DSpriteTextureSource({ kind: 'texture', textureId: 'a' })).toBe(true);
        expect(isRender2DSpriteTextureSource({ kind: 'material', materialId: 'b' })).toBe(false);
    });

    it('isRender2DSpriteMaterialSource returns true for material sources', () => {
        expect(isRender2DSpriteMaterialSource({ kind: 'material', materialId: 'b' })).toBe(true);
        expect(isRender2DSpriteMaterialSource({ kind: 'texture', textureId: 'a' })).toBe(false);
    });
});

describe('getRender2DSpriteSourceKey', () => {
    it('returns texture:<id> for texture sources', () => {
        expect(getRender2DSpriteSourceKey({ kind: 'texture', textureId: 'atlas/main' }))
            .toBe('texture:atlas/main');
    });

    it('returns material:<id> for material sources', () => {
        expect(getRender2DSpriteSourceKey({ kind: 'material', materialId: 'mat/sprite' }))
            .toBe('material:mat/sprite');
    });
});

describe('brand casts', () => {
    it('asRender2DTextureReference returns the same string', () => {
        const ref = asRender2DTextureReference('tex:main');
        expect(ref).toBe('tex:main');
    });

    it('asRender2DMaterialReference returns the same string', () => {
        const ref = asRender2DMaterialReference('mat:hero');
        expect(ref).toBe('mat:hero');
    });
});
