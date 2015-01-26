using MK6.AutomatedTesting;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MK6.AutomatedTesting.Runner.Runners
{
    public static class FixedTimeRunner
    {
        public static void Run(ScriptContext context, int workerCount)
        {
            var workerContexts = Enumerable.Range(0, workerCount)
                .Select(i => new WorkerContext(context, i))
                .ToList();

            var workers = Enumerable.Range(0, workerCount)
                .Select(workerIndex => Task.Run(() => RunWorker(workerContexts[workerIndex])))
                .ToArray();

            Task.WaitAll(workers);
        }

        private static void RunWorker(WorkerContext context)
        {
            Log.Debug("Starting worker {0}", context.WorkerIndex);
            var iterationCounter = 0;

            while(!context.CancellationToken.IsCancellationRequested)
            {
                Log.Debug("Worker {0}, starting iteration {1}", context.WorkerIndex, iterationCounter);
                ScriptRunner.Run(new IterationContext(context, iterationCounter));
                Log.Debug("Worker {0}, finished iteration {1}", context.WorkerIndex, iterationCounter);

                iterationCounter += 1;
            }

            Log.Debug("Worker {0} finished", context.WorkerIndex);
        }
    }
}
