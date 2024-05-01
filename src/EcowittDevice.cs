using Azure.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;

namespace Ecowitt
{
    internal class EcowittDevice
    {

        internal class APIResult {

            [JsonPropertyName("code")]
            public int Code { get; set; }
            [JsonPropertyName("msg")]
            public string? Message { get; set; }
            [JsonPropertyName("time")]
            public string? Time { get; set; }
            public List<object?>? Data {  get; set; }

        }
        
        public const string API_BASE_URL = "https://api.ecowitt.net/api/v3/";
        public const string API_READ_HISTORICAL_DATA = "device/history";
        public const string API_GET_DEVICE_INFO = "device/info";

        public EcowittDeviceConfiguration Configuration { get; set; }
        private string _apiKey;
        private string _applicationKey;
        private HttpClient _httpClient;

        public EcowittDevice(EcowittDeviceConfiguration configuration, string? apiKey, string? applicationKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentNullException("API key not set");
            if (string.IsNullOrWhiteSpace(applicationKey)) throw new ArgumentNullException("Application key not set");
            if (configuration == null) throw new ArgumentNullException("Device configuration not set");
            Configuration = configuration;
            if (!configuration.Validate(false, out string errorMessage)) throw new InvalidDataException("Bad device configuration: " + errorMessage);
            _apiKey = apiKey;
            _applicationKey = applicationKey;
            _httpClient = new HttpClient();
        }



        public async Task<string?> ReadHistoricalData(DateTime startTime, DateTime endTime, List<string>? customChannels = null)
        {
            if (startTime < DateTime.Now.AddDays(-90.0)) throw new ArgumentException("Start time too far in the past - maximum is 90 days");
            if (endTime > DateTime.Now.AddDays(1.0)) throw new ArgumentException("End time is too far in the future - maximum is 1 day");
            var startTimeString = startTime.ToString("u").Trim('Z');
            var endTimeString = endTime.ToString("u").Trim('Z');
            List<string>? channels;
            if (customChannels != null) channels = customChannels;
            else channels = Configuration.ConfiguredChannels;
            Dictionary<string, string> queryArgs = new Dictionary<string, string>
            {
                { "application_key", _applicationKey },
                { "api_key", _apiKey },
                { "mac", Configuration.MAC },
                { "temp_unitid", Configuration.TemperatureUnit.ToString() },
                { "pressure_unitid", Configuration.PressureUnit.ToString() },
                { "wind_speed_unitid", Configuration.WindSpeedUnit.ToString() },
                { "rainfall_unitid", Configuration.RainfallUnit.ToString() },
                { "solar_irradiance_unitid", Configuration.SolarIrradianceUnit.ToString() },
                { "start_date", startTimeString },
                { "end_date", endTimeString },
                { "call_back", string.Join(',',channels) }
            };            
            string queryString = "?" + BuildQueryString(queryArgs);
            string requestURL = API_BASE_URL + API_READ_HISTORICAL_DATA + queryString;
            string requestID = "( " + queryArgs["mac"] + " - " + queryArgs["call_back"] + " )";
            return await APICall(requestURL, requestID);
        }

        public async Task<string?> GetDeviceInfo()
        {
            Dictionary<string, string> queryArgs = new Dictionary<string, string>
            {
                { "application_key", _applicationKey },
                { "api_key", _apiKey },
                { "mac", Configuration.MAC }
            };
            string queryString = "?" + BuildQueryString(queryArgs);
            string requestURL = API_BASE_URL + API_GET_DEVICE_INFO + queryString;
            string requestID = "( " + queryArgs["mac"] + " )";
            return await APICall(requestURL, requestID);
        }

        private string BuildQueryString(Dictionary<string, string> argsList)
        {
            if (argsList == null) return "";
            StringBuilder s = new StringBuilder();
            bool first = true;
            foreach (var item in argsList)
            {
                if (first)
                {
                    s.AppendFormat(CultureInfo.InvariantCulture, "{0}=", item.Key);
                    first = false;
                }
                else s.AppendFormat(CultureInfo.InvariantCulture, "&{0}=", item.Key);
                s.Append(HttpUtility.UrlEncode(item.Value));
            }
            return s.ToString();
        }

        private async Task<string?> APICall(string requestURL, string requestID)
        {
            HttpRequestMessage request;
            var rnd = new Random();
            int delayMultiplier = 1;
            int retryCounter = 3;
            do
            {
                using (request = new HttpRequestMessage(new HttpMethod("GET"), requestURL))
                {
                    using (HttpResponseMessage res = await _httpClient.SendAsync(request))
                    {
                        using (HttpContent content = res.Content)
                        {
                            if (!res.IsSuccessStatusCode)
                            {
                                if (res.StatusCode == System.Net.HttpStatusCode.Forbidden) throw new UnauthorizedAccessException();
                                if (res.StatusCode == System.Net.HttpStatusCode.NotFound) throw new ArgumentException("Result not found");
                                throw new HttpRequestException("HTTP code: " + res.StatusCode + " Message: " + res.ReasonPhrase);
                            }
                            string data = await content.ReadAsStringAsync();
                            if (!string.IsNullOrWhiteSpace(data))
                            {
                                try
                                {
                                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                                    APIResult? result = JsonSerializer.Deserialize<APIResult>(data);
                                    if (result != null)
                                    {
                                        if (result.Code == 0)
                                        {
                                            if (result.Message == "success")
                                            {
                                                var em = string.Format("API success. {0}", requestID);
                                                return data;
                                            }
                                            else
                                            {
                                                var em = string.Format("API error. {0} Message not success: {1}",
                                                    requestID, result.Message);
                                                Console.WriteLine(em);
                                            }
                                        }
                                        else
                                        {
                                            var em = string.Format("API error. {0} Non-zero code: {1} Message: {2}",
                                                requestID, result.Code, result.Message);
                                            Console.WriteLine(em);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("API returned empty JSON");
                                    }
                                }
                                catch (JsonException ex)
                                {
                                    Console.WriteLine("Exception deserializing API result." + ex.Message);
                                }
                            }
                            else
                            {
                                Console.WriteLine("API returned empty result");
                            }

                            var currentRandomDelay = rnd.Next(2000, 4000);
                            Thread.Sleep(currentRandomDelay * delayMultiplier);
                            delayMultiplier = delayMultiplier * 2;
                        }
                    }
                }
                retryCounter--;
            } while (retryCounter > 0);
            Console.WriteLine("Max retries exceeded - giving up on API");
            return null;
        }

    }
}
