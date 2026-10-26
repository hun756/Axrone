using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using GltfAccessor = Axrone.AssetGltf.GltfRawModel.Accessor;
using GltfAttribute = Axrone.AssetGltf.GltfRawModel.Attribute;
using GltfMesh = Axrone.AssetGltf.GltfRawModel.Mesh;
using GltfMorphTarget = Axrone.AssetGltf.GltfRawModel.MorphTarget;
using GltfNode = Axrone.AssetGltf.GltfRawModel.Node;
using GltfPrimitive = Axrone.AssetGltf.GltfRawModel.Primitive;
using GltfPrimitiveMaterialVariant = Axrone.AssetGltf.GltfRawModel.PrimitiveMaterialVariant;

namespace Axrone.AssetGltf;

/// <summary>UTF-8 string stored in the document arena.</summary>
internal readonly struct GltfStringRef
{
    public static GltfStringRef Empty => default;

    public GltfStringRef(int offset, int length)
    {
        Offset = offset;
        Length = length;
    }

    public int Offset { get; }

    public int Length { get; }

    public bool IsEmpty => Length == 0;

    public bool IsPresent => Length > 0;
}

/// <summary>Slice into one of the document pools.</summary>
internal readonly struct GltfSlice<T>
{
    public static GltfSlice<T> Empty => default;

    public GltfSlice(int offset, int length)
    {
        Offset = offset;
        Length = length;
    }

    public int Offset { get; }

    public int Length { get; }

    public bool IsEmpty => Length == 0;
}

/// <summary>Allocation-free view over a pool slice.</summary>
internal readonly ref struct GltfSpan<T>
{
    private readonly ReadOnlySpan<T> _span;

    public GltfSpan(ReadOnlySpan<T> span) => _span = span;

    public int Count => _span.Length;

    public bool IsEmpty => _span.IsEmpty;

    public ref readonly T this[int index] => ref _span[index];

    public ReadOnlySpan<T> AsSpan() => _span;

    public Enumerator GetEnumerator() => new(_span);

    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<T> _span;
        private int _index;

        public Enumerator(ReadOnlySpan<T> span)
        {
            _span = span;
            _index = -1;
        }

        public bool MoveNext()
        {
            int next = _index + 1;
            if ((uint)next >= (uint)_span.Length)
            {
                return false;
            }

            _index = next;
            return true;
        }

        public ref readonly T Current => ref _span[_index];
    }
}

/// <summary>
/// Transient parse storage: pooled value-type slabs plus a UTF-8 string arena.
/// The owning <see cref="GltfRawModel"/> returns every rented array on dispose;
/// no span or slice may escape its lifetime.
/// </summary>
internal sealed class GltfParseArena : IDisposable
{
    private readonly GltfImportLimits _limits;
    private readonly CancellationToken _cancellationToken;
    private long _parseBytes;
    private bool _disposed;

    private GltfNode[] _nodes;
    private GltfMesh[] _meshes;
    private GltfPrimitive[] _primitives;
    private GltfAccessor[] _accessors;
    private GltfAttribute[] _attributes;
    private GltfMorphTarget[] _morphTargets;
    private GltfPrimitiveMaterialVariant[] _materialVariants;
    private int[] _intPool;
    private float[] _floatPool;
    private byte[] _stringArena;

    public GltfParseArena(GltfImportLimits limits, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        _cancellationToken = cancellationToken;

        _nodes = ArrayPool<GltfNode>.Shared.Rent(64);
        _meshes = ArrayPool<GltfMesh>.Shared.Rent(32);
        _primitives = ArrayPool<GltfPrimitive>.Shared.Rent(64);
        _accessors = ArrayPool<GltfAccessor>.Shared.Rent(64);
        _attributes = ArrayPool<GltfAttribute>.Shared.Rent(128);
        _morphTargets = ArrayPool<GltfMorphTarget>.Shared.Rent(32);
        _materialVariants = ArrayPool<GltfPrimitiveMaterialVariant>.Shared.Rent(32);
        _intPool = ArrayPool<int>.Shared.Rent(128);
        _floatPool = ArrayPool<float>.Shared.Rent(128);
        _stringArena = ArrayPool<byte>.Shared.Rent(1024);
    }

    public int NodeCount { get; private set; }

    public int MeshCount { get; private set; }

    public int PrimitiveCount { get; private set; }

    public int AccessorCount { get; private set; }

    public int AttributeCount { get; private set; }

    public int MorphTargetCount { get; private set; }

    public int MaterialVariantCount { get; private set; }

    public int IntPoolCount { get; private set; }

    public int FloatPoolCount { get; private set; }

    public int StringArenaCount { get; private set; }

    public bool IsDisposed => _disposed;

    /// <summary>Aborts parsing when the caller cancels.</summary>
    public void ThrowIfCancellationRequested() => _cancellationToken.ThrowIfCancellationRequested();

    public GltfSpan<GltfNode> Nodes
    {
        get
        {
            ThrowIfDisposed();
            return new GltfSpan<GltfNode>(_nodes.AsSpan(0, NodeCount));
        }
    }

