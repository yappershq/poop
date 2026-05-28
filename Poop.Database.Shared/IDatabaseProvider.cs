using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Prefix.Poop.Database.Shared;

/// <summary>
/// Centralized database provider. Obtained via SharpModuleManager using <see cref="Identity"/>.
/// </summary>
public interface IDatabaseProvider
{
    const string Identity = "Poop.Database";

    // Table management
    void InitTables(params Type[] entityTypes);
    string GetTableName<T>() where T : class, new();
    void CreateIndex<T>(string[] columnNames) where T : class, new();

    // Query builder
    IDatabaseQueryable<T> Queryable<T>() where T : class, new();

    // Insert
    Task<int> InsertAsync<T>(T entity) where T : class, new();
    Task<int> InsertRangeAsync<T>(List<T> entities) where T : class, new();
    Task<int> InsertReturnIdentityAsync<T>(T entity) where T : class, new();

    // Update
    Task<int> UpdateAsync<T>(T entity) where T : class, new();
    Task<int> UpdateRangeAsync<T>(List<T> entities) where T : class, new();
    Task<int> UpdateColumnsAsync<T>(T entity, Expression<Func<T, object>> columns) where T : class, new();

    // Upsert
    Task<int> UpsertAsync<T>(T entity, Expression<Func<T, object>> matchColumns) where T : class, new();

    // Delete
    Task<int> DeleteAsync<T>(Expression<Func<T, bool>> predicate) where T : class, new();

    // Transaction
    Task<IDatabaseTransaction> BeginTransactionAsync();
}
