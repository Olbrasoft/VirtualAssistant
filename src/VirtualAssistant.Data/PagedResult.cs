using Olbrasoft.Data.Paging;

namespace Olbrasoft.VirtualAssistant.Data;

/// <summary>
/// Represents a paginated list of items with total count metadata.
/// </summary>
/// <typeparam name="T">Type of items in the list.</typeparam>
/// <param name="items">The collection of items for the current page.</param>
/// <param name="totalCount">The total number of items across all pages.</param>
public class PagedResult<T>(IEnumerable<T> items, int totalCount) : List<T>(items), IPagedEnumerable<T>
{
    /// <summary>
    /// Gets the total number of items across all pages.
    /// </summary>
    public int TotalCount => totalCount;
}
