using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Ecowitt;
using System.Net;

namespace Ecowitt.AzureFunction
{
    public class EcowittAzureFunction
    {
        private readonly ILogger<EcowittAzureFunction> _logger;

        public EcowittAzureFunction(ILogger<EcowittAzureFunction> logger)
        {
            _logger = logger;
        }

        [Function("OnDemandPoll")]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            bool initialRun = false;
            if (bool.TryParse(req.Query["initialRun"], out bool parsedValue))
            {
                initialRun = parsedValue;
            }
            var ctrl = new Controler(ConfigurationContext.AzureFunction, true);
            ctrl.RunProcessing(DataProcessingMode.Online, initialRun);
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString($"Welcome to Azure Functions! Initial Run: {initialRun}");
            return response;            
        }
    }
}
