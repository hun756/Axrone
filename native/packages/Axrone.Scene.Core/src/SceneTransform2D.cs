namespace Axrone.Scene;

/// <summary>
/// Local 2D transform payload. Kept separate from the 3D payload so the 2D
/// profile does not pay for quaternion storage or matrix width it never uses.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct SceneTransform2D : ISceneDataComponent
{
    /// <summary>Local translation.</summary>
    public Vec2 Position;

    /// <summary>Local rotation around +Z, in radians.</summary>
    public float RotationRadians;

    /// <summary>Local scale.</summary>
    public Vec2 Scale;

    /// <summary>Increments on local mutation; caches compare this value.</summary>
    public uint Version;

    /// <summary>Dirty state of the cached local and world matrices.</summary>
    public SceneTransformDirty Dirty;

    /// <summary>Identity transform with dirty matrices.</summary>
    public static SceneTransform2D Identity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new()
        {
            Position = Vec2.Zero,
            RotationRadians = 0.0f,
            Scale = Vec2.One,
            Version = 1,
            Dirty = SceneTransformDirty.Local | SceneTransformDirty.World
        };
    }

    /// <summary>Creates a transform from translation, rotation and scale.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SceneTransform2D(in Vec2 position, float rotationRadians, in Vec2 scale)
    {
        Position = position;
        RotationRadians = rotationRadians;
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
    public void SetPosition(in Vec2 value)
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
    public void SetRotation(float radians)
    {
        if (RotationRadians.Equals(radians))
        {
            return;
        }

        RotationRadians = radians;
        MarkLocalDirty();
    }

    /// <summary>Sets the local scale and marks the transform dirty.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetScale(in Vec2 value)
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
    public void SetTRS(in Vec2 position, float rotationRadians, in Vec2 scale)
    {
        Position = position;
        RotationRadians = rotationRadians;
        Scale = scale;
        MarkLocalDirty();
    }

    /// <summary>Adds a local translation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Translate(in Vec2 delta)
    {
        Position += delta;
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
    public Mat3 ComposeLocalMatrix()
    {
        ComposeLocalMatrix(out Mat3 matrix);
        return matrix;
    }

    /// <summary>Composes the local matrix in the numeric row-vector convention.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void ComposeLocalMatrix(out Mat3 matrix)
    {
        Mat3 scale = Mat3.CreateScale(new Vec3(Scale.X, Scale.Y, 1.0f));
        Mat3 rotation = Mat3.CreateRotationZ(RotationRadians);
        Mat3 translation = Mat3.CreateTranslation(Position);

        Mat3.Multiply(in scale, in rotation, out Mat3 scaledRotation);
        Mat3.Multiply(in scaledRotation, in translation, out matrix);
    }
}
