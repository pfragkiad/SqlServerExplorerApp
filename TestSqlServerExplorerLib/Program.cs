using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SqlServerExplorerLib;

var host = Host
    .CreateDefaultBuilder()
    .ConfigureServices(services =>
    services.AddSingleton<SqlServerExplorer>()
).Build();



