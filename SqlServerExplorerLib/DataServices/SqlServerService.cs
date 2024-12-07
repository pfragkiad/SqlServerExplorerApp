using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace SqlServerExplorerLib.DataServices;

public class SqlServerService
{
    private readonly string? _connectionString;

    private readonly int _timeoutInSeconds;

    public SqlServerService(string connectionString, int timeoutInSeconds = 0)
    {
        _connectionString = connectionString;
        _timeoutInSeconds = timeoutInSeconds;
    }

    protected async Task<DataTable> GetDataTable(string sql, params (string parameterName, object value)[] parameters)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = _timeoutInSeconds;
        foreach (var (parameterName, value) in parameters)
            command.Parameters.AddWithValue(parameterName, value);
        DataTable dataTable = new();

        await Task.Run(() =>
        {
            SqlDataAdapter adapter = new(command);
            adapter.Fill(dataTable);
        });

        return dataTable;
    }

    protected async Task<DataSet> GetDataSet(string sql, params (string parameterName, object value)[] parameters)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = _timeoutInSeconds;
        foreach (var (parameterName, value) in parameters)
            command.Parameters.AddWithValue(parameterName, value);
        DataSet dataset = new();

        await Task.Run(() =>
        {
            SqlDataAdapter adapter = new(command);
            adapter.Fill(dataset);
        });

        return dataset;
    }

    protected async Task<T?> GetScalar<T>(string sql, params (string parameterName, object value)[] parameters) where T : struct
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = _timeoutInSeconds;

        foreach (var (parameterName, value) in parameters)
            command.Parameters.AddWithValue(parameterName, value);
        connection.Open();
        var result = await command.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value)
            return null;
        return (T)result;
    }

    protected async Task<List<T?>> GetList<T>(string sql, params (string parameterName, object value)[] parameters) where T : struct
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = _timeoutInSeconds;

        foreach (var (parameterName, value) in parameters)
            command.Parameters.AddWithValue(parameterName, value);
        connection.Open();
        var reader = await command.ExecuteReaderAsync();

        List<T?> list = [];
        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(0))
            { list.Add(null); continue; }

            list.Add((T)reader[0]);
        }
        return list;
    }

    protected async Task<List<string?>> GetStringList(string sql, params (string parameterName, object value)[] parameters)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = _timeoutInSeconds;
        foreach (var (parameterName, value) in parameters)
            command.Parameters.AddWithValue(parameterName, value);
        connection.Open();
        var reader = await command.ExecuteReaderAsync();

        List<string?> list = [];
        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(0))
            { list.Add(null); continue; }

            list.Add((string?)reader[0]);
        }
        return list;
    }
    protected async Task<DataRow?> GetSingleRow(string sql, params (string parameterName, object value)[] parameters)
    {
        DataTable table = await GetDataTable(sql, parameters);
        if (table.Rows.Count == 0)
            return null;
        return table.Rows[0];
    }

    protected static List<T?> TableToList<T>(DataTable table) where T : struct
    {
        return [.. table.AsEnumerable().Select(row =>
        {
            if (row[0] == DBNull.Value) return (T?)null;
            return (T)row[0];
        }
        )];
    }

    protected static List<string?> TableToStringList(DataTable table)
    {
        return [.. table.AsEnumerable().Select(row =>
        {
            if (row[0] == DBNull.Value) return "<Empty>";
            return (string)row[0];
        }
        )];
    }

    protected static string[] TableToStringArray(DataTable table)
    {
        return [.. table.AsEnumerable().Select(row =>
        {
            if (row[0] == DBNull.Value) return "<Empty>";
            return (string)row[0];
        }
        )];
    }


}
