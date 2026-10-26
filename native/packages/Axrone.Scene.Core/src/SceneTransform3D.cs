namespace Axrone.Scene;

/// <summary>
/// Local 3D transform payload. The struct is a data component: systems mutate
/// it by reference and the transform pass consumes the dirty bits. World
/// matrices live in a separate column so this record stays compact.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct SceneTransform3D : ISceneDataComponent
{
    /// <summary>Local translation.</summary>
    public Vec3 Position;

    /// <summary>Local rotation; expected to be unit length.</summary>
    public Quat Rotation;

    /// <summary>Local scale.</summary>
    public Vec3 Scale;

    /// <summary>Increments on local mutation; caches compare this value.</summary>
    public uint Version;

    /// <summary>Dirty state of the cached local and world matrices.</summary>
    public SceneTransformDirty Dirty;

    /// <summary>Identity transform with dirty matrices.</summary>
    public static SceneTransform3D Identity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new()
        {
            Position = Vec3.Zero,
            Rotation = Quat.Identity,
            Scale = Vec3.One,
            Version = 1,
            Dirty = SceneTransformDirty.Local | SceneTransformDirty.World
        };
    }

    /// <summary>Creates a transform from translation, rotation and scale.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SceneTransform3D(in Vec3 position, in Quat rotation, in Vec3 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
        Version = 1;
        Dirty = SceneTransformDirty.Local | SceneTransformDirty.World;
    }

    /// <summary>Whether the local matrix is stale.</summary>
    public bool IsLocalDirty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Dirty & SceneTransformDirty.Local) != 0;
    }

    /// <summary>Whether the world matrix is stale.</summary>
    public bool IsWorldDirty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Dirty & SceneTransformDirty.World) != 0;
    }

    /// <summary>Sets the local position and marks the transform dirty.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPosition(in Vec3 value)
    {
        if (Position.Equals(value))
        {
            return;
        }

        Position = value;
        MarkLocalDirty();
    }

    /// <summary>Sets the local rotation and marks the transform dirty.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetRotation(in Quat value)
    {
        if (Rotation.Equals(value))
        {
            return;
        }

        Rotation = value;
        MarkLocalDirty();
    }

    /// <summary>Sets the local scale and marks the transform dirty.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetScale(in Vec3 value)
    {
        if (Scale.Equals(value))
        {
            return;
        }

        Scale = value;
        MarkLocalDirty();
    }

    /// <summary>Replaces all local components in one dirty transition.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetTRS(in Vec3 position, in Quat rotation, in Vec3 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
        MarkLocalDirty();
    }

    /// <summary>Adds a local translation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Translate(in Vec3 delta)
    {
        Position += delta;
        MarkLocalDirty();
    }

    /// <summary>Normalizes the local rotation, falling back to identity when singular.</summary>
    public void NormalizeRotation()
    {
        Rotation = Quat.Normalize(Rotation);
        MarkLocalDirty();
    }

    /// <summary>Marks the local and world matrices stale.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkLocalDirty()
    {
        Dirty |= SceneTransformDirty.Local | SceneTransformDirty.World;
        Version++;
    }

    /// <summary>Marks the world matrix stale without changing local data.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkWorldDirty() => Dirty |= SceneTransformDirty.World;

    /// <summary>Clears the local dirty bit after rebuilding the local matrix.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearLocalDirty() => Dirty &= ~SceneTransformDirty.Local;

    /// <summary>Clears the world dirty bit after rebuilding the world matrix.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearWorldDirty() => Dirty &= ~SceneTransformDirty.World;

    /// <summary>Composes the local matrix in row-vector convention.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mat4 ComposeLocalMatrix()
    {
        ComposeLocalMatrix(out Mat4 matrix);
        return matrix;
    }

    /// <summary>Composes the local matrix in the numeric row-vector convention.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void ComposeLocalMatrix(out Mat4 matrix)
    {
        Mat4 scale = Mat4.CreateScale(Scale);
        Mat4 rotation = Mat4.CreateFromQuaternion(Rotation);
        Mat4 translation = Mat4.CreateTranslation(Position);

        Mat4.Multiply(in scale, in rotation, out Mat4 scaledRotation);
        Mat4.Multiply(in scaledRotation, in translation, out matrix);
    }
}
