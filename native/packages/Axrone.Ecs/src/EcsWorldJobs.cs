namespace Axrone.Ecs;

using Axrone.Collections;

/// <summary>
/// Visits one row whose change tick is newer than a checkpoint.
/// </summary>
/// <typeparam name="T1">Component type.</typeparam>
public interface IChangeVisitor<T1> where T1 : unmanaged
{
    /// <summary>Visits a changed row.</summary>
    void Visit(Entity entity, ref T1 value, int tick);
}

/// <summary>
/// World job dispatch: entity-aware policies plus the shared
/// <see cref="ISpanVisitor{T, TState}"/> and <see cref="ISlotConsumer{T, TState}"/>
/// batch visitors over column slices.
/// </summary>
public sealed partial class World
{
    /// <summary>
    /// Runs a one-component job policy over every matching row.
    /// </summary>
    public void Run<TJob, T1>()
        where TJob : struct, IJobPolicy<T1>
        where T1 : unmanaged
    {
        foreach (Chunk1<T1> chunk in Query<T1>())
        {
            ReadOnlySpan<Entity> entities = chunk.Entities;
            Span<T1> c1 = chunk.Column1[..chunk.Length];

            ref Entity entRef = ref MemoryMarshal.GetReference(entities);
            ref T1 c1Ref = ref MemoryMarshal.GetReference(c1);

            for (uint i = 0; i < (uint)chunk.Length; i++)
            {
                TJob.Execute(Unsafe.Add(ref entRef, i), ref Unsafe.Add(ref c1Ref, i));
            }
        }
    }

    /// <summary>
    /// Runs a two-component job policy over every matching row.
    /// </summary>
    public void Run<TJob, T1, T2>()
        where TJob : struct, IJobPolicy<T1, T2>
        where T1 : unmanaged
        where T2 : unmanaged
    {
        foreach (Chunk2<T1, T2> chunk in Query<T1, T2>())
        {
            ReadOnlySpan<Entity> entities = chunk.Entities;
            Span<T1> c1 = chunk.Column1[..chunk.Length];
            Span<T2> c2 = chunk.Column2[..chunk.Length];

            ref Entity entRef = ref MemoryMarshal.GetReference(entities);
            ref T1 c1Ref = ref MemoryMarshal.GetReference(c1);
            ref T2 c2Ref = ref MemoryMarshal.GetReference(c2);

            for (uint i = 0; i < (uint)chunk.Length; i++)
            {
                TJob.Execute(
                    Unsafe.Add(ref entRef, i),
                    ref Unsafe.Add(ref c1Ref, i),
                    ref Unsafe.Add(ref c2Ref, i));
            }
        }
    }

