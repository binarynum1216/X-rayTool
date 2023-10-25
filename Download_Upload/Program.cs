using Download_Upload;
using Serilog;

string currentDirectory = AppContext.BaseDirectory;
string configFile = Path.Combine(currentDirectory, "logs.txt");

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(configFile, rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    IHost host = Host.CreateDefaultBuilder(args)
                    .UseSerilog()
                    .ConfigureServices(services =>
                    {
                        services.AddHostedService<Worker>();
                    })
                    .UseWindowsService()
                    .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "The application failed to start");
}
finally
{
    Log.CloseAndFlush();
}


