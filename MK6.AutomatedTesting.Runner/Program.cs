using MK6.AutomatedTesting.Runner.Runners;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace MK6.AutomatedTesting.Runner
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length < 1)
                {
                    Console.WriteLine("Usage: MK6.AutomatedTesting.Runner.exe <configFile>");
                    return;
                }

                var configFilePath = args[0];

                var environment = BuildEnvironment(configFilePath);
                environment.Add(EnvironmentKeys.CancellationTokenSource, BuildCancellationTokenSource(environment));
                environment.Add(EnvironmentKeys.StartTime, DateTime.Now);

                SetupLogging(environment);
                SetupConsoleKeyHandlers(environment);

                RunTest(environment);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static IDictionary<string, object> BuildEnvironment(string configFilePath)
        {
            var configContent = ConfigurationReader.Read(configFilePath)
                .Select(c => new KeyValuePair<string, object>(c.Key, c.Value));
            var environment = new Dictionary<string, object>()
                .AddRange(configContent);
            return environment;
        }

        private static Script GetScript(IDictionary<string, object> environment)
        {
            var scriptType = GetEnumFromEnvironment<ScriptType>(
                environment, 
                RunnerConfigKeys.ScriptType, 
                "Unknown script type: {0}");

            switch (scriptType)
            {
                case ScriptType.Assembly:
                default:
                    var scriptSource = environment[RunnerConfigKeys.ScriptSource] as string;
                    return InstanceCreator.CreateInstanceOf<Script>(scriptSource);
            }
        }

        private static void SetupConsoleKeyHandlers(IDictionary<string, object> environment)
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler((s, e) =>
            {
                if (e.SpecialKey == ConsoleSpecialKey.ControlC)
                {
                    ShutdownScriptExecution(environment);
                    e.Cancel = true;
                }
            });
        }

        private static CancellationTokenSource BuildCancellationTokenSource(
            IDictionary<string, object> environment)
        {
            var runType = GetEnumFromEnvironment<RunType>(
                environment,
                RunnerConfigKeys.RunType,
                "Unrecognized run type: {0}");
            var runTimeInSeconds = default(int);
            var runTimeInSecondsString = default(object);

            if (runType == RunType.FixedTime
                && environment.TryGetValue(RunnerConfigKeys.RunTime, out runTimeInSecondsString)
                && int.TryParse(runTimeInSecondsString.ToString(), out runTimeInSeconds))
            {
                return new CancellationTokenSource(runTimeInSeconds * 1000);
            }

            return new CancellationTokenSource();
        }

        private static void ShutdownScriptExecution(IDictionary<string, object> environment)
        {
            var cancellationTokenSource = environment[EnvironmentKeys.CancellationTokenSource] as CancellationTokenSource;

            Log.Information("Shutting down script execution");
            cancellationTokenSource.Cancel();
        }

        private static void SetupLogging(IDictionary<string, object> environment)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.ColoredConsole()
                .WriteTo.RollingFile(@"Logs\log-{Date}.txt", retainedFileCountLimit: null)
                .CreateLogger();
            Log.Information("Started LoadTester");
        }

        private static void RunTest(IDictionary<string, object> environment)
        {
            var context = new ScriptContext(GetScript(environment), environment);
            var runType = GetEnumFromEnvironment<RunType>(
                environment, 
                RunnerConfigKeys.RunType, 
                "Unrecognized run type: {0}");
            var workerCount = int.Parse(environment[RunnerConfigKeys.Workers] as string);

            switch (runType)
            {
                case RunType.FixedTime:
                    FixedTimeRunner.Run(context, workerCount);
                    break;
                case RunType.FixedIterations:
                default:
                    FixedIterationsWorker.Run(context, workerCount);
                    break;
            }
        }

        private static T GetEnumFromEnvironment<T>(
            IDictionary<string, object> environment, 
            string environmentKey,
            string errorMessageTemplate)
            where T : struct
        {
            var envString = environment[environmentKey] as string;
            var enumValue = default(T);

            if (!Enum.TryParse<T>(envString, out enumValue))
            {
                throw new ApplicationException(
                    string.Format(errorMessageTemplate, envString));
            }

            return enumValue;
        }
    }
}
