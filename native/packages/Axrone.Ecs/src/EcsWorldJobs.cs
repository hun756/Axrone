namespace Axrone.Ecs;

using Axrone.Collections;

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
        where TContext : allows ref struct
    {
        ReadOnlySpan<Archetype> archs = _archetypes.AsSpan();
        for (int i = 0; i < archs.Length; i++)
        {
            Archetype arch = archs[i];
            if (arch.Count > 0 && arch.Mask.ContainsAll(mask))
            {
                ReadOnlySpan<Entity> entities = arch.Entities;
                ref Entity entRef = ref MemoryMarshal.GetReference(entities);
                for (uint e = 0; e < (uint)entities.Length; e++)
                {
                    TVisitor.Visit(Unsafe.Add(ref entRef, e), ref context);
                }
            }
        }
    }
}
