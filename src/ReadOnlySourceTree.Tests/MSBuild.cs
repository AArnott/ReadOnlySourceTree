// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

using Xunit;
using Xunit.Abstractions;

internal static class MSBuild
{
    internal static Task<BuildResultAndLogs> BuildAsync(this TestProject project, string targetToBuild = "Build", IDictionary<string, string> properties = null, ITestOutputHelper testLogger = null)
    {
        return ExecuteAsync(project.ProjectFullPath, targetToBuild, properties, testLogger);
    }

    internal static Task<BuildResultAndLogs> RebuildAsync(string projectPath, string projectName = null, IDictionary<string, string> properties = null, ITestOutputHelper testLogger = null)
    {
        var target = string.IsNullOrEmpty(projectName) ? "Rebuild" : projectName.Replace('.', '_') + ":Rebuild";
        return MSBuild.ExecuteAsync(projectPath, new[] { target }, properties, testLogger);
    }

    internal static Task<BuildResultAndLogs> ExecuteAsync(string projectPath, string targetToBuild, IDictionary<string, string> properties = null, ITestOutputHelper testLogger = null)
    {
        return MSBuild.ExecuteAsync(projectPath, new[] { targetToBuild }, properties, testLogger);
    }

    /// <summary>
    /// Builds a project.
    /// </summary>
    /// <param name="projectPath">The absolute path to the project.</param>
    /// <param name="targetsToBuild">The targets to build. If not specified, the project's default target will be invoked.</param>
    /// <param name="properties">The optional global properties to pass to the project. May come from the <see cref="MSBuild.Properties"/> static class.</param>
    /// <param name="testLogger">An optional xunit logger to which build output should be emitted.</param>
    /// <returns>A task whose result is the result of the build.</returns>
    internal static async Task<BuildResultAndLogs> ExecuteAsync(string projectPath, string[] targetsToBuild = null, IDictionary<string, string> properties = null, ITestOutputHelper testLogger = null)
    {
        targetsToBuild = targetsToBuild ?? new string[0];

        var logger = new EventLogger();
        var logLines = new List<string>();
        var parameters = new BuildParameters
        {
            DisableInProcNode = true,
            Loggers = new List<ILogger>
                {
                    new ConsoleLogger(LoggerVerbosity.Detailed, logLines.Add, null, null),
                    new ConsoleLogger(LoggerVerbosity.Minimal, v => testLogger?.WriteLine(v.TrimEnd()), null, null),
                    logger,
                },
        };

        BuildResult result;
        using (var buildManager = new BuildManager())
        {
            buildManager.BeginBuild(parameters);
            try
            {
                var requestData = new BuildRequestData(projectPath, properties ?? Properties.Default, null, targetsToBuild, null);
                var submission = buildManager.PendBuildRequest(requestData);
                result = await submission.ExecuteAsync();
            }
            finally
            {
                buildManager.EndBuild();
            }
        }

        return new BuildResultAndLogs(result, logger.LogEvents, logLines);
    }

    /// <summary>
    /// Builds a project.
    /// </summary>
    /// <param name="projectInstance">The project to build.</param>
    /// <param name="targetsToBuild">The targets to build. If not specified, the project's default target will be invoked.</param>
    /// <returns>A task whose result is the result of the build.</returns>
    internal static async Task<BuildResultAndLogs> ExecuteAsync(ProjectInstance projectInstance, params string[] targetsToBuild)
    {
        targetsToBuild = (targetsToBuild == null || targetsToBuild.Length == 0) ? projectInstance.DefaultTargets.ToArray() : targetsToBuild;

        var logger = new EventLogger();
        var logLines = new List<string>();
        var parameters = new BuildParameters
        {
            DisableInProcNode = true,
            Loggers = new List<ILogger>
                {
                    new ConsoleLogger(LoggerVerbosity.Detailed, logLines.Add, null, null),
                    logger,
                },
        };

        BuildResult result;
        using (var buildManager = new BuildManager())
        {
            buildManager.BeginBuild(parameters);
            try
            {
                var brdFlags = BuildRequestDataFlags.ProvideProjectStateAfterBuild;
                var requestData = new BuildRequestData(projectInstance, targetsToBuild, null, brdFlags);
                var submission = buildManager.PendBuildRequest(requestData);
                result = await submission.ExecuteAsync();
            }
            finally
            {
                buildManager.EndBuild();
            }
        }

        return new BuildResultAndLogs(result, logger.LogEvents, logLines);
    }

    private static Task<BuildResult> ExecuteAsync(this BuildSubmission submission)
    {
        var tcs = new TaskCompletionSource<BuildResult>();
        submission.ExecuteAsync(s => tcs.SetResult(s.BuildResult), null);
        return tcs.Task;
    }

    /// <summary>
    /// Common properties to pass to a build request.
    /// </summary>
    internal static class Properties
    {
        /// <summary>
        /// No properties. The project will be built in its default configuration.
        /// </summary>
        internal static readonly ImmutableDictionary<string, string> Empty = ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the global properties to pass to indicate where NuProj imports can be found.
        /// </summary>
        /// <remarks>
        /// For purposes of true verifications, this map of global properties should
        /// NOT include any that are propagated by project references from NuProj
        /// or else their presence here (which does not reflect what the user's solution
        /// typically builds with) may mask over-build errors that would otherwise
        /// be caught by our BuildResultAndLogs.AssertNoTargetsExecutedTwice method.
        /// </remarks>
        internal static readonly ImmutableDictionary<string, string> Default = Empty;
    }

