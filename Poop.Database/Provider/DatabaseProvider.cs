using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using SqlSugar;
using Prefix.Poop.Database.Shared;

namespace Prefix.Poop.Database.Provider;

internal sealed class DatabaseProvider(ISqlSugarClient db) : IDatabaseProvider
{
    public void InitTables(params Type[] entityTypes)
    {
        db.CodeFirst.InitTables(entityTypes);
    }

    public string GetTableName<T>() where T : class, new()
    {
        return db.EntityMaintenance.GetTableName<T>();
    }

    public void CreateIndex<T>(string[] columnNames) where T : class, new()
    {
        var tableName = db.EntityMaintenance.GetTableName<T>();
        var indexName = $"Index_{tableName}_{string.Join("_", columnNames)}";
        if (!db.DbMaintenance.IsAnyIndex(indexName))
        {
            db.DbMaintenance.CreateIndex(tableName, columnNames);
        }
    }

    public IDatabaseQueryable<T> Queryable<T>() where T : class, new()
    {
        return new DatabaseQueryable<T>(db.Queryable<T>());
    }

    public async Task<int> InsertAsync<T>(T entity) where T : class, new()
    {
        return await db.Insertable(entity).ExecuteCommandAsync();
    }

    public async Task<int> InsertRangeAsync<T>(List<T> entities) where T : class, new()
    {
        return await db.Insertable(entities).ExecuteCommandAsync();
    }

    public async Task<int> InsertReturnIdentityAsync<T>(T entity) where T : class, new()
    {
        return await db.Insertable(entity).ExecuteReturnIdentityAsync();
    }

    public async Task<int> UpdateAsync<T>(T entity) where T : class, new()
    {
        return await db.Updateable(entity).ExecuteCommandAsync();
    }

    public async Task<int> UpdateRangeAsync<T>(List<T> entities) where T : class, new()
    {
        return await db.Updateable(entities).ExecuteCommandAsync();
    }

    public async Task<int> UpdateColumnsAsync<T>(T entity, Expression<Func<T, object>> columns)
        where T : class, new()
    {
        return await db.Updateable(entity).UpdateColumns(columns).ExecuteCommandAsync();
    }

    public async Task<int> UpsertAsync<T>(T entity, Expression<Func<T, object>> matchColumns)
        where T : class, new()
    {
        return await db.Storageable(entity).WhereColumns(matchColumns).ExecuteCommandAsync();
    }

    public async Task<int> DeleteAsync<T>(Expression<Func<T, bool>> predicate) where T : class, new()
    {
        return await db.Deleteable<T>().Where(predicate).ExecuteCommandAsync();
    }

    public async Task<IDatabaseTransaction> BeginTransactionAsync()
    {
        await db.Ado.BeginTranAsync();
        return new DatabaseTransaction(db);
    }
}
