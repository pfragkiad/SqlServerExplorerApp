# SqlServerExplorerLib

_Explore and manipulate your SQL Server database the low-level way (a FAST alternative)._

Do you want just to copy a database table to a file or copy tables between databases as fast as possible?
If yes, you have come to the right place.

## How to install

Via tha Package Manager:
```powershell
Install-Package SqlServerExplorerLib
```

Via the .NET CLI
```bat
dotnet add package SqlServerExplorerLib
```

# How to use

In order to use the library you have to add the following using statements as shown below.
For all examples below the `sql` instance declared here is used as the `SqlServerExplorer` object and the `cs` variable as the connection string. 

```cs
using SqlServerExplorerLib;
using System.Data;

...
```

Initialize `sql` and `cs` directly:

```
var sql = new SqlServerExplorer();

//Note that you can get the connection string from the SQL Server Object Explorer (assuming you are using Visual Studio) by expanding the database node and then retrieving the ConnectionString from the Properties window
string cs = "my_connection_string";
```

We could also instantiate via dependency injection (DI).
Let's assume that we have an `appsettings.json` file in the project that is copied to the output directory.
```json
{
    "ConnectionStrings": {
        "Default": "Data Source=DESKTOPCOMP\\SERVER2019;Initial Catalog=co2;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=False;Application Intent=ReadWrite;Multi Subnet Failover=False"
    }
}
```

In all the following examples we assume that we used the 2nd approach. For the examples below we also instantiate a `ILogger` instance.
We also assume a `tableName` string that contains the target of all data operations in the database.

```
//example of using DI we could use the following:
var host = Host
    .CreateDefaultBuilder()
    .ConfigureServices(services => services.AddSingleton<SqlServerExplorer>())
    .Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SQLServerExplorer");
var configuration = host.Services.GetRequiredService<IConfiguration>();

string cs = configuration.GetConnectionString("Default")!;
var sql = host.Services.GetRequiredService<SqlServerExplorer>();

//we also assume that we have a table named TEST_TABLE
string tableName = "TEST_TABLE";
```

In all cases, it is best to nest the code in a `try`/`catch` block for debugging purposes:
```cs
try
{
    //all the code we write from now on
    //...
}
catch (Exception e)
{
    logger.LogCritical(e.Message);
    return;
}
```

In almost all methods we are using `async` methods internally, so we are using the `await` keyword accordingly.

## Test database connection

We can use the `TestConnection` method to check for database connectivity before doing any other operation.

```cs
bool testConnection = await sql.TestConnection(cs);
if (testConnection)
    logger.LogInformation("Connection is successfully tested.");
else
{
    logger.LogError("Could not connect to the database.");
    return;
}

```

## Check for table existence

The `TableExists` method returns `true` if the table exists. For example:

```cs
bool tableExists = await sql.TableExists(cs, tableName);
if (tableExists) logger.LogInformation("Table {t} already exists.", tableName);

//if not then create the table with int, string and float fields
if (!tableExists)
{
    await sql.Execute(cs, $"create table [{tableName}](id int, name nvarchar(50), value float)");
    logger.LogInformation("Successfully created table {t}.", tableName);
}
```

## Check for data existence

The `IsTableEmpty` method returns `true` if the table is empty. Raw operations are done using the functions `Query`,`QueryScalar<T>`, `Execute` as shown in the following examples. In the example below, we create a new table if the table does not exist. The `TruncateTable` function clears the table content.

```cs
bool isTableEmpty = await sql.IsTableEmpty(cs, tableName);
if (!isTableEmpty) logger.LogInformation("Table {t} is not empty.", tableName);

if (isTableEmpty)
{
    logger.LogInformation("Table {t} is empty. New values will be added.", tableName);

    //and insert some values
    await sql.Execute(cs, $@"
    insert into [{tableName}](id,name,value) values
    (1, 'boo',2.3), (2, 'mpe',1.2), (3,'keftes',4.5)");

    logger.LogInformation("Successfully added sample row to the table.");
}
...

//to clear all data from the table
await sql.TruncateTable(cs, tableName);
```

## Get all tables

The `GetTables` method returns the `Schema` and `Name` for each table in the database:

```cs
//list all tables
logger.LogInformation("Listing tables...");
List<SqlServerTable> tables = await sql.GetTables(cs);
foreach(var table in tables)
    logger.LogInformation("Schema: {s}, Name: {n}, Full name: {t}", table.Schema, table.Name, table.ToString());
/*
info: SQLServerExplorer[0]
        Schema: dbo, Name: TEST_TABLE, Full name: [dbo].[TEST_TABLE]
    */
```

## Get all fields

