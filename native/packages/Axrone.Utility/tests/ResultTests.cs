using Enterprise.Patterns.Result;

namespace Axrone.Utility.Tests.ResultMonad;

public class ResultTests
{
    // --- Unit ---

    [Fact]
    public void Unit_Value_And_Default_Are_Equal()
    {
        Unit.Value.Should().Be(Unit.Default);
    }

    [Fact]
    public void Unit_CompareTo_Returns_Zero()
    {
        Unit.Value.CompareTo(Unit.Default).Should().Be(0);
    }

    [Fact]
    public void Unit_ToString_Returns_Parens()
    {
        Unit.Value.ToString().Should().Be("()");
    }

    [Fact]
    public async Task Unit_TaskValue_Completes_Successfully()
    {
        var result = await Unit.TaskValue;
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task Unit_ValueTask_Completes_Successfully()
    {
        var result = await Unit.ValueTask;
        result.Should().Be(Unit.Value);
    }

    // --- Error ---

    [Fact]
    public void Error_None_Has_Empty_Code_And_Message()
    {
        Error.None.Code.Should().BeEmpty();
        Error.None.Message.Should().BeEmpty();
        Error.None.Type.Should().Be(ErrorType.Failure);
    }

    [Fact]
    public void Error_Factory_Creates_Correct_Type()
    {
        Error.Validation().Type.Should().Be(ErrorType.Validation);
        Error.NotFound().Type.Should().Be(ErrorType.NotFound);
        Error.Conflict().Type.Should().Be(ErrorType.Conflict);
        Error.Unauthorized().Type.Should().Be(ErrorType.Unauthorized);
        Error.Forbidden().Type.Should().Be(ErrorType.Forbidden);
        Error.Timeout().Type.Should().Be(ErrorType.Timeout);
        Error.Cancelled().Type.Should().Be(ErrorType.Cancelled);
        Error.Critical().Type.Should().Be(ErrorType.Critical);
        Error.Unexpected().Type.Should().Be(ErrorType.Unexpected);
        Error.Failure().Type.Should().Be(ErrorType.Failure);
    }

    [Fact]
    public void Error_FromException_Captures_Exception_Info()
    {
        var ex = new InvalidOperationException("test error");
        var error = Error.FromException(ex);

        error.Code.Should().Be("InvalidOperationException");
        error.Message.Should().Be("test error");
        error.Type.Should().Be(ErrorType.Unexpected);
        error.Exception.Should().BeSameAs(ex);
    }

    [Fact]
    public void Error_FromException_With_Custom_Code()
    {
        var ex = new InvalidOperationException("test");
        var error = Error.FromException(ex, "Custom.Code");
        error.Code.Should().Be("Custom.Code");
    }

    [Fact]
    public void Error_Equals_Same_Code_Message_Type()
    {
        var a = Error.Validation("CODE", "msg");
        var b = Error.Validation("CODE", "msg");
        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Error_Not_Equals_Different_Code()
    {
        var a = Error.Validation("CODE1", "msg");
        var b = Error.Validation("CODE2", "msg");
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Error_With_Metadata()
    {
        var meta = new Dictionary<string, object?> { ["key"] = "value" };
        var error = new Error("CODE", "msg", ErrorType.Failure, meta);
        error.Metadata.Should().ContainKey("key");
    }

    [Fact]
    public void Error_Null_Metadata_Defaults_To_Empty()
    {
        var error = new Error("CODE", "msg");
        error.Metadata.Should().NotBeNull();
        error.Metadata.Count.Should().Be(0);
    }

    // --- ErrorCollection ---

    [Fact]
    public void ErrorCollection_Empty_Has_Zero_Count()
    {
        ErrorCollection.Empty.Count.Should().Be(0);
    }

    [Fact]
    public void ErrorCollection_Single_Error()
    {
        var ec = new ErrorCollection(Error.NotFound());
        ec.Count.Should().Be(1);
        ec[0].Type.Should().Be(ErrorType.NotFound);
        ec.TopError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void ErrorCollection_Multiple_Errors()
    {
        var ec = new ErrorCollection(new[] { Error.Validation(), Error.NotFound() });
        ec.Count.Should().Be(2);
        ec[0].Type.Should().Be(ErrorType.Validation);
        ec[1].Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void ErrorCollection_From_List()
    {
        var list = new List<Error> { Error.Validation(), Error.NotFound(), Error.Conflict() };
        var ec = new ErrorCollection(list);
        ec.Count.Should().Be(3);
    }

    [Fact]
    public void ErrorCollection_From_Null_List_Is_Empty()
    {
        var ec = new ErrorCollection((List<Error>?)null);
        ec.Count.Should().Be(0);
    }

    [Fact]
    public void ErrorCollection_From_Empty_Array_Is_Empty()
    {
        var ec = new ErrorCollection(Array.Empty<Error>());
        ec.Count.Should().Be(0);
    }

    [Fact]
    public void ErrorCollection_Index_Out_Of_Range_Throws()
    {
        var ec = new ErrorCollection(Error.Validation());
        var act = () => ec[5];
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ErrorCollection_Implicit_From_Single_Error()
    {
        ErrorCollection ec = Error.Validation();
        ec.Count.Should().Be(1);
    }

    [Fact]
    public void ErrorCollection_AsSpan_Works()
    {
        var ec = new ErrorCollection(new[] { Error.Validation(), Error.NotFound() });
        ReadOnlySpan<Error> span = ec.AsSpan();
        span.Length.Should().Be(2);
    }

    [Fact]
    public void ErrorCollection_AsSpan_Single()
    {
        var ec = new ErrorCollection(Error.Validation());
        ReadOnlySpan<Error> span = ec.AsSpan();
        span.Length.Should().Be(1);
    }

    [Fact]
    public void ErrorCollection_AsSpan_Empty()
    {
        ReadOnlySpan<Error> span = ErrorCollection.Empty.AsSpan();
        span.Length.Should().Be(0);
    }

    [Fact]
    public void ErrorCollection_ToArray_Returns_Copy()
    {
        var ec = new ErrorCollection(new[] { Error.Validation(), Error.NotFound() });
        var arr = ec.ToArray();
        arr.Length.Should().Be(2);
    }

    [Fact]
    public void ErrorCollection_Enumerate_With_Custom_Enumerator()
    {
        var ec = new ErrorCollection(new[] { Error.Validation(), Error.NotFound(), Error.Conflict() });
        var items = new List<Error>();
        foreach (var e in ec)
        {
            items.Add(e);
        }
        items.Count.Should().Be(3);
    }

    [Fact]
    public void ErrorCollection_Equals_Same_Contents()
    {
        var a = new ErrorCollection(new[] { Error.Validation(), Error.NotFound() });
        var b = new ErrorCollection(new[] { Error.Validation(), Error.NotFound() });
        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void ErrorCollection_Not_Equals_Different_Contents()
    {
        var a = new ErrorCollection(new[] { Error.Validation() });
        var b = new ErrorCollection(new[] { Error.NotFound() });
        a.Equals(b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }

    // --- Result (non-generic) ---

    [Fact]
    public void Result_Success_IsSuccess()
    {
        var r = Result.Success();
        r.IsSuccess.Should().BeTrue();
        r.IsFailure.Should().BeFalse();
    }

    [Fact]
    public void Result_Failure_IsFailure()
    {
        var r = Result.Failure(Error.Validation());
        r.IsSuccess.Should().BeFalse();
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Result_Success_TopError_Is_None()
    {
        Result.Success().TopError.Should().Be(Error.None);
    }

    [Fact]
    public void Result_Failure_TopError_Returns_First_Error()
    {
        var r = Result.Failure(Error.NotFound());
        r.TopError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Result_Failure_Multiple_Errors()
    {
        var r = Result.Failure(Error.Validation(), Error.NotFound());
        r.Errors.Count.Should().Be(2);
    }

    [Fact]
    public void Result_Create_Condition_True_Returns_Success()
    {
        var r = Result.Create(true, Error.Validation());
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Result_Create_Condition_False_Returns_Failure()
    {
        var r = Result.Create(false, Error.Validation());
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Result_Create_With_Factory_Lazy()
    {
        bool called = false;
        var r = Result.Create(true, () => { called = true; return Error.Validation(); });
        r.IsSuccess.Should().BeTrue();
        called.Should().BeFalse();
    }

    [Fact]
    public void Result_Try_Success()
    {
        var r = Result.Try(() => { });
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Result_Try_Exception_Returns_Failure()
    {
        var r = Result.Try(() => throw new InvalidOperationException("boom"));
        r.IsFailure.Should().BeTrue();
        r.TopError.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Result_Try_With_Value_Success()
    {
        var r = Result.Try(() => 42);
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(42);
    }

    [Fact]
    public void Result_Try_With_Value_Exception()
    {
        var r = Result.Try<int>(() => throw new Exception("boom"));
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Result_TryAsync_Task_Success()
    {
        Func<Task> action = async () => await Task.CompletedTask;
        var r = await Result.TryAsync(action);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Result_TryAsync_Task_Exception()
    {
        Func<Task> action = async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("async boom");
        };
        var r = await Result.TryAsync(action);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Result_TryAsync_ValueTask_Success()
    {
        var r = await Result.TryAsync(() => new ValueTask());
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Result_TryAsync_With_Value_Task()
    {
        Func<Task<int>> func = async () => { await Task.Yield(); return 42; };
        var r = await Result.TryAsync(func);
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(42);
    }

    [Fact]
    public async Task Result_TryAsync_With_Value_ValueTask()
    {
        var r = await Result.TryAsync(() => new ValueTask<int>(42));
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(42);
    }

    [Fact]
    public void Result_Combine_All_Success()
    {
        var r = Result.Combine([Result.Success(), Result.Success()]);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Result_Combine_With_Failures()
    {
        var r = Result.Combine([Result.Success(), Result.Failure(Error.Validation()), Result.Failure(Error.NotFound())]);
        r.IsFailure.Should().BeTrue();
        r.Errors.Count.Should().Be(2);
    }

    [Fact]
    public void Result_Combine_Enumerable()
    {
        var results = new List<Result> { Result.Success(), Result.Failure(Error.Validation()) };
        var r = Result.Combine(results);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Result_Match_Success()
    {
        var output = Result.Success().Match(() => "ok", _ => "fail");
        output.Should().Be("ok");
    }

    [Fact]
    public void Result_Match_Failure()
    {
        var output = Result.Failure(Error.Validation()).Match(() => "ok", _ => "fail");
        output.Should().Be("fail");
    }

    [Fact]
    public void Result_Match_Void_Success()
    {
        int called = 0;
        Result.Success().Match(() => called = 1, _ => called = 2);
        called.Should().Be(1);
    }

    [Fact]
    public void Result_Match_Void_Failure()
    {
        int called = 0;
        Result.Failure(Error.Validation()).Match(() => called = 1, _ => called = 2);
        called.Should().Be(2);
    }

    [Fact]
    public void Result_Map_Success_Chains()
    {
        var r = Result.Success().Map(() => 42);
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(42);
    }

    [Fact]
    public void Result_Map_Failure_Propagates()
    {
        var r = Result.Failure(Error.Validation()).Map(() => 42);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Result_Map_To_Result_Success()
    {
        var r = Result.Success().Map(Result.Success);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Result_Bind_Success()
    {
        var r = Result.Success().Bind(() => Result<int>.Success(42));
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(42);
    }

    [Fact]
    public void Result_Bind_Failure_ShortCircuits()
    {
        var r = Result.Failure(Error.Validation()).Bind(() => Result<int>.Success(42));
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Result_Bind_NonGeneric_Success()
    {
        var r = Result.Success().Bind(Result.Success);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Result_Tap_Success_Executes()
    {
        int val = 0;
        var r = Result.Success().Tap(() => val = 1);
        val.Should().Be(1);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Result_Tap_Failure_Does_Not_Execute()
    {
        int val = 0;
        Result.Failure(Error.Validation()).Tap(() => val = 1);
        val.Should().Be(0);
    }

    [Fact]
    public void Result_TapError_Failure_Executes()
    {
        int val = 0;
        Result.Failure(Error.Validation()).TapError(_ => val = 1);
        val.Should().Be(1);
    }

    [Fact]
    public void Result_TapError_Success_Does_Not_Execute()
    {
        int val = 0;
        Result.Success().TapError(_ => val = 1);
        val.Should().Be(0);
    }

    [Fact]
    public void Result_OrElse_Success_Returns_Self()
    {
        var r = Result.Success().OrElse(Result.Success());
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Result_OrElse_Failure_Returns_Fallback()
    {
        var fallback = Result.Success();
        var r = Result.Failure(Error.Validation()).OrElse(fallback);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Result_OrElse_Factory()
    {
        var r = Result.Failure(Error.Validation()).OrElse(Result.Success);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Result_Implicit_From_Error()
    {
        Result r = Error.Validation();
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Result_Implicit_From_Error_Array()
    {
        Result r = new[] { Error.Validation(), Error.NotFound() };
        r.IsFailure.Should().BeTrue();
        r.Errors.Count.Should().Be(2);
    }

    [Fact]
    public void Result_Implicit_From_ErrorCollection()
    {
        ErrorCollection ec = new ErrorCollection(Error.Validation());
        Result r = ec;
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Result_Equals_Success()
    {
        Result.Success().Equals(Result.Success()).Should().BeTrue();
    }

    [Fact]
    public void Result_Equals_Failure_Same_Errors()
    {
        Result.Failure(Error.Validation()).Equals(Result.Failure(Error.Validation())).Should().BeTrue();
    }

    [Fact]
    public void Result_Not_Equals_Success_Vs_Failure()
    {
        (Result.Success() == Result.Failure(Error.Validation())).Should().BeFalse();
    }

    [Fact]
    public void Result_GetHashCode_Differs_Success_Vs_Failure()
    {
        Result.Success().GetHashCode().Should().NotBe(Result.Failure(Error.Validation()).GetHashCode());
    }

    // --- Result<T> ---

    [Fact]
    public void ResultT_Success_Has_Value()
    {
        var r = Result<int>.Success(42);
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(42);
    }

    [Fact]
    public void ResultT_Failure_Access_Value_Throws()
    {
        var r = Result<int>.Failure(Error.NotFound());
        var act = () => r.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResultT_TryGetValue_Success()
    {
        var r = Result<int>.Success(42);
        r.TryGetValue(out var val).Should().BeTrue();
        val.Should().Be(42);
    }

    [Fact]
    public void ResultT_TryGetValue_Failure()
    {
        var r = Result<int>.Failure(Error.NotFound());
        r.TryGetValue(out _).Should().BeFalse();
    }

    [Fact]
    public void ResultT_TryUnwrap_Success()
    {
        var r = Result<int>.Success(42);
        r.TryUnwrap(out var val, out var errors).Should().BeTrue();
        val.Should().Be(42);
    }

    [Fact]
    public void ResultT_TryUnwrap_Failure()
    {
        var r = Result<int>.Failure(Error.NotFound());
        r.TryUnwrap(out _, out var errors).Should().BeFalse();
        errors.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ResultT_ValueOrDefault_Success()
    {
        Result<int>.Success(42).ValueOrDefault(0).Should().Be(42);
    }

    [Fact]
    public void ResultT_ValueOrDefault_Failure()
    {
        Result<int>.Failure(Error.NotFound()).ValueOrDefault(99).Should().Be(99);
    }

    [Fact]
    public void ResultT_ValueOr_Factory()
    {
        var r = Result<int>.Failure(Error.NotFound());
        r.ValueOr(_ => 99).Should().Be(99);
    }

    [Fact]
    public void ResultT_Match_Success()
    {
        var r = Result<int>.Success(42);
        r.Match(v => v * 2, _ => 0).Should().Be(84);
    }

    [Fact]
    public void ResultT_Match_Failure()
    {
        var r = Result<int>.Failure(Error.NotFound());
        r.Match(v => v * 2, _ => 0).Should().Be(0);
    }

    [Fact]
    public void ResultT_Match_Void()
    {
        int captured = 0;
        Result<int>.Success(42).Match(v => captured = v, _ => { });
        captured.Should().Be(42);
    }

    [Fact]
    public void ResultT_Map_Success()
    {
        var r = Result<int>.Success(42).Map(v => v.ToString());
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be("42");
    }

    [Fact]
    public void ResultT_Map_Failure()
    {
        var r = Result<int>.Failure(Error.NotFound()).Map(v => v.ToString());
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ResultT_Bind_Success()
    {
        var r = Result<int>.Success(42).Bind(v => Result<string>.Success(v.ToString()));
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be("42");
    }

    [Fact]
    public void ResultT_Bind_Failure()
    {
        var r = Result<int>.Failure(Error.NotFound()).Bind(v => Result<string>.Success(v.ToString()));
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ResultT_Bind_To_NonGeneric_Success()
    {
        var r = Result<int>.Success(42).Bind(v => Result.Success());
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ResultT_BiMap_Success()
    {
        var r = Result<int>.Success(42).BiMap(v => v.ToString(), _ => Error.Critical());
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be("42");
    }

    [Fact]
    public void ResultT_BiMap_Failure()
    {
        var r = Result<int>.Failure(Error.NotFound()).BiMap(v => v.ToString(), _ => Error.Critical());
        r.IsFailure.Should().BeTrue();
        r.TopError.Type.Should().Be(ErrorType.Critical);
    }

    [Fact]
    public void ResultT_Ensure_Predicate_Passes()
    {
        var r = Result<int>.Success(42).Ensure(v => v > 0, Error.Validation());
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ResultT_Ensure_Predicate_Fails()
    {
        var r = Result<int>.Success(-1).Ensure(v => v > 0, Error.Validation());
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ResultT_Ensure_Factory()
    {
        var r = Result<int>.Success(-1).Ensure(v => v > 0, v => Error.Validation(message: $"Value {v} is negative"));
        r.IsFailure.Should().BeTrue();
        r.TopError.Message.Should().Contain("-1");
    }

    [Fact]
    public void ResultT_Ensure_On_Failure_Returns_Self()
    {
        var r = Result<int>.Failure(Error.NotFound()).Ensure(v => v > 0, Error.Validation());
        r.IsFailure.Should().BeTrue();
        r.TopError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void ResultT_Tap_Success()
    {
        int captured = 0;
        var r = Result<int>.Success(42).Tap(v => captured = v);
        captured.Should().Be(42);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ResultT_Tap_Failure()
    {
        int captured = 0;
        Result<int>.Failure(Error.NotFound()).Tap(v => captured = v);
        captured.Should().Be(0);
    }

    [Fact]
    public void ResultT_TapError()
    {
        ErrorCollection captured = default;
        Result<int>.Failure(Error.NotFound()).TapError(e => captured = e);
        captured.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ResultT_OrElse_Fallback()
    {
        var fallback = Result<int>.Success(99);
        var r = Result<int>.Failure(Error.NotFound()).OrElse(in fallback);
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(99);
    }

    [Fact]
    public void ResultT_OrElse_Factory()
    {
        var r = Result<int>.Failure(Error.NotFound()).OrElse(() => Result<int>.Success(99));
        r.Value.Should().Be(99);
    }

    [Fact]
    public void ResultT_OrElse_Error_Factory()
    {
        var r = Result<int>.Failure(Error.NotFound()).OrElse(_ => Result<int>.Success(99));
        r.Value.Should().Be(99);
    }

    [Fact]
    public void ResultT_Implicit_From_Value()
    {
        Result<int> r = 42;
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(42);
    }

    [Fact]
    public void ResultT_Implicit_From_Error()
    {
        Result<int> r = Error.NotFound();
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ResultT_Implicit_To_NonGeneric()
    {
        Result r = Result<int>.Success(42);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ResultT_Implicit_To_NonGeneric_Failure()
    {
        Result r = Result<int>.Failure(Error.NotFound());
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ResultT_Equals_Same_Value()
    {
        Result<int>.Success(42).Equals(Result<int>.Success(42)).Should().BeTrue();
    }

    [Fact]
    public void ResultT_Not_Equals_Different_Value()
    {
        (Result<int>.Success(42) == Result<int>.Success(99)).Should().BeFalse();
    }

    [Fact]
    public void ResultT_Equals_Same_Failure()
    {
        Result<int>.Failure(Error.Validation()).Equals(Result<int>.Failure(Error.Validation())).Should().BeTrue();
    }

    [Fact]
    public void ResultT_Success_Factory_Via_Result_Class()
    {
        var r = Result.Success(42);
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(42);
    }

    [Fact]
    public void ResultT_Failure_Factory_Via_Result_Class()
    {
        var r = Result.Failure<int>(Error.NotFound());
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ResultT_Create_Condition()
    {
        Result.Create(true, 42, Error.NotFound()).Value.Should().Be(42);
        Result.Create(false, 42, Error.NotFound()).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ResultT_Create_Factories()
    {
        Result.Create(true, () => 42, () => Error.NotFound()).Value.Should().Be(42);
        Result.Create(false, () => 42, () => Error.NotFound()).IsFailure.Should().BeTrue();
    }

    // --- ResultExtensions ---

    [Fact]
    public void Select_Maps_Value()
    {
        var r = Result<int>.Success(42).Select(v => v.ToString());
        r.Value.Should().Be("42");
    }

    [Fact]
    public void SelectMany_Chains()
    {
        var r = Result<int>.Success(42)
            .SelectMany(
                v => Result<string>.Success(v.ToString()),
                (i, s) => $"{s}:{i}");
        r.Value.Should().Be("42:42");
    }

    [Fact]
    public void SelectMany_Failure_ShortCircuits()
    {
        var r = Result<int>.Failure(Error.NotFound())
            .SelectMany(
                v => Result<string>.Success(v.ToString()),
                (i, s) => $"{s}:{i}");
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Where_Predicate_Passes()
    {
        var r = Result<int>.Success(42).Where(v => v > 0);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Where_Predicate_Fails()
    {
        var r = Result<int>.Success(-1).Where(v => v > 0);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Zip_Both_Success()
    {
        var r = Result<int>.Success(1).Zip(Result<string>.Success("a"));
        r.Value.Should().Be((1, "a"));
    }

    [Fact]
    public void Zip_One_Failure()
    {
        var r = Result<int>.Success(1).Zip(Result<string>.Failure(Error.NotFound()));
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Zip_With_Merger()
    {
        var r = Result<int>.Success(1).Zip(Result<int>.Success(2), (a, b) => a + b);
        r.Value.Should().Be(3);
    }

    [Fact]
    public void Combine_Static_3()
    {
        var r = ResultExtensions.Combine(
            Result<int>.Success(1),
            Result<string>.Success("a"),
            Result<bool>.Success(true));
        r.Value.Should().Be((1, "a", true));
    }

    [Fact]
    public void Combine_Static_4()
    {
        var r = ResultExtensions.Combine(
            Result<int>.Success(1),
            Result<int>.Success(2),
            Result<int>.Success(3),
            Result<int>.Success(4));
        r.Value.Should().Be((1, 2, 3, 4));
    }

    [Fact]
    public void Combine_Static_5()
    {
        var r = ResultExtensions.Combine(
            Result<int>.Success(1),
            Result<int>.Success(2),
            Result<int>.Success(3),
            Result<int>.Success(4),
            Result<int>.Success(5));
        r.Value.Should().Be((1, 2, 3, 4, 5));
    }

    [Fact]
    public void Collect_All_Success()
    {
        var results = new[] { Result<int>.Success(1), Result<int>.Success(2), Result<int>.Success(3) };
        var r = results.Collect();
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void Collect_With_Failures()
    {
        var results = new[] { Result<int>.Success(1), Result<int>.Failure(Error.NotFound()), Result<int>.Success(3) };
        var r = results.Collect();
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Collect_Empty()
    {
        var r = Array.Empty<Result<int>>().Collect();
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().BeEmpty();
    }

    [Fact]
    public void Partition_Separates_Success_And_Failure()
    {
        var results = new[] { Result<int>.Success(1), Result<int>.Failure(Error.NotFound()), Result<int>.Success(3) };
        var (successes, failures) = results.Partition();
        successes.Should().BeEquivalentTo(new[] { 1, 3 });
        failures.Count.Should().Be(1);
    }

    // --- Async extensions ---

    [Fact]
    public async Task MapAsync_Result_Value_Task()
    {
        var r = Result<int>.Success(42);
        var mapped = await r.MapAsync(v => Task.FromResult(v.ToString()));
        mapped.Value.Should().Be("42");
    }

    [Fact]
    public async Task MapAsync_Result_Value_ValueTask()
    {
        var r = Result<int>.Success(42);
        var mapped = await r.MapAsync(v => new ValueTask<string>(v.ToString()));
        mapped.Value.Should().Be("42");
    }

    [Fact]
    public async Task MapAsync_Failure_Propagates()
    {
        var r = Result<int>.Failure(Error.NotFound());
        var mapped = await r.MapAsync(v => Task.FromResult(v.ToString()));
        mapped.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task BindAsync_Result_Value_Task()
    {
        var r = Result<int>.Success(42);
        var bound = await r.BindAsync(v => Task.FromResult(Result<string>.Success(v.ToString())));
        bound.Value.Should().Be("42");
    }

    [Fact]
    public async Task BindAsync_Result_Value_ValueTask()
    {
        var r = Result<int>.Success(42);
        var bound = await r.BindAsync(v => new ValueTask<Result<string>>(Result<string>.Success(v.ToString())));
        bound.Value.Should().Be("42");
    }

    [Fact]
    public async Task TapAsync_Result_Value_Task()
    {
        int captured = 0;
        var r = Result<int>.Success(42);
        var tapped = await r.TapAsync(v => { captured = v; return Task.CompletedTask; });
        captured.Should().Be(42);
        tapped.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task TapAsync_Result_Value_ValueTask()
    {
        int captured = 0;
        var r = Result<int>.Success(42);
        var tapped = await r.TapAsync(v => { captured = v; return new ValueTask(); });
        captured.Should().Be(42);
    }

    [Fact]
    public async Task TapErrorAsync_Failure()
    {
        int captured = 0;
        var r = Result<int>.Failure(Error.NotFound());
        await r.TapErrorAsync(_ => { captured = 1; return Task.CompletedTask; });
        captured.Should().Be(1);
    }

    [Fact]
    public async Task EnsureAsync_Predicate_Passes()
    {
        var r = await Result<int>.Success(42).EnsureAsync(v => Task.FromResult(v > 0), Error.Validation());
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureAsync_Predicate_Fails()
    {
        var r = await Result<int>.Success(-1).EnsureAsync(v => Task.FromResult(v > 0), Error.Validation());
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task MapAsync_On_Task()
    {
        var task = Task.FromResult(Result<int>.Success(42));
        var mapped = await task.MapAsync(v => v.ToString());
        mapped.Value.Should().Be("42");
    }

    [Fact]
    public async Task MapAsync_On_Task_With_Async_Mapper()
    {
        var task = Task.FromResult(Result<int>.Success(42));
        var mapped = await task.MapAsync(v => Task.FromResult(v.ToString()));
        mapped.Value.Should().Be("42");
    }

    [Fact]
    public async Task MapAsync_On_ValueTask()
    {
        var vt = new ValueTask<Result<int>>(Result<int>.Success(42));
        var mapped = await vt.MapAsync(v => v.ToString());
        mapped.Value.Should().Be("42");
    }

    [Fact]
    public async Task BindAsync_On_Task()
    {
        var task = Task.FromResult(Result<int>.Success(42));
        var bound = await task.BindAsync(v => Result<string>.Success(v.ToString()));
        bound.Value.Should().Be("42");
    }

    [Fact]
    public async Task BindAsync_On_Task_With_Async_Binder()
    {
        var task = Task.FromResult(Result<int>.Success(42));
        var bound = await task.BindAsync(v => Task.FromResult(Result<string>.Success(v.ToString())));
        bound.Value.Should().Be("42");
    }

    [Fact]
    public async Task TapAsync_On_Task()
    {
        int captured = 0;
        var task = Task.FromResult(Result<int>.Success(42));
        await task.TapAsync(v => captured = v);
        captured.Should().Be(42);
    }

    [Fact]
    public async Task MatchAsync_On_Task()
    {
        var task = Task.FromResult(Result<int>.Success(42));
        var output = await task.MatchAsync(v => v * 2, _ => 0);
        output.Should().Be(84);
    }

    [Fact]
    public async Task MatchAsync_On_Task_FullAsync()
    {
        var task = Task.FromResult(Result<int>.Success(42));
        var output = await task.MatchAsync(
            v => Task.FromResult(v * 2),
            _ => Task.FromResult(0));
        output.Should().Be(84);
    }

    [Fact]
    public async Task SelectAsync_On_Task()
    {
        var task = Task.FromResult(Result<int>.Success(42));
        var r = await task.Select(v => v.ToString());
        r.Value.Should().Be("42");
    }

    [Fact]
    public async Task SelectManyAsync_On_Task()
    {
        var task = Task.FromResult(Result<int>.Success(42));
        var r = await task.SelectMany(
            v => Task.FromResult(Result<string>.Success(v.ToString())),
            (i, s) => $"{s}:{i}");
        r.Value.Should().Be("42:42");
    }

    // --- JSON serialization ---

    [Fact]
    public void Result_Success_RoundTrips_Json()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            Converters = { new ResultJsonConverter() }
        };
        var json = System.Text.Json.JsonSerializer.Serialize(Result.Success(), options);
        json.Should().Contain("true");
    }

    [Fact]
    public void Result_Failure_RoundTrips_Json()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            Converters = { new ResultJsonConverter() }
        };
        var result = Result.Failure(Error.Validation("V001", "Invalid input"));
        var json = System.Text.Json.JsonSerializer.Serialize(result, options);
        json.Should().Contain("false");
        json.Should().Contain("V001");
    }

    [Fact]
    public void ErrorCollection_RoundTrips_Json()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            Converters = { new ErrorCollectionJsonConverter() }
        };
        var ec = new ErrorCollection(new[] { Error.Validation(), Error.NotFound() });
        var json = System.Text.Json.JsonSerializer.Serialize(ec, options);
        json.Should().Contain("Validation");
        json.Should().Contain("NotFound");
    }

    [Fact]
    public void Result_SourceGen_Context_Compiles()
    {
        ResultJsonSerializerContext.Default.ErrorArray.Should().NotBeNull();
        ResultJsonSerializerContext.Default.ListError.Should().NotBeNull();
    }
}
