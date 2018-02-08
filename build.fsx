#r @"packages\Fake\tools\FakeLib.dll"
open Fake
let target = getBuildParam "target"
let configuration = getBuildParamOrDefault "configuration" "Release"
let signOutput = hasBuildParam "signOutput"

Target "Build" <| fun _ ->
    DotNetCli.Build <| fun p ->
        { p with
            Configuration = configuration
            Project = "EFCore.FSharp.sln" }

Target "Test" <| fun _ ->
    DotNetCli.Test <| fun p ->
        { p with
            Configuration = configuration
            Project = "EFCore.FSharp.Test/EFCore.FSharp.Test.fsproj" }

"Build"
    ==> "Test"

RunTargetOrDefault "Test"    