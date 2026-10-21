namespace Axrone.Shapes2D;

/// <summary>Trim-safe manual JSON serialization for shapes and paints.</summary>
public static class ShapeSerializer
{
    /// <summary>Creates a kind-prefixed FNV-1a fingerprint.</summary>
    public static string CreateFingerprint(Shape2D shape)
    {
        var buffer = new ArrayBufferWriter<byte>(512);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            SerializeShape(writer, shape);
        }

        using var hasher = HashEngine.CreateHasher(HashAlgorithmId.Fnv1a32);
        hasher.Append(buffer.WrittenSpan);
        string hex = hasher.GetCurrentDigest().ToString();
        return FormattableString.Invariant($"{shape.Kind.ToString().ToLowerInvariant()}:{hex}");
    }

    /// <summary>Serializes a shape to JSON.</summary>
    public static string StringifyShape(Shape2D shape)
    {
        var buffer = new ArrayBufferWriter<byte>(512);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            SerializeShape(writer, shape);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Serializes a paint to JSON.</summary>
    public static string StringifyPaint(ShapePaint paint)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            SerializePaint(writer, paint);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Writes a paint.</summary>
    public static void SerializePaint(Utf8JsonWriter writer, ShapePaint paint)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(paint);
        writer.WriteStartObject();
        switch (paint)
        {
            case SolidPaint solid:
                writer.WriteString("type", "paint/solid");
                writer.WriteStartArray("color");
                writer.WriteNumberValue(solid.Color.R);
                writer.WriteNumberValue(solid.Color.G);
                writer.WriteNumberValue(solid.Color.B);
                writer.WriteNumberValue(solid.Color.A);
                writer.WriteEndArray();
                break;
            case LinearGradientPaint linear:
                writer.WriteString("type", "paint/linear-gradient");
                writer.WriteStartArray("start");
                writer.WriteNumberValue(linear.Start.X);
                writer.WriteNumberValue(linear.Start.Y);
                writer.WriteEndArray();
                writer.WriteStartArray("end");
                writer.WriteNumberValue(linear.End.X);
                writer.WriteNumberValue(linear.End.Y);
                writer.WriteEndArray();
                SerializeStops(writer, linear.Stops);
                writer.WriteString("spread", FormatSpread(linear.Spread));
                writer.WriteString("colorSpace", FormatColorSpace(linear.ColorSpace));
                writer.WriteString("units", FormatUnits(linear.Units));
                break;
            case RadialGradientPaint radial:
                writer.WriteString("type", "paint/radial-gradient");
                writer.WriteStartArray("center");
                writer.WriteNumberValue(radial.Center.X);
                writer.WriteNumberValue(radial.Center.Y);
                writer.WriteEndArray();
                writer.WriteNumber("radius", radial.Radius);
                SerializeStops(writer, radial.Stops);
                writer.WriteString("spread", FormatSpread(radial.Spread));
                writer.WriteString("colorSpace", FormatColorSpace(radial.ColorSpace));
                writer.WriteString("units", FormatUnits(radial.Units));
                break;
            default:
                throw new UnreachableException();
        }

        writer.WriteEndObject();
    }

    private static void SerializeStops(Utf8JsonWriter writer, ImmutableArray<GradientStop> stops)
    {
        writer.WriteStartArray("stops");
        foreach (GradientStop stop in stops)
        {
            writer.WriteStartObject();
            writer.WriteNumber("offset", stop.Offset);
            writer.WriteStartArray("color");
            writer.WriteNumberValue(stop.Color.R);
            writer.WriteNumberValue(stop.Color.G);
            writer.WriteNumberValue(stop.Color.B);
            writer.WriteNumberValue(stop.Color.A);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>Writes a stroke.</summary>
    public static void SerializeStroke(Utf8JsonWriter writer, ShapeStroke stroke)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject();
        writer.WriteNumber("width", stroke.Width);
        writer.WriteString("alignment", FormatAlignment(stroke.Alignment));
        writer.WritePropertyName("paint");
        SerializePaint(writer, stroke.Paint);
        writer.WriteEndObject();
    }

    /// <summary>Writes a shape.</summary>
    public static void SerializeShape(Utf8JsonWriter writer, Shape2D shape)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(shape);
        writer.WriteStartObject();

        if (shape.Fill is not null)
        {
            writer.WritePropertyName("fill");
            SerializePaint(writer, shape.Fill);
        }
        else
        {
            writer.WriteNull("fill");
        }

        if (shape.Stroke.HasValue)
        {
            writer.WritePropertyName("stroke");
            SerializeStroke(writer, shape.Stroke.Value);
        }
        else
        {
            writer.WriteNull("stroke");
        }

        writer.WriteNumber("opacity", shape.Opacity);
        writer.WriteBoolean("visible", shape.Visible);
        if (!string.IsNullOrEmpty(shape.Name))
        {
            writer.WriteString("name", shape.Name);
        }

        switch (shape)
        {
            case RectangleShape rect:
                writer.WriteString("type", "shape/rectangle");
                writer.WriteNumber("x", rect.X);
                writer.WriteNumber("y", rect.Y);
                writer.WriteNumber("width", rect.Width);
                writer.WriteNumber("height", rect.Height);
                break;
            case CircleShape circle:
                writer.WriteString("type", "shape/circle");
                writer.WriteNumber("cx", circle.Cx);
                writer.WriteNumber("cy", circle.Cy);
                writer.WriteNumber("radius", circle.Radius);
                break;
            case EllipseShape ellipse:
                writer.WriteString("type", "shape/ellipse");
                writer.WriteNumber("cx", ellipse.Cx);
                writer.WriteNumber("cy", ellipse.Cy);
                writer.WriteNumber("radiusX", ellipse.RadiusX);
                writer.WriteNumber("radiusY", ellipse.RadiusY);
                break;
            case TriangleShape tri:
                writer.WriteString("type", "shape/triangle");
                writer.WriteStartArray("a");
                writer.WriteNumberValue(tri.A.X);
                writer.WriteNumberValue(tri.A.Y);
                writer.WriteEndArray();
                writer.WriteStartArray("b");
                writer.WriteNumberValue(tri.B.X);
                writer.WriteNumberValue(tri.B.Y);
                writer.WriteEndArray();
                writer.WriteStartArray("c");
                writer.WriteNumberValue(tri.C.X);
                writer.WriteNumberValue(tri.C.Y);
                writer.WriteEndArray();
                break;
            case LineShape line:
                writer.WriteString("type", "shape/line");
                writer.WriteStartArray("start");
                writer.WriteNumberValue(line.Start.X);
                writer.WriteNumberValue(line.Start.Y);
                writer.WriteEndArray();
                writer.WriteStartArray("end");
                writer.WriteNumberValue(line.End.X);
                writer.WriteNumberValue(line.End.Y);
                writer.WriteEndArray();
                break;
            case PolygonShape poly:
                writer.WriteString("type", "shape/polygon");
                writer.WriteStartArray("points");
                foreach (Vec2 p in poly.Outer.Points)
                {
                    writer.WriteStartArray();
                    writer.WriteNumberValue(p.X);
                    writer.WriteNumberValue(p.Y);
                    writer.WriteEndArray();
                }

                writer.WriteEndArray();
                writer.WriteStartArray("holes");
                foreach (PolygonRing h in poly.Holes)
                {
                    writer.WriteStartArray();
                    foreach (Vec2 hp in h.Points)
                    {
                        writer.WriteStartArray();
                        writer.WriteNumberValue(hp.X);
                        writer.WriteNumberValue(hp.Y);
                        writer.WriteEndArray();
                    }

                    writer.WriteEndArray();
                }

                writer.WriteEndArray();
                writer.WriteString("winding", poly.Outer.Winding == PolygonWinding.Ccw ? "ccw" : "cw");
                writer.WriteBoolean("closed", poly.Closed);
                writer.WriteBoolean("convex", poly.Convex);
                break;
        }

        writer.WriteEndObject();
    }

    /// <summary>Deserializes a shape from JSON.</summary>
    public static Shape2D DeserializeShape(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        return DeserializeShape(doc.RootElement);
    }

    /// <summary>Deserializes a shape from a JSON element.</summary>
    public static Shape2D DeserializeShape(JsonElement root)
    {
        if (!root.TryGetProperty("type", out JsonElement typeElem))
        {
            ThrowHelper.ThrowSerialization("Missing 'type' discriminator.");
        }

        string? type = typeElem.GetString();

        ShapePaint? fill = root.TryGetProperty("fill", out JsonElement fillElem) && fillElem.ValueKind == JsonValueKind.Object
            ? DeserializePaint(fillElem)
            : null;

        ShapeStroke? stroke = root.TryGetProperty("stroke", out JsonElement strokeElem) && strokeElem.ValueKind == JsonValueKind.Object
            ? DeserializeStroke(strokeElem)
            : null;

        float opacity = root.TryGetProperty("opacity", out JsonElement opElem) ? opElem.GetSingle() : 1.0f;
        bool visible = !root.TryGetProperty("visible", out JsonElement visElem) || visElem.GetBoolean();
        string? name = root.TryGetProperty("name", out JsonElement nameElem) ? nameElem.GetString() : null;

        Shape2D shape = type switch
        {
            "shape/rectangle" => ShapeFactory.CreateRectangle(
                root.GetProperty("x").GetSingle(),
                root.GetProperty("y").GetSingle(),
                root.GetProperty("width").GetSingle(),
                root.GetProperty("height").GetSingle(),
                fill, stroke, opacity, visible, name),
            "shape/circle" => ShapeFactory.CreateCircle(
                root.GetProperty("cx").GetSingle(),
                root.GetProperty("cy").GetSingle(),
                root.GetProperty("radius").GetSingle(),
                fill, stroke, opacity, visible, name),
            "shape/ellipse" => ShapeFactory.CreateEllipse(
                root.GetProperty("cx").GetSingle(),
                root.GetProperty("cy").GetSingle(),
                root.GetProperty("radiusX").GetSingle(),
                root.GetProperty("radiusY").GetSingle(),
                fill, stroke, opacity, visible, name),
            "shape/triangle" => ShapeFactory.CreateTriangle(
                ParsePoint(root.GetProperty("a")),
                ParsePoint(root.GetProperty("b")),
                ParsePoint(root.GetProperty("c")),
                fill, stroke, opacity, visible, name),
            "shape/line" => ShapeFactory.CreateLine(
                ParsePoint(root.GetProperty("start")),
                ParsePoint(root.GetProperty("end")),
                stroke.HasValue ? new ShapeStroke(stroke.Value.Paint, stroke.Value.Width, ShapeStrokeAlignment.Center) : null,
                opacity, visible, name),
            "shape/polygon" => ParsePolygon(root, fill, stroke, opacity, visible, name),
            _ => throw new SerializationException($"Unknown shape type: '{type}'."),
        };

        return shape;
    }

    private static PolygonShape ParsePolygon(JsonElement root, ShapePaint? fill, ShapeStroke? stroke, float opacity, bool visible, string? name)
    {
        JsonElement.ArrayEnumerator ptsArray = root.GetProperty("points").EnumerateArray();
        var outer = new List<Vec2>();
        foreach (JsonElement p in ptsArray)
        {
            outer.Add(ParsePoint(p));
        }

        var holes = new List<PolygonRing>();
        if (root.TryGetProperty("holes", out JsonElement holesElem) && holesElem.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement h in holesElem.EnumerateArray())
            {
                var hpts = new List<Vec2>();
                foreach (JsonElement hp in h.EnumerateArray())
                {
                    hpts.Add(ParsePoint(hp));
                }

                holes.Add(new PolygonRing(hpts.ToArray(), PolygonWinding.Cw));
            }
        }

        PolygonWinding winding = root.TryGetProperty("winding", out JsonElement wElem) && wElem.GetString() == "cw"
            ? PolygonWinding.Cw
            : PolygonWinding.Ccw;

        return ShapeFactory.CreatePolygon(outer.ToArray(), holes.ToArray(), winding, fill, stroke, opacity, visible, name);
    }

    /// <summary>Deserializes a paint from a JSON element.</summary>
    public static ShapePaint DeserializePaint(JsonElement root)
    {
        string? type = root.GetProperty("type").GetString();
        return type switch
        {
            "paint/solid" => new SolidPaint(ParseColor(root.GetProperty("color"))),
            "paint/linear-gradient" => new LinearGradientPaint(
                ParsePoint(root.GetProperty("start")),
                ParsePoint(root.GetProperty("end")),
                ParseStops(root.GetProperty("stops")),
                ParseSpread(root.GetProperty("spread").GetString()),
                ParseColorSpace(root.GetProperty("colorSpace").GetString()),
                ParseUnits(root.GetProperty("units").GetString())),
            "paint/radial-gradient" => new RadialGradientPaint(
                ParsePoint(root.GetProperty("center")),
                root.GetProperty("radius").GetSingle(),
                ParseStops(root.GetProperty("stops")),
                ParseSpread(root.GetProperty("spread").GetString()),
                ParseColorSpace(root.GetProperty("colorSpace").GetString()),
                ParseUnits(root.GetProperty("units").GetString())),
            _ => throw new SerializationException($"Unknown paint type: '{type}'."),
        };
    }

    /// <summary>Deserializes a stroke from a JSON element.</summary>
    public static ShapeStroke DeserializeStroke(JsonElement root)
    {
        float width = root.GetProperty("width").GetSingle();
        ShapeStrokeAlignment alignment = ParseAlignment(root.GetProperty("alignment").GetString());
        ShapePaint paint = DeserializePaint(root.GetProperty("paint"));
        return new ShapeStroke(paint, width, alignment);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vec2 ParsePoint(JsonElement elem) => new(elem[0].GetSingle(), elem[1].GetSingle());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ResolvedColor ParseColor(JsonElement elem) => new(elem[0].GetSingle(), elem[1].GetSingle(), elem[2].GetSingle(), elem[3].GetSingle());

    private static GradientStop[] ParseStops(JsonElement elem)
    {
        var stops = new List<GradientStop>(elem.GetArrayLength());
        foreach (JsonElement item in elem.EnumerateArray())
        {
            float offset = item.GetProperty("offset").GetSingle();
            ResolvedColor color = ParseColor(item.GetProperty("color"));
            stops.Add(new GradientStop(offset, color));
        }

        return stops.ToArray();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatAlignment(ShapeStrokeAlignment a) => a switch
    {
        ShapeStrokeAlignment.Inside => "inside",
        ShapeStrokeAlignment.Outside => "outside",
        _ => "center",
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ShapeStrokeAlignment ParseAlignment(string? value) => value switch
    {
        "inside" => ShapeStrokeAlignment.Inside,
        "outside" => ShapeStrokeAlignment.Outside,
        _ => ShapeStrokeAlignment.Center,
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatSpread(GradientSpread s) => s switch
    {
        GradientSpread.Repeat => "repeat",
        GradientSpread.Reflect => "reflect",
        _ => "pad",
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GradientSpread ParseSpread(string? s) => s switch
    {
        "repeat" => GradientSpread.Repeat,
        "reflect" => GradientSpread.Reflect,
        _ => GradientSpread.Pad,
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatColorSpace(GradientColorSpace c) => c switch
    {
        GradientColorSpace.LinearSrgb => "linear-srgb",
        GradientColorSpace.Hsl => "hsl",
        GradientColorSpace.Lab => "lab",
        _ => "srgb",
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GradientColorSpace ParseColorSpace(string? c) => c switch
    {
        "linear-srgb" => GradientColorSpace.LinearSrgb,
        "hsl" => GradientColorSpace.Hsl,
        "lab" => GradientColorSpace.Lab,
        _ => GradientColorSpace.Srgb,
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatUnits(GradientUnits u) => u == GradientUnits.Local ? "local" : "shape-bounds";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GradientUnits ParseUnits(string? u) => u == "local" ? GradientUnits.Local : GradientUnits.ShapeBounds;
}
