using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Prefix.Poop.Database.Shared;

/// <summary>
/// Fluent query builder for two-table join queries.
/// </summary>
public interface IDatabaseJoinQueryable<T1, T2>
{
    IDatabaseJoinQueryable<T1, T2> Where(Expression<Func<T1, T2, bool>> predicate);
    IDatabaseJoinQueryable<T1, T2> OrderByDescending(Expression<Func<T1, T2, object>> keySelector);
    IDatabaseJoinQueryable<T1, T2> Take(int count);

    Task<List<TResult>> SelectToListAsync<TResult>(Expression<Func<T1, T2, TResult>> selector)
        where TResult : class, new();
}
