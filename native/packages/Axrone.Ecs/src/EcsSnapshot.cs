namespace Axrone.Ecs;

using System.Buffers;
using System.Buffers.Binary;
using Axrone.Hash;

/// <summary>Result of restoring a world snapshot.</summary>
public sealed class WorldSnapshotRestoreResult
{
    internal WorldSnapshotRestoreResult(
        Entity[] sourceEntities,
        Entity[] restoredEntities,
        int tick,
        int archetypeCount)
    {
        SourceEntities = sourceEntities;
        RestoredEntities = restoredEntities;
        Tick = tick;
        ArchetypeCount = archetypeCount;
    }

    /// <summary>Handles written in the snapshot, in serialized order.</summary>
    public ReadOnlyMemory<Entity> SourceEntities { get; }

    /// <summary>Fresh handles created in the restored world, in the same order.</summary>
    public ReadOnlyMemory<Entity> RestoredEntities { get; }

    /// <summary>Restored world change tick.</summary>
    public int Tick { get; }

    /// <summary>Number of serialized archetype sections.</summary>
    public int ArchetypeCount { get; }

    /// <summary>Restored entity count.</summary>
    public int EntityCount => RestoredEntities.Length;
}

/// <summary>
/// Versioned, checksummed binary world snapshot. Component values are written through
/// explicitly registered codecs under stable ids; archetypes are sorted by stable
/// component signature and rows stay in dense order, so repeated saves of an unchanged
/// world are byte-identical. Restore validates the complete payload before touching the
/// world and requires an empty target world.
/// </summary>
public static class WorldSnapshotCodec
{
    private const uint Magic = 0x45525841u;
    private const ushort FormatVersion = 1;
    private const int HeaderSize = 16;
    private const int ChecksumSize = 4;
    private const int MaxComponents = 256;

    /// <summary>Serializes a world into a new byte array.</summary>
    public static byte[] Save(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        List<ArchetypeEntry> entries = CollectEntries(world);
        var writer = new ArrayBufferWriter<byte>(4096);
        var scratch = new ArrayBufferWriter<byte>(64);

        WriteUInt32(writer, Magic);
        WriteUInt16(writer, FormatVersion);
        WriteUInt16(writer, 0);
        WriteInt32(writer, world.Tick);
        WriteInt32(writer, entries.Count);

        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            ArchetypeEntry entry = entries[entryIndex];
            WriteUInt16(writer, (ushort)entry.StableIds.Length);
            for (int componentIndex = 0; componentIndex < entry.StableIds.Length; componentIndex++)
            {
                WriteUInt16(writer, entry.StableIds[componentIndex]);
            }

            WriteInt32(writer, entry.Archetype.Count);
            for (int row = 0; row < entry.Archetype.Count; row++)
            {
                Entity source = entry.Archetype.Entities[row];
                WriteInt32(writer, source.Id.Value);
                WriteInt32(writer, source.Version.Value);
                for (int componentIndex = 0; componentIndex < entry.Bindings.Length; componentIndex++)
                {
                    scratch.Clear();
                    entry.Bindings[componentIndex].Write(scratch, entry.Columns[componentIndex], row);
                    ReadOnlySpan<byte> payload = scratch.WrittenSpan;
                    WriteInt32(writer, payload.Length);
                    WriteBytes(writer, payload);
                }
            }
        }

