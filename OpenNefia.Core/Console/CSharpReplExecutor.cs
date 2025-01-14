﻿using CSharpRepl.Services;
using CSharpRepl.Services.Completion;
using CSharpRepl.Services.Logging;
using CSharpRepl.Services.Roslyn;
using CSharpRepl.Services.Roslyn.Formatting;
using CSharpRepl.Services.Roslyn.Scripting;
using CSharpRepl.Services.Theming;
using HarmonyLib;
using Microsoft.CodeAnalysis.CSharp;
using OpenNefia.Core.Asynchronous;
using OpenNefia.Core.Configuration;
using OpenNefia.Core.ContentPack;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Log;
using OpenNefia.Core.Reflection;
using OpenNefia.Core.Timing;
using OpenNefia.Core.Utility;
using PrettyPrompt.Consoles;
using PrettyPrompt.Highlighting;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using CSharpReplConfig = CSharpRepl.Services.Configuration;

namespace OpenNefia.Core.Console
{
    public interface ICSharpReplExecutor : IReplExecutor
    {
        RoslynServices RoslynServices { get; }

        Task InitializeRoslynAsync();
    }

    public class CSharpReplExecutor : ICSharpReplExecutor
    {
        public const string SawmillName = "repl.exec";

        [Dependency] private readonly IReflectionManager _reflectionManager = default!;
        [Dependency] private readonly IConsoleEx _console = default!;
        [Dependency] private readonly ILogManager _logManager = default!;
        [Dependency] private readonly ITaskRunner _taskRunner = default!;
        [Dependency] private readonly IConfigurationManager _config = default!;
        [Dependency] private readonly IResourceManager _resources = default!;

        private CSharpReplConfig _replConfig = default!;
        private RoslynServices _roslynServices = default!;
        private Level _detailLevel = Level.FirstDetailed;
        private bool IsInitialized = false;

        public RoslynServices RoslynServices => _roslynServices;

        internal sealed class OpenNefiaLogger : ITraceLogger
        {
            private readonly ISawmill _sawmill;

            public OpenNefiaLogger(ISawmill sawmill)
            {
                _sawmill = sawmill;
            }

            public void Log(string message)
            {
                _sawmill.Log(LogLevel.Debug, message);
            }

            public void Log(Func<string> message)
            {
                _sawmill.Log(LogLevel.Debug, message());
            }

            public void LogPaths(string message, Func<IEnumerable<string?>> paths)
            {
                _sawmill.Log(LogLevel.Debug, $"{message}, { string.Join(", ", paths())}");
            }
        }

        private static CSharpReplConfig BuildDefaultConfig(IReflectionManager reflectionManager)
        {
            var references = new HashSet<string>();

            foreach (var assembly in reflectionManager.Assemblies)
            {
                references.Add(assembly.Location);
            }

            return new CSharpReplConfig(
                references: references.ToArray(),
                usings: new string[]
                {
                    "OpenNefia.Core",
                    "OpenNefia.Core.GameObjects",
                    "OpenNefia.Core.Prototypes",
                    "OpenNefia.Core.Utility",
                    "OpenNefia.Core.UI",
                    "OpenNefia.Core.IoC",
                    "OpenNefia.Core.Game",
                    "OpenNefia.Core.Maps",
                    "OpenNefia.Core.Locale",
                    "OpenNefia.Content.GameObjects",
                    "OpenNefia.Content.UI",
                    "OpenNefia.Content.Logic",
                    "System.Linq"
                }
            );
        }

        public void Initialize()
        {
            _taskRunner.Run(InitializeRoslynAsync());
        }

        public async Task InitializeRoslynAsync()
        {
            if (IsInitialized)
                return;

            Logger.DebugS(SawmillName, "Initializing REPL executor...");

            using (var profiler = new ProfilerLogger(LogLevel.Debug, SawmillName, "REPL executor initialization"))
            {
                var logger = new OpenNefiaLogger(_logManager.GetSawmill(SawmillName));
                _replConfig = BuildDefaultConfig(_reflectionManager);
                _roslynServices = new RoslynServices(_console, _replConfig, logger);

                await _roslynServices.WarmUpAsync(_replConfig.LoadScriptArgs);
                var loadReferenceScript = string.Join("\r\n", _replConfig.References.Select(reference => $@"#r ""{reference}"""));
                var loadReferenceScriptResult = await _roslynServices.EvaluateAsync(loadReferenceScript);
                await PrintAsync(loadReferenceScriptResult, Level.FirstSimple);

                await LoadStartupScriptAsync();

                IsInitialized = true;
            }
        }

