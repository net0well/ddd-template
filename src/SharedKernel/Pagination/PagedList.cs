namespace SharedKernel.Pagination;

/// <summary>
/// One page of a larger result set. EF Core-free, so repositories in Domain can return it.
/// </summary>
public sealed class PagedList<T>
{
    public PagedList(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);

        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    public IReadOnlyList<T> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages => (int)((TotalCount + (long)PageSize - 1) / PageSize);

    public bool HasNextPage => (long)Page * PageSize < TotalCount;

    public bool HasPreviousPage => Page > 1;

    /// <summary>Projects the items, keeping the paging information.</summary>
    public PagedList<TResult> Map<TResult>(Func<T, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        return new PagedList<TResult>([.. Items.Select(selector)], Page, PageSize, TotalCount);
    }
}
