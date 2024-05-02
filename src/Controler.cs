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
        static readonly string[] data_samples = ["historical_data_3.json", "historical_data_2.json"];
        //static readonly string[] data_samples = ["historical_data_3.json"];
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
                    throw new InvalidDataException("Error loading configuration: " + configErrorMessage);
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

        private async Task<APIDeviceDetailData?> GetDeviceDetails(EcowittDeviceConfiguration configuredDevice)
        {
            if (!hasConfig) throw new InvalidOperationException("No configuration to get device details");            
            EcowittDevice device = new EcowittDevice(configuredDevice, _configuration.APIKey, _configuration.ApplicationKey);
            Console.WriteLine("Fetching device details for: " + device.Configuration.MAC + "\n ===");
            var result = await device.GetDeviceInfo();            
            //Console.WriteLine(dt.Result + "\n ===");
            if (string.IsNullOrWhiteSpace(result)) return null;
            try
            {
                JsonDocument json = JsonDocument.Parse(result);
                APIDeviceDetailData? deviceDetailData = JsonSerializer.Deserialize<APIDeviceDetailData>(
                    json.RootElement.GetProperty("data"));
                if (deviceDetailData != null)
                {
                    JsonSerializerOptions options = new JsonSerializerOptions() { WriteIndented = true };
                    var s = JsonSerializer.Serialize(deviceDetailData, options);
                    Console.WriteLine(s);                        
                }
                return deviceDetailData;
            }
            catch (JsonException ex)
            {
                Console.WriteLine("Failed to deserialize device details. " + ex.Message);
                return null;
            }                        
        }

        public void RunProcessing(DataProcessingMode mode, bool initialRun = false)
        {
            if (!hasConfig) throw new InvalidOperationException("No configuration to start processing");

            
            if (mode == DataProcessingMode.Offline) RunOfflineProcessing();
            if (mode == DataProcessingMode.Online) RunOnlineProcessing(initialRun);
        }

        private void RunOnlineProcessing(bool initialRun)
        {         
            DateTime endTime = DateTime.Now;
            DateTime startTime;
            if (initialRun) startTime = endTime.AddDays(-90);
            else startTime = endTime.AddMinutes(-360);
            // First get configured devices details to eliminate devices that do not exist in cloud API
            List<(Task<APIDeviceDetailData?>, EcowittDeviceConfiguration)> detailsRequestTasks = 
                new List<(Task<APIDeviceDetailData?>,EcowittDeviceConfiguration)>();
            foreach (var configuredDevice in _configuration.ConfigurationSettings.Devices)
            {
                var task = GetDeviceDetails(configuredDevice);             
                detailsRequestTasks.Add(new (task,configuredDevice));
                // rate throttling in API - 1 req / sec
                Thread.Sleep(1000);
            }
            Task.WaitAll(detailsRequestTasks.Select( x => x.Item1).ToArray());
            List<EcowittDeviceConfiguration> verifiedDevices = new List<EcowittDeviceConfiguration>();
            Dictionary<string, APIDeviceDetailData> devicesDetails = new Dictionary<string, APIDeviceDetailData>();
            foreach (var task in detailsRequestTasks)
            {
                if (task.Item1.IsCompleted)
                {
                    var deviceDetails = task.Item1.Result;
                    if (deviceDetails == null || !deviceDetails.Validate())
                    {
                        Console.WriteLine("Skipping device (no details or invalid device details) for: " + task.Item2.MAC);
                        continue;
                    }
                    else
                    {
                        verifiedDevices.Add(task.Item2);
                        if (!devicesDetails.ContainsKey(task.Item2.MAC)) devicesDetails.Add(task.Item2.MAC, deviceDetails);
                    }
                }
            }
            if (!verifiedDevices.Any())
            {
                Console.WriteLine("None of configured devices seems to be available");
                return;
            }
            // Now fetch the data for each device that has been confirmed to exist in the Ecowitt cloud using device configuration
            List<(Task<string?>,EcowittDevice)> dataRequestTasks = new List<(Task<string?>,EcowittDevice)>();
            foreach (var configuredDevice in verifiedDevices)
            {            
                var sb = new StringBuilder();
                foreach (var c in configuredDevice.ConfiguredChannels)
                {
                    sb.Append(c+",");
                }
                var channelsList = sb.ToString().Trim(',');
                EcowittDevice device = new EcowittDevice(configuredDevice, _configuration.APIKey, _configuration.ApplicationKey);
                Console.WriteLine("Fetching data for device: " + device.Configuration.MAC + " Channels: " +channelsList );
                var rTask = device.ReadHistoricalData(startTime, endTime);
                dataRequestTasks.Add(new(rTask, device));
                // rate throttling in API - 1 req / sec
                Thread.Sleep(1000);
            }
            var tasksList = dataRequestTasks.Select(x => x.Item1).ToArray();
            Task.WaitAll(tasksList);            
            // Using fetched data send it to output channels
            foreach (var task in dataRequestTasks)
            {
                if (task.Item1.IsCompleted)
                {
                    List<DataChannelMetaData> dataInputChannels = new List<DataChannelMetaData>();
                    List<DataChannelMetaData> channelsToBeProcessed = new List<DataChannelMetaData>();
                    var s = task.Item1.Result;
                    var inputData = new EcowittInputData(s);
                    try
                    {
                        inputData.ProcessInput();
                        dataInputChannels = inputData.GetChannels();
                    }
                    catch (Exception ex)
                    {
                        string em = string.Format("Failed to get input data for device: {0} . Error: {1}",
                            task.Item2.Configuration.MAC, ex.Message);
                        Console.WriteLine(em);
                        continue;
                    }
                    foreach (var channel in dataInputChannels)
                    {
                        if (task.Item2.Configuration.ConfiguredChannels.Contains(channel.ChannelName))
                        {
                            channelsToBeProcessed.Add(channel);
                        }
                    }
                    var deviceDetails = devicesDetails.Where(x => x.Key == task.Item2.Configuration.MAC).FirstOrDefault().Value;
                    if (deviceDetails == null) throw new NullReferenceException("Fatal error can't find configured device by MAC");
                    foreach (var channel in channelsToBeProcessed)
                    {                        
                        OutputChannelMetadata meta = new OutputChannelMetadata()
                        {
                            DeviceName = deviceDetails.Name,
                            MAC = deviceDetails.MAC,
                            StationType = deviceDetails.StationType,
                            DeviceCloudId = deviceDetails.Id,
                            DeviceCreationTime = deviceDetails.CreateTime,
                            DeviceLatitude = deviceDetails.Latitude,
                            DeviceLongitude = deviceDetails.Longitude,
                            ChannelName = channel.ChannelName
                        };
                        OutputChannelBehaviorConfiguration channelConfig = new OutputChannelBehaviorConfiguration()
                        {
                        };
                        var outputChannel = new CSVFileOutputChannel(meta, channelConfig);
                        if (outputChannel.InitChannel(out string errorMessage))
                        {
                            var channelData = inputData.GetChannel(channel.ChannelName);
                            if (channelData == null) continue;
                            outputChannel.AddData(channelData);
                            outputChannel.SaveData();
                        }
                        else
                        {
                            var em = string.Format("Error initializing channel: {0} for device: {1} Error message: {2}",
                                channel.ChannelName, task.Item2.Configuration.MAC, errorMessage);
                            Console.WriteLine(em);
                        }
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
            List<string> configuredchannels = _configuration.ConfigurationSettings.Devices[1].ConfiguredChannels;
            List<DataChannelMetaData> channelsToBeProcessed = new List<DataChannelMetaData>();

            for (int i = 0; i < data_samples.Length; i++)
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
                    OutputChannelBehaviorConfiguration channelConfig = new OutputChannelBehaviorConfiguration()
                    {
                    };
                    var outputChannel = new CSVFileOutputChannel(null,channelConfig);
                    outputChannel.InitChannel(out string message);
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
