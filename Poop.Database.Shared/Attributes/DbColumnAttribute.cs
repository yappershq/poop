using System;

namespace Prefix.Poop.Database.Shared;

/// <summary>
/// Configures a property as a database column. ORM-agnostic replacement for SqlSugar's [SugarColumn].
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DbColumnAttribute : Attribute
{
    public bool IsPrimaryKey { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsNullable { get; set; } = true;
    public int Length { get; set; }
    public string? DataType { get; set; }
}
