namespace Axrone.Animation.Tests;

public class ParameterTests
{
    private static ParameterStore Store() => new(new Dictionary<string, ParameterType>
    {
        ["speed"] = ParameterType.Float,
        ["count"] = ParameterType.Int,
        ["grounded"] = ParameterType.Bool,
        ["jump"] = ParameterType.Trigger,
    });

    [Fact]
    public void TypedAccess_RoundTrips()
    {
        ParameterStore store = Store();
        store.SetFloat("speed", 3.5f);
        store.GetFloat("speed").Should().Be(3.5f);
        store.SetInt("count", 7);
        store.GetInt("count").Should().Be(7);
        store.SetBool("grounded", true);
        store.GetBool("grounded").Should().BeTrue();
    }

    [Fact]
    public void DuplicatesAndTypeMismatch_Throw()
    {
        Action dup = () => { _ = new ParameterStore(new KeyValuePair<string, ParameterType>[] { new("a", ParameterType.Float), new("a", ParameterType.Int) }); };
        dup.Should().Throw<ValidationException>();

        ParameterStore store = Store();
        Action mismatch = () => store.SetFloat("count", 1.0f);
        mismatch.Should().Throw<StateMachineException>()
            .Where(e => e.Code == AnimationErrorCode.StateMachineTypeMismatch);

        Action missing = () => store.GetFloat("ghost");
        missing.Should().Throw<StateMachineException>()
            .Where(e => e.Code == AnimationErrorCode.StateMachineParameterNotFound);
    }

    [Fact]
    public void Triggers_ConsumeOnce()
    {
        ParameterStore store = Store();
        store.ConsumeTrigger("jump").Should().BeFalse();
        store.SetTrigger("jump");
        store.ConsumeTrigger("jump").Should().BeTrue();
        store.ConsumeTrigger("jump").Should().BeFalse();

        store.SetTrigger("jump");
        store.ClearTriggers();
        store.ConsumeTrigger("jump").Should().BeFalse();
    }

    [Fact]
    public void Conditions_EvaluatePerType()
    {
        ParameterStore store = Store();
        store.SetFloat("speed", 2.0f);
        store.SetInt("count", 3);
        store.SetBool("grounded", true);
        store.SetTrigger("jump");

        store.EvaluateCondition(new ParameterCondition("speed", ConditionOperator.GreaterThan, 1.0f)).Should().BeTrue();
        store.EvaluateCondition(new ParameterCondition("speed", ConditionOperator.Equal, 2.0f)).Should().BeTrue();
        store.EvaluateCondition(new ParameterCondition("count", ConditionOperator.LessThanOrEqual, 3.0f)).Should().BeTrue();
        store.EvaluateCondition(new ParameterCondition("grounded", ConditionOperator.Equal, 1.0f)).Should().BeTrue();
        store.EvaluateCondition(new ParameterCondition("jump", ConditionOperator.Equal, 0.0f)).Should().BeTrue();
        store.EvaluateCondition(new ParameterCondition("speed", ConditionOperator.LessThan, 1.0f)).Should().BeFalse();

        store.SetFloat("speed", float.NaN);
        store.GetFloat("speed").Should().Be(0.0f);
    }
}
