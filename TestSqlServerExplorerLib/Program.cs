using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlServerExplorerLib;
using System.Data;

var host = Host
    .CreateDefaultBuilder()
    //we could use dependency injection too
    .ConfigureServices(services => services.AddSingleton<SqlServerExplorer>())
    .Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SQLServerExplorer");

var configuration = host.Services.GetRequiredService<IConfiguration>();
//Assume that we have an appsettings.json file in the project that is copied to the output directory
//{
//    "ConnectionStrings": {
//        "Default": "Data Source=DESKTOPCOMP\\SERVER2019;Initial Catalog=co2;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=False;Application Intent=ReadWrite;Multi Subnet Failover=False"
//    }
//}


//Note that you can get the connection string from the SQL Server Object Explorer (assuming you are using Visual Studio) by expanding the database node and then retrieving the ConnectionString from the Properties window
string cs = configuration.GetConnectionString("Default")!;
var sql = host.Services.GetRequiredService<SqlServerExplorer>();
//we could also instantiate the object without using dependency injection
//var sql = new SqlServerExplorer();

//assume that we have a table named TEST_TABLE
string tableName = "TEST_TABLE";

try
{
    //checking SQL server connectivity to the database
    bool testConnection = await sql.TestConnection(cs);
    if (testConnection)
        logger.LogInformation("Connection is successfully tested.");
    else
    {
        logger.LogError("Could not connect to the database.");
        return;
    }
    //------------------------------------


    //check that the table exists---------
    bool tableExists = await sql.TableExists(cs, tableName);
    if (tableExists) logger.LogInformation("Table {t} already exists.", tableName);

    //if not then create the table with int, string and float fields
    if (!tableExists)
    {
        await sql.Execute(cs, $"create table [{tableName}](id int, name nvarchar(50), value float)");
        logger.LogInformation("Successfully created table {t}.", tableName);

    }
    //------------------------------------

    //check for data ---------------------
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

    //to clear all data from the table
    await sql.TruncateTable(cs, tableName);

    //------------------------------------


    //Data exploration--------------------

    //check the number of records
    int recordsCount = await sql.GetRecordsCount(cs,tableName);
    logger.LogInformation("Number of records: {c}.",recordsCount);

    //list all tables
    logger.LogInformation("Listing tables...");
    List<SqlServerTable> tables = await sql.GetTables(cs);
    foreach(var table in tables)
        logger.LogInformation("Schema: {s}, Name: {n}, Full name: {t}", table.Schema, table.Name, table.ToString());
    /*
    info: SQLServerExplorer[0]
          Schema: dbo, Name: TEST_TABLE, Full name: [dbo].[TEST_TABLE]
     */


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

    //we can also get the records count using the myTable object
    recordsCount = await sql.GetRecordsCount(cs, myTable);

    //------------------------------------

    //Data exports------------------------

    string selectSql = $"select * from [{tableName}]";

    //get custom scalar value
    int customCount = await sql.QueryScalar<int>(cs, $"{selectSql} where id>1");

    //get the datatable of a query result
    DataTable data = await sql.Query(cs, selectSql);

    //get top rows (practical for quick preview purposes)
    DataTable previewData = await sql.GetTopRecords(cs, myTable, count: 200);

    //get the data AND save the result to a file
    data = await sql.QueryToFile(cs, selectSql, targetFile: "./mydata.csv", fieldSeparator: ",");

    //save a DataTable to a file
    sql.SaveToFile(data, targetFile: "./mydata.csv", fieldSeparator: ",");

    //------------------------------------


    //Bulk copy operations----------------

    //to control batch operations
    sql.BulkCopyTimeoutInSeconds = 0; //0 (default) means no timeout
    sql.BatchSize = 0; //0 (default) means send all records at one (for large datasets this value needs tuning for maximum performance

    //copy data from a datatable to a table (fast way). The target table must pre-exist.
    //an optional event handler may notify when the data has been loaded prior to bulk-copying
    sql.LoadedData += (o, e) => { logger.LogInformation("Data has been loaded"); };
    await sql.CopyTo(cs, data, destinationTableName: "OTHER_TABLE");

    //copy data to another database. A table with the same name must pre-exist.
    await sql.CopyTo(cs, targetConnectionString: "other_database_string", table: myTable);
    //------------------------------------

}

catch (Exception e)
{
    logger.LogCritical(e.Message);
    return;
}



