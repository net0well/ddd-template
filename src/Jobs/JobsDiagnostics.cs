using System.Diagnostics;

namespace Jobs;

public static class JobsDiagnostics
{
    /// <summary>Name to register in OpenTelemetry tracing (<c>AddSource</c>) to get one trace per job execution.</summary>
    public const string ActivitySourceName = "Jobs";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>Readable type name for logs and spans: <c>CommandJob&lt;SendWelcomeEmailCommand&gt;</c>.</summary>
    internal static string DisplayName(Type type) =>
        type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]}<{string.Join(", ", type.GetGenericArguments().Select(DisplayName))}>"
            : type.Name;
}
