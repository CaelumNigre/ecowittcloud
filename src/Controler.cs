using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Ecowitt.EcowittDevice;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Ecowitt
{
    public enum DataProcessingMode { Online, Offline };

    internal class Controler
    {
        

        private Configuration _configuration;
        static readonly string[] data_samples = ["historical_data.json", "historical_data_2.json"];
        private bool hasConfig = false;

        public Controler(ConfigurationContext context, bool useKV) {
            Console.WriteLine("Initializing configuration settings");
            _configuration = new Configuration("configuration.json", context, useKV);
            if (_configuration == null) throw new NullReferenceException("Failed to initialize configuration");
            Console.WriteLine("Reading configuration file");
            if (!_configuration.ReadConfiguration(out string configErrorMessage))
            {
                Console.WriteLine("Error loading configuration: " + configErrorMessage);
            }
            else
            {
                Console.WriteLine("Validating configuration settings");
                if (!_configuration.ValidateConfiguration(out configErrorMessage))
                {
                    Console.WriteLine("Error loading configuration: " + configErrorMessage);
                }
                else hasConfig = true;
            }
        }

        public static DateTime UnixTimeStampToDateTime(uint unixTimeStamp)
        {            
            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(unixTimeStamp).ToUniversalTime();
            return dateTime;
        }

        public void RunProcessing(DataProcessingMode mode)
        {
            if (!hasConfig) throw new InvalidOperationException("No configuration to start processing");

            
            if (mode == DataProcessingMode.Offline) RunOfflineProcessing();
            if (mode == DataProcessingMode.Online) RunOnlineProcessing();
        }

        private void RunOnlineProcessing()
        {
            List<string> configuredchannels = _configuration.ConfigurationSettings.Devices[0].ConfiguredChannels;
            List<DataChannelMetaData> dataInputChannels = new List<DataChannelMetaData>();
            List<DataChannelMetaData> channelsToBeProcessed = new List<DataChannelMetaData>();

            DateTime endTime = DateTime.Now;
            DateTime startTime = endTime.AddMinutes(-360);
            List<(Task<string>,EcowittDevice)> requestTasks = new List<(Task<string>,EcowittDevice)>();
            foreach (var configuredDevice in _configuration.ConfigurationSettings.Devices)
            {
                EcowittDevice device = new EcowittDevice(configuredDevice, _configuration.APIKey, _configuration.ApplicationKey);
                Console.WriteLine("Fetching data for device: " + device.Configuration.MAC);
                var rTask = device.ReadHistoricalData(startTime, endTime);
                requestTasks.Add(new (rTask,device));
            }
            var tasksList = requestTasks.Select(x => x.Item1).ToArray();
            Task.WaitAll(tasksList);
            foreach (var task in requestTasks)
            {
                if (task.Item1.IsCompleted)
                {
                    var s = task.Item1.Result;
                    var inputData = new EcowittInputData(s);
                    try
                    {
                        inputData.ProcessInput();
                        dataInputChannels = inputData.GetChannels();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return;
                    }
                    foreach (var channel in dataInputChannels)
                    {
                        if (configuredchannels.Contains(channel.ChannelName))
                        {
                            channelsToBeProcessed.Add(channel);
                        }
                    }
                    foreach (var channel in channelsToBeProcessed)
                    {
                        var outputChannel = new CSVFileOutputChannel(channel.ChannelName);
                        outputChannel.InitChannel();
                        var channelData = inputData.GetChannel(channel.ChannelName);
                        if (channelData == null) continue;
                        outputChannel.AddData(channelData);
                        outputChannel.SaveData();
                    }
                }
                else
                {
                    Console.WriteLine("Failed to get data for device: " + task.Item2.Configuration.MAC);
                }
            }            
        }

        private void RunOfflineProcessing()
        {
            List<DataChannelMetaData> dataInputChannels = new List<DataChannelMetaData>();
            List<string> configuredchannels = _configuration.ConfigurationSettings.Devices[0].ConfiguredChannels;
            List<DataChannelMetaData> channelsToBeProcessed = new List<DataChannelMetaData>();

            for (int i = 0; i < 2; i++)
            {
                var s = ReadJsonFromFile(data_samples[i]);
                var inputData = new EcowittInputData(s);
                try
                {
                    inputData.ProcessInput();
                    dataInputChannels = inputData.GetChannels();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return;
                }
                foreach (var channel in dataInputChannels)
                {
                    if (configuredchannels.Contains(channel.ChannelName))
                    {
                        channelsToBeProcessed.Add(channel);
                    }
                }
                foreach (var channel in channelsToBeProcessed)
                {
                    var outputChannel = new CSVFileOutputChannel(channel.ChannelName);
                    outputChannel.InitChannel();
                    var channelData = inputData.GetChannel(channel.ChannelName);
                    if (channelData == null) continue;
                    outputChannel.AddData(channelData);
                    outputChannel.SaveData();
                }
            }
        }

        private string ReadJsonFromFile(string fName)
        {
            string s;
            using (StreamReader reader = new StreamReader(fName, Encoding.UTF8))
            {
                s = reader.ReadToEnd();
                reader.Close();
            }
            return s;
        }
    }
}
