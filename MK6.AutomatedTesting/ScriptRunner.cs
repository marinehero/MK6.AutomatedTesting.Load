using Serilog;
using System;
using System.Threading;

namespace MK6.AutomatedTesting
{
    public static class ScriptRunner
    {
        public static void Run(IterationContext context)
        {
            var cancellationToken = context.CancellationToken;
            var script = context.Script;

            script.PreScriptHook.Invoke(context);

            try
            {
                foreach (var step in script.Steps)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Log.Information(
                            "Cancellation requessted. Stopping worker {0} iteration {1}",
                            context.WorkerIndex,
                            context.IterationIndex);
                        break;
                    }

                    step.Runner.Invoke(context, step, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                LogStepException(context, ex);
            }
            finally
            {
                try
                {
                    foreach (var step in script.FinallySteps)
                    {
                        step.Runner.Invoke(context, step, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    LogStepException(context, ex);
                }

                script.PostScriptHook.Invoke(context);
            }
        }

        private static void LogStepException(IterationContext context, Exception ex)
        {
            Log.Error(
                ex,
                "Worker {0}, iteration {1}: Error during request",
                context.WorkerIndex,
                context.IterationIndex);
        }
    }
}