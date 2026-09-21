namespace Axrone.Utility.Builders;

/// <summary>Exceptionless build outcome; carries the failure reason on the cold path only.</summary>
public readonly record struct BuilderDiagnostic(BuilderStatusCode Code, string Message)
{
    /// <summary>Successful build outcome.</summary>
    public static BuilderDiagnostic Ok => new(BuilderStatusCode.Success, string.Empty);

    /// <summary>Failed build outcome.</summary>
    public static BuilderDiagnostic Fail(BuilderStatusCode code, string message) => new(code, message);

    /// <summary>Whether the build succeeded.</summary>
    public bool IsSuccess => Code == BuilderStatusCode.Success;
}
