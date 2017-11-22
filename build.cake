var target = Argument<string>("target");
var configuration = Argument<string>("configuration");
var signOutput = HasArgument("signOutput");

Task("Build")
    .Does(
        () =>
            DotNetCoreBuild(
                "EFCore.FSharp.sln",
                new DotNetCoreBuildSettings
                {
                    Configuration = configuration,
                    MSBuildSettings = new DotNetCoreMSBuildSettings
                    {
                        MaxCpuCount = 0,
                        NoLogo = true,
                        Properties =
                        {
                            { "SignOutput", new[] { signOutput.ToString() } }
                        }
                    }
                }));

Task("Test")
    .IsDependentOn("Build")
    .Does(
        () =>
            DotNetCoreTest(
                "EFCore.FSharp.Test/EFCore.FSharp.Test.fsproj",
                new DotNetCoreTestSettings
                {
                    Configuration = configuration,
                    NoBuild = true,
                    NoRestore = true
                }));

RunTarget(target);
