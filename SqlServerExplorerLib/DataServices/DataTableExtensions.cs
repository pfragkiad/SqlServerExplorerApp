﻿using Microsoft.Extensions.Configuration;
using System.Data;

namespace SqlServerExplorerLib.DataServices;

public static class DataTableExtensions
{

    public static List<T?> ToList<T>(this DataTable table) where T : struct
    {
        return [.. table.AsEnumerable().Select(row =>
    {
        if (row[0] == DBNull.Value) return (T?)null;
        return (T)row[0];
    }
    )];
    }

    public static T?[] ToArray<T>(this DataTable table) where T : struct
    {
        return [.. table.AsEnumerable().Select(row =>
    {
        if (row[0] == DBNull.Value) return (T?)null;
        return (T)row[0];
    }
    )];
    }

    public static string[] ToStringArray(this DataTable table, bool useEmptyStringForNull)
    {
        return [.. table.AsEnumerable().Select(row =>
    {
        if (row[0] == DBNull.Value)
            return useEmptyStringForNull ? "<Empty>": null;

        return (string)row[0];
    }
    )];
    }

    public static List<string?> ToStringList(this DataTable table, bool useEmptyStringForNull)
    {
        return [.. table.AsEnumerable().Select(row =>
    {
        if (row[0] == DBNull.Value)
            return useEmptyStringForNull? "<Empty>": null;
        return (string)row[0];
    }
    )];
    }

    //public static List<int?> ToIntList(this DataTable table, bool useZeroForNull)
    //{
    //    return [.. table.AsEnumerable().Select(row =>
    //{
    //    if (row[0] == DBNull.Value)
    //        return useZeroForNull ? 0 : (int?)null;
    //    return (int)row[0];
    //}
    //)];
    //}
}