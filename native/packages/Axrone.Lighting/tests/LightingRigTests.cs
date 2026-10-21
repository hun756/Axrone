namespace Axrone.Lighting.Tests;

public sealed class LightingRigTests : IDisposable
{
    private readonly LightingRig _rig = RigBuilder<StageInitial>.Create().Seal().Build();

    public void Dispose() => _rig.Dispose();

    [Fact]
    public void AddGetRemove_VersionBumps()
    {
        RigVersion v0 = _rig.Version;
        LightId id = _rig.NextLightId();
        var light = LightData.CreateDirectional(
            id, 1,
            new Vec3(0, -1, 0), Vec3.One, Vec3.Zero,
            new LightIntensity(1.5f), LightPriority.Zero);

        _rig.Add(light);
        _rig.Version.Should().Be(v0.Next());
        _rig.Count.Should().Be(1);
        _rig.TryGet(id, out LightData got).Should().BeTrue();
        got.Intensity.Value.Should().Be(1.5f);

        var brighter = LightData.CreateDirectional(
            id, 1,
            new Vec3(0, -1, 0), Vec3.One, Vec3.Zero,
            new LightIntensity(3.0f), LightPriority.Zero);
        _rig.Update(brighter);
        _rig.TryGet(id, out LightData updated).Should().BeTrue();
        updated.Intensity.Value.Should().Be(3.0f);

        _rig.Remove(id).Should().BeTrue();
        _rig.Count.Should().Be(0);
        _rig.Contains(id).Should().BeFalse();
    }

    [Fact]
    public void Add_Duplicate_Throws()
    {
        LightId id = _rig.NextLightId();
        _rig.Add(LightData.CreatePoint(
            id, 1, Vec3.Zero, Vec3.One,
            LightIntensity.Default, LightPriority.Zero,
            LightRange.Default, AttenuationFactor.Default));
        Action act = () => _rig.Add(LightData.CreatePoint(
            id, 1, Vec3.Zero, Vec3.One,
            LightIntensity.Default, LightPriority.Zero,
            LightRange.Default, AttenuationFactor.Default));
        act.Should().Throw<LightingValidationException>().WithMessage("*duplicate-id*");
    }

    [Fact]
    public void Resolver_Influence_SelectsNearest()
    {
        var resolver = new LightingFrameResolver(new LightingCapacity(
            maxDirectionalLights: 0, maxPointLights: 1, maxSpotLights: 0, maxLocalLights: 1));

        using var rig = RigBuilder<StageInitial>.Create().Seal().Build();
        rig.Add(LightData.CreatePoint(
            rig.NextLightId(), 1, new Vec3(0, 0, 100), Vec3.One,
            new LightIntensity(1.0f), LightPriority.Zero,
            new LightRange(1000.0f), AttenuationFactor.Default));
        rig.Add(LightData.CreatePoint(
            rig.NextLightId(), 2, new Vec3(0, 0, 1), Vec3.One,
            new LightIntensity(1.0f), LightPriority.Zero,
            new LightRange(1000.0f), AttenuationFactor.Default));

        LightingSelectionState state = resolver.Resolve(
            rig, new ResolveOptions(new Vec3(0, 0, 0), LightSortStrategy.Influence));

        state.Stats.SelectedPointCount.Should().Be(1);
        state.Stats.TotalPointCount.Should().Be(2);
        state.Stats.OmittedPointCount.Should().Be(1);
        state.PointPositions.Span[2].Should().BeApproximately(1.0f, 1e-5f);
        state.PointRanges.Span[0].Should().BeApproximately(1000.0f, 1e-3f);
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesLights()
    {
        using var rig = RigBuilder<StageInitial>.Create().Seal().Build();
        LightId id = rig.NextLightId();
        rig.Add(LightData.CreateDirectional(
            id, 1, new Vec3(0, -1, 0), Vec3.One, Vec3.Zero,
            new LightIntensity(2.0f), LightPriority.Zero));

        string json = LightingSerializer.ToJson(rig);
        DeserializationResult parsed = LightingSerializer.ParseSafe(json);

        parsed.Success.Should().BeTrue();
        parsed.Issues.Should().BeEmpty();
        parsed.Rig.Should().NotBeNull();
        parsed.Rig!.Count.Should().Be(1);
        parsed.Rig.TryGet(id, out LightData light).Should().BeTrue();
        light.Intensity.Value.Should().BeApproximately(2.0f, 1e-6f);
        parsed.Rig.Dispose();
    }

    [Fact]
    public void ParseSafe_BadVersion_Fails()
    {
        DeserializationResult result = LightingSerializer.ParseSafe("{\"version\":2,\"rigId\":1,\"lights\":[]}");
        result.Success.Should().BeFalse();
        result.Issues.Should().ContainSingle().Which.Code.Should().Be("lighting.parse.version");
    }

    [Fact]
    public void ForEach_VisitsAllLights()
    {
        using var rig = RigBuilder<StageInitial>.Create().Seal().Build();
        rig.Add(LightData.CreateDirectional(
            rig.NextLightId(), 1, Vec3.UnitY, Vec3.One, Vec3.Zero,
            LightIntensity.Default, LightPriority.Zero));
        rig.Add(LightData.CreatePoint(
            rig.NextLightId(), 2, Vec3.Zero, Vec3.One,
            LightIntensity.Default, LightPriority.Zero,
            LightRange.Default, AttenuationFactor.Default));

        var state = new CounterState();
        rig.ForEach<Counter, CounterState>(ref state);
        state.Count.Should().Be(2);
    }

    private struct CounterState
    {
        public int Count;
    }

    private struct Counter : ILightVisitor<CounterState>
    {
        public static void Visit(ref readonly LightData light, scoped ref CounterState state) => state.Count++;
    }
}
