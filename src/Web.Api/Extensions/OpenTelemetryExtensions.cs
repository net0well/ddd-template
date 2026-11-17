using System.Diagnostics;
using Jobs;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Web.Api.Extensions;

internal static class OpenTelemetryExtensions
{
    internal static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        string jobsDashboardPath = configuration["Jobs:Dashboard:Path"] ?? "/hangfire";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new DropParentlessClientSpansSampler()))
                .AddSource(JobsDiagnostics.ActivitySourceName)
                .AddAspNetCoreInstrumentation(options => options.Filter = httpContext =>
                    !httpContext.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) &&
                    !httpContext.Request.Path.StartsWithSegments(jobsDashboardPath, StringComparison.OrdinalIgnoreCase))
                .AddHttpClientInstrumentation()
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        // Traces and metrics go to any OTLP backend (Grafana/Tempo, Aspire dashboard, Jaeger…) when
        // OTEL_EXPORTER_OTLP_ENDPOINT is set. Logs are exported by Serilog (see LoggingExtensions).
        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            services.AddOpenTelemetry().UseOtlpExporter();
        }

        return services;
    }

    // A client span (database query, outgoing HTTP call) without a parent comes from background polling —
    // Hangfire checking its queues every few seconds. Dropping those keeps the traces of requests and jobs
    // visible. Child spans still follow their parent's decision.
    private sealed class DropParentlessClientSpansSampler : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) =>
            new(samplingParameters.Kind == ActivityKind.Client ? SamplingDecision.Drop : SamplingDecision.RecordAndSample);
    }
}
