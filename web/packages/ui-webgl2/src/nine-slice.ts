/**
 * Nine-slice span math for the UI renderer.
 *
 * Extracted from renderer.ts as pure functions so they can be tested in
 * isolation and later hosted in @axrone/render-core for sharing with
 * render-2d.
 */
import type {
    CornerRadii,
    EdgeInsets,
    ImageRenderCommand,
} from '@axrone/ui/types';

/** A single axis span produced by the nine-slice resolver. */
export type SliceSpan = {
    offset: number;
    size: number;
    uvOffset: number;
    uvSize: number;
};

/** Creates a zeroed span for scratch reuse. */
export const createSliceSpan = (): SliceSpan => ({ offset: 0, size: 0, uvOffset: 0, uvSize: 0 });

/** Creates a triple of scratch spans (start / center / end) for one axis. */
export const createSliceSpanTriple = (): readonly [SliceSpan, SliceSpan, SliceSpan] => [
    createSliceSpan(),
    createSliceSpan(),
    createSliceSpan(),
];

/**
 * Resolves a single axis (column or row) of a nine-slice into three spans.
 *
 * When the combined borders exceed the available extent the borders are
 * proportionally scaled down so the center slice never goes negative.
 *
 * @param extent - Display size along the axis (px).
 * @param startBorder - Start border width (px).
 * @param endBorder - End border width (px).
 * @param sourceExtent - Source image size along the axis (px).
 * @param uvOffset - UV offset of the image rect.
 * @param uvExtent - UV extent of the image rect.
 * @param out - Three pre-allocated spans to write into.
 */
export const resolveSliceSpans = (
    extent: number,
    startBorder: number,
    endBorder: number,
    sourceExtent: number,
    uvOffset: number,
    uvExtent: number,
    out: readonly [SliceSpan, SliceSpan, SliceSpan]
): void => {
    const totalBorder = startBorder + endBorder;
    const scale = totalBorder > extent && totalBorder > 0 ? extent / totalBorder : 1;
    const start = startBorder * scale;
    const end = endBorder * scale;
    const startUv = sourceExtent > 0 ? startBorder / sourceExtent : 0;
    const endUv = sourceExtent > 0 ? endBorder / sourceExtent : 0;
    const first = out[0];
    const middle = out[1];
    const last = out[2];
    first.offset = 0;
    first.size = start;
    first.uvOffset = uvOffset;
    first.uvSize = startUv;
    middle.offset = start;
    middle.size = Math.max(0, extent - start - end);
    middle.uvOffset = uvOffset + startUv;
    middle.uvSize = Math.max(0, uvExtent - startUv - endUv);
    last.offset = extent - end;
    last.size = end;
    last.uvOffset = uvOffset + uvExtent - endUv;
    last.uvSize = endUv;
};

/** Frozen zero-radii constant used when nine-slicing strips the original radius. */
export const ZERO_RADII: CornerRadii = Object.freeze({
    topLeft: 0,
    topRight: 0,
    bottomRight: 0,
    bottomLeft: 0,
});

/**
 * Expands a bordered image command into up to nine sub-commands by resolving
 * the column and row spans and emitting the Cartesian product.
 *
 * Material-backed sources always go through this path because material
 * renderers consume full commands.
 */
export const sliceImageCommand = (
    command: ImageRenderCommand,
    border: EdgeInsets,
    columnSpans: readonly [SliceSpan, SliceSpan, SliceSpan],
    rowSpans: readonly [SliceSpan, SliceSpan, SliceSpan]
): readonly ImageRenderCommand[] => {
    resolveSliceSpans(
        command.width,
        border.left,
        border.right,
        Math.max(1, command.source.width),
        command.uvRect.x,
        command.uvRect.width,
        columnSpans
    );
    resolveSliceSpans(
        command.height,
        border.top,
        border.bottom,
        Math.max(1, command.source.height),
        command.uvRect.y,
        command.uvRect.height,
        rowSpans
    );
    const fillCenter = command.fillCenter !== false;
    const slices: ImageRenderCommand[] = [];
    for (let row = 0; row < rowSpans.length; row += 1) {
        const vertical = rowSpans[row];
        if (vertical.size <= 0 || vertical.uvSize <= 0) {
            continue;
        }
        for (let column = 0; column < columnSpans.length; column += 1) {
            if (row === 1 && column === 1 && !fillCenter) {
                continue;
            }
            const horizontal = columnSpans[column];
            if (horizontal.size <= 0 || horizontal.uvSize <= 0) {
                continue;
            }
            slices.push({
                ...command,
                x: command.x + horizontal.offset,
                y: command.y + vertical.offset,
                width: horizontal.size,
                height: vertical.size,
                uvRect: {
                    x: horizontal.uvOffset,
                    y: vertical.uvOffset,
                    width: horizontal.uvSize,
                    height: vertical.uvSize,
                },
                radius: ZERO_RADII,
                border: undefined,
            });
        }
    }
    return slices;
};
