using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace SqlServerExplorerLib.DataServices;

public class SqlServerService
{
    private readonly string? _connectionString;

    private int _timeoutInSeconds;

    public SqlServerService(string connectionString, int timeoutInSeconds = 0)
    {
        _connectionString = connectionString;
        _timeoutInSeconds = timeoutInSeconds;
    }


    /// <summary>
    /// Gloabl timeout setting for all queries. Default is 0 which means no timeout.
    /// </summary>
    public int TimeoutInSeconds
    {
        get => _timeoutInSeconds;
        set => _timeoutInSeconds = Math.Max(value, 0);
    }

    protected async Task<bool> TableExists(string tableName, string tableSchema = "dbo",int? timeoutInSeconds = null )
    {
        /*
SELECT *
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME = 'sadd' AND TABLE_SCHEMA = 'dbo';
         */

        string sql = "SELECT top 1 * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @tableName AND TABLE_SCHEMA = @tableSchema";
        DataTable table = await GetDataTable(sql, timeoutInSeconds, parameters: [("@tableName", tableName), ("@tableSchema", tableSchema)]);
        return table.Rows.Count > 0;
    }

    protected async Task<DataTable> GetDataTable(string sql, int? timeoutInSeconds = null, params (string parameterName, object value)[] parameters)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = timeoutInSeconds ?? _timeoutInSeconds;
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

    protected async Task<DataSet> GetDataSet(string sql, int? timeoutInSeconds = null, params (string parameterName, object value)[] parameters)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = timeoutInSeconds ?? _timeoutInSeconds;
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

    protected async Task<T?> GetScalar<T>(string sql, int? timeoutInSeconds=null, params (string parameterName, object value)[] parameters) where T : struct
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = timeoutInSeconds ?? _timeoutInSeconds;

        foreach (var (parameterName, value) in parameters)
            command.Parameters.AddWithValue(parameterName, value);
        connection.Open();
        var result = await command.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value)
            return null;
        return (T)result;
    }

    protected async Task<List<T?>> GetList<T>(string sql, int? timeoutInSeconds=null, params (string parameterName, object value)[] parameters) where T : struct
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = timeoutInSeconds ?? _timeoutInSeconds;

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

    protected async Task<List<string?>> GetStringList(string sql, int? timeoutInSeconds= null, params (string parameterName, object value)[] parameters)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = timeoutInSeconds ?? _timeoutInSeconds;
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
    protected async Task<DataRow?> GetSingleRow(string sql, int? timeoutInSeconds = null, params (string parameterName, object value)[] parameters)
    {
        DataTable table = await GetDataTable(sql, timeoutInSeconds, parameters);
        if (table.Rows.Count == 0)
            return null;
        return table.Rows[0];
    }
}
