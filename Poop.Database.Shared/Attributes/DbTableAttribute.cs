using System;

namespace Prefix.Poop.Database.Shared;

/// <summary>
/// Maps a class to a database table. ORM-agnostic replacement for SqlSugar's [SugarTable].
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DbTableAttribute(string tableName) : Attribute
{
    public string TableName { get; } = tableName;
}
