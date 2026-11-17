using SharedKernel.Pagination;

namespace Domain.UnitTests.SharedKernel;

public sealed class PagedListTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    public void TotalPages_Should_RoundUp(int totalCount, int pageSize, int expected)
    {
        // Act
        var page = new PagedList<int>([], 1, pageSize, totalCount);

        // Assert
        page.TotalPages.ShouldBe(expected);
    }

    [Fact]
    public void Navigation_Should_ReflectThePosition()
    {
        // Act
        var middle = new PagedList<int>([4, 5, 6], 2, 3, 9);
        var last = new PagedList<int>([7, 8, 9], 3, 3, 9);

        // Assert
        middle.HasPreviousPage.ShouldBeTrue();
        middle.HasNextPage.ShouldBeTrue();
        last.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void Map_Should_KeepThePagingInformation()
    {
        // Act
        PagedList<string> mapped = new PagedList<int>([1, 2], 2, 2, 5).Map(number => $"#{number}");

        // Assert
        mapped.Items.ShouldBe(["#1", "#2"]);
        (mapped.Page, mapped.PageSize, mapped.TotalCount).ShouldBe((2, 2, 5));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    public void Constructor_Should_RejectInvalidPaging(int page, int pageSize)
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => new PagedList<int>([], page, pageSize, 0));
    }
}
