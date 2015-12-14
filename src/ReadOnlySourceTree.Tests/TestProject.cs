// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Validation;

internal class TestProject : IDisposable
{
    private TestProject(string projectFullPath)
    {
        Requires.NotNullOrEmpty(projectFullPath, nameof(projectFullPath));

        this.ProjectFullPath = projectFullPath;
    }

    public string Name => Path.GetFileNameWithoutExtension(this.ProjectFullPath);

    public string ProjectFullPath { get; }

    public string ProjectDirectory => Path.GetDirectoryName(this.ProjectFullPath);

    public string SrcDirectory => Path.GetFullPath(Path.Combine(this.ProjectDirectory, @"..\"));

    public string BinDirectory => Path.GetFullPath(Path.Combine(this.SrcDirectory, @"..\bin\"));

    public void Dispose()
    {
        try
        {
            // Delete the parent directory of the project directory,
            // since when we extracted it all we created the project directory
            // within the temporary randomly named directory.
            Directory.Delete(Path.GetDirectoryName(this.ProjectDirectory), true);
        }
        catch (UnauthorizedAccessException)
        {
            // Loading assemblies can lock this directory. :(
        }
    }

    public Project LoadProject(IDictionary<string, string> properties = null)
    {
        return new Project(this.ProjectFullPath, properties ?? MSBuild.Properties.Default, null, new ProjectCollection());
    }

    internal static async Task<TestProject> ExtractAsync(string testProjectName, bool explicitSrcRoot)
    {
        string resourceNamePrefix = $"ReadOnlySourceTree.Tests.Scenarios.{testProjectName}.";

        string repoDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string srcDirectory = Path.Combine(repoDirectory, "src");
        Directory.CreateDirectory(srcDirectory);
        if (explicitSrcRoot)
        {
            File.WriteAllText(Path.Combine(srcDirectory, ".RepoSrcRoot"), string.Empty);
        }
        else
        {
            File.WriteAllText(Path.Combine(repoDirectory, ".gitignore"), string.Empty);
        }

        // Ensure the project directory is named after the testProject so that restoring project.json works.
        // See https://github.com/NuGet/Home/issues/1479
        string projectDirectory = Path.Combine(srcDirectory, Path.GetFileNameWithoutExtension(testProjectName));
        Directory.CreateDirectory(projectDirectory);

        string projectFileName = null;
        var testAssets = from name in Assembly.GetExecutingAssembly().GetManifestResourceNames()
                         where name.StartsWith(resourceNamePrefix)
                         select name;
        foreach (var assetName in testAssets)
        {
            string fileName = assetName.Substring(resourceNamePrefix.Length);
            fileName = Path.GetFileNameWithoutExtension(fileName).Replace('.', Path.DirectorySeparatorChar)
                + Path.GetExtension(fileName);
            if (Path.GetExtension(fileName).EndsWith("proj"))
            {
                projectFileName = fileName;
            }

            using (var sourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(assetName))
            {
                string fullPath = Path.Combine(projectDirectory, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                using (var targetStream = File.OpenWrite(fullPath))
                {
                    await sourceStream.CopyToAsync(targetStream);
                }
            }
        }

        string projectFullPath = Path.Combine(projectDirectory, projectFileName);
        return new TestProject(projectFullPath);
    }

    internal void SetProperty(string name, string value)
    {
        var pc = new ProjectCollection();
        var project = pc.LoadProject(this.ProjectFullPath);
        project.SetProperty(name, value);
        project.Save();
    }
}
