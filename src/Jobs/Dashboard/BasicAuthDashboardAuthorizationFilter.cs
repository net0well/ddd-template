using System.Security.Cryptography;
using System.Text;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace Jobs.Dashboard;

/// <summary>
/// Protects the Hangfire dashboard with HTTP Basic authentication (credentials from <c>Jobs:Dashboard</c>).
/// Serve it over HTTPS outside local development: Basic auth sends the password in every request.
/// </summary>
internal sealed class BasicAuthDashboardAuthorizationFilter(string username, string password)
    : IDashboardAuthorizationFilter
{
    private const string Scheme = "Basic ";

    private readonly byte[] _usernameHash = Hash(username);
    private readonly byte[] _passwordHash = Hash(password);

    public bool Authorize(DashboardContext context)
    {
        HttpContext httpContext = context.GetHttpContext();

        if (TryReadCredentials(httpContext.Request.Headers.Authorization, out string user, out string pass))
        {
            // Both comparisons always run, in constant time, so timing reveals nothing.
            bool userMatches = CryptographicOperations.FixedTimeEquals(Hash(user), _usernameHash);
            bool passwordMatches = CryptographicOperations.FixedTimeEquals(Hash(pass), _passwordHash);

            if (userMatches && passwordMatches)
            {
                return true;
            }
        }

        // Makes the browser show its log-in prompt (Hangfire answers 401).
        httpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"Jobs\", charset=\"UTF-8\"";

        return false;
    }

    private static bool TryReadCredentials(string? header, out string user, out string pass)
    {
        user = string.Empty;
        pass = string.Empty;

        if (header is null || !header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string encoded = header[Scheme.Length..].Trim();
        byte[] buffer = new byte[encoded.Length];

        if (!Convert.TryFromBase64String(encoded, buffer, out int written))
        {
            return false;
        }

        string decoded = Encoding.UTF8.GetString(buffer, 0, written);
        int separator = decoded.IndexOf(':', StringComparison.Ordinal);

        if (separator < 0)
        {
            return false;
        }

        user = decoded[..separator];
        pass = decoded[(separator + 1)..];

        return true;
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