    public GltfSpan<GltfMesh> Meshes
    {
        get
        {
            ThrowIfDisposed();
            return new GltfSpan<GltfMesh>(_meshes.AsSpan(0, MeshCount));
        }
    }

    public GltfSpan<GltfAccessor> Accessors
    {
        get
        {
            ThrowIfDisposed();
            return new GltfSpan<GltfAccessor>(_accessors.AsSpan(0, AccessorCount));
        }
    }

    public GltfNode GetNode(int index)
    {
        ThrowIfDisposed();
        return _nodes[index];
    }

    public GltfMesh GetMesh(int index)
    {
        ThrowIfDisposed();
        return _meshes[index];
    }

    public GltfPrimitive GetPrimitive(int index)
    {
        ThrowIfDisposed();
        return _primitives[index];
    }

    public GltfAccessor GetAccessor(int index)
    {
        ThrowIfDisposed();
        return _accessors[index];
    }

    public GltfAttribute GetAttribute(int index)
    {
        ThrowIfDisposed();
        return _attributes[index];
    }

    public GltfMorphTarget GetMorphTarget(int index)
    {
        ThrowIfDisposed();
        return _morphTargets[index];
    }

    public GltfPrimitiveMaterialVariant GetMaterialVariant(int index)
    {
        ThrowIfDisposed();
        return _materialVariants[index];
    }

    public int GetInt(int index)
    {
        ThrowIfDisposed();
        return _intPool[index];
    }

    public float GetFloat(int index)
    {
        ThrowIfDisposed();
        return _floatPool[index];
    }

    public GltfSpan<GltfPrimitive> GetPrimitives(GltfSlice<GltfPrimitive> slice)
    {
        ThrowIfDisposed();
        return new(_primitives.AsSpan(slice.Offset, slice.Length));
    }

    public GltfAttributeMap GetAttributes(GltfSlice<GltfAttribute> slice)
    {
        ThrowIfDisposed();
        return new(_attributes.AsSpan(slice.Offset, slice.Length), _stringArena.AsSpan(0, StringArenaCount));
    }

    public GltfSpan<GltfMorphTarget> GetMorphTargets(GltfSlice<GltfMorphTarget> slice)
    {
        ThrowIfDisposed();
        return new(_morphTargets.AsSpan(slice.Offset, slice.Length));
    }

    public GltfSpan<GltfPrimitiveMaterialVariant> GetMaterialVariants(
        GltfSlice<GltfPrimitiveMaterialVariant> slice)
    {
        ThrowIfDisposed();
        return new(_materialVariants.AsSpan(slice.Offset, slice.Length));
    }

    public GltfSpan<int> GetInts(GltfSlice<int> slice)
    {
        ThrowIfDisposed();
        return new(_intPool.AsSpan(slice.Offset, slice.Length));
    }

    public GltfSpan<float> GetFloats(GltfSlice<float> slice)
    {
        ThrowIfDisposed();
        return new(_floatPool.AsSpan(slice.Offset, slice.Length));
    }

    public ReadOnlySpan<byte> GetStringBytes(GltfStringRef value)
    {
        ThrowIfDisposed();
        return value.IsEmpty ? [] : _stringArena.AsSpan(value.Offset, value.Length);
    }

    public string GetString(GltfStringRef value)
    {
        ThrowIfDisposed();
        return value.IsEmpty ? string.Empty : Encoding.UTF8.GetString(_stringArena, value.Offset, value.Length);
    }

    public void AddNode(in GltfNode node)
    {
        EnsureCount(NodeCount, _limits.MaxNodeCount, "MaxNodeCount");
        EnsureCapacity(ref _nodes, NodeCount);
        AddBytes<GltfNode>();
        _nodes[NodeCount++] = node;
    }

    public void AddMesh(in GltfMesh mesh)
    {
        EnsureCount(MeshCount, _limits.MaxMeshCount, "MaxMeshCount");
        EnsureCapacity(ref _meshes, MeshCount);
        AddBytes<GltfMesh>();
        _meshes[MeshCount++] = mesh;
    }

    public void AddPrimitive(in GltfPrimitive primitive)
    {
        EnsureCount(PrimitiveCount, _limits.MaxPrimitiveCount, "MaxPrimitiveCount");
        EnsureCapacity(ref _primitives, PrimitiveCount);
        AddBytes<GltfPrimitive>();
        _primitives[PrimitiveCount++] = primitive;
    }

    public void AddAccessor(in GltfAccessor accessor)
    {
        EnsureCount(AccessorCount, _limits.MaxAccessorCount, "MaxAccessorCount");
        EnsureCapacity(ref _accessors, AccessorCount);
        AddBytes<GltfAccessor>();
        _accessors[AccessorCount++] = accessor;
    }

    public void AddAttribute(in GltfAttribute attribute)
    {
        EnsureCount(AttributeCount, _limits.MaxAttributeCount, "MaxAttributeCount");
        EnsureCapacity(ref _attributes, AttributeCount);
        AddBytes<GltfAttribute>();
        _attributes[AttributeCount++] = attribute;
    }

