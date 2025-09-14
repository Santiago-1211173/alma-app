using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.WebApi.Common;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items, int Page, int PageSize, int Total, int TotalPages)
{
    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int total)
        => new(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
}
