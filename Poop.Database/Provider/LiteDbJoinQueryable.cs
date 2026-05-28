using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using LiteDB;
using Prefix.Poop.Database.Shared;

namespace Prefix.Poop.Database.Provider;

/// <summary>
/// In-memory join implementation for LiteDB.
/// Executes the T1 query first, then fetches matching T2 records and joins in memory.
/// Suitable for small result sets (e.g., leaderboards with Take/Limit).
/// </summary>
internal sealed class LiteDbJoinQueryable<T1, T2>(
    LiteDatabase db,
    ILiteQueryable<T1> query,
    Expression<Func<T1, T2, bool>> joinExpression
) : IDatabaseJoinQueryable<T1, T2>
    where T1 : class, new()
    where T2 : class, new()
{
    private Expression<Func<T1, T2, bool>>? _whereExpr;
    private Expression<Func<T1, T2, object>>? _orderByDescExpr;
    private int? _take;

    public IDatabaseJoinQueryable<T1, T2> Where(Expression<Func<T1, T2, bool>> predicate)
    {
        _whereExpr = predicate;
        return this;
    }

    public IDatabaseJoinQueryable<T1, T2> OrderByDescending(
        Expression<Func<T1, T2, object>> keySelector
    )
    {
        _orderByDescExpr = keySelector;
        return this;
    }

    public IDatabaseJoinQueryable<T1, T2> Take(int count)
    {
        _take = count;
        return this;
    }

    public Task<List<TResult>> SelectToListAsync<TResult>(
        Expression<Func<T1, T2, TResult>> selector
    )
        where TResult : class, new()
    {
        return Task.Run(() =>
        {
            // 1. Execute T1 query
            var list1 = query.ToList();

            // 2. Fetch all T2 records
            var collectionName = GetCollectionName(typeof(T2));
            var col2 = db.GetCollection<T2>(collectionName);
            var list2 = col2.FindAll().ToList();

            // 3. Join in memory
            var joinFunc = joinExpression.Compile();
            IEnumerable<(T1 t1, T2 t2)> joined =
                from t1 in list1
                from t2 in list2
                where joinFunc(t1, t2)
                select (t1, t2);

            // 4. Apply Where
            if (_whereExpr != null)
            {
                var whereFunc = _whereExpr.Compile();
                joined = joined.Where(pair => whereFunc(pair.t1, pair.t2));
            }

            // 5. Apply OrderByDescending
            if (_orderByDescExpr != null)
            {
                var orderFunc = _orderByDescExpr.Compile();
                joined = joined.OrderByDescending(pair => orderFunc(pair.t1, pair.t2));
            }

            // 6. Apply Take
            if (_take.HasValue)
            {
                joined = joined.Take(_take.Value);
            }

            // 7. Select
            var selectFunc = selector.Compile();
            return joined.Select(pair => selectFunc(pair.t1, pair.t2)).ToList();
        });
    }

    private static string GetCollectionName(Type type)
    {
        var attr = type.GetCustomAttribute<DbTableAttribute>();
        return attr?.TableName ?? type.Name;
    }
}
