using Azure.Identity;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using static Ecowitt.EcowittDevice;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Ecowitt
{
    public enum DataProcessingMode { Online, Offline };

   

    internal class Controler
    {
        private class DataRequestTask
        {
            public Task<string?>? Task { get; set; }
            public EcowittDevice Device { get; set; }
            public int ConfiguredDeviceIndex { get; set; }
            public int requestIndex { get; set; }              
        }

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
            if (initialRun) startTime = endTime.AddDays(-89);
            else startTime = endTime.AddMinutes(-360);
            // First get configured devices details to eliminate devices that do not exist in cloud API
            List<(Task<APIDeviceDetailData?>, EcowittDeviceConfiguration,int)> detailsRequestTasks = 
                new List<(Task<APIDeviceDetailData?>,EcowittDeviceConfiguration,int)>();
            for (int i=0; i<_configuration.ConfigurationSettings.Devices.Count; i++)
            {
                var configuredDevice = _configuration.ConfigurationSettings.Devices[i];
                var task = GetDeviceDetails(configuredDevice);             
                detailsRequestTasks.Add(new (task,configuredDevice,i));
                // rate throttling in API - 1 req / sec
                Thread.Sleep(1000);
            }
            Task.WaitAll(detailsRequestTasks.Select( x => x.Item1).ToArray());
            List<(EcowittDeviceConfiguration,int)> verifiedDevices = new List<(EcowittDeviceConfiguration,int)>();
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
                        verifiedDevices.Add(new (task.Item2,task.Item3));
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
            List<DataRequestTask> dataRequestTasks = new List<DataRequestTask>();
            foreach (var configuredDevice in verifiedDevices)
            {            
                var sb = new StringBuilder();
                foreach (var c in configuredDevice.Item1.ConfiguredChannels)
                {
                    sb.Append(c+",");
                }
                var channelsList = sb.ToString().Trim(',');
                EcowittDevice device = new EcowittDevice(configuredDevice.Item1, _configuration.APIKey, _configuration.ApplicationKey);
                Console.WriteLine("Fetching data for device: " + device.Configuration.MAC + " Channels: " +channelsList );
                if (initialRun)
                {
                    DateTime currentStartTime = startTime;
                    DateTime currentEndTime = currentStartTime.AddMinutes(1439);
                    int idx = 0;
                    while (currentStartTime < endTime.AddDays(-1))
                    {
                        DataRequestTask drTask = new DataRequestTask();
                        drTask.Task = device.ReadHistoricalData(currentStartTime, currentEndTime);                        
                        drTask.Device = device;
                        drTask.ConfiguredDeviceIndex = configuredDevice.Item2;
                        drTask.requestIndex = idx;
                        dataRequestTasks.Add(drTask);
                        currentStartTime = currentStartTime.AddDays(1);
                        currentEndTime = currentStartTime.AddMinutes(1439);
                        idx++;
                        Thread.Sleep(5000);
                    }
                    DataRequestTask lastTask = new DataRequestTask();
                    lastTask.Task = device.ReadHistoricalData(currentStartTime, endTime);
                    lastTask.Device = device;
                    lastTask.ConfiguredDeviceIndex = configuredDevice.Item2;
                    lastTask.requestIndex = idx;
                    dataRequestTasks.Add(lastTask);
                }
                else
                {
                    DataRequestTask drTask = new DataRequestTask();
                    drTask.Task = device.ReadHistoricalData(startTime, endTime);
                    drTask.Device = device;
                    drTask.ConfiguredDeviceIndex = configuredDevice.Item2;
                    drTask.requestIndex = 0;
                    dataRequestTasks.Add(drTask);
                }
                // rate throttling in API - 1 req / sec
                Thread.Sleep(1000);
            }
            var tasksList = dataRequestTasks.Select(x => x.Task).ToArray();
            Task.WaitAll(tasksList);
            // sort data fetch results so they are properly sorted by requestIndex
            dataRequestTasks.Sort((a, b) =>
            {
                if (a.ConfiguredDeviceIndex < b.ConfiguredDeviceIndex) return -1; 
                if (a.ConfiguredDeviceIndex == b.ConfiguredDeviceIndex)
                {
                    if (a.requestIndex < b.requestIndex) return -1; 
                    if (a.requestIndex > b.requestIndex) return 1;
                    return 0;
                }
                return 1;
            });
            // Initialize output channels                        
            Dictionary<string,IOutputChannel> outputChannels = new Dictionary<string,IOutputChannel>();
            foreach (var verifiedDevice in verifiedDevices)
            {
                var deviceItem = devicesDetails.Where( x => x.Key == verifiedDevice.Item1.MAC).FirstOrDefault();
                var deviceDetails = deviceItem.Value;
                foreach (var inputChannel in verifiedDevice.Item1.ConfiguredChannels)
                {
                    string id = verifiedDevice.Item1.MAC + inputChannel;
                    if (outputChannels.ContainsKey(id)) continue;
                    OutputChannelMetadata meta = new OutputChannelMetadata()
                    {
                        DeviceName = deviceDetails.Name,
                        MAC = deviceDetails.MAC,
                        StationType = deviceDetails.StationType,
                        DeviceCloudId = deviceDetails.Id,
                        DeviceCreationTime = deviceDetails.CreateTime,
                        DeviceLatitude = deviceDetails.Latitude,
                        DeviceLongitude = deviceDetails.Longitude,
                        ChannelName = inputChannel
                    };
                    OutputChannelConfiguration currentChannelConfigurationSettings =
                        _configuration.ConfigurationSettings.Devices[verifiedDevice.Item2].OutputChannel;
                    meta.TimestampFormat = currentChannelConfigurationSettings.TimeStampFormat;
                    OutputChannelBehaviorConfiguration channelConfig = new OutputChannelBehaviorConfiguration()
                    {
                        AllowLocationChange = currentChannelConfigurationSettings.LocationChangesAllowed,
                        AllowStationTypeChange = currentChannelConfigurationSettings.StationTypeChangesAllowed
                    };
                    var configuredOutputChannelID = currentChannelConfigurationSettings.ID;
                    var configuredOutputChannel =
                        _configuration.ConfigurationSettings.OutputChannels.Where(x => x.ID == configuredOutputChannelID).FirstOrDefault();
                    if (configuredOutputChannel == null) throw new NullReferenceException("Fatal error can't find output channel configuration");
                    IOutputChannel? outputChannel = null;
                    switch (configuredOutputChannel.Type)
                    {
                        case ChannelTypes.File:
                            {
                                outputChannel = new CSVFileOutputChannel(configuredOutputChannel.URL, meta, channelConfig);
                                if (!outputChannel.InitChannel(out string errorMessage))
                                {
                                    var em = string.Format("Error initializing channel: {0} for device: {1} Error message: {2}",
                                        inputChannel, verifiedDevice.Item1.MAC, errorMessage);
                                    Console.WriteLine(em);
                                    outputChannel = null;
                                }
                                break;
                            }
                        case ChannelTypes.Blob:
                            {
                                outputChannel = new BlobOutputChannel(configuredOutputChannel.URL, 
                                    new DefaultAzureCredential(_configuration.AzureCredential), 
                                    meta, channelConfig);
                                if (!outputChannel.InitChannel(out string errorMessage))
                                {
                                    var em = string.Format("Error initializing channel: {0} for device: {1} Error message: {2}",
                                        inputChannel, verifiedDevice.Item1.MAC, errorMessage);
                                    outputChannel = null;
                                    Console.WriteLine(em);
                                }
                                break;
                            }
                        default:
                            {
                                var em = string.Format("This output channel type is not implemented yet. {0}",
                                    configuredOutputChannel.Type.ToString());
                                //throw new NotImplementedException(em);
                                Console.WriteLine(em);
                                break;
                            }
                    }
                    if (outputChannel!=null) outputChannels.Add(id,outputChannel);
                }
            }            
            // Using fetched data send it to output channels
            foreach (var task in dataRequestTasks)
            {
                if (task.Task.IsCompleted)
                {                    
                    var s = task.Task.Result;
                    var inputData = new EcowittInputData(s);
                    List<DataChannelMetaData> dataInputChannels;
                    try
                    {
                        inputData.ProcessInput();
                        dataInputChannels = inputData.GetChannels();
                    }
                    catch (Exception ex)
                    {
                        string em = string.Format("Failed to get input data for device: {0} . Error: {1}",
                            task.Device.Configuration.MAC, ex.Message);
                        Console.WriteLine(em);
                        continue;
                    }
                    foreach (var channel in dataInputChannels)
                    {
                        string id = task.Device.Configuration.MAC + channel.ChannelName;
                        if (outputChannels.ContainsKey(id))
                        {
                            var outputChannel = outputChannels[id];
                            var inputChannel = inputData.GetChannel(channel.ChannelName);
                            outputChannel.AddData(inputChannel);
                        }
                        else
                        {
                            string em = string.Format("No valid output channel for device: {0} channel: {1}",
                            task.Device.Configuration.MAC,channel.ChannelName);
                            Console.WriteLine(em);
                        }
                    }                   
                }
                else
                {
                    Console.WriteLine("Failed to get data for device: " + task.Device.Configuration.MAC + " request id: " + task.requestIndex);
                }                
            }
            foreach (var outputChannel in outputChannels)
            {
                outputChannel.Value.SaveData();
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
                    OutputChannelMetadata channelMetadata = new OutputChannelMetadata()
                    {
                        ChannelName = channel.ChannelName,
                        DeviceName = "ecowitt_dummy",
                        StationType = "dummy_station_type", 
                        MAC = "00:00:00:00:00:00"
                    };
                    var outputChannel = new CSVFileOutputChannel(null,channelMetadata,channelConfig);
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