    public void AddMorphTarget(in GltfMorphTarget target)
    {
        EnsureCount(MorphTargetCount, _limits.MaxMorphTargetCount, "MaxMorphTargetCount");
        EnsureCapacity(ref _morphTargets, MorphTargetCount);
        AddBytes<GltfMorphTarget>();
        _morphTargets[MorphTargetCount++] = target;
    }

    public void AddMaterialVariant(in GltfPrimitiveMaterialVariant mapping)
    {
        EnsureCount(MaterialVariantCount, _limits.MaxMaterialVariantCount, "MaxMaterialVariantCount");
        EnsureCapacity(ref _materialVariants, MaterialVariantCount);
        AddBytes<GltfPrimitiveMaterialVariant>();
        _materialVariants[MaterialVariantCount++] = mapping;
    }

    public void AddInt(int value)
    {
        EnsureCapacity(ref _intPool, IntPoolCount);
        AddBytes<int>();
        _intPool[IntPoolCount++] = value;
    }

    public void AddFloat(float value)
    {
        EnsureCapacity(ref _floatPool, FloatPoolCount);
        AddBytes<float>();
        _floatPool[FloatPoolCount++] = value;
    }

    /// <summary>Copies an unescaped or escaped JSON string into the arena.</summary>
    public GltfStringRef AppendString(ref Utf8JsonReader reader)
    {
        if (reader.ValueIsEscaped)
        {
            string value = reader.GetString() ?? string.Empty;
            return AppendUtf8(Encoding.UTF8.GetBytes(value));
        }

        return AppendUtf8(reader.ValueSpan);
    }

    public GltfStringRef AppendUtf8(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return GltfStringRef.Empty;
        }

        if (bytes.Length > _limits.MaxParseBytes - _parseBytes)
        {
            ThrowHelper.ThrowLimit("MaxParseBytes", _parseBytes + bytes.Length, _limits.MaxParseBytes);
        }

        EnsureCapacity(ref _stringArena, StringArenaCount, bytes.Length);
        int offset = StringArenaCount;
        bytes.CopyTo(_stringArena.AsSpan(offset));
        StringArenaCount += bytes.Length;
        _parseBytes += bytes.Length;
        return new GltfStringRef(offset, bytes.Length);
    }

    public GltfSlice<int> CaptureInts(int start) => new(start, IntPoolCount - start);

    public GltfSlice<float> CaptureFloats(int start) => new(start, FloatPoolCount - start);

    public GltfSlice<GltfAttribute> CaptureAttributes(int start) =>
        new(start, AttributeCount - start);

    public GltfSlice<GltfMorphTarget> CaptureMorphTargets(int start) =>
        new(start, MorphTargetCount - start);

    public GltfSlice<GltfPrimitiveMaterialVariant> CaptureMaterialVariants(int start) =>
        new(start, MaterialVariantCount - start);

    private void AddBytes<T>()
        where T : unmanaged
    {
        int size = Unsafe.SizeOf<T>();
        if (size > _limits.MaxParseBytes - _parseBytes)
        {
            ThrowHelper.ThrowLimit("MaxParseBytes", _parseBytes + size, _limits.MaxParseBytes);
        }

        _parseBytes += size;
    }

    private static void EnsureCount(int count, int maximum, string limitName)
    {
        if ((uint)count >= (uint)maximum)
        {
            ThrowHelper.ThrowLimit(limitName, count + 1, maximum);
        }
    }

    private static void EnsureCapacity<T>(ref T[] array, int count, int addition = 1)
    {
        if (count + addition <= array.Length)
        {
            return;
        }

        int target = array.Length > int.MaxValue / 2 ? int.MaxValue : array.Length * 2;
        if (target < count + addition)
        {
            target = count + addition;
        }

        T[] rented = ArrayPool<T>.Shared.Rent(target);
        array.AsSpan(0, count).CopyTo(rented);
        ArrayPool<T>.Shared.Return(array);
        array = rented;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ArrayPool<GltfNode>.Shared.Return(_nodes);
        ArrayPool<GltfMesh>.Shared.Return(_meshes);
        ArrayPool<GltfPrimitive>.Shared.Return(_primitives);
        ArrayPool<GltfAccessor>.Shared.Return(_accessors);
        ArrayPool<GltfAttribute>.Shared.Return(_attributes);
        ArrayPool<GltfMorphTarget>.Shared.Return(_morphTargets);
        ArrayPool<GltfPrimitiveMaterialVariant>.Shared.Return(_materialVariants);
        ArrayPool<int>.Shared.Return(_intPool);
        ArrayPool<float>.Shared.Return(_floatPool);
        ArrayPool<byte>.Shared.Return(_stringArena);
        _nodes = [];
        _meshes = [];
        _primitives = [];
        _accessors = [];
        _attributes = [];
        _morphTargets = [];
        _materialVariants = [];
        _intPool = [];
        _floatPool = [];
        _stringArena = [];
    }
}
