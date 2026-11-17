using Application.Abstractions.DependencyInjection;
using Hangfire;
using Hangfire.PostgreSql;
using Jobs.Abstractions;
using Jobs.Dashboard;
using Jobs.Filters;
using Jobs.Recurring;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobs;

public static class DependencyInjection
{
    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JobsOptions>()
            .BindConfiguration(JobsOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(o => o.Server.WorkerCount is >= 1 and <= 100, "Jobs:Server:WorkerCount must be between 1 and 100.")
            .Validate(o => o.Server.QueuePollIntervalInSeconds is >= 1 and <= 300, "Jobs:Server:QueuePollIntervalInSeconds must be between 1 and 300.")
            .Validate(
                o => !o.Dashboard.Enabled || o.Dashboard is { Username.Length: > 0, Password.Length: >= 8 },
                "Jobs:Dashboard:Username and Jobs:Dashboard:Password (8+ characters) are required when the dashboard is enabled.")
            .ValidateOnStart();

        services.AddHangfire((serviceProvider, hangfire) =>
        {
            JobsOptions options = serviceProvider.GetRequiredService<IOptions<JobsOptions>>().Value;
            string connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Database")
                ?? throw new InvalidOperationException("The 'Database' connection string is required by the Jobs storage.");

            hangfire
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(
                    storage => storage.UseNpgsqlConnection(connectionString),
                    new PostgreSqlStorageOptions
                    {
                        SchemaName = options.Schema,
                        PrepareSchemaIfNecessary = true,
                        QueuePollInterval = TimeSpan.FromSeconds(options.Server.QueuePollIntervalInSeconds)
                    })
                .UseFilter(new JobObservabilityFilter(serviceProvider.GetRequiredService<ILoggerFactory>()));
        });

        // Whether this process runs a server is decided before the container exists, so it is read eagerly.
        JobsOptions jobsOptions = configuration.GetSection(JobsOptions.SectionName).Get<JobsOptions>() ?? new JobsOptions();

        if (jobsOptions.Server.Enabled)
        {
            services.AddHangfireServer((serviceProvider, server) =>
            {
                server.Queues = Queues.All;
                server.WorkerCount = serviceProvider.GetRequiredService<IOptions<JobsOptions>>().Value.Server.WorkerCount;
            });

            services.AddHostedService<RecurringJobsSynchronizer>();
        }

        services.Scan(scan => scan.FromAssembliesOf(typeof(DependencyInjection))
            .AddClasses(classes => classes.AssignableTo<IRecurringJob>(), publicOnly: false)
            .AsSelfWithInterfaces(type => type == typeof(IRecurringJob))
            .WithScopedLifetime());

        services.AddInjectableServices(typeof(DependencyInjection).Assembly);

        return services;
    }

    /// <summary>Maps the Hangfire dashboard (Basic auth) when <c>Jobs:Dashboard:Enabled</c> is true.</summary>
    public static IEndpointRouteBuilder MapJobsDashboard(this IEndpointRouteBuilder endpoints)
    {
        JobsDashboardOptions dashboard = endpoints.ServiceProvider
            .GetRequiredService<IOptions<JobsOptions>>().Value.Dashboard;

        if (!dashboard.Enabled)
        {
            return endpoints;
        }

        endpoints.MapHangfireDashboard(dashboard.Path, new DashboardOptions
        {
            DashboardTitle = "Jobs",
            DisplayStorageConnectionString = false,
            Authorization = [new BasicAuthDashboardAuthorizationFilter(dashboard.Username, dashboard.Password)]
        });

        return endpoints;
    }
}
