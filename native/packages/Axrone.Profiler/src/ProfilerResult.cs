namespace Axrone.Profiler;

/// <summary>Profiler session result.</summary>
public sealed record ProfilerResult(
    string SessionId,
    long StartedAtMs,
    long EndedAtMs,
    long TotalSamples,
    HistogramStatistics Metrics,
    IReadOnlyList<FlameGraphNode> FlameGraphs,
    IReadOnlyList<TimelineRecordEvent> TimelineEvents);

/// <summary>Perfetto trace phase.</summary>
public enum PerfettoPhase : byte
{
    /// <summary>Slice begin.</summary>
    Begin = 0,

    /// <summary>Slice end.</summary>
    End = 1,

    /// <summary>Complete slice with duration.</summary>
    Complete = 2,

    /// <summary>Instant event.</summary>
    Instant = 3,
}

/// <summary>One Perfetto trace event.</summary>
public readonly record struct PerfettoEvent(
    long TimestampNs,
    string Name,
    PerfettoPhase Phase,
    long DurationNs = 0L,
    string? Category = null)
{
    /// <summary>Phase letter for the trace format.</summary>
    public char PhaseLetter => Phase switch
    {
        PerfettoPhase.Begin => 'B',
        PerfettoPhase.End => 'E',
        PerfettoPhase.Complete => 'X',
        _ => 'i',
    };
}

/// <summary>Perfetto trace container.</summary>
public sealed record PerfettoTraceData(
    string Version,
    string SessionId,
    long StartedAtNs,
    long? EndedAtNs,
    IReadOnlyList<PerfettoEvent> Events);

/// <summary>Records complete slices and exports Perfetto trace JSON.</summary>
public sealed class PerfettoSerializer : IDisposable
{
    private readonly List<PerfettoEvent> _events = new();
    private readonly Lock _gate = new();
    private readonly string _sessionId = Guid.NewGuid().ToString("N");
    private readonly long _startedAtNs;
    private long? _endedAtNs;
    private int _disposed;

    /// <summary>Creates a serializer.</summary>
    public PerfettoSerializer()
    {
        _startedAtNs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * TimeConversion.Milliseconds;
    }

    /// <summary>Trace session id.</summary>
    public string SessionId => _sessionId;

    /// <summary>Records one complete slice.</summary>
    public void RecordSample(string name, long durationNs = 0L, string? category = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (durationNs < 0L)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(durationNs), "Duration must not be negative.");
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _events.Add(new PerfettoEvent(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * TimeConversion.Milliseconds,
                name,
                PerfettoPhase.Complete,
                durationNs,
                category));
        }
    }

    /// <summary>Closes the session and snapshots the trace.</summary>
    public PerfettoTraceData GetPerfettoData()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _endedAtNs ??= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * TimeConversion.Milliseconds;
            return new PerfettoTraceData("2", _sessionId, _startedAtNs, _endedAtNs, _events.ToArray());
        }
    }

    /// <summary>Serializes the trace to JSON without reflection.</summary>
    public string ToJson()
    {
        PerfettoTraceData data = GetPerfettoData();
        var builder = new System.Text.StringBuilder(256);
        builder.Append("{\"version\":\"");
        builder.Append(data.Version);
        builder.Append("\",\"sessionId\":\"");
        builder.Append(data.SessionId);
        builder.Append("\",\"startedAtNs\":");
        builder.Append(data.StartedAtNs.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"endedAtNs\":");
        builder.Append(data.EndedAtNs?.ToString(CultureInfo.InvariantCulture) ?? "null");
        builder.Append(",\"events\":[");
        for (int i = 0; i < data.Events.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            PerfettoEvent e = data.Events[i];
            builder.Append("{\"timestampNs\":");
            builder.Append(e.TimestampNs.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"name\":\"");
            AppendEscaped(builder, e.Name);
            builder.Append("\",\"ph\":\"");
            builder.Append(e.PhaseLetter);
            builder.Append("\",\"dur\":");
            builder.Append(e.DurationNs.ToString(CultureInfo.InvariantCulture));
            if (e.Category is not null)
            {
                builder.Append(",\"cat\":\"");
                AppendEscaped(builder, e.Category);
                builder.Append('\"');
            }

            builder.Append('}');
        }

        builder.Append("]}");
        return builder.ToString();
    }

    private static void AppendEscaped(System.Text.StringBuilder builder, string value)
    {
        foreach (char c in value)
        {
            switch (c)
            {
                case '\"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < 0x20)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _events.Clear();
        }

        GC.SuppressFinalize(this);
    }
}
