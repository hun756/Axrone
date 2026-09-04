import type {
    WidgetId,
    LayoutBox,
    RenderCommand,
    QuadRenderCommand,
    StrokeRenderCommand,
    TextRenderCommand,
    ImageRenderCommand,
    CustomRenderCommand,
    UIFrame,
    UIFrameMetrics,
    TextLayoutResult,
    ResolvedWidgetStyle,
    ResolvedWidgetImage,
    ResolvedTextBlock,
    ResolvedFocusPolicy,
    ResolvedLayout,
} from '../types';
import type { WidgetController } from '../widget';
import { NodeFlag } from './node-flags';
import {
    buildCaretCommand,
    buildLineDecorationCommands,
    buildSelectionCommands,
    resolveImageCommand,
} from './runtime-frame';
import {
    EMPTY_FOCUS_INPUT,
    TRANSPARENT,
    intersectRect,
} from './internals';

/**
 * Adapter interface providing the RenderCommandBuilder access to UIRuntime's
 * SoA arrays and helper methods without circular imports.
 */
export interface RenderCommandBuilderHost<TPayload = unknown> {
    readonly flags: Uint32Array;
    readonly parent: Int32Array;
    readonly firstChild: Int32Array;
    readonly nextSibling: Int32Array;
    readonly sequence: Uint32Array;
    readonly styles: Array<ResolvedWidgetStyle | null>;
    readonly layouts: Array<ResolvedLayout | null>;
    readonly images: Array<ResolvedWidgetImage | null>;
    readonly texts: Array<ResolvedTextBlock | null>;
    readonly focuses: Array<ResolvedFocusPolicy | null>;
    readonly records: Array<{ controller: string | null; props: Record<string, unknown> } | null>;
    readonly states: unknown[];
    readonly rootId: WidgetId;
    readonly viewportWidth: number;
    readonly viewportHeight: number;
    readonly lastLayoutPasses: number;
    isVisible(index: number): boolean;
    readBox(index: number): LayoutBox;
    resolveTextLayoutForRender(index: number): TextLayoutResult | null;
    resolveControllerCached(controllerName: string | null): WidgetController<any, any, any> | null;
    getFocused(): WidgetId | null;
    getWidgetCount(): number;
    getRuntime(): any;
}

/**
 * Builds render commands from the widget tree.
 *
 * Responsibilities:
 * - Traverse the widget tree and emit render commands (quad, stroke, text, image, custom)
 * - Sort commands by z-index and sequence
 * - Emit focus-ring overlay
 * - Compute frame metrics
 */
