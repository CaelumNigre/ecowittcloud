using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Ecowitt;

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole(options =>
    {
        options.IncludeScopes = true;
    });
});
var host = builder.Build();

var rootCommand = new RootCommand
{
    new Option<string>(new string[] { "--mode", "-m" },
        getDefaultValue: () => "offline",
        description: "Specify the processing mode: online or offline (using data samples in "),
    new Option<bool>(
        "--initial",
        () => false,
        "Run the processing in the initialization mode")
};
rootCommand.Description = "Ecowitt data processing application";
rootCommand.Handler = CommandHandler.Create<string, bool>((mode, initial) =>
{
    var logger = host.Services.GetService<ILogger<Program>>();
    var ctrl = new Controler(ConfigurationContext.Cmdline, logger, true);
    if (mode == "online")
    {
        ctrl.RunProcessing(DataProcessingMode.Online, initial);
    }
    else if (mode == "offline")
    {
        ctrl.RunProcessing(DataProcessingMode.Offline, initial);
    }
    else
    {
        Console.WriteLine("Please specify a valid mode: online or offline");
        return 1;
    }
    Thread.Sleep(2000);
    return 0;
});
return rootCommand.InvokeAsync(args).Result;
