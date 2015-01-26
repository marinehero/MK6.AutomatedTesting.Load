using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MK6.AutomatedTesting.Load
{
    public static class HttpRequestsStepRunner
    {
        public static void Run(
            IterationContext context,
            Step step,
            CancellationToken cancellationToken)
        {
            var client = context.Environment["HttpClient"] as HttpClient;
            var httpRequestStep = step as HttpRequestsStep;
            var requestTasks = httpRequestStep.Requests
                .Select(r => Task.Run(
                    () =>
                    {
                        var response = HttpRequestsStepRunner.RunHttpRequest(
                            context,
                            client,
                            r.Description,
                            r.GetHttpRequest(context),
                            cancellationToken);
                        var extractedContent = r.ExtractContentToEnvironment(context, response);

                        return new
                        {
                            Response = response,
                            ExtractedContent = extractedContent
                        };

                    }))
                    .ToArray();

            Task.WaitAll(requestTasks);

            AddContentExtractsToEnvironment(
                context,
                requestTasks
                    .Select(r => r.Result.ExtractedContent));
        }

        private static IDictionary<string, string> RunHttpRequest(
            IterationContext context,
            HttpClient client,
            string description,
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var requestId = Guid.NewGuid();
            var originalUrl = request.RequestUri.ToString();

            var startTime = DateTime.Now;
            var timer = new Stopwatch();

            try
            {
                timer.Start();
                Log.Information("Starting request {Data}",
                    new 
                    { 
                        RequestId = requestId, 
                        WorkerId = context.WorkerIndex, 
                        WorkerCount = context.Environment["Workers"], 
                        Iteration = context.IterationIndex, 
                        Description = description, 
                        OriginalUrl = originalUrl 
                    });
                var response = client.SendAsync(request, cancellationToken).Result;

                timer.Stop();
                var endTime = DateTime.Now;

                Log.Information("Finished request {Data}",
                    new
                    {
                        RequestId = requestId,
                        WorkerId = context.WorkerIndex,
                        WorkerCount = context.Environment["Workers"],
                        Iteration = context.IterationIndex,
                        Description = description, 
                        FinalUrl = response.RequestMessage.RequestUri.AbsolutePath,
                        Status = response.StatusCode,
                        ResponseTime = timer.ElapsedMilliseconds,
                        ResponseSize = response.Content.ReadAsByteArrayAsync().Result.Count()
                    });

                return BuildResponse(
                    context,
                    description,
                    request,
                    startTime,
                    endTime,
                    timer,
                    response.StatusCode.ToString(),
                    response.Content.ReadAsStringAsync().Result);
            }
            catch (AggregateException ex)
            {
                timer.Stop();
                Log.Information("Request was cancelled");
                return BuildResponse(
                    context,
                    description,
                    request,
                    startTime,
                    DateTime.UtcNow,
                    timer,
                    "Cancelled",
                    string.Empty,
                    ex.Message);
            }
            catch (Exception ex)
            {
                timer.Stop();
                Log.Error(ex, "Error during timed request to '{0}'", request.RequestUri.ToString());

                return BuildResponse(
                    context,
                    description,
                    request,
                    startTime,
                    DateTime.UtcNow,
                    timer,
                    "Error",
                    string.Empty,
                    ex.Message);
            }
        }

        private static Dictionary<string, string> BuildResponse(
            IterationContext context,
            string description,
            HttpRequestMessage request,
            DateTime startTime,
            DateTime endTime,
            Stopwatch timer,
            string statusCode,
            string content,
            string errorMessage = "")
        {
            return new Dictionary<string, string>
                {
                    { "WorkerIndex", context.WorkerIndex.ToString()},
                    { "IterationIndex", context.IterationIndex.ToString() },
                    { "Description", description },
                    { "StartTime", startTime.ToLongTimeString() },
                    { "EndTime", endTime.ToLongTimeString() },
                    { "RequestUrl", request.RequestUri.AbsoluteUri },
                    { "StatusCode", statusCode },
                    { "ResponseTime", timer.ElapsedMilliseconds.ToString() },
                    { "Content", content },
                    { "ErrorMessage", errorMessage }
                };
        }

        private static void AddContentExtractsToEnvironment(IterationContext context, IEnumerable<IDictionary<string, object>> contentExtracts)
        {
            foreach (var contentExtract in contentExtracts)
            {
                foreach (var key in contentExtract.Keys)
                {
                    context.Environment.SafeAdd(key, contentExtract[key]);
                }
            }
        }
    }
}
