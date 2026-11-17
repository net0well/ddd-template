using System.Globalization;
using System.Reflection;
using Application;
using Infrastructure;
using Jobs;
using Microsoft.Extensions.Options;
using Serilog;
using Web.Api;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

// Catches startup failures before the configured logger exists.
Log.Logger = new LoggerConfiguration().WriteTo.Console(formatProvider: CultureInfo.InvariantCulture).CreateLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.AddSerilogLogging();

    builder.Services
        .AddApplication()
        .AddPresentation()
        .AddInfrastructure(builder.Configuration)
        .AddJobs(builder.Configuration);

    builder.Services.AddSwaggerWithAuth();
    builder.Services.AddObservability(builder.Configuration, builder.Environment.ApplicationName);
    builder.Services.AddForwardedHeadersInternal(builder.Configuration);
    builder.Services.AddRateLimitingInternal(builder.Configuration);
    builder.Services.AddCorsInternal(builder.Configuration);
    builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

    WebApplication app = builder.Build();

    // First: everything after it (HSTS, logs, rate limiter) must see the real client IP and scheme.
    app.UseForwardedHeaders();

    // Migrations are not applied here: run `dotnet ef database update` (see README).
    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerWithUi();
    }
    else
    {
        app.UseHsts();
    }

    // Order matters: correlation id and request logging wrap everything, so they also see handled errors.
    app.UseRequestContextLogging();
    app.UseRequestLogging(app.Services.GetRequiredService<IOptions<JobsOptions>>().Value.Dashboard.Path);
    app.UseExceptionHandler();
    app.UseStatusCodePages(EmptyErrorResponseWriter.WriteAsync);
    app.UseSecurityHeaders();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapEndpoints();
    app.MapHealthCheckEndpoints();
    app.MapJobsDashboard();

    await app.RunAsync();
}
// HostAbortedException is how the EF Core tools stop the host after reading the model; not a failure.
// Logged for the log sinks and rethrown so the process fails and tests show the real cause.
#pragma warning disable S2139
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "Application terminated unexpectedly");
    throw;
}
#pragma warning restore S2139
finally
{
    await Log.CloseAndFlushAsync();
}