The `GetFields` method return all the fields in a specific table. Use the `tables` instance from the previous code fragment:
```cs

//get the fields for the table we created
var myTable = tables.First(f=>f.Name == tableName);
logger.LogInformation("Listing fields...");
List<SqlServerField> fields = await sql.GetFields(cs,myTable);
foreach(var field in fields)
    logger.LogInformation("Position: {o}, Name: {s}, Type: {t1}, SqlType: {t2}, Max length: {l}, Nullable: {n}",
        field.OrdinalPosition,
        field.Name,
        field.DataType.Name,
        field.SqlServerDataType,
        field.MaximumCharacterLength, //for (n)varchar only
        field.IsNullable
        );
/*
info: SQLServerExplorer[0]
        Position: 1, Name: id, Type: Int32, SqlType: int, Max length: (null), Nullable: True
info: SQLServerExplorer[0]
        Position: 2, Name: name, Type: String, SqlType: nvarchar(50), Max length: 50, Nullable: True
info: SQLServerExplorer[0]
        Position: 3, Name: value, Type: Double, SqlType: float, Max length: (null), Nullable: True
    */
```

The `GetRecordsCount` returns the number of records for a specified table:

```cs
//check the number of records
int recordsCount = await sql.GetRecordsCount(cs,tableName);
logger.LogInformation("Number of records: {c}.",recordsCount);

//we can also get the records count using the myTable object
recordsCount = await sql.GetRecordsCount(cs, myTable);
```

## DataTable and file operations

The `Query` and `GetTopRecords` get a `DataTable` which we can use in any other operations subsequently:

```cs
string selectSql = $"select * from [{tableName}]";

//get custom scalar value
int customCount = await sql.QueryScalar<int>(cs, $"{selectSql} where id>1");

//get the datatable of a query result
DataTable data = await sql.Query(cs, selectSql);

//get top rows (practical for quick preview purposes)
DataTable previewData = await sql.GetTopRecords(cs, myTable, count: 200);
```

There are 2 ways to save a `DataTable` to a file: indirectly via querying the database or directly via passing a `DataTable` instance. To safe to a file we use the `QueryToFile` and `SaveToFile` methods:

```cs
//get the data AND save the result to a file
data = await sql.QueryToFile(cs, selectSql, targetFile: "./mydata.csv", fieldSeparator: ";");

//save a DataTable to a file
sql.SaveToFile(data, targetFile: "./mydata.csv", fieldSeparator: ";");
```

## Bulk copy operations

In order to write data to a table in the database, we need to use bulk copy operations. These are used internally via the `CopyTo` function. The `CopyTo` has 2 overloads. We can copy data from datatable to a target database table, or we can copy data from one database to another database. The `BulkCopyTimeoutInSeconds` and `BatchSize` properties are used to control globally the bulk copy behavior.

```cs
//to control batch operations
sql.BulkCopyTimeoutInSeconds = 0; //0 (default) means no timeout
sql.BatchSize = 0; //0 (default) means send all records at one (for large datasets this value needs tuning for maximum performance

//copy data from a datatable to a table (fast way). The target table must pre-exist.
//an optional event handler may notify when the data has been loaded prior to bulk-copying
sql.LoadedData += (o, e) => { logger.LogInformation("Data has been loaded"); };
await sql.CopyTo(cs, data, destinationTableName: "OTHER_TABLE");

//copy data to another database. A table with the same name must pre-exist.
await sql.CopyTo(cs, targetConnectionString: "other_database_string", table: myTable, destinationTableName: "TARGET_TABLE");
```

## SqlServerService

The class is the most low-level way to interact with the database in an asynchronous way. It provides the following methods:

- `GetDataTable`,  `GetDataSet`, `GetScalar`, `GetList`, `GetStringList`, `GetSingleRow`

To use this class we need to derive a class from it, as shown in the example below:

```cs

public readonly struct ClientYearStats
{
    public int Count { get; init; }
    punlic float Average { get; init; }
}

public class MyService : SqlServerService
{
    public MyService(string connectionString) : base(connectionString) { }

    public async Task<Stats> GetStats(string client, int year)
    {
        const string sql = "select stats.Count, stats.Average from stats where client=@client and year=@year";
      
      DataTable dataTable = await GetDataTable( sql,
        parameters: ("@client", client), ("@year", year));

        List<ClientYearStats> stats = [.. dataTable.AsEnumerable().Select(r => new ClientYearStats
        {
            Count = r.Field<int>("Count"),
            Average = r.Field<float>("Average")
        }).ToList();
        
        return stats;
     }

}
```

Note that the example above, *does not use Reflection* which is the case for common libraries such as  `Dapper` to automatically map the fields to the struct. This makes the current example AOT safe.

## STAY TUNED