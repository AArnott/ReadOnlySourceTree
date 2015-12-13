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
