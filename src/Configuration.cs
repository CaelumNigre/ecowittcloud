using Ecowitt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ecowitt
{
    public enum TimestampFormats { Timestamp, UTC }

    internal class EcowittDevice
    {
        public string MAC { get; set; } = "00:00:00:00:00:00";
        public int PollRateMinutes { get; set; } = 60;
        public List<string> ConfiguredChannels { get; set; } = new List<string>();
        public int TemperatureUnit { get; set; } = 1;
        public int PressureUnit { get; set; } = 3;
        public int WindSpeedUnit { get; set; } = 6;
        public int RainfallUnit { get; set; } = 12;
        public int SolarIrradianceUnit { get; set; } = 16;
        public OutputChannelConfiguration OutputChannel { get; set; } = new OutputChannelConfiguration();
    }

    internal class OutputChannelConfiguration
    {
        public string? Type { get; set; }
        public int ID { get; set; }
        public Dictionary<string, string> CustomChannelsNames { get; set; } = new Dictionary<string, string>();
        public string? TimeStampFormat { get; set; } = "Timestamp";
    }

    internal class OutputChannelDefinition
    {
        public string? Type { get; set; }
        public int ID { get; set; }
        public string? URL { get; set; }
    }

    internal class ConfigurationData
    {
        public List<EcowittDevice> Devices { get; set; } = new List<EcowittDevice>();
        public List<OutputChannelDefinition> OutputChannels { get; set; } = new List<OutputChannelDefinition>();
    }

    internal class Configuration
    {
        public readonly string ConfigFileName;
        private string? _rawConfig = null;
        public ConfigurationData ConfigurationSettings { get; private set;}

        public Configuration(string configFileName) {
            ConfigFileName = configFileName;
            ConfigurationSettings = new ConfigurationData();
        }

        public bool ReadConfiguration(out string errorMessage)
        {
            errorMessage = "";
            try
            {
                using (StreamReader sr = new StreamReader(ConfigFileName))
                {
                    _rawConfig = sr.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
            return true;
        }

        public bool ValidateConfiguration(out string errorMessage)
        {
            errorMessage = "";
            if (_rawConfig == null) return false;
            var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, };
            try
            {
                var config = JsonSerializer.Deserialize<ConfigurationData>(_rawConfig, options);
                if (config == null) return false;
                if (config.Devices == null || !config.Devices.Any())
                {
                    errorMessage = "No devices found in configuration";
                    return false;
                }
                if (config.OutputChannels == null || !config.OutputChannels.Any())
                {
                    errorMessage = "No output channels found in configuration";
                    return false;
                }
                foreach (var channelDefinition in config.OutputChannels)
                {
                    if (!Enum.TryParse(typeof(ChannelTypes),channelDefinition.Type, out var channelType))
                    {
                        errorMessage = "Invalid channel type: " + channelDefinition.Type;
                        return false;
                    }
                    if (channelDefinition.ID < 0)
                    {
                        errorMessage = "Invalid channel ID";
                        return false;
                    }
                    if (!Uri.TryCreate(channelDefinition.URL, new UriCreationOptions(),out Uri? result))
                    {
                        errorMessage = "Invalid URL: " + channelDefinition.URL;
                        return false;
                    }
                }
                foreach (var device in config.Devices)
                {
                    if (device.MAC == "00:00:00:00:00:00")
                    {
                        errorMessage = "MAC address not defined for device";
                        return false;
                    }
                    if (device.PollRateMinutes < 10 || device.PollRateMinutes > 10080 )
                    {
                        errorMessage = "Invalid device poll rate";
                        return false;
                    }
                    if (device.ConfiguredChannels == null || !device.ConfiguredChannels.Any())
                    {
                        errorMessage = "No channels defined for device";
                        return false;
                    }
                    if (device.OutputChannel == null)
                    {
                        errorMessage = "No output channel defined for device";
                        return false;
                    }
                    if (!Enum.TryParse(typeof(ChannelTypes), device.OutputChannel.Type, out var channelType))
                    {
                        errorMessage = "Invalid channel type: " + device.OutputChannel.Type;
                        return false;
                    }
                    if (device.OutputChannel.ID < 0)
                    {
                        errorMessage = "Invalid channel ID";
                        return false;
                    }
                    if (!Enum.TryParse(typeof(TimestampFormats), device.OutputChannel.TimeStampFormat, out var timestampFormat))
                    {
                        errorMessage = "Invalid timestamp format: " + device.OutputChannel.TimeStampFormat;
                        return false;
                    }
//FIXME add validation of channel ID with channel definitions
                }
                ConfigurationSettings = config;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
            return true;
        }

        public List<DataChannelMetaData> GetConfiguredInputChannels()
        {
            var result = new List<DataChannelMetaData>();
            DataChannelMetaData a = new DataChannelMetaData("indoor");
            result.Add(a);
            a = new DataChannelMetaData("outdoor");
            result.Add(a);
            a = new DataChannelMetaData("rainfall");
            result.Add(a);
            a = new DataChannelMetaData("wind");
            result.Add(a);
            a = new DataChannelMetaData("lightning");
            result.Add(a);
            return result;
        }


    }
}
