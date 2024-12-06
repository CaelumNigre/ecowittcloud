using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Ecowitt;

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
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            bool initialRun = false;
            if (bool.TryParse(req.Query["initialRun"], out bool parsedValue))
            {
                initialRun = parsedValue;
            }
            var ctrl = new Controler(ConfigurationContext.AzureFunction, true);
            return new OkObjectResult($"Welcome to Azure Functions! Initial Run: {initialRun}");
        }
    }
}
