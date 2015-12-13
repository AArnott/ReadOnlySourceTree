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

    [Fact]
    public async Task TargetPath()
    {
        TestProject project = await this.PrepareProjectAsync(TestProjects.DefaultCSharpClassLibrary);
        var evaluation = project.LoadProject();
        string expectedPath = Path.Combine("..", "..", "bin", DefaultConfiguration, project.Name, evaluation.GetPropertyValue("TargetFileName"));
        var actualPath = evaluation.GetPropertyValue("TargetPath");
        Assert.Equal(expectedPath, actualPath);
    }

    [Fact]
    public async Task PathModifiers_x64()
    {
        TestProject project = await this.PrepareProjectAsync(TestProjects.DefaultCSharpClassLibrary);
        var evaluation = project.LoadProject(MSBuild.Properties.Default.Add("Platform", "x64"));

        string expectedOutputPath = Path.Combine("..", "..", "bin", "x64", DefaultConfiguration, project.Name) + Path.DirectorySeparatorChar;
        string actualOutputPath = evaluation.GetPropertyValue("OutputPath");
        Assert.Equal(expectedOutputPath, actualOutputPath);

        string expectedIntermediateOutputPath = Path.Combine("..", "..", "obj", "x64", DefaultConfiguration, project.Name) + Path.DirectorySeparatorChar;
        var actualIntermediateOutputPath = evaluation.GetPropertyValue("IntermediateOutputPath");
        Assert.Equal(expectedIntermediateOutputPath, actualIntermediateOutputPath);

        string expectedTargetPath = Path.Combine("..", "..", "bin", "x64", DefaultConfiguration, project.Name, evaluation.GetPropertyValue("TargetFileName"));
        var actualTargetPath = evaluation.GetPropertyValue("TargetPath");
        Assert.Equal(expectedTargetPath, actualTargetPath);
    }

    [Theory]
    [InlineData(TestProjects.CSharpLibraryWithXmlDoc)]
    [InlineData(TestProjects.DefaultCSharpClassLibrary)]
    public async Task NoBinUnderProject(string testProjectName)
    {
        var project = await this.PrepareProjectAsync(testProjectName);
        var buildResult = await project.BuildAsync();
        buildResult.AssertSuccessfulBuild();
        Assert.False(Directory.Exists(Path.Combine(project.ProjectDirectory, "bin")));
    }

    [Theory]
    [InlineData(TestProjects.CSharpLibraryWithXmlDoc)]
    [InlineData(TestProjects.DefaultCSharpClassLibrary)]
    public async Task NoObjUnderProject(string testProjectName)
    {
        var project = await this.PrepareProjectAsync(testProjectName);
        var buildResult = await project.BuildAsync();
        buildResult.AssertSuccessfulBuild();
        Assert.False(Directory.Exists(Path.Combine(project.ProjectDirectory, "obj")));
    }

    private async Task<TestProject> PrepareProjectAsync(string testProjectName)
    {
        var project = await TestProject.ExtractAsync(testProjectName);
        NuGetHelper.InstallPackage(project, this.logger);
        return project;
    }
}
