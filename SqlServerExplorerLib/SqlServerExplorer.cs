﻿using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Types;
using System.Data;

namespace SqlServerExplorerLib;

public class SqlServerExplorer
{
    public async Task<bool> TestConnection(string connectionString, int? timeoutInSeconds = null)
    {
        try
        {
            Task createConnectionTask = Task.Run(async () =>
            {
                using SqlConnection connection = new(connectionString);
                await connection.OpenAsync();
                await connection.CloseAsync();
            });
            Task timeoutTask = timeoutInSeconds is not null ? Task.Delay(timeoutInSeconds.Value * 1000) : Task.CompletedTask;

            if (timeoutInSeconds is not null)
            {
                var taskFinished = await Task.WhenAny(createConnectionTask, timeoutTask);
                bool isTimeout = taskFinished == timeoutTask || taskFinished.IsFaulted;
                if (isTimeout)
                    return false;
            }
            else
                await createConnectionTask;

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<List<SqlServerTable>> GetTables(string connectionString, int? timeoutInSeconds = null)
    {
        DataTable data = await Query(connectionString, "select TABLE_SCHEMA, TABLE_NAME from INFORMATION_SCHEMA.TABLES", timeoutInSeconds);

        var tables = data.Rows.Cast<DataRow>().Select(r =>
            new SqlServerTable()
            {
                Schema = r.Field<string>("TABLE_SCHEMA")!,
                Name = r.Field<string>("TABLE_NAME")!
            }).ToList();

        return tables;
    }

    public async Task<bool> TableExists(string connectionString, string tableName, int? timeoutInSeconds = null)
    {
        int? result = await QueryScalar<int?>(connectionString, $"select 1 from INFORMATION_SCHEMA.TABLES where TABLE_NAME = '{tableName}'",timeoutInSeconds);
        return result == 1;
    }
    
    public async Task<bool> IsTableEmpty(string connectionString, string tableName, int? timeoutInSeconds = null)
    {
        int count = await QueryScalar<int>(connectionString, $"select count(*) from (select top 1 * from [{tableName}]) A",timeoutInSeconds);
        return count == 0;
    }

    public async Task TruncateTable(string connectionString, string tableName, int? timeoutInSeconds = null)
    {
        await Execute(connectionString, $"truncate table [{tableName}]",timeoutInSeconds);
    }


    public async Task<List<SqlServerField>> GetFields(string connectionString, SqlServerTable table, int? timeoutInSeconds  = null)
    {
        DataTable data = await Query(connectionString, $"select COLUMN_NAME, ORDINAL_POSITION, IS_NULLABLE, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_PRECISION_RADIX, DATETIME_PRECISION FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N'{table.Name}' AND TABLE_SCHEMA=N'{table.Schema}'",timeoutInSeconds);

        List<SqlServerField> fields = new List<SqlServerField>();
        foreach (DataRow r in data.Rows.Cast<DataRow>())
        {
            string dataType = r.Field<string>("DATA_TYPE")!;
            int? maximumCharacters = r.Field<int?>("CHARACTER_MAXIMUM_LENGTH");

            string columnName = r.Field<string>("COLUMN_NAME")!;
            bool isNullable = r.Field<string>("IS_NULLABLE") == "YES";
            int ordinalPosition = r.Field<int>("ORDINAL_POSITION");

            bool isCharacter = maximumCharacters.HasValue;
            if (isCharacter && dataType != "geography")
                dataType += maximumCharacters != -1 ? $"({maximumCharacters})" : "(max)";

            maximumCharacters = maximumCharacters.HasValue && maximumCharacters.Value != -1 ? maximumCharacters.Value : null;

            Type? type = null;
            if (dataType == "geography")
                //SQLGeography -> data_type = geography, character_maximum_length = -1
                type = typeof(SqlGeography);
            else if (isCharacter)
                type = typeof(string);
            else if (dataType == "int")
                //int -> data_type = int, numeric_precision = 10, numeric_precision_radix = 10
                type = typeof(int);
            else if (dataType == "bigint")
                //long -> data_type = bigint, numeric_precision = 19, numeric_precision_radix = 10
                type = typeof(long);
            else if (dataType == "float")
                //double -> data_type = float, numeric_precision = 53, numeric_precision_radix = 2
                type = typeof(double);
            else if (dataType == "bit")
                //bool -> data_type = bit
                type = typeof(bool);
            else if (dataType == "datetime" || dataType == "datetime2" || dataType == "date")
                //datetime -> data_type = "datetime", datetime_precision = 3
                //datetime -> data_type = "datetime2", datetime_precision = 7
                //datetime -> data_type = "date", datetime_precision = 0
                type = typeof(DateTime);
            else if (dataType == "datetimeoffset")
                //datetimeoffset -> data_type = "datetimeoffset", datetime_precision = 7
                type = typeof(DateTimeOffset);
            else if (dataType == "uniqueidentifier")
                type = typeof(Guid);
            //byte -> data_type = "varbinary",character_maximum_length = -1, character_octet_length = -1
            else if (dataType == "varbinary")
                type = typeof(byte[]);

            if (type is null) throw new NotImplementedException($"Not implemented: {dataType}");

            fields.Add(new SqlServerField
            {
                Name = columnName,
                DataType = type,
                MaximumCharacterLength = maximumCharacters,
                IsNullable = isNullable,
                OrdinalPosition = ordinalPosition,
                SqlServerDataType = dataType
            });
        }

        return fields;

    }

    public async Task<int> GetRecordsCount(string connectionString, SqlServerTable table)
    {
        return await QueryScalar<int>(connectionString, $"select count(*) from {table}");
    }


    public async Task<int> GetRecordsCount(string connectionString, string table)
    {
        return await QueryScalar<int>(connectionString, $"select count(*) from [{table}]");
    }


    public async Task<DataTable> GetTopRecords(string connectionString, SqlServerTable table, int count = 500)
    {
        return await Query(connectionString, $"select top {count} * from {table}");
    }

    public event EventHandler? LoadedData;


    #region Bulk copy operations

    public int BatchSize { get; set; } = 0;

    public int BulkCopyTimeoutInSeconds { get; set; } = 0;


    public async Task CopyTo(string sourceConnectionString, string targetConnectionString, SqlServerTable table, string? destinationTableName = null)
    {
        DataTable data = await Query(sourceConnectionString, $"select * from {table}");
        LoadedData?.Invoke(this, new EventArgs());
        destinationTableName ??= table.ToString();
        await CopyTo(targetConnectionString, data, destinationTableName);
    }

    public async Task CopyTo(string targetConnectionString, DataTable data, string destinationTableName)
    {
        using SqlConnection target = new(targetConnectionString);
        await target.OpenAsync();
        SqlBulkCopy copier = new(target)
        {
            DestinationTableName = destinationTableName,
            BulkCopyTimeout = this.BulkCopyTimeoutInSeconds,
            BatchSize = this.BatchSize
        };
        await copier.WriteToServerAsync(data);
    }

    #endregion

    #region Raw queries

    public async Task Execute(string connectionString, string sqlText, int? commandTimeoutInSeconds = 30)
    {
        using SqlConnection source = new(connectionString);
        SqlCommand sql = new(sqlText, source)
        {
            CommandTimeout = commandTimeoutInSeconds ?? 30
        };
        source.Open();
        await sql.ExecuteNonQueryAsync();
        source.Close();
    }

    public async Task Execute(string connectionString, string sqlText, SqlParameter[] parameters, int? commandTimeoutInSeconds = 30)
    {
        using SqlConnection source = new(connectionString);
        SqlCommand sql = new(sqlText, source);
        sql.CommandTimeout = commandTimeoutInSeconds ?? 30;
        source.Open();
        if (parameters is not null)
            sql.Parameters.AddRange(parameters);

        await sql.ExecuteNonQueryAsync();
        source.Close();
    }

    public async Task<DataTable> Query(string connectionString, string sqlText, int? commandTimeoutInSeconds = 30)
    {
        using SqlConnection source = new(connectionString);
        SqlCommand sql = new(sqlText, source)
        {
            CommandTimeout = commandTimeoutInSeconds ?? 30
        };
        SqlDataAdapter sqlDataAdapter = new(sql);
        DataTable data = new();
        data.BeginLoadData();
        await Task.Run(() => sqlDataAdapter.Fill(data));
        data.EndLoadData();
        return data;
    }

    /*
 categoriesAdapter.SelectCommand.Parameters.Add(
    "@CategoryName", SqlDbType.VarChar, 80).Value = "toasters";
  categoriesAdapter.SelectCommand.Parameters.Add(
    "@SerialNum", SqlDbType.Int).Value = 239;
  categoriesAdapter.Fill(categoriesDataSet);
     */


    public async Task<DataTable> Query(string connectionString, string sqlText, SqlParameter[] parameters, int? commandTimeOutInSeconds = 30)
    {
        using SqlConnection source = new(connectionString);
        SqlCommand sql = new(sqlText, source)
        {
            CommandTimeout = commandTimeOutInSeconds ?? 30
        };

        if (parameters is not null)
            sql.Parameters.AddRange(parameters);

        SqlDataAdapter sqlDataAdapter = new(sql);
        DataTable data = new();
        data.BeginLoadData();
        await Task.Run(() => sqlDataAdapter.Fill(data));
        data.EndLoadData();
        return data;
    }

    public async Task<T?> QueryScalar<T>(string connectionString, string sqlText, int? commandTimeOutInSeconds = 30)
    {
        using SqlConnection connection = new(connectionString);
        SqlCommand sql = new(sqlText, connection)
        {
            CommandTimeout = commandTimeOutInSeconds ??= 30
        };
        await connection.OpenAsync();
        T? result = (T?)(await sql.ExecuteScalarAsync());
        return result;
    }

    public async Task<T?> QueryScalar<T>(string connectionString, string sqlText, SqlParameter[] parameters, int? commandTimeOutInSeconds = 30)
    {
        using SqlConnection connection = new(connectionString);
        SqlCommand sql = new(sqlText, connection)
        {
            CommandTimeout = commandTimeOutInSeconds ??= 30
        };

        if (parameters is not null)
            sql.Parameters.AddRange(parameters);

        await connection.OpenAsync();
        T? result = (T?)(await sql.ExecuteScalarAsync());
        return result;
    }

    #endregion

    #region Save to file operations

    public async Task<DataTable> QueryToFile(string connectionString, string sqlText, string targetFile, string fieldSeparator = ",", int? commandTimeoutInSeconds = 30)
    {
        var table = await Query(connectionString, sqlText, commandTimeoutInSeconds);

        SaveToFile(table, targetFile, fieldSeparator);

        return table;
    }

    public void SaveToFile(DataTable table, string targetFile, string fieldSeparator = ",")
    {
        string[] columnNames = table.Columns.Cast<DataColumn>().Select(h => h.ColumnName).ToArray();
        string header = string.Join(fieldSeparator, columnNames);

        using (StreamWriter writer = new StreamWriter(targetFile, false))
        {
            writer.WriteLine(header);

            foreach (var r in table.Rows.Cast<DataRow>())
            {
                string values = string.Join(fieldSeparator, columnNames.Select(c => $"{r[c]}"));
                writer.WriteLine(values);
            }
        }
    }
    #endregion
}

