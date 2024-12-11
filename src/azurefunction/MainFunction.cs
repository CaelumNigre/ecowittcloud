using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Ecowitt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

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
            ctrl.RunProcessing(DataProcessingMode.Online, initialRun);
            return new OkObjectResult($"Welcome to Azure Functions! Initial Run: {initialRun}");
        }

        [Function("ScheduledPoll")]
        public void ScheduledRun([TimerTrigger("0 */60 * * * *")] TimerInfo timer)
        {
            _logger.LogInformation("C# Timer trigger function executed at: {time}", DateTime.Now);

            var ctrl = new Controler(ConfigurationContext.AzureFunction, true);
            ctrl.RunProcessing(DataProcessingMode.Online, false);
        }
    }
}
