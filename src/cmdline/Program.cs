using Ecowitt;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;

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
rootCommand.Handler = CommandHandler.Create<string,bool>((mode, initial) =>
{
    var ctrl = new Controler(ConfigurationContext.Cmdline, true);

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
