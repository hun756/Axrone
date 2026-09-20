namespace Axrone.Utility.Builders;

/// <summary>Outcome category of a builder operation.</summary>
public enum BuilderStatusCode : byte
{
    Success = 0,
    MissingRequiredField = 1,
    ValidationFailed = 2,
}
