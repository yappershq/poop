using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using SqlSugar;
using Prefix.Poop.Database.Shared;

namespace Prefix.Poop.Database.Provider;

internal sealed class DatabaseJoinQueryable<T1, T2>(ISugarQueryable<T1, T2> query)
    : IDatabaseJoinQueryable<T1, T2>
    where T1 : class, new()
    where T2 : class, new()
{
    public IDatabaseJoinQueryable<T1, T2> Where(Expression<Func<T1, T2, bool>> predicate)
    {
        query = query.Where(predicate);
        return this;
    }

    public IDatabaseJoinQueryable<T1, T2> OrderByDescending(Expression<Func<T1, T2, object>> keySelector)
    {
        query = query.OrderByDescending(keySelector);
        return this;
    }

    public IDatabaseJoinQueryable<T1, T2> Take(int count)
    {
        query = query.Take(count);
        return this;
    }

    public async Task<List<TResult>> SelectToListAsync<TResult>(
        Expression<Func<T1, T2, TResult>> selector)
        where TResult : class, new()
    {
        return await query.Select(selector).ToListAsync();
    }
}
