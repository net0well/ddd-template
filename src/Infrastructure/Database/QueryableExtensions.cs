using Microsoft.EntityFrameworkCore;
using SharedKernel.Pagination;

namespace Infrastructure.Database;

internal static class QueryableExtensions
{
    public static async Task<PagedList<T>> ToPagedListAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        int totalCount = await query.CountAsync(cancellationToken);

        List<T> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<T>(items, page, pageSize, totalCount);
    }

    /// <summary>Escapes <c>%</c>, <c>_</c> and <c>\</c> so user input is matched literally by LIKE/ILIKE.</summary>
    public static string EscapeLikePattern(this string value) =>
        value.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
