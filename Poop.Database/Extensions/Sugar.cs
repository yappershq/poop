using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlSugar;
using Prefix.Poop.Database.Shared;

namespace Prefix.Poop.Database.Extensions;

internal static class SugarExtensions
{
    internal static ConnectionConfig BuildConnectionConfig(
        IConfiguration configuration,
        string sharpPath
    )
    {
        var dbTypeStr = configuration["Database:Type"] ?? "sqlite";
        var host = configuration["Database:Host"] ?? "localhost";
        var port = configuration["Database:Port"] ?? "3306";
        var database = configuration["Database:Database"] ?? "poop";
        var user = configuration["Database:User"] ?? "root";
        var password = configuration["Database:Password"] ?? "";

        var dbType = dbTypeStr.ToLowerInvariant() switch
        {
            "mysql" => DbType.MySql,
            "postgresql" => DbType.PostgreSQL,
            _ => throw new NotSupportedException(
                $"Database type '{dbTypeStr}' is not supported. Supported types: mysql, postgresql"
            ),
        };

        var connectionString = dbType switch
        {
            DbType.MySql =>
                $"Server={host};Port={port};Database={database};User={user};Password={password};",
            DbType.PostgreSQL =>
                $"Host={host};Port={port};Database={database};Username={user};Password={password};",
            _ => throw new NotSupportedException($"Database type '{dbTypeStr}' is not supported."),
        };

        return new ConnectionConfig
        {
            DbType = dbType,
            ConnectionString = connectionString,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
            MoreSettings = new ConnMoreSettings { DisableNvarchar = true },
            LanguageType = LanguageType.English,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityNameService = (type, entity) =>
                {
                    var attr = type.GetCustomAttribute<DbTableAttribute>();
                    if (attr != null)
                    {
                        entity.DbTableName = attr.TableName;
                    }
                },
                EntityService = (prop, column) =>
                {
                    var attr = prop.GetCustomAttribute<DbColumnAttribute>();
                    if (attr == null) return;

                    if (attr.IsPrimaryKey) column.IsPrimarykey = true;
                    if (attr.IsIdentity) column.IsIdentity = true;
                    column.IsNullable = attr.IsNullable;
                    // Primary key / identity columns must be NOT NULL for AUTO_INCREMENT
                    if (attr.IsPrimaryKey || attr.IsIdentity) column.IsNullable = false;
                    if (attr.Length > 0) column.Length = attr.Length;
                    if (!string.IsNullOrEmpty(attr.DataType)) column.DataType = attr.DataType;
                },
            },
        };
    }
}
