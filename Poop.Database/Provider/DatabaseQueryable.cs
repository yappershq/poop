using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using SqlSugar;
using Prefix.Poop.Database.Shared;

namespace Prefix.Poop.Database.Provider;

internal sealed class DatabaseQueryable<T>(ISugarQueryable<T> query) : IDatabaseQueryable<T>
    where T : class, new()
{
    public IDatabaseQueryable<T> Where(Expression<Func<T, bool>> predicate)
    {
        query = query.Where(predicate);
        return this;
    }

    public IDatabaseQueryable<T> OrderBy(Expression<Func<T, object>> keySelector)
    {
        query = query.OrderBy(keySelector);
        return this;
    }

    public IDatabaseQueryable<T> OrderByDescending(Expression<Func<T, object>> keySelector)
    {
        query = query.OrderByDescending(keySelector);
        return this;
    }

    public IDatabaseQueryable<T> Take(int count)
    {
        query = query.Take(count);
        return this;
    }

    public IDatabaseJoinQueryable<T, T2> InnerJoin<T2>(Expression<Func<T, T2, bool>> joinExpression)
        where T2 : class, new()
    {
        var joinQuery = query.InnerJoin(joinExpression);
        return new DatabaseJoinQueryable<T, T2>(joinQuery);
    }

    public async Task<T> FirstAsync()
    {
        return await query.FirstAsync();
    }

    public async Task<T?> FirstOrDefaultAsync()
    {
        return await query.FirstAsync();
    }

    public async Task<T> FirstAsync(Expression<Func<T, bool>> predicate)
    {
        return await query.FirstAsync(predicate);
    }

    public async Task<List<T>> ToListAsync()
    {
        return await query.ToListAsync();
    }

    public async Task<int> CountAsync()
    {
        return await query.CountAsync();
    }
}