        uint checksum = Crc32Algorithm.Hash(writer.WrittenSpan).Value;
        byte[] result = new byte[writer.WrittenCount + ChecksumSize];
        writer.WrittenSpan.CopyTo(result);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(result.Length - ChecksumSize), checksum);
        return result;
    }

    /// <summary>Validates and restores a snapshot into an empty world.</summary>
    public static WorldSnapshotRestoreResult Restore(World world, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Entities.AliveCount != 0)
        {
            EcsThrowHelper.ThrowSnapshotNotEmpty();
        }

        if (data.Length < HeaderSize + ChecksumSize)
        {
            EcsThrowHelper.ThrowSnapshotInvalid("Snapshot is shorter than its header.");
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(data);
        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);
        _ = BinaryPrimitives.ReadUInt16LittleEndian(data[6..]);
        int tick = BinaryPrimitives.ReadInt32LittleEndian(data[8..]);
        int archetypeCount = BinaryPrimitives.ReadInt32LittleEndian(data[12..]);
        if (magic != Magic || version != FormatVersion)
        {
            EcsThrowHelper.ThrowSnapshotInvalid("Snapshot magic or format version is not supported.");
        }

        if (archetypeCount < 0)
        {
            EcsThrowHelper.ThrowSnapshotInvalid("Snapshot archetype count is negative.");
        }

        uint storedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(data[^ChecksumSize..]);
        uint actualChecksum = Crc32Algorithm.Hash(data[..^ChecksumSize]).Value;
        if (storedChecksum != actualChecksum)
        {
            EcsThrowHelper.ThrowSnapshotChecksum();
        }

        ReadOnlySpan<byte> payload = data[HeaderSize..^ChecksumSize];
        Validate(payload, archetypeCount);

        var cursor = new SnapshotCursor(payload);
        var sources = new List<Entity>();
        var restored = new List<Entity>();
        for (int archetypeIndex = 0; archetypeIndex < archetypeCount; archetypeIndex++)
        {
            int componentCount = cursor.ReadUInt16();
            var bindings = new IComponentCodecBinding[componentCount];
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                ushort stableId = cursor.ReadUInt16();
                if (!ComponentCodecRegistry.TryGet(stableId, out IComponentCodecBinding? binding))
                {
                    EcsThrowHelper.ThrowSnapshotMissingStableCodec(stableId);
                }

                bindings[componentIndex] = binding!;
            }

            int entityCount = cursor.ReadInt32();
            for (int entityIndex = 0; entityIndex < entityCount; entityIndex++)
            {
                int id = cursor.ReadInt32();
                int entityVersion = cursor.ReadInt32();
                var source = new Entity(new EntityId(id), new EntityVersion(entityVersion));
                Entity created = world.BeginEntity().Commit();
                sources.Add(source);
                restored.Add(created);

                for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                {
                    int length = cursor.ReadInt32();
                    ReadOnlySpan<byte> componentPayload = cursor.ReadBytes(length);
                    bindings[componentIndex].Apply(world, in created, componentPayload);
                }
            }
        }

        if (!cursor.AtEnd)
        {
            EcsThrowHelper.ThrowSnapshotInvalid("Snapshot contains trailing bytes.");
        }

        world.RestoreTick(tick);
        return new WorldSnapshotRestoreResult(
            sources.ToArray(),
            restored.ToArray(),
            tick,
            archetypeCount);
    }

    private static void Validate(ReadOnlySpan<byte> payload, int archetypeCount)
    {
        var cursor = new SnapshotCursor(payload);
        for (int archetypeIndex = 0; archetypeIndex < archetypeCount; archetypeIndex++)
        {
            int componentCount = cursor.ReadUInt16();
            if (componentCount > MaxComponents)
            {
                EcsThrowHelper.ThrowSnapshotInvalid("Snapshot archetype exceeds the component limit.");
            }

            var bindings = new IComponentCodecBinding[componentCount];
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                ushort stableId = cursor.ReadUInt16();
                if (!ComponentCodecRegistry.TryGet(stableId, out IComponentCodecBinding? binding))
                {
                    EcsThrowHelper.ThrowSnapshotMissingStableCodec(stableId);
                }

                bindings[componentIndex] = binding!;
            }

            int entityCount = cursor.ReadInt32();
            if (entityCount < 0)
            {
                EcsThrowHelper.ThrowSnapshotInvalid("Snapshot entity count is negative.");
            }

            for (int entityIndex = 0; entityIndex < entityCount; entityIndex++)
            {
                cursor.Skip(8);
                for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                {
                    int length = cursor.ReadInt32();
                    ReadOnlySpan<byte> componentPayload = cursor.ReadBytes(length);
                    if (!bindings[componentIndex].Validate(componentPayload))
                    {
                        EcsThrowHelper.ThrowSnapshotInvalidPayload();
                    }
                }
            }
        }

        if (!cursor.AtEnd)
        {
            EcsThrowHelper.ThrowSnapshotInvalid("Snapshot contains trailing bytes.");
        }
    }

    private static List<ArchetypeEntry> CollectEntries(World world)
    {
        var entries = new List<ArchetypeEntry>();
        ReadOnlySpan<Archetype> archetypes = world.Archetypes.AsSpan();
        for (int archetypeIndex = 0; archetypeIndex < archetypes.Length; archetypeIndex++)
        {
            Archetype archetype = archetypes[archetypeIndex];
            if (archetype.Count == 0)
            {
                continue;
            }

            var parts = new List<ComponentPart>();
            for (ushort value = 0; value < 256; value++)
            {
                var componentId = new ComponentId(value);
                if (!archetype.Mask.IsSet(componentId))
                {
                    continue;
                }

                IComponentColumn? column = archetype.GetColumn(componentId);
                if (column is null)
                {
                    EcsThrowHelper.ThrowSnapshotInvalid("Archetype mask and columns disagree.");
                }

                if (!ComponentCodecRegistry.TryGet(componentId, out IComponentCodecBinding? binding))
                {
                    EcsThrowHelper.ThrowSnapshotMissingCodec(componentId);
                }

                parts.Add(new ComponentPart(binding!.StableId, column, binding));
            }

            parts.Sort(static (left, right) => left.StableId.CompareTo(right.StableId));
            var stableIds = new ushort[parts.Count];
            var columns = new IComponentColumn[parts.Count];
            var bindings = new IComponentCodecBinding[parts.Count];
            for (int index = 0; index < parts.Count; index++)
            {
                stableIds[index] = parts[index].StableId;
                columns[index] = parts[index].Column;
                bindings[index] = parts[index].Binding;
            }

            entries.Add(new ArchetypeEntry(archetype, stableIds, columns, bindings));
        }

        entries.Sort(static (left, right) => CompareEntries(left, right));
        return entries;
    }

    private static int CompareEntries(ArchetypeEntry left, ArchetypeEntry right)
    {
        int length = Math.Min(left.StableIds.Length, right.StableIds.Length);
        for (int index = 0; index < length; index++)
        {
            int comparison = left.StableIds[index].CompareTo(right.StableIds[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        int lengthComparison = left.StableIds.Length.CompareTo(right.StableIds.Length);
        return lengthComparison != 0
            ? lengthComparison
            : left.Archetype.Id.Value.CompareTo(right.Archetype.Id.Value);
    }

    private static void WriteUInt16(ArrayBufferWriter<byte> writer, ushort value)
    {
        Span<byte> span = writer.GetSpan(sizeof(ushort));
        BinaryPrimitives.WriteUInt16LittleEndian(span, value);
        writer.Advance(sizeof(ushort));
    }

    private static void WriteUInt32(ArrayBufferWriter<byte> writer, uint value)
    {
        Span<byte> span = writer.GetSpan(sizeof(uint));
        BinaryPrimitives.WriteUInt32LittleEndian(span, value);
        writer.Advance(sizeof(uint));
    }

    private static void WriteInt32(ArrayBufferWriter<byte> writer, int value)
    {
        Span<byte> span = writer.GetSpan(sizeof(int));
        BinaryPrimitives.WriteInt32LittleEndian(span, value);
        writer.Advance(sizeof(int));
    }

    private static void WriteBytes(ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return;
        }

        Span<byte> span = writer.GetSpan(bytes.Length);
        bytes.CopyTo(span);
        writer.Advance(bytes.Length);
    }

    private readonly record struct ComponentPart(
        ushort StableId,
        IComponentColumn Column,
        IComponentCodecBinding Binding);

    private sealed class ArchetypeEntry
    {
        public readonly Archetype Archetype;
        public readonly ushort[] StableIds;
        public readonly IComponentColumn[] Columns;
        public readonly IComponentCodecBinding[] Bindings;

        public ArchetypeEntry(
            Archetype archetype,
            ushort[] stableIds,
            IComponentColumn[] columns,
            IComponentCodecBinding[] bindings)
        {
            Archetype = archetype;
            StableIds = stableIds;
            Columns = columns;
            Bindings = bindings;
        }
    }

    private ref struct SnapshotCursor
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _offset;

        public SnapshotCursor(ReadOnlySpan<byte> data)
        {
            _data = data;
            _offset = 0;
        }

        public readonly bool AtEnd => _offset == _data.Length;

        public ushort ReadUInt16()
        {
            Ensure(sizeof(ushort));
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_data[_offset..]);
            _offset += sizeof(ushort);
            return value;
        }

        public int ReadInt32()
        {
            Ensure(sizeof(int));
            int value = BinaryPrimitives.ReadInt32LittleEndian(_data[_offset..]);
            _offset += sizeof(int);
            return value;
        }

        public void Skip(int count)
        {
            Ensure(count);
            _offset += count;
        }

        public ReadOnlySpan<byte> ReadBytes(int count)
        {
            if (count < 0)
            {
                EcsThrowHelper.ThrowSnapshotInvalid("Snapshot payload length is negative.");
            }

            Ensure(count);
            ReadOnlySpan<byte> slice = _data.Slice(_offset, count);
            _offset += count;
            return slice;
        }

        private readonly void Ensure(int count)
        {
            if (count < 0 || _offset > _data.Length - count)
            {
                EcsThrowHelper.ThrowSnapshotInvalid("Snapshot is truncated.");
            }
        }
    }
}
