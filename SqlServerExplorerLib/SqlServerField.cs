namespace SqlServerExplorerLib;


public class SqlServerField : IComparable<SqlServerField>
{
    public required string Name { get; init; }

    public required int OrdinalPosition { get; init; }

    public required bool IsNullable { get; init; }   

    public required Type DataType { get; init; }

    public required string SqlServerDataType { get; init; }

    public int? MaximumCharacterLength { get; init; }

    public int CompareTo(SqlServerField? other)
    {
        return OrdinalPosition.CompareTo(other?.OrdinalPosition ?? 0);
    }

    public override string ToString()
    {
        string name = Name.Contains(" ") ? $"[{Name}]" : Name;
        return $"{name} ({SqlServerDataType}, {(IsNullable ? "null" :"not null")})";
    }

}