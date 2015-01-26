using MK6.AutomatedTesting;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace MK6.AutomatedTesting.Runner.Runners
{
    public static class FixedIterationsWorker
    {
        public static void Run(ScriptContext context, int workerCount)
        {
            var workerContexts = Enumerable.Range(0, workerCount)
                .Select(i => new WorkerContext(context, i))
                .ToList();

            var workers = Enumerable.Range(0, workerCount)
                .Select(workerIndex => 
                    Task.Factory.StartNew(
                        () => RunWorker(workerContexts[workerIndex]), 
                        TaskCreationOptions.LongRunning))
                .ToArray();

            Task.WaitAll(workers);
        }

        private static void RunWorker(WorkerContext context)
        {
            Log.Debug("Starting worker {0}", context.WorkerIndex);

            var iterationsPerWorker = GetIterationsPerWorker(context);

            Log.Information("Worker {0} will run {1} iterations", context.WorkerIndex, iterationsPerWorker);

            for (var iterationCounter = 0; iterationCounter < iterationsPerWorker; iterationCounter += 1)
            {
                Log.Debug("Worker {0}, starting iteration {1}", context.WorkerIndex, iterationCounter);
                ScriptRunner.Run(new IterationContext(context, iterationCounter));
                Log.Debug("Worker {0}, finished iteration {1}", context.WorkerIndex, iterationCounter);
            }

            Log.Debug("Worker {0} finished", context.WorkerIndex);
        }

        private static int GetIterationsPerWorker(WorkerContext context)
        {
            var iterationsPerWorker = default(int);
            var iterationsPerWorkerString = default(object);

            if (context.Environment.TryGetValue("IterationsPerWorker", out iterationsPerWorkerString))
            {
                if (!int.TryParse(iterationsPerWorkerString.ToString(), out iterationsPerWorker))
                {
                    throw new ApplicationException("Invalid value of 'IterationsPerWorker' configuration value. Value must be an integer.");
                }
            }

            return iterationsPerWorker;
        }
    }
}
