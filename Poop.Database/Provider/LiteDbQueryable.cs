using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using LiteDB;
using Prefix.Poop.Database.Shared;

namespace Prefix.Poop.Database.Provider;

internal sealed class LiteDbQueryable<T> : IDatabaseQueryable<T>
    where T : class, new()
{
    private readonly LiteDatabase _db;
    private ILiteQueryable<T> _query;
    private int? _limit;

    public LiteDbQueryable(LiteDatabase db, ILiteQueryable<T> query)
    {
        _db = db;
        _query = query;
    }

    public IDatabaseQueryable<T> Where(Expression<Func<T, bool>> predicate)
    {
        _query = _query.Where(predicate);
        return this;
    }

    public IDatabaseQueryable<T> OrderBy(Expression<Func<T, object>> keySelector)
    {
        _query = _query.OrderBy(keySelector);
        return this;
    }

    public IDatabaseQueryable<T> OrderByDescending(Expression<Func<T, object>> keySelector)
    {
        _query = _query.OrderByDescending(keySelector);
        return this;
    }

    public IDatabaseQueryable<T> Take(int count)
    {
        _limit = count;
        return this;
    }

    public IDatabaseJoinQueryable<T, T2> InnerJoin<T2>(Expression<Func<T, T2, bool>> joinExpression)
        where T2 : class, new()
    {
        return new LiteDbJoinQueryable<T, T2>(_db, _query, joinExpression);
    }

    public Task<T> FirstAsync()
    {
        return Task.Run(() =>
            _limit.HasValue
                ? _query.Limit(_limit.Value).First()
                : _query.First()
        );
    }

    public Task<T?> FirstOrDefaultAsync()
    {
        return Task.Run<T?>(() =>
            _limit.HasValue
                ? _query.Limit(_limit.Value).FirstOrDefault()
                : _query.FirstOrDefault()
        );
    }

    public Task<T> FirstAsync(Expression<Func<T, bool>> predicate)
    {
        _query = _query.Where(predicate);
        return Task.Run(() =>
            _limit.HasValue
                ? _query.Limit(_limit.Value).First()
                : _query.First()
        );
    }

    public Task<List<T>> ToListAsync()
    {
        return Task.Run(() =>
            _limit.HasValue
                ? _query.Limit(_limit.Value).ToList()
                : _query.ToList()
        );
    }

    public Task<int> CountAsync()
    {
        return Task.Run(() =>
            _limit.HasValue
                ? _query.Limit(_limit.Value).Count()
                : _query.Count()
        );
    }
}
