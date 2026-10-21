namespace Axrone.Shapes2D;

/// <summary>Ear clipping triangulation with hole bridging.</summary>
internal static class PolygonTriangulator
{
    /// <summary>Triangulates a simple ring, remapping to original indices.</summary>
    public static uint[] TriangulateEarClipping(ReadOnlySpan<Vec2> points)
    {
        int initialCount = points.Length;
        if (initialCount < 3)
        {
            return [];
        }

        double area = PolygonMath.SignedArea(points);
        if (Math.Abs(area) <= ShapeConstants.DoubleEpsilon)
        {
            ThrowHelper.ThrowShapeValidation("Polygon has zero signed area; ring is degenerate.");
        }

        bool reversed = area < -ShapeConstants.DoubleEpsilon;
        Vec2[]? rentedWorking = null;
        uint[]? rentedIds = null;

        Span<Vec2> working = initialCount <= 64
            ? stackalloc Vec2[initialCount]
            : (rentedWorking = ArrayPool<Vec2>.Shared.Rent(initialCount)).AsSpan(0, initialCount);

        Span<uint> vertexIds = initialCount <= 64
            ? stackalloc uint[initialCount]
            : (rentedIds = ArrayPool<uint>.Shared.Rent(initialCount)).AsSpan(0, initialCount);

        try
        {
            if (reversed)
            {
                working[0] = points[0];
                vertexIds[0] = 0;
                for (int i = 1; i < initialCount; i++)
                {
                    working[i] = points[initialCount - i];
                    vertexIds[i] = (uint)(initialCount - i);
                }
            }
            else
            {
                points.CopyTo(working);
                for (uint i = 0; i < (uint)initialCount; i++)
                {
                    vertexIds[(int)i] = i;
                }
            }

            int vertexCount = initialCount;
            uint[] indices = new uint[(initialCount - 2) * 3];
            int outIdx = 0;
            int maxIterations = vertexCount * vertexCount + 1;
            int iterations = 0;

            while (vertexCount > 3 && iterations++ < maxIterations)
            {
                bool earFound = false;
                for (int i = 0; i < vertexCount; i++)
                {
                    int a = i;
                    int b = (i + 1) % vertexCount;
                    int c = (i + 2) % vertexCount;

                    ref readonly Vec2 va = ref working[a];
                    ref readonly Vec2 vb = ref working[b];
                    ref readonly Vec2 vc = ref working[c];

                    float cross = (vb.X - va.X) * (vc.Y - vb.Y) - (vb.Y - va.Y) * (vc.X - vb.X);
                    if (cross <= ShapeConstants.Epsilon)
                    {
                        continue;
                    }

                    bool isEar = true;
                    for (int j = 0; j < vertexCount; j++)
                    {
                        if (j == a || j == b || j == c)
                        {
                            continue;
                        }

                        if (IsPointInTriangle(working[j], va, vb, vc))
                        {
                            isEar = false;
                            break;
                        }
                    }

                    if (!isEar)
                    {
                        continue;
                    }

                    indices[outIdx++] = vertexIds[a];
                    indices[outIdx++] = vertexIds[b];
                    indices[outIdx++] = vertexIds[c];

                    for (int k = b; k < vertexCount - 1; k++)
                    {
                        working[k] = working[k + 1];
                        vertexIds[k] = vertexIds[k + 1];
                    }

                    vertexCount--;
                    earFound = true;
                    break;
                }

                if (!earFound)
                {
                    break;
                }
            }

            if (vertexCount != 3)
            {
                ThrowHelper.ThrowShapeValidation($"Polygon ear clipping failed with {vertexCount} vertices remaining. Ring is degenerate.");
            }

            indices[outIdx++] = vertexIds[0];
            indices[outIdx++] = vertexIds[1];
            indices[outIdx++] = vertexIds[2];

            return indices;
        }
        finally
        {
            if (rentedWorking is not null)
            {
                ArrayPool<Vec2>.Shared.Return(rentedWorking);
            }

            if (rentedIds is not null)
            {
                ArrayPool<uint>.Shared.Return(rentedIds);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPointInTriangle(Vec2 p, Vec2 a, Vec2 b, Vec2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);
        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Sign(Vec2 p, Vec2 a, Vec2 b) => (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);

    /// <summary>Triangulates an outer ring with holes.</summary>
    public static (Vec2[] Positions, uint[] Indices) TriangulateWithHoles(
        ReadOnlySpan<Vec2> outer,
        ReadOnlySpan<PolygonRing> holes)
    {
        if (outer.Length < 3)
        {
            ThrowHelper.ThrowShapeValidation("Outer ring must have at least 3 vertices.");
        }

        int totalVertices = outer.Length;
        for (int i = 0; i < holes.Length; i++)
        {
            if (holes[i].Points.Length < 3)
            {
                ThrowHelper.ThrowShapeValidation("Hole ring must have at least 3 vertices.");
            }

            totalVertices += holes[i].Points.Length;
        }

        Vec2[] positions = GC.AllocateUninitializedArray<Vec2>(totalVertices);
        int posOffset = 0;

        bool outerNeedsReverse = PolygonMath.SignedArea(outer) < -ShapeConstants.DoubleEpsilon;
        if (outerNeedsReverse)
        {
            for (int i = 0; i < outer.Length; i++)
            {
                positions[posOffset++] = outer[outer.Length - 1 - i];
            }
        }
        else
        {
            outer.CopyTo(positions.AsSpan(posOffset));
            posOffset += outer.Length;
        }

        int[] holeIndices = GC.AllocateUninitializedArray<int>(holes.Length);
        for (int h = 0; h < holes.Length; h++)
        {
            holeIndices[h] = posOffset;
            ReadOnlySpan<Vec2> hp = holes[h].Points.AsSpan();
            bool holeNeedsReverse = PolygonMath.SignedArea(hp) > ShapeConstants.DoubleEpsilon;
            if (holeNeedsReverse)
            {
                for (int i = 0; i < hp.Length; i++)
                {
                    positions[posOffset++] = hp[hp.Length - 1 - i];
                }
            }
            else
            {
                hp.CopyTo(positions.AsSpan(posOffset));
                posOffset += hp.Length;
            }
        }

        var triangulator = new EarcutEngine(positions, holeIndices);
        uint[] indices = triangulator.Triangulate();
        return (positions, indices);
    }

    private ref struct EarcutEngine
    {
        private readonly ReadOnlySpan<Vec2> _data;
        private readonly ReadOnlySpan<int> _holeIndices;
        private double[] _nodeX;
        private double[] _nodeY;
        private int[] _nodePrev;
        private int[] _nodeNext;
        private int[] _nodeVertex;
        private int _nodeCount;
        private readonly int _nodeCapacity;
        private readonly List<uint> _indices;

        public EarcutEngine(ReadOnlySpan<Vec2> data, ReadOnlySpan<int> holeIndices)
        {
            _data = data;
            _holeIndices = holeIndices;
            _nodeCapacity = (data.Length * 3) + (holeIndices.Length * 6) + 32;
            _nodeX = ArrayPool<double>.Shared.Rent(_nodeCapacity);
            _nodeY = ArrayPool<double>.Shared.Rent(_nodeCapacity);
            _nodePrev = ArrayPool<int>.Shared.Rent(_nodeCapacity);
            _nodeNext = ArrayPool<int>.Shared.Rent(_nodeCapacity);
            _nodeVertex = ArrayPool<int>.Shared.Rent(_nodeCapacity);
            _nodeCount = 0;
            _indices = new List<uint>(data.Length * 3);
        }

        public uint[] Triangulate()
        {
            try
            {
                int outerEnd = _holeIndices.Length > 0 ? _holeIndices[0] : _data.Length;
                int outerNode = CreateLinkedList(0, outerEnd, true);
                if (outerNode < 0 || _nodeNext[outerNode] == _nodePrev[outerNode])
                {
                    return [];
                }

                if (_holeIndices.Length > 0)
                {
                    outerNode = EliminateHoles(outerNode);
                }

                EarcutLinked(outerNode);
                return _indices.ToArray();
            }
            finally
            {
                ArrayPool<double>.Shared.Return(_nodeX);
                ArrayPool<double>.Shared.Return(_nodeY);
                ArrayPool<int>.Shared.Return(_nodePrev);
                ArrayPool<int>.Shared.Return(_nodeNext);
                ArrayPool<int>.Shared.Return(_nodeVertex);
            }
        }

        private int AddNode(double x, double y, int vertex)
        {
            if (_nodeCount >= _nodeCapacity)
            {
                ThrowHelper.ThrowShapeValidation("Polygon triangulation exceeded internal node pool.");
            }

            int id = _nodeCount++;
            _nodeX[id] = x;
            _nodeY[id] = y;
            _nodeVertex[id] = vertex;
            return id;
        }

        private int InsertNode(int last, double x, double y, int vertex)
        {
            int p = AddNode(x, y, vertex);
            int next = _nodeNext[last];
            _nodeNext[p] = next;
            _nodePrev[p] = last;
            _nodePrev[next] = p;
            _nodeNext[last] = p;
            return p;
        }

        private void RemoveNode(int p)
        {
            int prev = _nodePrev[p];
            int next = _nodeNext[p];
            _nodeNext[prev] = next;
            _nodePrev[next] = prev;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double AreaOf(int p, int q, int r) =>
            (_nodeY[q] - _nodeY[p]) * (_nodeX[r] - _nodeX[q]) -
            (_nodeX[q] - _nodeX[p]) * (_nodeY[r] - _nodeY[q]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PointsEqual(int a, int b) => _nodeX[a] == _nodeX[b] && _nodeY[a] == _nodeY[b];

        private int CreateLinkedList(int start, int end, bool clockwise)
        {
            if (end - start < 3)
            {
                return -1;
            }

            int first = AddNode(_data[start].X, _data[start].Y, start);
            _nodeNext[first] = first;
            _nodePrev[first] = first;
            int last = first;

            for (int i = start + 1; i < end; i++)
            {
                last = InsertNode(last, _data[i].X, _data[i].Y, i);
            }

            if (PointsEqual(last, _nodeNext[last]))
            {
                RemoveNode(last);
            }

            return FilterPoints(first, first);
        }

        private int FilterPoints(int start, int endIn)
        {
            bool full = endIn == start;
            int end = endIn;
            int p = start;
            bool again;
            do
            {
                again = false;
                if (p != _nodeNext[p] && (PointsEqual(p, _nodeNext[p]) || AreaOf(_nodePrev[p], p, _nodeNext[p]) == 0.0))
                {
                    if (full || p == end)
                    {
                        end = _nodePrev[p];
                    }

                    RemoveNode(p);
                    p = _nodePrev[p];
                    again = true;
                }
                else if (full || p != end)
                {
                    p = _nodeNext[p];
                    again = !full;
                }
            }
            while (again || p != end);

            return end;
        }

        private int EliminateHoles(int outerNode)
        {
            var queue = new List<int>(_holeIndices.Length);
            for (int i = 0; i < _holeIndices.Length; i++)
            {
                int start = _holeIndices[i];
                int end = i < _holeIndices.Length - 1 ? _holeIndices[i + 1] : _data.Length;
                int list = CreateLinkedList(start, end, false);
                if (list >= 0)
                {
                    if (list == _nodeNext[list])
                    {
                        list = FilterPoints(list, list);
                    }

                    queue.Add(GetLeftmost(list));
                }
            }

            double[] nodeX = _nodeX;
            queue.Sort((a, b) => nodeX[a].CompareTo(nodeX[b]));

            for (int i = 0; i < queue.Count; i++)
            {
                outerNode = EliminateHole(queue[i], outerNode);
            }

            return FilterPoints(outerNode, outerNode);
        }

        private int GetLeftmost(int start)
        {
            int p = start;
            int leftmost = start;
            do
            {
                if (_nodeX[p] < _nodeX[leftmost] || (_nodeX[p] == _nodeX[leftmost] && _nodeY[p] < _nodeY[leftmost]))
                {
                    leftmost = p;
                }

                p = _nodeNext[p];
            }
            while (p != start);

            return leftmost;
        }

        private int EliminateHole(int hole, int outerNode)
        {
            int bridge = FindHoleBridge(hole, outerNode);
            if (bridge < 0)
            {
                ThrowHelper.ThrowShapeValidation("Failed to establish bridge between hole and outer ring.");
            }

            int bridgeReverse = SplitPolygon(bridge, hole);
            FilterPoints(bridgeReverse, _nodeNext[bridgeReverse]);
            return FilterPoints(bridge, _nodeNext[bridge]);
        }

        private int FindHoleBridge(int hole, int outerNode)
        {
            int p = outerNode;
            double hx = _nodeX[hole];
            double hy = _nodeY[hole];
            double qx = double.NegativeInfinity;
            int m = -1;

            if (PointsEqual(hole, outerNode))
            {
                return outerNode;
            }

            do
            {
                int next = _nodeNext[p];
                double py = _nodeY[p];
                double ny = _nodeY[next];
                if (hy <= py && hy >= ny && ny != py)
                {
                    double x = _nodeX[p] + ((hy - py) * (_nodeX[next] - _nodeX[p])) / (ny - py);
                    if (x <= hx && x > qx)
                    {
                        qx = x;
                        m = _nodeX[p] < _nodeX[next] ? p : next;
                        if (x == hx)
                        {
                            return m;
                        }
                    }
                }

                p = next;
            }
            while (p != outerNode);

            if (m < 0)
            {
                return -1;
            }

            double mx = _nodeX[m];
            double my = _nodeY[m];
            double tanMin = double.PositiveInfinity;
            p = outerNode;

            do
            {
                double px = _nodeX[p];
                double py = _nodeY[p];
                if (hx >= px && px >= mx && hx != px &&
                    PointInTriangleInclusive(hy < my ? hx : qx, hy, mx, my, hy < my ? qx : hx, hy, px, py))
                {
                    double tan = Math.Abs(hy - py) / (hx - px);
                    if (LocallyInside(p, hole) && tan < tanMin)
                    {
                        m = p;
                        tanMin = tan;
                    }
                }

                p = _nodeNext[p];
            }
            while (p != outerNode);

            return m;
        }

        private int SplitPolygon(int a, int b)
        {
            int a2 = AddNode(_nodeX[a], _nodeY[a], _nodeVertex[a]);
            int b2 = AddNode(_nodeX[b], _nodeY[b], _nodeVertex[b]);
            int an = _nodeNext[a];
            int bp = _nodePrev[b];

            _nodeNext[a] = b;
            _nodePrev[b] = a;

            _nodeNext[a2] = an;
            _nodePrev[an] = a2;

            _nodeNext[b2] = a2;
            _nodePrev[a2] = b2;

            _nodeNext[bp] = b2;
            _nodePrev[b2] = bp;

            return b2;
        }

        private bool LocallyInside(int a, int b) =>
            AreaOf(_nodePrev[a], a, _nodeNext[a]) < 0.0
                ? AreaOf(a, b, _nodeNext[a]) >= 0.0 && AreaOf(a, _nodePrev[a], b) >= 0.0
                : AreaOf(a, b, _nodePrev[a]) < 0.0 || AreaOf(a, _nodeNext[a], b) < 0.0;

        private static bool PointInTriangleInclusive(double ax, double ay, double bx, double by, double cx, double cy, double px, double py) =>
            (cx - px) * (ay - py) >= (ax - px) * (cy - py) &&
            (ax - px) * (by - py) >= (bx - px) * (ay - py) &&
            (bx - px) * (cy - py) >= (cx - px) * (by - py);

        private void EarcutLinked(int earStart)
        {
            int ear = earStart;
            int stop = ear;

            while (_nodePrev[ear] != _nodeNext[ear])
            {
                int prev = _nodePrev[ear];
                int next = _nodeNext[ear];

                if (AreaOf(prev, ear, next) < 0.0 && IsEar(ear))
                {
                    _indices.Add((uint)_nodeVertex[prev]);
                    _indices.Add((uint)_nodeVertex[ear]);
                    _indices.Add((uint)_nodeVertex[next]);

                    RemoveNode(ear);
                    ear = next;
                    stop = next;
                    continue;
                }

                ear = next;
                if (ear == stop)
                {
                    ear = FilterPoints(ear, ear);
                    break;
                }
            }
        }

        private bool IsEar(int ear)
        {
            int a = _nodePrev[ear];
            int c = _nodeNext[ear];
            double ax = _nodeX[a];
            double ay = _nodeY[a];
            double bx = _nodeX[ear];
            double by = _nodeY[ear];
            double cx = _nodeX[c];
            double cy = _nodeY[c];

            int p = _nodeNext[c];
            while (p != a)
            {
                if (PointInTriangleInclusive(ax, ay, bx, by, cx, cy, _nodeX[p], _nodeY[p]) &&
                    AreaOf(_nodePrev[p], p, _nodeNext[p]) >= 0.0)
                {
                    return false;
                }

                p = _nodeNext[p];
            }

            return true;
        }
    }
}
