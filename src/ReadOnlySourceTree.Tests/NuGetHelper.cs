// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet;
using NuGet.Common;
using Validation;
using Xunit;
using Xunit.Abstractions;

internal static class NuGetHelper
{
    internal static void InstallPackage(this TestProject project, ITestOutputHelper logger)
    {
        InstallPackages(project, logger, "ReadOnlySourceTree");
    }

    internal static void InstallPackages(this TestProject project, ITestOutputHelper logger, params string[] packageIds)
    {
        Requires.NotNull(project, nameof(project));

        // Look for packages in the test's bin directory
        IPackageRepository repo = PackageRepositoryFactory.Default.CreateRepository(
            Path.Combine(Environment.CurrentDirectory, @"..\ReadOnlySourceTree"));
        var packagesDir = Directory.CreateDirectory(Path.Combine(project.ProjectDirectory, "packages"));
        var localRepo = new LocalPackageRepository(packagesDir.FullName);
        var packageManager = new PackageManager(repo, packagesDir.FullName);

        // We must take special care to pick the version of the package that matches this test's build.
        var ownInformationalVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var match = Regex.Match(ownInformationalVersion.InformationalVersion, @"-(?<specialVersion>\w+\+g[]a-z0-9]+)");
        var packages = packageIds.Select(packageId => repo.FindPackagesById(packageId).First(p => p.Version.SpecialVersion == match.Groups["specialVersion"].Value.Replace('+', '-'))).ToList();
        Assert.NotEmpty(packages);

        string projectJsonPath = Path.Combine(project.ProjectDirectory, "project.json");
        bool projectJsonMode = File.Exists(projectJsonPath);
        if (projectJsonMode)
        {
            dynamic projectJsonObject = JsonConvert.DeserializeObject(File.ReadAllText(projectJsonPath));
            foreach (var package in packages)
            {
                projectJsonObject.dependencies[package.Id] = package.Version.ToString();
                DeletePackageFromCache(package); // Make sure NuGet will restore from what we just built.
            }

            File.WriteAllText(projectJsonPath, JsonConvert.SerializeObject(projectJsonObject, Formatting.Indented));
            NuGetRestore(project.ProjectDirectory, logger);
        }
        else
        {
            var projectSystem = new MSBuildProjectSystem(project.ProjectFullPath);
            var packagePathResolver = new DefaultPackagePathResolver(packagesDir.FullName);
            var projectManager = new ProjectManager(repo, packagePathResolver, projectSystem, localRepo);
            projectManager.Logger = logger != null ? new NuGetTestLogger(logger) : NullLogger.Instance;
            foreach (var package in packages)
            {
                projectManager.AddPackageReference(package, false, true);
                projectSystem.Save();

                // We also need to expand the package nupkg file. But it won't expand if it
                // sees the file is already on disk. So we "uninstall" it first.
                packageManager.UninstallPackage(package);
                packageManager.InstallPackage(package, false, true); // expand the package
            }
        }
    }

    private static void DeletePackageFromCache(IPackage package)
    {
        Requires.NotNull(package, nameof(package));
        string cacheLocation = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget",
            "packages",
            package.Id,
            package.Version.ToNormalizedString());
        if (Directory.Exists(cacheLocation))
        {
            Directory.Delete(cacheLocation, true);
        }
    }

    private static void NuGetRestore(string directory, ITestOutputHelper logger)
    {
        Requires.NotNullOrEmpty(directory, nameof(directory));

        var arguments = new StringBuilder();
        arguments.Append("restore ");
        if (File.Exists(Path.Combine(directory, "project.json")))
        {
            arguments.Append("project.json ");
        }

        arguments.Append("-Source \"");
        arguments.Append(Environment.CurrentDirectory);
        arguments.Append("\"");

        var psi = new ProcessStartInfo
        {
            WorkingDirectory = directory,
            FileName = Path.Combine(Environment.CurrentDirectory, "nuget3.exe"),
            Arguments = arguments.ToString(),
            UseShellExecute = false,
            RedirectStandardError = logger != null,
            RedirectStandardOutput = logger != null,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        logger?.WriteLine($"{psi.WorkingDirectory}>{psi.FileName} {psi.Arguments}");

        var restoreProcess = Process.Start(psi);

        int errorsLogged = 0;
        restoreProcess.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                if (e.Data.StartsWith("WARNING:"))
                {
                    errorsLogged++;
                }

                logger?.WriteLine(e.Data);
            }
        };
        restoreProcess.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                errorsLogged++;
                logger?.WriteLine(e.Data);
            }
        };
        restoreProcess.BeginOutputReadLine();
        restoreProcess.BeginErrorReadLine();

        restoreProcess.WaitForExit();
        Assert.Equal(0, restoreProcess.ExitCode);
        Assert.Equal(0, errorsLogged);
    }

    private class NuGetTestLogger : ILogger
    {
        private readonly ITestOutputHelper logger;

        internal NuGetTestLogger(ITestOutputHelper logger)
        {
            this.logger = logger;
        }

        public void Log(MessageLevel level, string message, params object[] args)
        {
            this.logger.WriteLine(message, args);
        }

        public FileConflictResolution ResolveFileConflict(string message)
        {
            throw new NotImplementedException();
        }
    }
}
