using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.WebApi.Common
{

    public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
    {
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
        {
            return new PagedResult<T>(items, page, pageSize, totalCount);
        }
    }
}
