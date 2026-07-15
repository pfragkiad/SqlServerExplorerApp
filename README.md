# SqlServerExplorerLib

Low-level, async-first SQL Server operations with minimal overhead.

`SqlServerExplorerLib` is focused on practical data access tasks such as:

- validating connectivity,
- inspecting tables and columns,
- running raw SQL queries,
- exporting `DataTable` results to delimited files,
- and performing fast bulk-copy transfers between sources.

## Installation

Package Manager:

```powershell
Install-Package SqlServerExplorerLib
```

.NET CLI:

```bash
dotnet add package SqlServerExplorerLib
```

## Quick start

```csharp
using SqlServerExplorerLib;
using System.Data;

var sql = new SqlServerExplorer();
string cs = "my_connection_string";
```

Dependency injection is also supported:

```csharp
var host = Host
    .CreateDefaultBuilder()
    .ConfigureServices(services => services.AddSingleton<SqlServerExplorer>())
    .Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SQLServerExplorer");
var configuration = host.Services.GetRequiredService<IConfiguration>();

string cs = configuration.GetConnectionString("Default")!;
var sql = host.Services.GetRequiredService<SqlServerExplorer>();
string tableName = "TEST_TABLE";
```

## Core capabilities

### Connectivity

```csharp
bool ok = await sql.TestConnection(cs);
```

Optional timeout:

```csharp
bool ok = await sql.TestConnection(cs, timeoutInSeconds: 5);
```

### Schema exploration

- `GetTables(connectionString)`
- `GetFields(connectionString, SqlServerTable)`
- `TableExists(connectionString, tableName)`

```csharp
List<SqlServerTable> tables = await sql.GetTables(cs);
var myTable = tables.First(t => t.Name == tableName);
List<SqlServerField> fields = await sql.GetFields(cs, myTable);
```

### Record and table utilities

- `IsTableEmpty(connectionString, tableName)`
- `GetRecordsCount(connectionString, tableName)`
- `GetRecordsCount(connectionString, SqlServerTable)`
- `TruncateTable(connectionString, tableName)`

### Raw SQL operations

`SqlServerExplorer` supports both plain and parameterized overloads:

- `Execute(...)`
- `Query(...)`
- `QueryScalar<T>(...)`

```csharp
await sql.Execute(cs, "create table [TEST_TABLE](id int, name nvarchar(50), value float)");

int count = await sql.QueryScalar<int>(cs, "select count(*) from [TEST_TABLE]");

DataTable data = await sql.Query(cs, "select * from [TEST_TABLE]");
```

### Data preview and export

- `GetTopRecords(connectionString, table, count)`
- `QueryToFile(connectionString, sqlText, targetFile, fieldSeparator)`
- `SaveToFile(DataTable, targetFile, fieldSeparator)`

```csharp
DataTable preview = await sql.GetTopRecords(cs, myTable, count: 200);
DataTable all = await sql.QueryToFile(cs, "select * from [TEST_TABLE]", "./mydata.csv", ";");
sql.SaveToFile(all, "./mydata.csv", ";");
```

### Bulk copy

Use high-throughput copy operations with global settings:

- `BatchSize`
- `BulkCopyTimeoutInSeconds`
- `LoadedData` event
- `CopyTo(targetConnectionString, DataTable, destinationTableName)`
- `CopyTo(sourceConnectionString, targetConnectionString, SqlServerTable, destinationTableName?)`

```csharp
sql.BulkCopyTimeoutInSeconds = 0; // 0 = no timeout
sql.BatchSize = 0; // 0 = all rows in one batch

sql.LoadedData += (_, _) => logger.LogInformation("Data has been loaded.");
await sql.CopyTo(cs, all, destinationTableName: "OTHER_TABLE");
```

## Additional existing capabilities

### `SqlServerService` base class (advanced/low-level)

For custom services, derive from `SqlServerExplorerLib.DataServices.SqlServerService` and use:

- `GetDataTable`
- `GetDataSet`
- `GetScalar<T>`
- `ExecuteNonQuery`
- `GetList<T>`
- `GetStringList`
- `GetSingleRow`
- `BulkCopy`

This provides a low-level async API while keeping full control of SQL and mapping.

### Data conversion helpers

Namespace: `SqlServerExplorerLib.DataServices`

- `DataTableExtensions.ToList<T>()`
- `DataTableExtensions.ToArray<T>()`
- `DataTableExtensions.ToStringArray(useEmptyStringForNull)`
- `DataTableExtensions.ToStringList(useEmptyStringForNull)`
- `DataRowExtensions.ForceReadDouble/Int/String/Bool/DateTime(...)`
- `DataRowExtensions.SafeReadEnum<T>(...)`

### Metadata models

- `SqlServerTable` (schema + name + formatted `[schema].[name]` output)
- `SqlServerField` (position, CLR type, SQL type, max length, nullability)

## Notes

- Most operations are async and intended to be awaited.
- A defensive `try/catch` around integration code is recommended.
- For best safety, prefer parameterized SQL overloads whenever possible.
