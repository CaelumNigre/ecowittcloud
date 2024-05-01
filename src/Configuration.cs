using Ecowitt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Azure.Identity;
using System.Reflection;
using Azure.Core.Diagnostics;

namespace Ecowitt
{
    public enum TimestampFormats { Timestamp, UTC };
    public enum ConfigurationContext { Cmdline, AzureFunction };    

    internal class EcowittDeviceConfiguration
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

        public bool Validate(bool validateOutputChannels, out string errorMessage)
        {
            errorMessage = "";
            if (MAC == "00:00:00:00:00:00")
            {
                errorMessage = "MAC address not defined for device";
                return false;
            }
            if (PollRateMinutes < 10 || PollRateMinutes > 10080)
            {
                errorMessage = "Invalid device poll rate";
                return false;
            }
            if (ConfiguredChannels == null || !ConfiguredChannels.Any())
            {
                errorMessage = "No channels defined for device";
                return false;
            }
            if (validateOutputChannels)
            {
                if (OutputChannel == null)
                {
                    errorMessage = "No output channel defined for device";
                    return false;
                }
                if (!Enum.TryParse(typeof(ChannelTypes), OutputChannel.Type, out var channelType))
                {
                    errorMessage = "Invalid channel type: " + OutputChannel.Type;
                    return false;
                }
                if (OutputChannel.ID < 0)
                {
                    errorMessage = "Invalid channel ID";
                    return false;
                }
                if (!Enum.TryParse(typeof(TimestampFormats), OutputChannel.TimeStampFormat, out var timestampFormat))
                {
                    errorMessage = "Invalid timestamp format: " + OutputChannel.TimeStampFormat;
                    return false;
                }
            }
            return true;
        }
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
        public List<EcowittDeviceConfiguration> Devices { get; set; } = new List<EcowittDeviceConfiguration>();
        public List<OutputChannelDefinition> OutputChannels { get; set; } = new List<OutputChannelDefinition>();
    }

    internal class Configuration
    {
        public readonly string ConfigFileName;
        private string? _rawConfig = null;
        private ConfigurationContext _context;
        private IConfigurationRoot _environmentConfig;
        private IConfigurationRoot _secretsConfig;          
        
        public ConfigurationData ConfigurationSettings { get; private set;}
        public string? APIKey { get; private set; }
        public string? ApplicationKey { get; private set; }
        public readonly bool UseKeyVaultForSecrets;


        public Configuration(string configFileName, ConfigurationContext context, bool useKV = true)
        {
            ConfigFileName = configFileName;
            ConfigurationSettings = new ConfigurationData();
            _context = context;
            _environmentConfig = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            UseKeyVaultForSecrets = useKV;
            if (useKV)
            {
                var kv = _environmentConfig["KV_NAME"];
                if (string.IsNullOrEmpty(kv)) throw new InvalidOperationException("No Key Vault configured");
                var tid = _environmentConfig["TENANT_ID"];
                DefaultAzureCredentialOptions _defaultAzureCredentialOptions;  
                if (tid != null)
                    _defaultAzureCredentialOptions = new DefaultAzureCredentialOptions()
                    {
                        ExcludeAzureCliCredential = true,
                        ExcludeAzurePowerShellCredential = true,
                        ExcludeManagedIdentityCredential = true,
                        TenantId = tid
                    };
                else
                {
                    _defaultAzureCredentialOptions = new DefaultAzureCredentialOptions()
                    {
                        ExcludeAzureCliCredential = true,
                        ExcludeAzurePowerShellCredential = true,
                        ExcludeManagedIdentityCredential = true
                    };
                }
                var _keyVaultUri = new Uri("https://" + kv + ".vault.azure.net");
                _secretsConfig = new ConfigurationBuilder()
                    .AddAzureKeyVault(_keyVaultUri, new DefaultAzureCredential(_defaultAzureCredentialOptions))
                    .Build();                
            }
            else
            {
                _secretsConfig = new ConfigurationBuilder()
                    .AddJsonFile("secrets.json")
                    .Build();
            }
            APIKey = _secretsConfig["api-key"];
            if (string.IsNullOrWhiteSpace(APIKey)) throw new InvalidOperationException("No API key provided");
            ApplicationKey = _secretsConfig["application-key"];
            if (string.IsNullOrWhiteSpace(ApplicationKey)) throw new InvalidOperationException("No Application key provided");
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
                    if (_context == ConfigurationContext.AzureFunction)
                    {
                        if (!Uri.TryCreate(channelDefinition.URL, new UriCreationOptions(), out Uri? result))
                        {
                            errorMessage = "Invalid URL: " + channelDefinition.URL;
                            return false;
                        }
                    }
                }
                foreach (var device in config.Devices)
                {
                    if (!device.Validate(true, out  errorMessage)) return false;                                        
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

    }
}
