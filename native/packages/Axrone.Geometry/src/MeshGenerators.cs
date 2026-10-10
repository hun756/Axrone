namespace Axrone.Geometry;

/// <summary>
/// Hardened procedural mesh generators: zero heap allocation in steady state,
/// generic over the sink, vertex and index types.
/// </summary>
public static class ProceduralPrimitives
{
    /// <summary>Emits a UV sphere.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void EmitSphere<TSink, TVertex, TIndex>(ref TSink sink, in SphereConfig config)
        where TSink : IMeshSink<TVertex, TIndex>, allows ref struct
        where TVertex : unmanaged, IVertex<TVertex>
        where TIndex : unmanaged, System.Numerics.IBinaryInteger<TIndex>
    {
        uint widthSegments = Math.Max(3, config.WidthSegments.Value);
        uint heightSegments = Math.Max(2, config.HeightSegments.Value);
        float radius = config.Radius.Value;
        float phiStart = config.PhiStart.Value;
        float phiLength = config.PhiLength.Value;
        float thetaStart = config.ThetaStart.Value;
        float thetaLength = config.ThetaLength.Value;

        uint startVertex = sink.CurrentVertexCount;

        for (uint iy = 0; iy <= heightSegments; iy++)
        {
            float v = (float)iy / heightSegments;
            float theta = thetaStart + v * thetaLength;
            float sinTheta = MathF.Sin(theta);
            float cosTheta = MathF.Cos(theta);

            for (uint ix = 0; ix <= widthSegments; ix++)
            {
                float u = (float)ix / widthSegments;
                float phi = phiStart + u * phiLength;
                float sinPhi = MathF.Sin(phi);
                float cosPhi = MathF.Cos(phi);

                float x = -radius * cosPhi * sinTheta;
                float y = radius * cosTheta;
                float z = radius * sinPhi * sinTheta;

                Vec3 pos = new(x, y, z);
                Normal3D norm = Normal3D.FromVec3(pos);
                TexCoord uv = new(u, 1.0f - v);

                sink.AppendVertex(TVertex.Create(new Position3D(x, y, z), norm, uv, Tangent4D.Default));
            }
        }

        for (uint iy = 0; iy < heightSegments; iy++)
        {
            for (uint ix = 0; ix < widthSegments; ix++)
            {
                uint a = startVertex + iy * (widthSegments + 1) + ix;
                uint b = startVertex + (iy + 1) * (widthSegments + 1) + ix;
                uint c = startVertex + (iy + 1) * (widthSegments + 1) + (ix + 1);
                uint d = startVertex + iy * (widthSegments + 1) + (ix + 1);

                if (iy != 0 || thetaStart > 0.0f)
                {
                    sink.AppendTriangle(
                        TIndex.CreateChecked(a),
                        TIndex.CreateChecked(b),
                        TIndex.CreateChecked(d)
                    );
                }
                if (iy != heightSegments - 1 || (thetaStart + thetaLength) < MathF.PI)
                {
                    sink.AppendTriangle(
                        TIndex.CreateChecked(b),
                        TIndex.CreateChecked(c),
                        TIndex.CreateChecked(d)
                    );
                }
            }
        }
    }