    /// <summary>
    /// Runs a three-component job policy over every matching row.
    /// </summary>
    public void Run<TJob, T1, T2, T3>()
        where TJob : struct, IJobPolicy<T1, T2, T3>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        foreach (Chunk3<T1, T2, T3> chunk in Query<T1, T2, T3>())
        {
            ReadOnlySpan<Entity> entities = chunk.Entities;
            Span<T1> c1 = chunk.Column1[..chunk.Length];
            Span<T2> c2 = chunk.Column2[..chunk.Length];
            Span<T3> c3 = chunk.Column3[..chunk.Length];

            ref Entity entRef = ref MemoryMarshal.GetReference(entities);
            ref T1 c1Ref = ref MemoryMarshal.GetReference(c1);
            ref T2 c2Ref = ref MemoryMarshal.GetReference(c2);
            ref T3 c3Ref = ref MemoryMarshal.GetReference(c3);

            for (uint i = 0; i < (uint)chunk.Length; i++)
            {
                TJob.Execute(
                    Unsafe.Add(ref entRef, i),
                    ref Unsafe.Add(ref c1Ref, i),
                    ref Unsafe.Add(ref c2Ref, i),
                    ref Unsafe.Add(ref c3Ref, i));
            }
        }
    }

    /// <summary>
    /// Runs a four-component job policy over every matching row.
    /// </summary>
    public void Run<TJob, T1, T2, T3, T4>()
        where TJob : struct, IJobPolicy<T1, T2, T3, T4>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
    {
        foreach (Chunk4<T1, T2, T3, T4> chunk in Query<T1, T2, T3, T4>())
        {
            ReadOnlySpan<Entity> entities = chunk.Entities;
            Span<T1> c1 = chunk.Column1[..chunk.Length];
            Span<T2> c2 = chunk.Column2[..chunk.Length];
            Span<T3> c3 = chunk.Column3[..chunk.Length];
            Span<T4> c4 = chunk.Column4[..chunk.Length];

            ref Entity entRef = ref MemoryMarshal.GetReference(entities);
            ref T1 c1Ref = ref MemoryMarshal.GetReference(c1);
            ref T2 c2Ref = ref MemoryMarshal.GetReference(c2);
            ref T3 c3Ref = ref MemoryMarshal.GetReference(c3);
            ref T4 c4Ref = ref MemoryMarshal.GetReference(c4);

            for (uint i = 0; i < (uint)chunk.Length; i++)
            {
                TJob.Execute(
                    Unsafe.Add(ref entRef, i),
                    ref Unsafe.Add(ref c1Ref, i),
                    ref Unsafe.Add(ref c2Ref, i),
                    ref Unsafe.Add(ref c3Ref, i),
                    ref Unsafe.Add(ref c4Ref, i));
            }
        }
    }

    /// <summary>
    /// Visits every row whose changed tick is newer than <paramref name="sinceTick"/>.
    /// </summary>
    public void ForEachChanged<TVisitor, T1>(int sinceTick, ref TVisitor visitor)
        where TVisitor : struct, IChangeVisitor<T1>
        where T1 : unmanaged
    {
        foreach (Chunk1<T1> chunk in Query<T1>())
        {
            Span<T1> column = chunk.Column1;
            ReadOnlySpan<int> ticks = chunk.Changed1;
            ReadOnlySpan<Entity> entities = chunk.Entities;
            for (int row = 0; row < chunk.Length; row++)
            {
                if (EcsTick.IsNewer(ticks[row], sinceTick))
                {
                    visitor.Visit(entities[row], ref column[row], ticks[row]);
                }
            }
        }
    }

    /// <summary>
    /// Visits every row whose added tick is newer than <paramref name="sinceTick"/>.
    /// </summary>
    public void ForEachAdded<TVisitor, T1>(int sinceTick, ref TVisitor visitor)
        where TVisitor : struct, IChangeVisitor<T1>
        where T1 : unmanaged
    {
        foreach (Chunk1<T1> chunk in Query<T1>())
        {
            Span<T1> column = chunk.Column1;
            ReadOnlySpan<int> ticks = chunk.Added1;
            ReadOnlySpan<Entity> entities = chunk.Entities;
            for (int row = 0; row < chunk.Length; row++)
            {
                if (EcsTick.IsNewer(ticks[row], sinceTick))
                {
                    visitor.Visit(entities[row], ref column[row], ticks[row]);
                }
            }
        }
    }

    /// <summary>
    /// Runs a shared span visitor over every matching column slice.
    /// </summary>
    public void RunSpan<TVisitor, TState, T1>(ref TVisitor visitor, ref TState state)
        where TVisitor : struct, ISpanVisitor<T1, TState>
        where TState : allows ref struct
        where T1 : unmanaged
    {
        foreach (Chunk1<T1> chunk in Query<T1>())
        {
            visitor.Visit(chunk.Column1[..chunk.Length], ref state);
        }
    }

    /// <summary>
    /// Runs a shared slot consumer over every matching row.
    /// </summary>
    public void RunItem<TConsumer, TState, T1>(ref TConsumer consumer, ref TState state)
        where TConsumer : struct, ISlotConsumer<T1, TState>
        where TState : allows ref struct
        where T1 : unmanaged
    {
        foreach (Chunk1<T1> chunk in Query<T1>())
        {
            Span<T1> slice = chunk.Column1[..chunk.Length];
            ref T1 first = ref MemoryMarshal.GetReference(slice);
            for (uint i = 0; i < (uint)slice.Length; i++)
            {
                consumer.Consume(in Unsafe.Add(ref first, i), ref state);
            }
        }
    }

    /// <summary>
    /// Visits every entity matching a mask with stack-only context.
    /// </summary>
    public void QueryEntities<TVisitor, TContext>(in ComponentMask mask, ref TContext context)
        where TVisitor : struct, IEntityVisitor<TContext>
        where TContext : allows ref struct => QueryEntities<TVisitor, TContext>(in mask, ComponentMask.Empty, ref context);

    /// <summary>
    /// Visits every entity matching required and excluded masks with stack-only context.
    /// </summary>
    public void QueryEntities<TVisitor, TContext>(
        in ComponentMask all,
        in ComponentMask none,
        ref TContext context)
        where TVisitor : struct, IEntityVisitor<TContext>
        where TContext : allows ref struct
    {
        int[] matches = _queryCache.Resolve(all, none, _archetypes);
        ReadOnlySpan<Archetype> archetypes = _archetypes.AsSpan();
        for (int index = 0; index < matches.Length; index++)
        {
            Archetype arch = archetypes[matches[index]];
            if (arch.Count == 0)
            {
                continue;
            }

            ReadOnlySpan<Entity> entities = arch.Entities;
            ref Entity entRef = ref MemoryMarshal.GetReference(entities);
            for (uint e = 0; e < (uint)entities.Length; e++)
            {
                TVisitor.Visit(Unsafe.Add(ref entRef, e), ref context);
            }
        }
    }
}
