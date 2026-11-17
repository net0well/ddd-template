using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Web.Api.Extensions;

internal static class ForwardedHeadersExtensions
{
    private const string SectionName = "ForwardedHeaders";

    /// <summary>
    /// Behind a reverse proxy the connection comes from the proxy: the client IP (used by the rate limiter and
    /// the logs) and scheme are read from X-Forwarded-For/Proto, but only when sent by a trusted proxy.
    /// Loopback is trusted by default; add the proxy addresses or networks in configuration.
    /// </summary>
    internal static IServiceCollection AddForwardedHeadersInternal(this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(SectionName);

        string[] proxies = section.GetSection("KnownProxies").Get<string[]>() ?? [];
        string[] networks = section.GetSection("KnownNetworks").Get<string[]>() ?? [];

        // Parsed here so a typo fails at startup instead of silently trusting nobody.
        IPAddress[] knownProxies = [.. proxies.Select(IPAddress.Parse)];
        System.Net.IPNetwork[] knownNetworks = [.. networks.Select(network => System.Net.IPNetwork.Parse(network))];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = section.GetValue<int?>("ForwardLimit") ?? 1;

            foreach (IPAddress proxy in knownProxies)
            {
                options.KnownProxies.Add(proxy);
            }

            foreach (System.Net.IPNetwork network in knownNetworks)
            {
                options.KnownIPNetworks.Add(network);
            }
        });

        return services;
    }

    /// <summary>The client address (after the forwarded headers), with IPv4-mapped IPv6 shown as plain IPv4.</summary>
    internal static string? GetClientIp(this HttpContext httpContext)
    {
        IPAddress? address = httpContext.Connection.RemoteIpAddress;

        return (address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4() : address)?.ToString();
    }
}
