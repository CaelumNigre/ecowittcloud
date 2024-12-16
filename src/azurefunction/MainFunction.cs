using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Ecowitt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;

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
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req, FunctionContext context)
        {
            bool initialRun = false;
            if (bool.TryParse(req.Query["initialRun"], out bool parsedValue))
            {
                initialRun = parsedValue;
            }
            using (_logger.BeginScope("[{invocationid}]", context.InvocationId))
            {
                try
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    var ctrl = new Controler(ConfigurationContext.AzureFunction, _logger, true);
                    stopwatch.Stop();
                    _logger.LogInformation("Configuration processing time: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
                    stopwatch.Restart();
                    ctrl.RunProcessing(DataProcessingMode.Online, initialRun);
                    stopwatch.Stop();
                    _logger.LogInformation("Data collection time: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
                    return new StatusCodeResult(StatusCodes.Status200OK);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing data");
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
            }
        }

        [Function("ScheduledPoll")]
        public void ScheduledRun([TimerTrigger("0 */60 * * * *")] TimerInfo timer, FunctionContext context)
        {
            using (_logger.BeginScope("[{invocationid}]", context.InvocationId))
            {
                try
                {
                    _logger.LogInformation("Starting scheduled poll. Running late: {pastDue}",timer.IsPastDue);
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    var ctrl = new Controler(ConfigurationContext.AzureFunction, _logger, true);
                    stopwatch.Stop();
                    _logger.LogInformation("Configuration processing time: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
                    stopwatch.Restart();
                    ctrl.RunProcessing(DataProcessingMode.Online, false);
                    stopwatch.Stop();
                    _logger.LogInformation("Data collection time: {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing data");                    
                }
            }
        }
    }
}