    internal class BuildResultAndLogs
    {
        internal BuildResultAndLogs(BuildResult result, List<BuildEventArgs> events, IReadOnlyList<string> logLines)
        {
            this.Result = result;
            this.LogEvents = events;
            this.LogLines = logLines;
        }

        internal BuildResult Result { get; private set; }

        internal List<BuildEventArgs> LogEvents { get; private set; }

        internal IEnumerable<BuildErrorEventArgs> ErrorEvents
        {
            get { return this.LogEvents.OfType<BuildErrorEventArgs>(); }
        }

        internal IEnumerable<BuildWarningEventArgs> WarningEvents
        {
            get { return this.LogEvents.OfType<BuildWarningEventArgs>(); }
        }

        internal IReadOnlyList<string> LogLines { get; private set; }

        internal string EntireLog
        {
            get { return string.Join(string.Empty, this.LogLines); }
        }

        internal void AssertSuccessfulBuild()
        {
            Assert.False(this.ErrorEvents.Any(), this.ErrorEvents.Select(e => e.Message).FirstOrDefault());
            this.AssertNoTargetsExecutedTwice();
            Assert.Equal(BuildResultCode.Success, this.Result.OverallResult);
        }

        internal void AssertUnsuccessfulBuild()
        {
            Assert.Equal(BuildResultCode.Failure, this.Result.OverallResult);
            Assert.True(this.ErrorEvents.Any(), this.ErrorEvents.Select(e => e.Message).FirstOrDefault());
        }

        private static string SerializeProperties(IDictionary<string, string> properties)
        {
            return string.Join(",", properties.Select(kv => $"{kv.Key}={kv.Value}"));
        }

        /// <summary>
        /// Verifies that we don't have multi-proc build bugs that may cause
        /// build failures as a result of projects building multiple times.
        /// </summary>
        private void AssertNoTargetsExecutedTwice()
        {
            var projectPathToId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var configurations = new Dictionary<long, ProjectStartedEventArgs>();
            foreach (var projectStarted in this.LogEvents.OfType<ProjectStartedEventArgs>())
            {
                if (!configurations.ContainsKey(projectStarted.BuildEventContext.ProjectInstanceId))
                {
                    configurations.Add(projectStarted.BuildEventContext.ProjectInstanceId, projectStarted);
                }

                long existingId;
                if (projectPathToId.TryGetValue(projectStarted.ProjectFile, out existingId))
                {
                    if (existingId != projectStarted.BuildEventContext.ProjectInstanceId)
                    {
                        var originalProjectStarted = configurations[existingId];
                        var originalRequestingProject = configurations[originalProjectStarted.ParentProjectBuildEventContext.ProjectInstanceId].ProjectFile;

                        var requestingProject = configurations[projectStarted.ParentProjectBuildEventContext.ProjectInstanceId].ProjectFile;

                        var globalPropertiesFirst = originalProjectStarted.GlobalProperties.Select(kv => $"{kv.Key}={kv.Value}").ToImmutableHashSet();
                        var globalPropertiesSecond = projectStarted.GlobalProperties.Select(kv => $"{kv.Key}={kv.Value}").ToImmutableHashSet();
                        var inFirstNotSecond = globalPropertiesFirst.Except(globalPropertiesSecond);
                        var inSecondNotFirst = globalPropertiesSecond.Except(globalPropertiesFirst);

                        var messageBuilder = new StringBuilder();
                        messageBuilder.AppendLine($@"Project ""{projectStarted.ProjectFile}"" was built twice. ");
                        messageBuilder.Append($@"The first build request came from ""{originalRequestingProject}""");
                        if (inFirstNotSecond.IsEmpty)
                        {
                            messageBuilder.AppendLine();
                        }
                        else
                        {
                            messageBuilder.AppendLine($" and defined these unique global properties: {string.Join(",", inFirstNotSecond)}");
                        }

                        messageBuilder.Append($@"The subsequent build request came from ""{requestingProject}""");
                        if (inSecondNotFirst.IsEmpty)
                        {
                            messageBuilder.AppendLine();
                        }
                        else
                        {
                            messageBuilder.AppendLine($" and defined these unique global properties: {string.Join(",", inSecondNotFirst)}");
                        }

                        Assert.False(true, messageBuilder.ToString());
                    }
                }
                else
                {
                    projectPathToId.Add(projectStarted.ProjectFile, projectStarted.BuildEventContext.ProjectInstanceId);
                }
            }
        }
    }

    private class EventLogger : ILogger
    {
        private IEventSource eventSource;

        internal EventLogger()
        {
            this.Verbosity = LoggerVerbosity.Normal;
            this.LogEvents = new List<BuildEventArgs>();
        }

        public LoggerVerbosity Verbosity { get; set; }

        public string Parameters { get; set; }

        public List<BuildEventArgs> LogEvents { get; set; }

        public void Initialize(IEventSource eventSource)
        {
            this.eventSource = eventSource;
            this.eventSource.AnyEventRaised += this.EventSourceAnyEventRaised;
        }

        public void Shutdown()
        {
            this.eventSource.AnyEventRaised -= this.EventSourceAnyEventRaised;
        }

        private void EventSourceAnyEventRaised(object sender, BuildEventArgs e)
        {
            this.LogEvents.Add(e);
        }
    }
}
