// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class BuildIntegrationTests
{
    private readonly ITestOutputHelper logger;

    internal static string DefaultConfiguration = Environment.GetEnvironmentVariable("Configuration") ?? "Debug";

    public BuildIntegrationTests(ITestOutputHelper logger)
    {
        this.logger = logger;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TargetPath(bool explicitSrcRoot)
    {
        TestProject project = await this.PrepareProjectAsync(TestProjects.DefaultCSharpClassLibrary, explicitSrcRoot);
        var evaluation = project.LoadProject();
        string expectedPath = Path.Combine("..", "..", "bin", DefaultConfiguration, project.Name, evaluation.GetPropertyValue("TargetFileName"));
        var actualPath = evaluation.GetPropertyValue("TargetPath");
        Assert.Equal(expectedPath, actualPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Paths_AnyCPU(bool explicitSrcRoot)
    {
        TestProject project = await this.PrepareProjectAsync(TestProjects.DefaultCSharpClassLibrary, explicitSrcRoot);
        var evaluation = project.LoadProject();

        string expectedOutputPath = Path.Combine("..", "..", "bin", DefaultConfiguration, project.Name) + Path.DirectorySeparatorChar;
        string actualOutputPath = evaluation.GetPropertyValue("OutputPath");
        Assert.Equal(expectedOutputPath, actualOutputPath);
        Assert.Equal($@"bin\{DefaultConfiguration}\", evaluation.GetPropertyValue("TestBeforeTargets_OutputPath"));

        string expectedIntermediateOutputPath = Path.Combine("..", "..", "obj", DefaultConfiguration, project.Name) + Path.DirectorySeparatorChar;
        var actualIntermediateOutputPath = evaluation.GetPropertyValue("IntermediateOutputPath");
        Assert.Equal(expectedIntermediateOutputPath, actualIntermediateOutputPath);
        Assert.Equal(expectedIntermediateOutputPath, evaluation.GetPropertyValue("TestBeforeTargets_IntermediateOutputPath"));

        string expectedTargetPath = Path.Combine("..", "..", "bin", DefaultConfiguration, project.Name, evaluation.GetPropertyValue("TargetFileName"));
        var actualTargetPath = evaluation.GetPropertyValue("TargetPath");
        Assert.Equal(expectedTargetPath, actualTargetPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PathModifiers_x64(bool explicitSrcRoot)
    {
        TestProject project = await this.PrepareProjectAsync(TestProjects.DefaultCSharpClassLibrary, explicitSrcRoot);
        var evaluation = project.LoadProject(MSBuild.Properties.Default.Add("Platform", "x64"));

        string expectedOutputPath = Path.Combine("..", "..", "bin", "x64", DefaultConfiguration, project.Name) + Path.DirectorySeparatorChar;
        string actualOutputPath = evaluation.GetPropertyValue("OutputPath");
        Assert.Equal(expectedOutputPath, actualOutputPath);
        Assert.Equal($@"bin\x64\{DefaultConfiguration}\", evaluation.GetPropertyValue("TestBeforeTargets_OutputPath"));

        string expectedIntermediateOutputPath = Path.Combine("..", "..", "obj", "x64", DefaultConfiguration, project.Name) + Path.DirectorySeparatorChar;
        var actualIntermediateOutputPath = evaluation.GetPropertyValue("IntermediateOutputPath");
        Assert.Equal(expectedIntermediateOutputPath, actualIntermediateOutputPath);
        Assert.Equal(expectedIntermediateOutputPath, evaluation.GetPropertyValue("TestBeforeTargets_IntermediateOutputPath"));

        string expectedTargetPath = Path.Combine("..", "..", "bin", "x64", DefaultConfiguration, project.Name, evaluation.GetPropertyValue("TargetFileName"));
        var actualTargetPath = evaluation.GetPropertyValue("TargetPath");
        Assert.Equal(expectedTargetPath, actualTargetPath);
    }

    [Theory]
    [CombinatorialData]
    public async Task NoBinUnderProject(
        [CombinatorialValues(TestProjects.CSharpLibraryWithXmlDoc, TestProjects.DefaultCSharpClassLibrary)] string testProjectName,
        bool explicitSrcRoot)
    {
        var project = await this.PrepareProjectAsync(testProjectName, explicitSrcRoot);
        var buildResult = await project.BuildAsync();
        buildResult.AssertSuccessfulBuild();
        Assert.False(Directory.Exists(Path.Combine(project.ProjectDirectory, "bin")));
    }

    [Theory]
    [CombinatorialData]
    public async Task NoObjUnderProject(
        [CombinatorialValues(TestProjects.CSharpLibraryWithXmlDoc, TestProjects.DefaultCSharpClassLibrary)] string testProjectName,
        bool explicitSrcRoot)
    {
        var project = await this.PrepareProjectAsync(testProjectName, explicitSrcRoot);
        var buildResult = await project.BuildAsync();
        buildResult.AssertSuccessfulBuild();
        Assert.False(Directory.Exists(Path.Combine(project.ProjectDirectory, "obj")));
    }

    private async Task<TestProject> PrepareProjectAsync(string testProjectName, bool explicitSrcRoot)
    {
        var project = await TestProject.ExtractAsync(testProjectName, explicitSrcRoot);
        NuGetHelper.InstallPackage(project, this.logger);
        return project;
    }
}
