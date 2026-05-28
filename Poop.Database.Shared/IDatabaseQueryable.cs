using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Prefix.Poop.Database.Shared;

/// <summary>
/// Fluent query builder for single-table queries.
/// </summary>
public interface IDatabaseQueryable<T>
{
    IDatabaseQueryable<T> Where(Expression<Func<T, bool>> predicate);
    IDatabaseQueryable<T> OrderBy(Expression<Func<T, object>> keySelector);
    IDatabaseQueryable<T> OrderByDescending(Expression<Func<T, object>> keySelector);
    IDatabaseQueryable<T> Take(int count);

    IDatabaseJoinQueryable<T, T2> InnerJoin<T2>(Expression<Func<T, T2, bool>> joinExpression)
        where T2 : class, new();

    Task<T> FirstAsync();
    Task<T?> FirstOrDefaultAsync();
    Task<T> FirstAsync(Expression<Func<T, bool>> predicate);
    Task<List<T>> ToListAsync();
    Task<int> CountAsync();
}
