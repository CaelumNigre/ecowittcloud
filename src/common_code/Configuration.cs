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
using static Ecowitt.EcowittDevice;
using System.Text.Json.Serialization;

namespace Ecowitt
{
    public enum TimestampFormats { Timestamp, UTC };
    public enum ConfigurationContext { Cmdline, AzureFunction };  
    

    internal class EcowittDeviceConfiguration
    {
        public string MAC { get; set; } = "00:00:00:00:00:00";
        public int PollRateMinutes { get; set; } = 60;
        public List<string> ConfiguredChannels { get; set; } = new List<string>();
        public EcowittTemperatureUnits TemperatureUnit { get; set; } = EcowittTemperatureUnits.Celsius;
        public EcowittPressureUnits PressureUnit { get; set; } = EcowittPressureUnits.hPa;
        public EcowittWindSpeedUnits WindSpeedUnit { get; set; } = EcowittWindSpeedUnits.mps;
        public EcowittRainfallUnits RainfallUnit { get; set; } = EcowittRainfallUnits.mm;
        public EcowittSolarIrradianceUnits SolarIrradianceUnit { get; set; } = EcowittSolarIrradianceUnits.Wpm;
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
                if (OutputChannel.ID < 0)
                {
                    errorMessage = "Invalid channel ID";
                    return false;
                }                
            }
            return true;
        }
    }

    internal class OutputChannelConfiguration
    {        
        public int ID { get; set; }
        public Dictionary<string, string> CustomChannelsNames { get; set; } = new Dictionary<string, string>();
        public TimestampFormats TimeStampFormat { get; set; } = TimestampFormats.Timestamp;
        public bool LocationChangesAllowed { get; set; } = false;
        public bool StationTypeChangesAllowed { get; set; } = false;    
    }

    internal class OutputChannelDefinition
    {
        public ChannelTypes Type { get; set; }
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
        public DefaultAzureCredentialOptions AzureCredential { get; private set; }
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
                if (context == ConfigurationContext.Cmdline)
                {
                    AzureCredential = new DefaultAzureCredentialOptions()
                    {
                        ExcludeAzureCliCredential = true,
                        ExcludeAzurePowerShellCredential = true,
                        ExcludeManagedIdentityCredential = true,                        
                    };
                }
                else
                {
                    AzureCredential = new DefaultAzureCredentialOptions()
                    {
                        ExcludeAzureCliCredential = true,
                        ExcludeAzurePowerShellCredential = true
                    };
                }
                if (tid != null)
                {
                    AzureCredential.TenantId = tid;                                       
                }                
                var _keyVaultUri = new Uri("https://" + kv + ".vault.azure.net");
                _secretsConfig = new ConfigurationBuilder()
                    .AddAzureKeyVault(_keyVaultUri, new DefaultAzureCredential(AzureCredential))
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

            var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip,
                Converters = {
                        new JsonStringEnumConverter()
                    }
                };
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
                List<int> channelsIds = new List<int>();
                foreach (var channelDefinition in config.OutputChannels)
                {
                    if (channelDefinition.ID < 0)
                    {
                        errorMessage = "Invalid channel ID";
                        return false;
                    }
                    if (channelsIds.Contains(channelDefinition.ID))
                    {
                        errorMessage = "Duplicate channel ID";
                        return false;
                    }
                    channelsIds.Add(channelDefinition.ID);
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
                    if (!channelsIds.Contains(device.OutputChannel.ID))
                    {
                        errorMessage = "Nonexistent output channel ID: "+device.OutputChannel.ID;
                        return false;
                    }
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
