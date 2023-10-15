using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace SqlServerExplorerLib;

public class SqlServerTable : IComparable<SqlServerTable>
{
    public required string Schema { get; init; }

    public required string Name { get; init; }

    public int CompareTo(SqlServerTable? other)
    {
        return this.ToString().CompareTo(other?.ToString() ?? "");
    }

    public override string ToString() => $"[{Schema}].[{Name}]";

    
}
