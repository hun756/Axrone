namespace Axrone.Shapes2D;

/// <summary>Contour approximation, bounds, stroke offsets and mesh building.</summary>
internal static class GeometryEngine
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ApproximateSegments(float radiusX, float radiusY, ShapeApproximationOptions options)
    {
        float tol = MathF.Max(options.CurveTolerance, ShapeConstants.Epsilon);
        int minSeg = Math.Max(3, options.MinCurveSegments);
        int maxSeg = Math.Max(minSeg, options.MaxCurveSegments);
        float radius = MathF.Max(MathF.Abs(radiusX), MathF.Abs(radiusY));

        if (radius <= ShapeConstants.Epsilon)
        {
            return minSeg;
        }

        float ratio = Math.Clamp(1f - tol / radius, -1f, 1f);
        float theta = MathF.Max(ShapeConstants.Epsilon, 2f * MathF.Acos(ratio));
        int segments = (int)MathF.Ceiling(ShapeConstants.Tau / theta);
        return Math.Clamp(segments, minSeg, maxSeg);
    }

    public static float[] GetContour(Shape2D shape, ShapeApproximationOptions options = default)
    {
        switch (shape)
        {
            case RectangleShape rect:
                return
                [
                    rect.X, rect.Y,
                    rect.X + rect.Width, rect.Y,
                    rect.X + rect.Width, rect.Y + rect.Height,
                    rect.X, rect.Y + rect.Height,
                ];
            case TriangleShape tri:
                return
                [
                    tri.A.X, tri.A.Y,
                    tri.B.X, tri.B.Y,
                    tri.C.X, tri.C.Y,
                ];
            case LineShape line:
                return
                [
                    line.Start.X, line.Start.Y,
                    line.End.X, line.End.Y,
                ];
            case PolygonShape poly:
            {
                Vec2[] outer = poly.Outer.Points.ToArray();
                float[] contour = GC.AllocateUninitializedArray<float>(outer.Length * 2);
                for (int i = 0; i < outer.Length; i++)
                {
                    contour[i * 2] = outer[i].X;
                    contour[i * 2 + 1] = outer[i].Y;
                }

                return contour;
            }
            case CircleShape circle:
            {
                int segments = ApproximateSegments(circle.Radius, circle.Radius, options);
                float[] contour = GC.AllocateUninitializedArray<float>(segments * 2);
                float step = ShapeConstants.Tau / segments;
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * step;
                    contour[i * 2] = circle.Cx + MathF.Cos(angle) * circle.Radius;
                    contour[i * 2 + 1] = circle.Cy + MathF.Sin(angle) * circle.Radius;
                }

                return contour;
            }
            case EllipseShape ellipse:
            {
                int segments = ApproximateSegments(ellipse.RadiusX, ellipse.RadiusY, options);
                float[] contour = GC.AllocateUninitializedArray<float>(segments * 2);
                float step = ShapeConstants.Tau / segments;
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * step;
                    contour[i * 2] = ellipse.Cx + MathF.Cos(angle) * ellipse.RadiusX;
                    contour[i * 2 + 1] = ellipse.Cy + MathF.Sin(angle) * ellipse.RadiusY;
                }

                return contour;
            }
            default:
                throw new UnreachableException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShapeBounds GetGeometryBounds(Shape2D shape) =>
        shape switch
        {
            RectangleShape r => r.AsGeometry().ComputeBounds(),
            CircleShape c => c.AsGeometry().ComputeBounds(),
            EllipseShape e => e.AsGeometry().ComputeBounds(),
            TriangleShape t => t.AsGeometry().ComputeBounds(),
            LineShape l => l.AsGeometry().ComputeBounds(),
            PolygonShape p => PolygonMath.ComputeBounds(p.Outer.Points.AsSpan()),
            _ => throw new UnreachableException(),
        };

    public static ShapeBounds GetShapeBounds(Shape2D shape, ShapeApproximationOptions options = default)
    {
        ShapeBounds geom = GetGeometryBounds(shape);
        if (!shape.Stroke.HasValue)
        {
            return geom;
        }

        ShapeStroke stroke = shape.Stroke.Value;
        GetStrokeOffsets(stroke, out float outerOffset, out _);
        if (outerOffset <= ShapeConstants.Epsilon)
        {
            return geom;
        }

        switch (shape)
        {
            case RectangleShape:
            case CircleShape:
            case EllipseShape:
                return geom.Expand(outerOffset);
            case LineShape:
                return geom.Expand(stroke.Width * 0.5f);
            case TriangleShape:
            case PolygonShape:
            {
                float[] contour = GetContour(shape, options);
                float[] offset = OffsetConvexContour(contour, outerOffset);
                return ComputeContourBounds(offset);
            }
            default:
                return geom;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetStrokeOffsets(ShapeStroke stroke, out float outer, out float inner)
    {
        switch (stroke.Alignment)
        {
            case ShapeStrokeAlignment.Inside:
                outer = 0f;
                inner = stroke.Width;
                break;
            case ShapeStrokeAlignment.Outside:
                outer = stroke.Width;
                inner = 0f;
                break;
            default:
                outer = stroke.Width * 0.5f;
                inner = stroke.Width * 0.5f;
                break;
        }
    }

    public static float[] OffsetConvexContour(float[] contour, float distance)
    {
        int count = contour.Length / 2;
        float signedArea = (float)PolygonMath.SignedArea(MemoryMarshal.Cast<float, Vec2>(contour));
        float winding = signedArea >= 0f ? 1f : -1f;
        float[] offset = GC.AllocateUninitializedArray<float>(contour.Length);

        for (int i = 0; i < count; i++)
        {
            int prev = (i + count - 1) % count;
            int next = (i + 1) % count;

            float px = contour[i * 2];
            float py = contour[i * 2 + 1];
            float prevX = contour[prev * 2];
            float prevY = contour[prev * 2 + 1];
            float nextX = contour[next * 2];
            float nextY = contour[next * 2 + 1];

            float pdx = px - prevX;
            float pdy = py - prevY;
            float pLen = MathF.Sqrt(pdx * pdx + pdy * pdy);
            float pDirX = pLen <= ShapeConstants.Epsilon ? 0f : pdx / pLen;
            float pDirY = pLen <= ShapeConstants.Epsilon ? 0f : pdy / pLen;

            float ndx = nextX - px;
            float ndy = nextY - py;
            float nLen = MathF.Sqrt(ndx * ndx + ndy * ndy);
            float nDirX = nLen <= ShapeConstants.Epsilon ? 0f : ndx / nLen;
            float nDirY = nLen <= ShapeConstants.Epsilon ? 0f : ndy / nLen;

            float pNormX = winding * pDirY;
            float pNormY = winding * -pDirX;
            float nNormX = winding * nDirY;
            float nNormY = winding * -nDirX;

            float miterX = pNormX + nNormX;
            float miterY = pNormY + nNormY;
            float miterLen = MathF.Sqrt(miterX * miterX + miterY * miterY);

            if (miterLen <= ShapeConstants.Epsilon)
            {
                offset[i * 2] = px + pNormX * distance;
                offset[i * 2 + 1] = py + pNormY * distance;
                continue;
            }

            float nmX = miterX / miterLen;
            float nmY = miterY / miterLen;
            float proj = nmX * pNormX + nmY * pNormY;

            if (MathF.Abs(proj) <= ShapeConstants.Epsilon || 1f / MathF.Abs(proj) > ShapeConstants.MiterLimit)
            {
                offset[i * 2] = px + pNormX * distance;
                offset[i * 2 + 1] = py + pNormY * distance;
                continue;
            }

            float scale = distance / proj;
            offset[i * 2] = px + nmX * scale;
            offset[i * 2 + 1] = py + nmY * scale;
        }

        return offset;
    }

    public static void ComputeStrokeOffsetPositions(
        ReadOnlySpan<Vec2> contour,
        float outerOffset,
        float innerOffset,
        out float[] outer,
        out float[] inner)
    {
        int count = contour.Length;
        float signedArea = (float)PolygonMath.SignedArea(contour);
        float windingSign = signedArea >= 0f ? 1f : -1f;

        outer = GC.AllocateUninitializedArray<float>(count * 2);
        inner = GC.AllocateUninitializedArray<float>(count * 2);

        for (int i = 0; i < count; i++)
        {
            int prev = (i + count - 1) % count;
            int next = (i + 1) % count;

            Vec2 p = contour[i];
            Vec2 pPrev = contour[prev];
            Vec2 pNext = contour[next];

            float prevDx = p.X - pPrev.X;
            float prevDy = p.Y - pPrev.Y;
            float prevLen = MathF.Sqrt(prevDx * prevDx + prevDy * prevDy);
            float prevDirX = prevLen <= ShapeConstants.Epsilon ? 0f : prevDx / prevLen;
            float prevDirY = prevLen <= ShapeConstants.Epsilon ? 0f : prevDy / prevLen;

            float nextDx = pNext.X - p.X;
            float nextDy = pNext.Y - p.Y;
            float nextLen = MathF.Sqrt(nextDx * nextDx + nextDy * nextDy);
            float nextDirX = nextLen <= ShapeConstants.Epsilon ? 0f : nextDx / nextLen;
            float nextDirY = nextLen <= ShapeConstants.Epsilon ? 0f : nextDy / nextLen;

            float prevNormX = windingSign * prevDirY;
            float prevNormY = windingSign * -prevDirX;
            float nextNormX = windingSign * nextDirY;
            float nextNormY = windingSign * -nextDirX;

            float miterX = prevNormX + nextNormX;
            float miterY = prevNormY + nextNormY;
            float miterLen = MathF.Sqrt(miterX * miterX + miterY * miterY);

            float outX, outY, inX, inY;

            if (miterLen <= ShapeConstants.Epsilon)
            {
                outX = p.X + prevNormX * outerOffset;
                outY = p.Y + prevNormY * outerOffset;
                inX = p.X - prevNormX * innerOffset;
                inY = p.Y - prevNormY * innerOffset;
            }
            else
            {
                float nmX = miterX / miterLen;
                float nmY = miterY / miterLen;
                float proj = nmX * prevNormX + nmY * prevNormY;

                if (proj > ShapeConstants.Epsilon && (1f / proj) <= ShapeConstants.MiterLimit)
                {
                    float scale = 1f / proj;
                    outX = p.X + nmX * outerOffset * scale;
                    outY = p.Y + nmY * outerOffset * scale;
                    inX = p.X - nmX * innerOffset * scale;
                    inY = p.Y - nmY * innerOffset * scale;
                }
                else
                {
                    outX = p.X + prevNormX * outerOffset;
                    outY = p.Y + prevNormY * outerOffset;
                    inX = p.X - prevNormX * innerOffset;
                    inY = p.Y - prevNormY * innerOffset;
                }
            }

            outer[i * 2] = outX;
            outer[i * 2 + 1] = outY;
            inner[i * 2] = inX;
            inner[i * 2 + 1] = inY;
        }
    }

    public static ShapeBounds ComputeContourBounds(float[] contour)
    {
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < contour.Length; i += 2)
        {
            float x = contour[i];
            float y = contour[i + 1];
            if (x < minX)
            {
                minX = x;
            }

            if (y < minY)
            {
                minY = y;
            }

            if (x > maxX)
            {
                maxX = x;
            }

            if (y > maxY)
            {
                maxY = y;
            }
        }

        return ShapeBounds.Create(minX, minY, maxX, maxY);
    }

    public static ShapeMesh2D? BuildFillMesh(Shape2D shape, ShapeApproximationOptions options = default)
    {
        if (shape is LineShape)
        {
            return null;
        }

        if (shape is PolygonShape poly)
        {
            if (poly.Holes.Length == 0)
            {
                if (poly.Convex)
                {
                    return BuildConvexFanMesh(GetContour(poly, options));
                }

                uint[] indices = PolygonTriangulator.TriangulateEarClipping(poly.Outer.Points.AsSpan());
                float[] positions = GetContour(poly, options);
                ShapeIndexBuffer idxBuf = ShapeIndexBuffer.FromIndices(indices, positions.Length / 2);
                return new ShapeMesh2D(positions, idxBuf, ComputeContourBounds(positions));
            }

            (Vec2[] pos, uint[] idx) = PolygonTriangulator.TriangulateWithHoles(poly.Outer.Points.AsSpan(), poly.Holes.AsSpan());
            float[] flatPos = GC.AllocateUninitializedArray<float>(pos.Length * 2);
            for (int i = 0; i < pos.Length; i++)
            {
                flatPos[i * 2] = pos[i].X;
                flatPos[i * 2 + 1] = pos[i].Y;
            }

            ShapeIndexBuffer holeIdxBuf = ShapeIndexBuffer.FromIndices(idx, flatPos.Length / 2);
            return new ShapeMesh2D(flatPos, holeIdxBuf, ComputeContourBounds(flatPos));
        }

        return BuildConvexFanMesh(GetContour(shape, options));
    }

    public static ShapeMesh2D BuildConvexFanMesh(float[] contour)
    {
        int count = contour.Length / 2;
        uint[] indices = GC.AllocateUninitializedArray<uint>((count - 2) * 3);
        int outIdx = 0;
        for (uint i = 1; i < count - 1; i++)
        {
            indices[outIdx++] = 0;
            indices[outIdx++] = i;
            indices[outIdx++] = i + 1;
        }

        ShapeIndexBuffer idxBuf = ShapeIndexBuffer.FromIndices(indices, count);
        return new ShapeMesh2D((float[])contour.Clone(), idxBuf, ComputeContourBounds(contour));
    }

    public static ShapeMesh2D? BuildStrokeMesh(Shape2D shape, ShapeApproximationOptions options = default)
    {
        if (!shape.Stroke.HasValue)
        {
            return null;
        }

        ShapeStroke stroke = shape.Stroke.Value;
        if (shape is LineShape line)
        {
            float halfWidth = stroke.Width * 0.5f;
            float dx = line.End.X - line.Start.X;
            float dy = line.End.Y - line.Start.Y;
            float len = MathF.Sqrt(dx * dx + dy * dy);

            if (len <= ShapeConstants.Epsilon)
            {
                float[] square =
                [
                    line.Start.X - halfWidth, line.Start.Y - halfWidth,
                    line.Start.X + halfWidth, line.Start.Y - halfWidth,
                    line.Start.X + halfWidth, line.Start.Y + halfWidth,
                    line.Start.X - halfWidth, line.Start.Y + halfWidth,
                ];
                return BuildConvexFanMesh(square);
            }

            float nx = (-dy / len) * halfWidth;
            float ny = (dx / len) * halfWidth;

            float[] pos =
            [
                line.Start.X + nx, line.Start.Y + ny,
                line.End.X + nx, line.End.Y + ny,
                line.End.X - nx, line.End.Y - ny,
                line.Start.X - nx, line.Start.Y - ny,
            ];

            uint[] idx = [0, 1, 2, 0, 2, 3];
            ShapeIndexBuffer idxBuf = ShapeIndexBuffer.FromIndices(idx, 4);
            return new ShapeMesh2D(pos, idxBuf, ComputeContourBounds(pos));
        }

        GetStrokeOffsets(stroke, out float outerOffset, out float innerOffset);

        if (shape is PolygonShape poly)
        {
            var meshes = new List<ShapeMesh2D>(1 + poly.Holes.Length)
            {
                BuildRingStrokeMesh(poly.Outer.Points.AsSpan(), outerOffset, innerOffset),
            };

            foreach (PolygonRing hole in poly.Holes)
            {
                meshes.Add(BuildRingStrokeMesh(hole.Points.AsSpan(), outerOffset, innerOffset));
            }

            return CombineMeshes(meshes);
        }

        float[] contour = GetContour(shape, options);
        float[] outerC = outerOffset <= ShapeConstants.Epsilon ? contour : OffsetConvexContour(contour, outerOffset);
        float[]? innerC = innerOffset <= ShapeConstants.Epsilon ? null : OffsetConvexContour(contour, -innerOffset);
        return BuildRingMesh(outerC, innerC);
    }

    private static ShapeMesh2D BuildRingStrokeMesh(ReadOnlySpan<Vec2> ring, float outerOffset, float innerOffset)
    {
        if (PolygonMath.IsConvexPolygon(ring))
        {
            float[] flat = GC.AllocateUninitializedArray<float>(ring.Length * 2);
            for (int i = 0; i < ring.Length; i++)
            {
                flat[i * 2] = ring[i].X;
                flat[i * 2 + 1] = ring[i].Y;
            }

            float[] outerC = outerOffset <= ShapeConstants.Epsilon ? flat : OffsetConvexContour(flat, outerOffset);
            float[]? innerC = innerOffset <= ShapeConstants.Epsilon ? null : OffsetConvexContour(flat, -innerOffset);
            return BuildRingMesh(outerC, innerC);
        }

        ComputeStrokeOffsetPositions(ring, outerOffset, innerOffset, out float[] outerContour, out float[] innerContour);
        return BuildRingMesh(outerContour, innerContour);
    }

    private static ShapeMesh2D BuildRingMesh(float[] outer, float[]? inner)
    {
        if (inner is null || Math.Abs(PolygonMath.SignedArea(MemoryMarshal.Cast<float, Vec2>(inner))) <= ShapeConstants.DoubleEpsilon)
        {
            return BuildConvexFanMesh(outer);
        }

        int count = outer.Length / 2;
        float[] positions = GC.AllocateUninitializedArray<float>(outer.Length + inner.Length);
        Array.Copy(outer, 0, positions, 0, outer.Length);
        Array.Copy(inner, 0, positions, outer.Length, inner.Length);

        uint[] indices = GC.AllocateUninitializedArray<uint>(count * 6);
        int outIdx = 0;

        for (uint i = 0; i < count; i++)
        {
            uint next = (uint)((i + 1) % count);
            uint innerIdx = i + (uint)count;
            uint innerNext = next + (uint)count;

            indices[outIdx++] = i;
            indices[outIdx++] = next;
            indices[outIdx++] = innerNext;

            indices[outIdx++] = i;
            indices[outIdx++] = innerNext;
            indices[outIdx++] = innerIdx;
        }

        ShapeIndexBuffer idxBuf = ShapeIndexBuffer.FromIndices(indices, positions.Length / 2);
        return new ShapeMesh2D(positions, idxBuf, ComputeContourBounds(positions));
    }

    private static ShapeMesh2D CombineMeshes(List<ShapeMesh2D> meshes)
    {
        if (meshes.Count == 0)
        {
            return new ShapeMesh2D([], default, ShapeBounds.Empty);
        }

        if (meshes.Count == 1)
        {
            return meshes[0];
        }

        int totalVerts = 0;
        int totalIndices = 0;
        for (int i = 0; i < meshes.Count; i++)
        {
            totalVerts += meshes[i].VertexCount;
            totalIndices += meshes[i].IndexCount;
        }

        float[] positions = GC.AllocateUninitializedArray<float>(totalVerts * 2);
        uint[] indices = GC.AllocateUninitializedArray<uint>(totalIndices);

        int posOffset = 0;
        int idxOffset = 0;
        uint vertOffset = 0;

        for (int m = 0; m < meshes.Count; m++)
        {
            ShapeMesh2D mesh = meshes[m];
            Array.Copy(mesh.Positions, 0, positions, posOffset, mesh.Positions.Length);
            for (int i = 0; i < mesh.Indices.Length; i++)
            {
                indices[idxOffset + i] = mesh.Indices[i] + vertOffset;
            }

            posOffset += mesh.Positions.Length;
            idxOffset += mesh.Indices.Length;
            vertOffset += (uint)mesh.VertexCount;
        }

        ShapeIndexBuffer idxBuf = ShapeIndexBuffer.FromIndices(indices, totalVerts);
        return new ShapeMesh2D(positions, idxBuf, ComputeContourBounds(positions));
    }
}
