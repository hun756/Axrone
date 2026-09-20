namespace Axrone.Event;

/// <summary>Why an envelope was moved to the dead-letter queue.</summary>
public enum DeadLetterReason : byte
{
    HandlerException = 0,
    DispatchFailure = 1,
    ValidationFailed = 2,
    PoisonMessage = 3,
}
