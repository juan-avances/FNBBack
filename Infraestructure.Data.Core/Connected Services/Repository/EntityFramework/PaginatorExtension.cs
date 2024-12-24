using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Domain.Core.Pagination;
using AutoMapper.QueryableExtensions;
using Microsoft.Extensions.Configuration;
using AutoMapper;

namespace Infraestructure.Data.Core.Repository.EntityFramework
{
    public static class PaginatorExtension
    {
        public static async Task<PagedResult> GetPagedAsync<T, TKey, TResult>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize,
        Expression<Func<T, bool>> filter = null,
        Expression<Func<T, TKey>> orderBy = null,
        bool ascending = true,
        IMapper mapper = null
        )
        {
            var result = new PagedResult
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            if (filter != null)
            {
                query = query.Where((Expression<Func<T, bool>>)filter).AsQueryable();
                var sql = IQueryableExtensions.ToSql(query);
            }

            result.TotalItems = await query.CountAsync();

            if (orderBy != null)
            {
                query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            }


            if (mapper != null)
            {
                result.Items = (await query.Skip((pageNumber) * pageSize)
                                    .Take(pageSize).ProjectTo<TResult>(mapper.ConfigurationProvider).ToListAsync()).Cast<object>().ToList();
            }
            else
            {
                result.Items = (await query.Skip((pageNumber) * pageSize)
                                    .Take(pageSize).ToListAsync()).Cast<object>().ToList();


            }


            return result;

        }
    }
}
