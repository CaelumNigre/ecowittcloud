using Ecowitt;

// See https://aka.ms/new-console-template for more information

var ctrl = new Controler(ConfigurationContext.Cmdline, true);
//ctrl.RunProcessing(DataProcessingMode.Online,true);
ctrl.RunProcessing(DataProcessingMode.Online, false);
//ctrl.RunProcessing(DataProcessingMode.Offline);
Thread.Sleep(2000);