export class RenderCommandBuilder<TPayload = unknown> {
    /**
     * Builds the render frame by traversing the widget tree and collecting commands.
     */
    build(host: RenderCommandBuilderHost<TPayload>): UIFrame<TPayload> {
        const commands: RenderCommand<TPayload>[] = [];
        let visibleWidgetCount = 0;
        let textCommandCount = 0;
        let imageCommandCount = 0;
        let strokeCommandCount = 0;
        let customCommandCount = 0;
        let glyphCount = 0;
        const visit = (index: number, clip: LayoutBox | null): void => {
            if (!host.isVisible(index)) {
                return;
            }
            visibleWidgetCount += 1;
            const style = host.styles[index]!;
            const box = host.readBox(index);
            const nextClip = style.clip ? intersectRect(clip, box) : clip;
            if (style.clip && nextClip === null) {
                return;
            }
            const zIndex = host.layouts[index]!.zIndex;
            if (style.background.a > 0 || (style.borderWidth > 0 && style.borderColor.a > 0)) {
                const quad: QuadRenderCommand = {
                    kind: 'quad',
                    widget: index as WidgetId,
                    x: box.x,
                    y: box.y,
                    width: box.width,
                    height: box.height,
                    zIndex,
                    color: style.background,
                    borderColor: style.borderColor,
                    borderWidth: style.borderWidth,
                    radius: style.radius,
                    opacity: style.opacity,
                    clip: nextClip,
                };
                commands.push(quad);
            }
            if (style.strokes.length > 0) {
                const stroke: StrokeRenderCommand = {
                    kind: 'stroke',
                    widget: index as WidgetId,
                    x: box.x,
                    y: box.y,
                    width: box.width,
                    height: box.height,
                    zIndex,
                    opacity: style.opacity,
                    clip: nextClip,
                    strokes: style.strokes,
                };
                strokeCommandCount += 1;
                commands.push(stroke);
            }
            const image = host.images[index];
            if (image) {
                const imageCommand = resolveImageCommand(index, box, image, style, nextClip, zIndex);
                if (imageCommand) {
                    imageCommandCount += 1;
                    commands.push(imageCommand);
                }
            }
            const textLayout = host.resolveTextLayoutForRender(index);
            if (textLayout) {
                const textStyle = host.texts[index]!;
                if (textStyle.selectionColor.a > 0) {
                    for (const quad of buildSelectionCommands(index, box, textStyle, textLayout, nextClip, zIndex, style.opacity)) {
                        commands.push(quad);
                    }
                }
                if (textLayout.glyphs.length > 0) {
                    if (textStyle.shadowColor.a > 0 && (textStyle.shadowOffsetX !== 0 || textStyle.shadowOffsetY !== 0)) {
                        const shadowCommand: TextRenderCommand = {
                            kind: 'text',
                            widget: index as WidgetId,
                            x: box.contentX + textStyle.shadowOffsetX,
                            y: box.contentY + textStyle.shadowOffsetY,
                            zIndex,
                            color: textStyle.shadowColor,
                            outlineColor: TRANSPARENT,
                            outlineWidth: 0,
                            edgeSoftness: textStyle.edgeSoftness,
                            opacity: style.opacity,
                            clip: nextClip,
                            layout: textLayout,
                        };
                        textCommandCount += 1;
                        glyphCount += textLayout.glyphs.length;
                        commands.push(shadowCommand);
                    }
                    for (const quad of buildLineDecorationCommands(index, box, textStyle, textLayout, nextClip, zIndex, style.opacity)) {
                        commands.push(quad);
                    }
                    const textCommand: TextRenderCommand = {
                        kind: 'text',
                        widget: index as WidgetId,
                        x: box.contentX,
                        y: box.contentY,
                        zIndex,
                        color: host.texts[index]!.color,
                        outlineColor: host.texts[index]!.outlineColor,
                        outlineWidth: host.texts[index]!.outlineWidth,
                        edgeSoftness: host.texts[index]!.edgeSoftness,
                        opacity: style.opacity,
                        clip: nextClip,
                        layout: textLayout,
                    };
                    textCommandCount += 1;
                    glyphCount += textLayout.glyphs.length;
                    commands.push(textCommand);
                }
                const caretCommand = buildCaretCommand(index, box, textStyle, textLayout, nextClip, zIndex, style.opacity);
                if (caretCommand) {
                    commands.push(caretCommand);
                }
            }
            const record = host.records[index]!;
            const controller = host.resolveControllerCached(record.controller);
            controller?.render?.({
                runtime: host.getRuntime(),
                widget: index as WidgetId,
                props: record.props,
                state: host.states[index],
                push: (payload: TPayload) => {
                    const command: CustomRenderCommand<TPayload> = {
                        kind: 'custom',
                        widget: index as WidgetId,
                        zIndex,
                        clip: nextClip,
                        payload,
                    };
                    customCommandCount += 1;
                    commands.push(command);
                },
            });
            for (let child = host.firstChild[index]; child !== 0; child = host.nextSibling[child]) {
                visit(child, nextClip);
            }
        };
        visit(host.rootId, null);
        commands.sort((left, right) => {
            if (left.zIndex !== right.zIndex) {
                return left.zIndex - right.zIndex;
            }
            return host.sequence[left.widget as number] - host.sequence[right.widget as number];
        });

        // ── Focus ring overlay ──────────────────────────────────────────────
        const focusedWidget = host.getFocused();
        if (focusedWidget !== null) {
            const focusIndex = focusedWidget as number;
            const focusPolicy = host.focuses[focusIndex];
            if (focusPolicy && (host.flags[focusIndex] & NodeFlag.Allocated) !== 0) {
                const focusBox = host.readBox(focusIndex);
                const ringOffset = focusPolicy.ringOffset;
                const ringWidth = focusPolicy.ringWidth;
                const ringColor = focusPolicy.ringColor;

                // Compute the parent clip by walking up the ancestor chain.
                let parentClip: LayoutBox | null = null;
                for (let ancestor = host.parent[focusIndex]; ancestor !== 0; ancestor = host.parent[ancestor]) {
                    if (!host.isVisible(ancestor)) {
                        continue;
                    }
                    const ancestorStyle = host.styles[ancestor];
                    if (ancestorStyle && ancestorStyle.clip) {
                        const ancestorBox = host.readBox(ancestor);
                        parentClip = parentClip === null ? ancestorBox : intersectRect(parentClip, ancestorBox);
                        if (parentClip === null) {
                            break;
                        }
                    }
                }

                const focusStyle = host.styles[focusIndex];
                const baseRadius = focusStyle?.radius ?? { topLeft: 0, topRight: 0, bottomRight: 0, bottomLeft: 0 };
                const ringExpansion = ringOffset + ringWidth;

                const focusRingQuad: QuadRenderCommand = {
                    kind: 'quad',
                    widget: focusIndex as WidgetId,
                    x: focusBox.x - ringExpansion,
                    y: focusBox.y - ringExpansion,
                    width: focusBox.width + ringExpansion * 2,
                    height: focusBox.height + ringExpansion * 2,
                    zIndex: Number.MAX_SAFE_INTEGER,
                    color: TRANSPARENT,
                    borderColor: ringColor,
                    borderWidth: ringWidth,
                    radius: {
                        topLeft: baseRadius.topLeft + ringExpansion,
                        topRight: baseRadius.topRight + ringExpansion,
                        bottomRight: baseRadius.bottomRight + ringExpansion,
                        bottomLeft: baseRadius.bottomLeft + ringExpansion,
                    },
                    opacity: 1,
                    clip: parentClip,
                };
                commands.push(focusRingQuad);
            }
        }

        const metrics: UIFrameMetrics = {
            widgetCount: host.getWidgetCount(),
            visibleWidgetCount,
            renderCount: commands.length,
            customCommandCount,
            imageCommandCount,
            textCommandCount,
            strokeCommandCount,
            glyphCount,
            layoutPasses: host.lastLayoutPasses,
        };
        return {
            viewportWidth: host.viewportWidth,
            viewportHeight: host.viewportHeight,
            commands,
            metrics,
        };
    }
}
