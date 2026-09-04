import type {
    FontFaceId,
    FontGlyphBitmapFormat,
    GlyphAtlasEntry,
    GlyphAtlasPageId,
    GlyphAtlasPageSnapshot,
} from '../types';
import { createAtlasEntryKey } from './source';

export interface GlyphAtlasSource {
    readonly codePoint: number;
    readonly rasterSize?: number;
    readonly width: number;
    readonly height: number;
    readonly originX?: number;
    readonly originY?: number;
    readonly data?: ArrayBuffer | ArrayBufferView | null;
    readonly format?: FontGlyphBitmapFormat;
    readonly rowStride?: number;
    readonly distanceRange?: number;
}

export interface GlyphAtlasOptions {
    readonly maxPages?: number;
    readonly onEvictPage?: (page: GlyphAtlasPageSnapshot) => void;
}

interface AtlasPage {
    readonly id: GlyphAtlasPageId;
    readonly width: number;
    readonly height: number;
    cursorX: number;
    cursorY: number;
    rowHeight: number;
    readonly entries: Map<string, GlyphAtlasEntry>;
    lastAccess: number;
}

export class GlyphAtlas {
    private readonly faceId: FontFaceId;
    private readonly width: number;
    private readonly height: number;
    private readonly padding: number;
    private readonly maxPages: number;
    private readonly onEvictPage: GlyphAtlasOptions['onEvictPage'];
    private readonly pages: AtlasPage[] = [];
    private readonly entries = new Map<string, GlyphAtlasEntry>();
    private nextPageId = 1;
    private frameCounter = 0;

    constructor(faceId: FontFaceId, width: number, height: number, padding: number, options?: GlyphAtlasOptions) {
        this.faceId = faceId;
        this.width = Math.max(8, Math.floor(width));
        this.height = Math.max(8, Math.floor(height));
        this.padding = Math.max(0, Math.floor(padding));
        this.maxPages = Math.max(1, options?.maxPages ?? 4);
        this.onEvictPage = options?.onEvictPage;
    }

    get(codePoint: number, rasterSize?: number): GlyphAtlasEntry | null {
        const entry = this.entries.get(createAtlasEntryKey(codePoint, rasterSize));
        if (entry) {
            this.touchPage(entry.page);
            return entry;
        }
        return null;
    }

    ensure(glyph: GlyphAtlasSource): GlyphAtlasEntry {
        const key = createAtlasEntryKey(glyph.codePoint, glyph.rasterSize);
        const existing = this.entries.get(key);
        if (existing) {
            this.touchPage(existing.page);
            return existing;
        }
        const width = Math.max(1, Math.ceil(glyph.width));
        const height = Math.max(1, Math.ceil(glyph.height));
        const paddedWidth = width + this.padding * 2;
        const paddedHeight = height + this.padding * 2;
        let page = this.pages[this.pages.length - 1];
        if (!page) {
            page = this.createPage();
        }
        if (page.cursorX + paddedWidth > page.width) {
            page.cursorX = 0;
            page.cursorY += page.rowHeight;
            page.rowHeight = 0;
        }
        if (page.cursorY + paddedHeight > page.height) {
            page = this.createPage();
        }
        const x = page.cursorX + this.padding;
        const y = page.cursorY + this.padding;
        const format: FontGlyphBitmapFormat = glyph.format ?? 'alpha8';
        const rowStride = glyph.rowStride ?? width * (format === 'rgba8' ? 4 : 1);
        const entry: GlyphAtlasEntry = {
            faceId: this.faceId,
            page: page.id,
            pageWidth: page.width,
            pageHeight: page.height,
            codePoint: glyph.codePoint,
            rasterSize: glyph.rasterSize,
            x,
            y,
            width,
            height,
            originX: glyph.originX ?? 0,
            originY: glyph.originY ?? height,
            format,
            rowStride,
            distanceRange: glyph.distanceRange ?? 1,
            u0: x / page.width,
            v0: y / page.height,
            u1: (x + width) / page.width,
            v1: (y + height) / page.height,
            data: glyph.data ?? null,
        };
        page.entries.set(key, entry);
        this.entries.set(key, entry);
        page.cursorX += paddedWidth;
        page.rowHeight = Math.max(page.rowHeight, paddedHeight);
        return entry;
    }

    snapshot(): readonly GlyphAtlasPageSnapshot[] {
        return this.pages.map((page) => ({
            id: page.id as number,
            width: page.width,
            height: page.height,
            entries: [...page.entries.values()],
        }));
    }

    restore(pages: readonly GlyphAtlasPageSnapshot[]): void {
        this.pages.length = 0;
        this.entries.clear();
        let maxPageId = 0;
        for (const pageSnapshot of pages) {
            const page: AtlasPage = {
                id: pageSnapshot.id as GlyphAtlasPageId,
                width: pageSnapshot.width,
                height: pageSnapshot.height,
                cursorX: 0,
                cursorY: 0,
                rowHeight: 0,
                entries: new Map<string, GlyphAtlasEntry>(),
                lastAccess: 0,
            };
            for (const entry of pageSnapshot.entries) {
                const key = createAtlasEntryKey(entry.codePoint, entry.rasterSize);
                page.entries.set(key, entry);
                this.entries.set(key, entry);
                page.cursorX = Math.max(page.cursorX, entry.x + entry.width + this.padding);
                page.cursorY = Math.max(page.cursorY, entry.y);
                page.rowHeight = Math.max(page.rowHeight, entry.height + this.padding * 2);
            }
            this.pages.push(page);
            maxPageId = Math.max(maxPageId, pageSnapshot.id);
        }
        this.nextPageId = maxPageId + 1;
        this.evictIfNeeded();
    }

    /** Advance the internal frame counter; call once per render frame. */
    tick(): void {
        this.frameCounter += 1;
    }

    clear(): void {
        this.pages.length = 0;
        this.entries.clear();
        this.nextPageId = 1;
    }

    private touchPage(pageId: GlyphAtlasPageId): void {
        for (const page of this.pages) {
            if (page.id === pageId) {
                page.lastAccess = this.frameCounter;
                return;
            }
        }
    }

    private evictIfNeeded(): void {
        while (this.pages.length > this.maxPages) {
            let lruIndex = 0;
            let lruAccess = Infinity;
            for (let i = 0; i < this.pages.length; i += 1) {
                if (this.pages[i].lastAccess < lruAccess) {
                    lruAccess = this.pages[i].lastAccess;
                    lruIndex = i;
                }
            }
            const evicted = this.pages[lruIndex];
            // Remove all entries belonging to the evicted page from the global map.
            for (const [entryKey] of evicted.entries) {
                this.entries.delete(entryKey);
            }
            this.pages.splice(lruIndex, 1);
            if (this.onEvictPage) {
                this.onEvictPage({
                    id: evicted.id as number,
                    width: evicted.width,
                    height: evicted.height,
                    entries: [...evicted.entries.values()],
                });
            }
        }
    }

    private createPage(): AtlasPage {
        const page: AtlasPage = {
            id: this.nextPageId as GlyphAtlasPageId,
            width: this.width,
            height: this.height,
            cursorX: 0,
            cursorY: 0,
            rowHeight: 0,
            entries: new Map<string, GlyphAtlasEntry>(),
            lastAccess: this.frameCounter,
        };
        this.nextPageId += 1;
        this.pages.push(page);
        this.evictIfNeeded();
        return page;
    }
}