    /// <summary>Emits a subdivided icosphere. Subdivisions clamp at 6 (327680 triangles).</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void EmitIcosphere<TSink, TVertex, TIndex>(ref TSink sink, Metric radius, uint subdivisions)
        where TSink : IMeshSink<TVertex, TIndex>, allows ref struct
        where TVertex : unmanaged, IVertex<TVertex>
        where TIndex : unmanaged, System.Numerics.IBinaryInteger<TIndex>
    {
        uint levels = Math.Min(subdivisions, 6);
        float r = radius.Value;
        float t = (1.0f + MathF.Sqrt(5.0f)) * 0.5f;

        ReadOnlySpan<Vec3> baseVertices =
        [
            Vec3.Normalize(new Vec3(-1, t, 0)) * r,
            Vec3.Normalize(new Vec3(1, t, 0)) * r,
            Vec3.Normalize(new Vec3(-1, -t, 0)) * r,
            Vec3.Normalize(new Vec3(1, -t, 0)) * r,
            Vec3.Normalize(new Vec3(0, -1, t)) * r,
            Vec3.Normalize(new Vec3(0, 1, t)) * r,
            Vec3.Normalize(new Vec3(0, -1, -t)) * r,
            Vec3.Normalize(new Vec3(0, 1, -t)) * r,
            Vec3.Normalize(new Vec3(t, 0, -1)) * r,
            Vec3.Normalize(new Vec3(t, 0, 1)) * r,
            Vec3.Normalize(new Vec3(-t, 0, -1)) * r,
            Vec3.Normalize(new Vec3(-t, 0, 1)) * r
        ];

        ReadOnlySpan<uint> baseIndices =
        [
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        ];

        NativeBuffer<Vec3> verts = new(1024);
        using NativeBuffer<uint> inds = new(2048);

        for (int i = 0; i < baseVertices.Length; i++) verts.Append(baseVertices[i]);
        for (int i = 0; i < baseIndices.Length; i++) inds.Append(baseIndices[i]);

        uint estimatedEdges = 30u * (1u << checked((int)(levels * 2 + 1)));
        UnmanagedEdgeTable edgeTable = new(estimatedEdges);

        try
        {
            for (uint s = 0; s < levels; s++)
            {
                nuint currentIndicesCount = inds.Length;
                using NativeBuffer<uint> nextIndices = new(currentIndicesCount * 4);

                for (nuint i = 0; i < currentIndicesCount; i += 3)
                {
                    uint i0 = inds.AsRef(i);
                    uint i1 = inds.AsRef(i + 1);
                    uint i2 = inds.AsRef(i + 2);

                    uint im01 = GetOrCreateMidpoint(ref verts, ref edgeTable, i0, i1, r);
                    uint im12 = GetOrCreateMidpoint(ref verts, ref edgeTable, i1, i2, r);
                    uint im20 = GetOrCreateMidpoint(ref verts, ref edgeTable, i2, i0, r);

                    nextIndices.Append(i0); nextIndices.Append(im01); nextIndices.Append(im20);
                    nextIndices.Append(i1); nextIndices.Append(im12); nextIndices.Append(im01);
                    nextIndices.Append(i2); nextIndices.Append(im20); nextIndices.Append(im12);
                    nextIndices.Append(im01); nextIndices.Append(im12); nextIndices.Append(im20);
                }

                inds.Clear();
                Span<uint> nextSpan = nextIndices.AsSpan();
                for (int k = 0; k < nextSpan.Length; k++)
                {
                    inds.Append(nextSpan[k]);
                }
            }

            uint startVertex = sink.CurrentVertexCount;
            ReadOnlySpan<Vec3> finalVerts = verts.AsReadOnlySpan();
            for (int i = 0; i < finalVerts.Length; i++)
            {
                Vec3 pos = finalVerts[i];
                Normal3D norm = Normal3D.FromVec3(pos);
                float u = 0.5f + MathF.Atan2(norm.Z, norm.X) / (MathF.PI * 2.0f);
                float v = 0.5f - MathF.Asin(Math.Clamp(norm.Y, -1.0f, 1.0f)) / MathF.PI;

                sink.AppendVertex(TVertex.Create(new Position3D(pos.X, pos.Y, pos.Z), norm, new TexCoord(u, v), Tangent4D.Default));
            }

            ReadOnlySpan<uint> finalInds = inds.AsReadOnlySpan();
            for (int i = 0; i < finalInds.Length; i += 3)
            {
                sink.AppendTriangle(
                    TIndex.CreateChecked(startVertex + finalInds[i]),
                    TIndex.CreateChecked(startVertex + finalInds[i + 1]),
                    TIndex.CreateChecked(startVertex + finalInds[i + 2])
                );
            }
        }
        finally
        {
            verts.Dispose();
            edgeTable.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetOrCreateMidpoint(
        ref NativeBuffer<Vec3> verts,
        ref UnmanagedEdgeTable edgeTable,
        uint v0,
        uint v1,
        float radius)
    {
        uint nextIdx = (uint)verts.Length;
        if (edgeTable.TryGetOrAdd(v0, v1, nextIdx, out uint existing))
        {
            return existing;
        }

        Vec3 p0 = verts.AsRef(v0);
        Vec3 p1 = verts.AsRef(v1);
        Vec3 mid = Vec3.Normalize((p0 + p1) * 0.5f) * radius;
        verts.Append(mid);
        return nextIdx;
    }

    /// <summary>Emits a box with per-face subdivisions.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void EmitBox<TSink, TVertex, TIndex>(ref TSink sink, in BoxConfig config)
        where TSink : IMeshSink<TVertex, TIndex>, allows ref struct
        where TVertex : unmanaged, IVertex<TVertex>
        where TIndex : unmanaged, System.Numerics.IBinaryInteger<TIndex>
    {
        float w = config.Width.Value;
        float h = config.Height.Value;
        float d = config.Depth.Value;
        uint ws = Math.Max(1, config.WidthSegments.Value);
        uint hs = Math.Max(1, config.HeightSegments.Value);
        uint ds = Math.Max(1, config.DepthSegments.Value);

        EmitBoxFace<TSink, TVertex, TIndex>(ref sink, new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1), w, h, d * 0.5f, ws, hs);
        EmitBoxFace<TSink, TVertex, TIndex>(ref sink, new Vec3(-1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, -1), w, h, d * 0.5f, ws, hs);
        EmitBoxFace<TSink, TVertex, TIndex>(ref sink, new Vec3(0, 0, -1), new Vec3(0, 1, 0), new Vec3(1, 0, 0), d, h, w * 0.5f, ds, hs);
        EmitBoxFace<TSink, TVertex, TIndex>(ref sink, new Vec3(0, 0, 1), new Vec3(0, 1, 0), new Vec3(-1, 0, 0), d, h, w * 0.5f, ds, hs);
        EmitBoxFace<TSink, TVertex, TIndex>(ref sink, new Vec3(1, 0, 0), new Vec3(0, 0, -1), new Vec3(0, 1, 0), w, d, h * 0.5f, ws, ds);
        EmitBoxFace<TSink, TVertex, TIndex>(ref sink, new Vec3(1, 0, 0), new Vec3(0, 0, 1), new Vec3(0, -1, 0), w, d, h * 0.5f, ws, ds);
    }

    private static void EmitBoxFace<TSink, TVertex, TIndex>(
        ref TSink sink,
        Vec3 uDir,
        Vec3 vDir,
        Vec3 norm,
        float width,
        float height,
        float depthOffset,
        uint gridX,
        uint gridY)
        where TSink : IMeshSink<TVertex, TIndex>, allows ref struct
        where TVertex : unmanaged, IVertex<TVertex>
        where TIndex : unmanaged, System.Numerics.IBinaryInteger<TIndex>
    {
        uint startVertex = sink.CurrentVertexCount;
        Normal3D n = Normal3D.FromVec3(norm);

        for (uint iy = 0; iy <= gridY; iy++)
        {
            float v = (float)iy / gridY;
            float py = (v - 0.5f) * height;

            for (uint ix = 0; ix <= gridX; ix++)
            {
                float u = (float)ix / gridX;
                float px = (u - 0.5f) * width;

                Vec3 pos = uDir * px + vDir * py + norm * depthOffset;
                sink.AppendVertex(TVertex.Create(new Position3D(pos.X, pos.Y, pos.Z), n, new TexCoord(u, 1.0f - v), Tangent4D.Default));
            }
        }

        for (uint iy = 0; iy < gridY; iy++)
        {
            for (uint ix = 0; ix < gridX; ix++)
            {
                uint a = startVertex + iy * (gridX + 1) + ix;
                uint b = startVertex + (iy + 1) * (gridX + 1) + ix;
                uint c = startVertex + (iy + 1) * (gridX + 1) + (ix + 1);
                uint d = startVertex + iy * (gridX + 1) + (ix + 1);

                sink.AppendTriangle(
                    TIndex.CreateChecked(a),
                    TIndex.CreateChecked(b),
                    TIndex.CreateChecked(d)
                );
                sink.AppendTriangle(
                    TIndex.CreateChecked(b),
                    TIndex.CreateChecked(c),
                    TIndex.CreateChecked(d)
                );
            }
        }
    }

    /// <summary>Emits a cylinder with optional caps.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void EmitCylinder<TSink, TVertex, TIndex>(ref TSink sink, in CylinderConfig config)
        where TSink : IMeshSink<TVertex, TIndex>, allows ref struct
        where TVertex : unmanaged, IVertex<TVertex>
        where TIndex : unmanaged, System.Numerics.IBinaryInteger<TIndex>
    {
        uint radialSegments = Math.Max(3, config.RadialSegments.Value);
        uint heightSegments = Math.Max(1, config.HeightSegments.Value);
        float radiusTop = config.RadiusTop.Value;
        float radiusBottom = config.RadiusBottom.Value;
        float height = config.Height.Value;
        float halfHeight = height * 0.5f;
        float thetaStart = config.ThetaStart.Value;
        float thetaLength = config.ThetaLength.Value;

        uint startVertex = sink.CurrentVertexCount;
        float slope = (radiusBottom - radiusTop) / height;

        for (uint y = 0; y <= heightSegments; y++)
        {
            float v = (float)y / heightSegments;
            float radius = v * (radiusBottom - radiusTop) + radiusTop;
            float py = halfHeight - v * height;

            for (uint x = 0; x <= radialSegments; x++)
            {
                float u = (float)x / radialSegments;
                float theta = thetaStart + u * thetaLength;
                float sinT = MathF.Sin(theta);
                float cosT = MathF.Cos(theta);

                Vec3 pos = new(radius * sinT, py, radius * cosT);
                Normal3D norm = Normal3D.FromVec3(new Vec3(sinT, slope, cosT));
                sink.AppendVertex(TVertex.Create(new Position3D(pos.X, pos.Y, pos.Z), norm, new TexCoord(u, 1.0f - v), Tangent4D.Default));
            }
        }

        for (uint y = 0; y < heightSegments; y++)
        {
            for (uint x = 0; x < radialSegments; x++)
            {
                uint a = startVertex + y * (radialSegments + 1) + x;
                uint b = startVertex + (y + 1) * (radialSegments + 1) + x;
                uint c = startVertex + (y + 1) * (radialSegments + 1) + (x + 1);
                uint d = startVertex + y * (radialSegments + 1) + (x + 1);

                sink.AppendTriangle(
                    TIndex.CreateChecked(a),
                    TIndex.CreateChecked(b),
                    TIndex.CreateChecked(d)
                );
                sink.AppendTriangle(
                    TIndex.CreateChecked(b),
                    TIndex.CreateChecked(c),
                    TIndex.CreateChecked(d)
                );
            }
        }

        if (!config.OpenEnded)
        {
            if (radiusTop > 0.0f)
            {
                EmitCylinderCap<TSink, TVertex, TIndex>(ref sink, radiusTop, halfHeight, radialSegments, thetaStart, thetaLength, true);
            }
            if (radiusBottom > 0.0f)
            {
                EmitCylinderCap<TSink, TVertex, TIndex>(ref sink, radiusBottom, -halfHeight, radialSegments, thetaStart, thetaLength, false);
            }
        }
    }

    private static void EmitCylinderCap<TSink, TVertex, TIndex>(
        ref TSink sink,
        float radius,
        float yPos,
        uint radialSegments,
        float thetaStart,
        float thetaLength,
        bool isTop)
        where TSink : IMeshSink<TVertex, TIndex>, allows ref struct
        where TVertex : unmanaged, IVertex<TVertex>
        where TIndex : unmanaged, System.Numerics.IBinaryInteger<TIndex>
    {
        uint centerVertex = sink.CurrentVertexCount;
        Normal3D norm = isTop ? Normal3D.UnitY : Normal3D.NegativeUnitY;
        sink.AppendVertex(TVertex.Create(new Position3D(0, yPos, 0), norm, TexCoord.Center, Tangent4D.Default));

        uint perimeterStart = sink.CurrentVertexCount;
        for (uint x = 0; x <= radialSegments; x++)
        {
            float u = (float)x / radialSegments;
            float theta = thetaStart + u * thetaLength;
            float cos = MathF.Cos(theta);
            float sin = MathF.Sin(theta);

            Vec3 pos = new(radius * sin, yPos, radius * cos);
            TexCoord uv = new(sin * 0.5f + 0.5f, cos * 0.5f + 0.5f);
            sink.AppendVertex(TVertex.Create(new Position3D(pos.X, pos.Y, pos.Z), norm, uv, Tangent4D.Default));
        }

        for (uint x = 0; x < radialSegments; x++)
        {
            uint p1 = perimeterStart + x;
            uint p2 = perimeterStart + x + 1;

            if (isTop)
            {
                sink.AppendTriangle(
                    TIndex.CreateChecked(centerVertex),
                    TIndex.CreateChecked(p1),
                    TIndex.CreateChecked(p2)
                );
            }
            else
            {
                sink.AppendTriangle(
                    TIndex.CreateChecked(centerVertex),
                    TIndex.CreateChecked(p2),
                    TIndex.CreateChecked(p1)
                );
            }
        }
    }
}
