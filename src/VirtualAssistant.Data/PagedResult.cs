using Olbrasoft.Data.Paging;

namespace Olbrasoft.VirtualAssistant.Data;

public class PagedResult<T>(IEnumerable<T> items, int totalCount) : List<T>(items), IPagedEnumerable<T>
{
    public int TotalCount => totalCount;
}
