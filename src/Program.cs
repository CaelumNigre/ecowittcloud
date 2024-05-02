using Ecowitt;

// See https://aka.ms/new-console-template for more information

var ctrl = new Controler(ConfigurationContext.Cmdline, false);
ctrl.RunProcessing(DataProcessingMode.Online);
//ctrl.RunProcessing(DataProcessingMode.Offline);
Thread.Sleep(2000);