namespace Axrone.Numeric;

public partial struct vec3
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(vec3 left, vec3 right) =>
        MathF.FusedMultiplyAdd(left.X, right.X, MathF.FusedMultiplyAdd(left.Y, right.Y, left.Z * right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DotStrict(vec3 left, vec3 right) =>
        (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Cross(vec3 left, vec3 right) =>
        new(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float LengthSquared() => Dot(this, this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float Length() => MathF.Sqrt(LengthSquared());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(vec3 value1, vec3 value2)
    {
        float dx = value1.X - value2.X;
        float dy = value1.Y - value2.Y;
        float dz = value1.Z - value2.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(vec3 value1, vec3 value2) =>
        MathF.Sqrt(DistanceSquared(value1, value2));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Normalize(vec3 value)
    {
        float lengthSquared = value.LengthSquared();
        if (lengthSquared > 0.0f)
        {
            float invLength = 1.0f / MathF.Sqrt(lengthSquared);
            if (float.IsFinite(invLength) && invLength > 0.0f)
            {
                return new vec3(value.X * invLength, value.Y * invLength, value.Z * invLength);
            }

            float len = MathF.Sqrt(lengthSquared);
            if (len > 0.0f)
            {
                return new vec3(value.X / len, value.Y / len, value.Z / len);
            }
        }

        return Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryNormalize(vec3 value, out vec3 result) =>
        TryNormalize(value, out result, 0.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryNormalize(vec3 value, out vec3 result, float tolerance)
    {
        float lengthSquared = value.LengthSquared();
        float tolSquared = tolerance > 0.0f ? tolerance * tolerance : 0.0f;
        if (lengthSquared > tolSquared && lengthSquared > 0.0f)
        {
            float invLength = 1.0f / MathF.Sqrt(lengthSquared);
            if (float.IsFinite(invLength) && invLength > 0.0f)
            {
                result = new vec3(value.X * invLength, value.Y * invLength, value.Z * invLength);
                return true;
            }

            float len = MathF.Sqrt(lengthSquared);
            if (len > 0.0f)
            {
                result = new vec3(value.X / len, value.Y / len, value.Z / len);
                return true;
            }
        }

        result = Zero;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Reflect(vec3 vector, vec3 normal)
    {
        float dot2 = Dot(vector, normal) * 2.0f;
        return new vec3(
            vector.X - (normal.X * dot2),
            vector.Y - (normal.Y * dot2),
            vector.Z - (normal.Z * dot2)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Project(vec3 vector, vec3 onNormal)
    {
        float sqrMag = Dot(onNormal, onNormal);
        if (sqrMag <= 0.0f)
        {
            return Zero;
        }

        float scale = Dot(vector, onNormal) / sqrMag;
        return onNormal * scale;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 ProjectOnPlane(vec3 vector, vec3 planeNormal) =>
        vector - Project(vector, planeNormal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Slide(vec3 vector, vec3 normal)
    {
        float normalSq = Dot(normal, normal);
        if (normalSq <= 0.0f)
        {
            return vector;
        }

        return vector - (normal * (Dot(vector, normal) / normalSq));
    }

    public static float Angle(vec3 from, vec3 to)
    {
        float maxA = MathF.Max(MathF.Abs(from.X), MathF.Max(MathF.Abs(from.Y), MathF.Abs(from.Z)));
        float maxB = MathF.Max(MathF.Abs(to.X), MathF.Max(MathF.Abs(to.Y), MathF.Abs(to.Z)));
        if (maxA <= 0.0f || maxB <= 0.0f)
        {
            return 0.0f;
        }

        vec3 a = from * (1.0f / maxA);
        vec3 b = to * (1.0f / maxB);

        float lenA = a.Length();
        float lenB = b.Length();
        if (lenA <= 0.0f || lenB <= 0.0f)
        {
            return 0.0f;
        }

        float cos = Dot(a, b) / (lenA * lenB);
        return MathF.Acos(Math.Clamp(cos, -1.0f, 1.0f));
    }

    public static float SignedAngle(vec3 from, vec3 to, vec3 axis)
    {
        float unsignedAngle = Angle(from, to);
        vec3 cross = Cross(from, to);
        float sign = Dot(axis, cross);
        return sign < 0.0f ? -unsignedAngle : unsignedAngle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Clamp(vec3 value, vec3 min, vec3 max)
    {
        float x = value.X;
        x = (x < min.X) ? min.X : x;
        x = (x > max.X) ? max.X : x;

        float y = value.Y;
        y = (y < min.Y) ? min.Y : y;
        y = (y > max.Y) ? max.Y : y;

        float z = value.Z;
        z = (z < min.Z) ? min.Z : z;
        z = (z > max.Z) ? max.Z : z;

        return new vec3(x, y, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 ClampNative(vec3 value, vec3 min, vec3 max) => Clamp(value, min, max);

    public static vec3 ClampLength(vec3 value, float minLength, float maxLength)
    {
        float sqrMagnitude = value.LengthSquared();
        if (sqrMagnitude <= 0.0f)
        {
            return minLength > 0.0f ? new vec3(minLength, 0.0f, 0.0f) : Zero;
        }

        float magnitude = MathF.Sqrt(sqrMagnitude);
        if (magnitude < minLength)
        {
            return value * (minLength / magnitude);
        }

        if (magnitude > maxLength)
        {
            return value * (maxLength / magnitude);
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Min(vec3 left, vec3 right) =>
        new(MathF.Min(left.X, right.X), MathF.Min(left.Y, right.Y), MathF.Min(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Max(vec3 left, vec3 right) =>
        new(MathF.Max(left.X, right.X), MathF.Max(left.Y, right.Y), MathF.Max(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MinNative(vec3 left, vec3 right) => Min(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MaxNative(vec3 left, vec3 right) => Max(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MinNumber(vec3 left, vec3 right) =>
        new(float.MinNumber(left.X, right.X), float.MinNumber(left.Y, right.Y), float.MinNumber(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MaxNumber(vec3 left, vec3 right) =>
        new(float.MaxNumber(left.X, right.X), float.MaxNumber(left.Y, right.Y), float.MaxNumber(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MinMagnitude(vec3 left, vec3 right) =>
        new(float.MinMagnitude(left.X, right.X), float.MinMagnitude(left.Y, right.Y), float.MinMagnitude(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MaxMagnitude(vec3 left, vec3 right) =>
        new(float.MaxMagnitude(left.X, right.X), float.MaxMagnitude(left.Y, right.Y), float.MaxMagnitude(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MinMagnitudeNumber(vec3 left, vec3 right) =>
        new(float.MinMagnitudeNumber(left.X, right.X), float.MinMagnitudeNumber(left.Y, right.Y), float.MinMagnitudeNumber(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MaxMagnitudeNumber(vec3 left, vec3 right) =>
        new(float.MaxMagnitudeNumber(left.X, right.X), float.MaxMagnitudeNumber(left.Y, right.Y), float.MaxMagnitudeNumber(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 CopySign(vec3 value, vec3 sign) =>
        new(MathF.CopySign(value.X, sign.X), MathF.CopySign(value.Y, sign.Y), MathF.CopySign(value.Z, sign.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 FusedMultiplyAdd(vec3 left, vec3 right, vec3 addend) =>
        new(
            MathF.FusedMultiplyAdd(left.X, right.X, addend.X),
            MathF.FusedMultiplyAdd(left.Y, right.Y, addend.Y),
            MathF.FusedMultiplyAdd(left.Z, right.Z, addend.Z)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MultiplyAddEstimate(vec3 left, vec3 right, vec3 addend) =>
        FusedMultiplyAdd(left, right, addend);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Abs(vec3 value) =>
        new(MathF.Abs(value.X), MathF.Abs(value.Y), MathF.Abs(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Sqrt(vec3 value) =>
        new(MathF.Sqrt(value.X), MathF.Sqrt(value.Y), MathF.Sqrt(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 SquareRoot(vec3 value) => Sqrt(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Sin(vec3 vector) =>
        new(MathF.Sin(vector.X), MathF.Sin(vector.Y), MathF.Sin(vector.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Cos(vec3 vector) =>
        new(MathF.Cos(vector.X), MathF.Cos(vector.Y), MathF.Cos(vector.Z));

    public static (vec3 Sin, vec3 Cos) SinCos(vec3 vector)
    {
        (float sX, float cX) = MathF.SinCos(vector.X);
        (float sY, float cY) = MathF.SinCos(vector.Y);
        (float sZ, float cZ) = MathF.SinCos(vector.Z);
        return (new vec3(sX, sY, sZ), new vec3(cX, cY, cZ));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Exp(vec3 vector) =>
        new(MathF.Exp(vector.X), MathF.Exp(vector.Y), MathF.Exp(vector.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Log(vec3 vector) =>
        new(MathF.Log(vector.X), MathF.Log(vector.Y), MathF.Log(vector.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Log2(vec3 vector) =>
        new(MathF.Log2(vector.X), MathF.Log2(vector.Y), MathF.Log2(vector.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Hypot(vec3 x, vec3 y) => Sqrt((x * x) + (y * y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 DegreesToRadians(vec3 degrees) => degrees * (MathF.PI / 180.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 RadiansToDegrees(vec3 radians) => radians * (180.0f / MathF.PI);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Floor(vec3 value) =>
        new(MathF.Floor(value.X), MathF.Floor(value.Y), MathF.Floor(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Ceiling(vec3 value) =>
        new(MathF.Ceiling(value.X), MathF.Ceiling(value.Y), MathF.Ceiling(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Round(vec3 value) =>
        new(MathF.Round(value.X), MathF.Round(value.Y), MathF.Round(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Truncate(vec3 value) =>
        new(MathF.Truncate(value.X), MathF.Truncate(value.Y), MathF.Truncate(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Lerp(vec3 a, vec3 b, float t) =>
        new(
            MathF.FusedMultiplyAdd(b.X - a.X, t, a.X),
            MathF.FusedMultiplyAdd(b.Y - a.Y, t, a.Y),
            MathF.FusedMultiplyAdd(b.Z - a.Z, t, a.Z)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 LerpClamped(vec3 a, vec3 b, float t) =>
        Lerp(a, b, Math.Clamp(t, 0.0f, 1.0f));

    public static vec3 SmoothStep(vec3 from, vec3 to, float amount)
    {
        amount = Math.Clamp(amount, 0.0f, 1.0f);
        float factor = amount * amount * (3.0f - (2.0f * amount));
        return Lerp(from, to, factor);
    }

    public static vec3 Slerp(vec3 a, vec3 b, float t)
    {
        float lenA = a.Length();
        float lenB = b.Length();

        if (lenA <= 0.0f || lenB <= 0.0f)
        {
            return Lerp(a, b, t);
        }

        float targetLength = MathF.FusedMultiplyAdd(lenB - lenA, t, lenA);
        vec3 unitA = a / lenA;
        vec3 unitB = b / lenB;

        float dot = Math.Clamp(Dot(unitA, unitB), -1.0f, 1.0f);

        if (dot > 0.9995f)
        {
            vec3 result = Lerp(unitA, unitB, t);
            return Normalize(result) * targetLength;
        }

        if (dot < -0.9995f)
        {
            vec3 ortho = GetOrthogonal(unitA);
            float angle = MathF.PI * t;
            return ((unitA * MathF.Cos(angle)) + (ortho * MathF.Sin(angle))) * targetLength;
        }

        float theta = MathF.Acos(dot);
        float sinTheta = MathF.Sin(theta);
        float factorA = MathF.Sin((1.0f - t) * theta) / sinTheta;
        float factorB = MathF.Sin(t * theta) / sinTheta;

        return ((unitA * factorA) + (unitB * factorB)) * targetLength;
    }

    public static vec3 GetOrthogonal(vec3 v)
    {
        if (v.LengthSquared() <= 0.0f)
        {
            return UnitX;
        }

        vec3 abs = Abs(v);
        vec3 other = abs.X < abs.Y
            ? (abs.X < abs.Z ? UnitX : UnitZ)
            : (abs.Y < abs.Z ? UnitY : UnitZ);
        return Normalize(Cross(v, other));
    }

    public static vec3 MoveTowards(vec3 current, vec3 target, float maxDistanceDelta)
    {
        float dx = target.X - current.X;
        float dy = target.Y - current.Y;
        float dz = target.Z - current.Z;
        float distSq = (dx * dx) + (dy * dy) + (dz * dz);

        if (distSq == 0.0f || (maxDistanceDelta >= 0.0f && distSq <= maxDistanceDelta * maxDistanceDelta))
        {
            return target;
        }

        float dist = MathF.Sqrt(distSq);
        float ratio = maxDistanceDelta / dist;
        return new vec3(
            MathF.FusedMultiplyAdd(dx, ratio, current.X),
            MathF.FusedMultiplyAdd(dy, ratio, current.Y),
            MathF.FusedMultiplyAdd(dz, ratio, current.Z)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 TransformCustom<TTransformer, TState>(vec3 value, scoped ref TState state)
        where TTransformer : IVectorTransformer<TState>
        where TState : allows ref struct =>
        TTransformer.Transform(value, ref state);

    public static vec3 SumAll(params ReadOnlySpan<vec3> vectors)
    {
        vec3 accumulator = Zero;
        for (int i = 0; i < vectors.Length; i++)
        {
            accumulator += vectors[i];
        }

        return accumulator;
    }

    public static vec3 Average(params ReadOnlySpan<vec3> vectors)
    {
        if (vectors.IsEmpty)
        {
            NumericThrowHelper.ThrowArgumentException("Average requires at least one vector.");
        }

        return SumAll(vectors) / (float)vectors.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sum(vec3 vector) => vector.X + vector.Y + vector.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool All(vec3 vector) => vector.X != 0.0f && vector.Y != 0.0f && vector.Z != 0.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AllWhereAllBitsSet(vec3 vector) =>
        BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF &&
        BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF &&
        BitConverter.SingleToUInt32Bits(vector.Z) == 0xFFFF_FFFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Any(vec3 vector) => vector.X != 0.0f || vector.Y != 0.0f || vector.Z != 0.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AnyWhereAllBitsSet(vec3 vector) =>
        BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF ||
        BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF ||
        BitConverter.SingleToUInt32Bits(vector.Z) == 0xFFFF_FFFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool None(vec3 vector) => !Any(vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NoneWhereAllBitsSet(vec3 vector) => !AnyWhereAllBitsSet(vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count(vec3 vector)
    {
        int count = 0;
        if (vector.X != 0.0f)
        {
            count++;
        }

        if (vector.Y != 0.0f)
        {
            count++;
        }

        if (vector.Z != 0.0f)
        {
            count++;
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountWhereAllBitsSet(vec3 vector)
    {
        int count = 0;
        if (BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF)
        {
            count++;
        }

        if (BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF)
        {
            count++;
        }

        if (BitConverter.SingleToUInt32Bits(vector.Z) == 0xFFFF_FFFF)
        {
            count++;
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAll(vec3 left, vec3 right) => left == right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAny(vec3 left, vec3 right) =>
        left.X == right.X || left.Y == right.Y || left.Z == right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(vec3 other, float tolerance) =>
        MathF.Abs(X - other.X) <= tolerance &&
        MathF.Abs(Y - other.Y) <= tolerance &&
        MathF.Abs(Z - other.Z) <= tolerance;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equals(vec3 left, vec3 right, float tolerance = DefaultTolerance) =>
        left.Equals(right, tolerance);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool BitEquals(vec3 other) =>
        BitConverter.SingleToUInt32Bits(X) == BitConverter.SingleToUInt32Bits(other.X) &&
        BitConverter.SingleToUInt32Bits(Y) == BitConverter.SingleToUInt32Bits(other.Y) &&
        BitConverter.SingleToUInt32Bits(Z) == BitConverter.SingleToUInt32Bits(other.Z);
}
