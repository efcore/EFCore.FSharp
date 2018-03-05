#r @"packages/build/Fake/tools/FakeLib.dll"
open Fake
let configuration = getBuildParamOrDefault "configuration" "Release"
let signOutput = hasBuildParam "signOutput"

Target "Build" <| fun _ ->
    DotNetCli.Build <| fun p ->
        { p with
            AdditionalArgs = [ sprintf "/p:SignOutput=%O" signOutput; "/nologo" ]
            Configuration = configuration
            Project = "EFCore.FSharp.sln" }

Target "Test" <| fun _ ->
    DotNetCli.Test <| fun p ->
        { p with
            Configuration = configuration
            AdditionalArgs = [ "--no-build"; "--no-restore" ]
            Project = "EFCore.FSharp.Test/EFCore.FSharp.Test.fsproj" }

"Build"
    ==> "Test"

RunTargetOrDefault "Test"    