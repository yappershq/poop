using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using LiteDB;
using Prefix.Poop.Database.Shared;

namespace Prefix.Poop.Database.Provider;

internal sealed class LiteDbDatabaseProvider(LiteDatabase db) : IDatabaseProvider
{
    private static string GetCollectionName(Type type)
    {
        var attr = type.GetCustomAttribute<DbTableAttribute>();
        return attr?.TableName ?? type.Name;
    }

    private ILiteCollection<T> GetCollection<T>() where T : class, new()
    {
        return db.GetCollection<T>(GetCollectionName(typeof(T)));
    }

    public void InitTables(params Type[] entityTypes)
    {
        // LiteDB auto-creates collections on first use.
        // Ensure they exist by touching them.
        foreach (var type in entityTypes)
        {
            var name = GetCollectionName(type);
            db.GetCollection(name);
        }
    }

    public string GetTableName<T>() where T : class, new()
    {
        return GetCollectionName(typeof(T));
    }

    public void CreateIndex<T>(string[] columnNames) where T : class, new()
    {
        var col = GetCollection<T>();
        foreach (var columnName in columnNames)
        {
            col.EnsureIndex(columnName);
        }
    }

    public IDatabaseQueryable<T> Queryable<T>() where T : class, new()
    {
        return new LiteDbQueryable<T>(db, GetCollection<T>().Query());
    }

    public Task<int> InsertAsync<T>(T entity) where T : class, new()
    {
        return Task.Run(() =>
        {
            var col = GetCollection<T>();
            col.Insert(entity);
            return 1;
        });
    }

    public Task<int> InsertRangeAsync<T>(List<T> entities) where T : class, new()
    {
        return Task.Run(() =>
        {
            var col = GetCollection<T>();
            return col.InsertBulk(entities);
        });
    }

    public Task<int> InsertReturnIdentityAsync<T>(T entity) where T : class, new()
    {
        return Task.Run(() =>
        {
            var col = GetCollection<T>();
            var bsonId = col.Insert(entity);
            return bsonId.AsInt32;
        });
    }

    public Task<int> UpdateAsync<T>(T entity) where T : class, new()
    {
        return Task.Run(() =>
        {
            var col = GetCollection<T>();
            return col.Update(entity) ? 1 : 0;
        });
    }

    public Task<int> UpdateRangeAsync<T>(List<T> entities) where T : class, new()
    {
        return Task.Run(() =>
        {
            var col = GetCollection<T>();
            return col.Update(entities);
        });
    }

    public Task<int> UpdateColumnsAsync<T>(T entity, Expression<Func<T, object>> columns)
        where T : class, new()
    {
        return Task.Run(() =>
        {
            var col = GetCollection<T>();

            // Get the Id from entity to find the existing document
            var idProp = typeof(T).GetProperty("Id");
            var id = idProp?.GetValue(entity);

            if (id == null || (id is int intId && intId == 0))
            {
                return 0;
            }

            var existing = col.FindById(new BsonValue(id));
            if (existing == null) return 0;

            // Extract column names from the expression and copy only those
            var columnNames = ExtractMemberNames(columns);
            foreach (var name in columnNames)
            {
                var prop = typeof(T).GetProperty(name);
                if (prop != null)
                {
                    prop.SetValue(existing, prop.GetValue(entity));
                }
            }

            return col.Update(existing) ? 1 : 0;
        });
    }

    public Task<int> UpsertAsync<T>(T entity, Expression<Func<T, object>> matchColumns)
        where T : class, new()
    {
        return Task.Run(() =>
        {
            var col = GetCollection<T>();
            var (propName, propValue) = ExtractMatchColumn(entity, matchColumns);

            var existing = col.FindOne(Query.EQ(propName, new BsonValue(propValue)));
            if (existing != null)
            {
                // Copy the Id from existing to entity so Update targets the right document
                var idProp = typeof(T).GetProperty("Id");
                if (idProp != null)
                {
                    idProp.SetValue(entity, idProp.GetValue(existing));
                }

                col.Update(entity);
            }
            else
            {
                col.Insert(entity);
            }

            return 1;
        });
    }

    public Task<int> DeleteAsync<T>(Expression<Func<T, bool>> predicate) where T : class, new()
    {
        return Task.Run(() =>
        {
            var col = GetCollection<T>();
            return col.DeleteMany(predicate);
        });
    }

    public Task<IDatabaseTransaction> BeginTransactionAsync()
    {
        return Task.Run<IDatabaseTransaction>(() => new LiteDbTransaction(db));
    }

    #region Helpers

    private static (string PropertyName, object Value) ExtractMatchColumn<T>(
        T entity,
        Expression<Func<T, object>> matchColumns
    )
    {
        var body = matchColumns.Body;
        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            body = unary.Operand;

        if (body is MemberExpression member)
        {
            var propName = member.Member.Name;
            var propValue = matchColumns.Compile()(entity);
            return (propName, propValue);
        }

        throw new ArgumentException("matchColumns must be a simple property expression");
    }

    private static List<string> ExtractMemberNames<T>(Expression<Func<T, object>> expression)
    {
        var body = expression.Body;
        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            body = unary.Operand;

        if (body is NewExpression newExpr)
        {
            return newExpr.Members?.Select(m => m.Name).ToList() ?? [];
        }

        if (body is MemberExpression member)
        {
            return [member.Member.Name];
        }

        throw new ArgumentException("Cannot extract member names from expression");
    }

    #endregion
}
