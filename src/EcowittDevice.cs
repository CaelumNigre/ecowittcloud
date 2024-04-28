using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecowitt
{
    internal class EcowittDevice
    {
        
        public const string _API_BASE_URL = "https://api.ecowitt.net/api/v3/";

        public EcowittDeviceConfiguration Configuration { get; set; }
        private string? _apiKey;
        private string? _applicationKey;

        public EcowittDevice(EcowittDeviceConfiguration configuration, string? apiKey, string? applicationKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentNullException("API key not set");
            if (string.IsNullOrWhiteSpace(applicationKey)) throw new ArgumentNullException("Application key not set");
            if (configuration == null) throw new ArgumentNullException("Device configuration not set");
            Configuration = configuration;
            if (!configuration.Validate(false, out string errorMessage)) throw new InvalidDataException("Bad device configuration: " + errorMessage);
            _apiKey = apiKey;
            _applicationKey = applicationKey;
        }


    }
}
