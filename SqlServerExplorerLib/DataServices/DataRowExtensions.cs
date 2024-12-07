using System.Data;

namespace SqlServerExplorerLib.DataServices;

public static class DataRowExtensions
{
    public static double ForceReadDouble(this DataRow row, string columnName)
    {
        return row[columnName] is DBNull ? 0 : row.Field<double>(columnName);
    }

    public static int ForceReadInt(this DataRow row, string columnName)
    {
        return row[columnName] is DBNull ? 0 : row.Field<int>(columnName);
    }
    public static string ForceReadString(this DataRow row, string columnName)
    {
        return row[columnName] is DBNull ? string.Empty : row.Field<string>(columnName)!;
    }

    public static bool ForceReadBool(this DataRow row, string columnName)
    {
        return row[columnName] is not DBNull && row.Field<bool>(columnName);
    }

    public static DateTime FoceReadDateTime(this DataRow row, string columnName)
    {
        return row[columnName] is DBNull ? DateTime.MinValue : row.Field<DateTime>(columnName);
    }

    public static T? SafeReadEnum<T>(this DataRow row, string columnName) where T : struct
    {
        return row[columnName] is DBNull ? null : row.Field<T>(columnName);
    }


}