        public ReplExecutionResult LoadStartupScript()
        {
            return _taskRunner.Run(LoadStartupScriptAsync());
        }

        private async Task<ReplExecutionResult> LoadStartupScriptAsync()
        {
            var autoloadScriptPathString = _config.GetCVar(CVars.ReplAutoloadScript);

            if (string.IsNullOrEmpty(autoloadScriptPathString))
                return new ReplExecutionResult.Success("");

            var autoloadScriptPath = new ResourcePath(autoloadScriptPathString);
            if (_resources.ContentFileExists(autoloadScriptPath))
            {
                _console.WriteLine($"Loading startup script: {autoloadScriptPath}");
                var autoloadScript = _resources.ContentFileReadAllText(autoloadScriptPath);
                var autoloadScriptResult = await _roslynServices.EvaluateAsync(autoloadScript);
                await PrintAsync(autoloadScriptResult, Level.FirstSimple);
                return ToReplResult(autoloadScriptResult);
            }
            else
            {
                _console.WriteErrorLine($"Startup script not found: {autoloadScriptPathString}");
                return new ReplExecutionResult.Error(new InvalidOperationException($"Startup script not found: {autoloadScriptPathString}"));
            }
        }

        public IReadOnlyCollection<CompletionItemWithDescription> Complete(string text, int caret)
        {
            return _roslynServices.CompleteAsync(text, caret).GetAwaiter().GetResult();
        }

        private async Task PrintAsync(EvaluationResult result, Level level)
        {
            switch (result)
            {
                case EvaluationResult.Success ok:
                    var formatted = await FormatResultObject(ok?.ReturnValue, level);
                    _console.Write(formatted);
                    break;
                case EvaluationResult.Error err:
                    var formattedError = await _roslynServices.PrettyPrintAsync(err.Exception, level);
                    _console.WriteErrorLine(AnsiColor.Red.GetEscapeSequence() + formattedError.ToString() + AnsiEscapeCodes.Reset);
                    break;
                case EvaluationResult.Cancelled:
                    _console.WriteErrorLine(
                       AnsiColor.Yellow.GetEscapeSequence() + "Operation cancelled." + AnsiEscapeCodes.Reset
                    );
                    break;
            }
        }

        private async Task<string> FormatResultObject(object? returnValue, Level level = Level.FirstSimple)
        {
            if (returnValue == null)
                return "null";
            else if (returnValue is string)
                return $"\"{returnValue}\"";

            // Use ToString() if it's overridden on the type, else do property dumping.
            var strValue = returnValue.ToString() ?? "<???>";
            var toString = returnValue.GetType().GetMethod("ToString", Type.EmptyTypes);
            if (toString == null || toString.DeclaringType != returnValue.GetType()) {
                var pretty = await _roslynServices.PrettyPrintAsync(returnValue, level);
                return pretty.GetSegments(_console).Join(null, ""); // TODO use Spectre.Console API
            }
            return strValue;
        }

        public ReplExecutionResult Execute(string code)
        {
            var result = _roslynServices.EvaluateAsync(code, _replConfig.LoadScriptArgs, new CancellationToken()).GetAwaiter().GetResult();

            PrintAsync(result, Level.FirstDetailed).GetAwaiter().GetResult();
            return ToReplResult(result);
        }

        private ReplExecutionResult ToReplResult(EvaluationResult result)
        {
            switch (result)
            {
                case EvaluationResult.Success success:
                    return new ReplExecutionResult.Success(FormatResultObject(success.ReturnValue).GetAwaiter().GetResult(), success.ReturnValue);
                case EvaluationResult.Error err:
                    return new ReplExecutionResult.Error(err.Exception);
                default:
                    return new ReplExecutionResult.Error(new InvalidOperationException("Could not process REPL result"));
            }
        }
    }
}
