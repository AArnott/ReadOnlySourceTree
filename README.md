Read only source tree
======================

[![Build status](https://ci.appveyor.com/api/projects/status/6sl6g515a8btj7f4/branch/master?svg=true)](https://ci.appveyor.com/project/AArnott/readonlysourcetree/branch/master)

This project contains a NuGet package that will cause any project it is installed into
to build its primary output and intermediate files to bin and obj directories at the
root of your repository instead of as subdirectories to the project directory.

This makes structuring your repository after this pattern much easier:

    RepoRoot\
        README.md
        LICENSE
        src\
            solution.sln
            project1\
            project2\
        bin\
            debug\
                project1\
                project2\
        obj\
            debug\
                project1\
                project2\
        packages\
            binary_dependency1\
            binary_dependency2\

This isolation of source files from build outputs can be useful for many reasons, including:

1. Delete all build outputs for your entire repository just by deleting the top level bin and obj folders.
2. Zip up your src directory and only get source -- no binaries.

## Additional steps

### Identifying the root of your repository

Heuristics are used by default to determine where the root of your repository is.
You can see the heuristics in [this MSBuild file's][1] definition of the MSBuild `RepoRoot` property.
It looks for files that are commonly found in the root of the repo.
If the heuristics don't fit your repository, you can create an empty `.RepoSrcRoot` file
in the top-level `src` folder of your repository.

### Consolidate all NuGet packages 

By default NuGet will expand packages to a directory beneath your solution directory.
To get it to install packages to a sibling of your src directory instead,
create a nuget.config file in the root of your repository with this content:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <config>
    <add key="repositorypath" value="packages" />
  </config>
</configuration>
```

[1]: src\ReadOnlySourceTree\build\ReadOnlySourceTree.props
