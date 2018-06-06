#r "paket: groupref FakeBuild //"
#load "./.fake/build.fsx/intellisense.fsx"
open Fake.Core
open Fake.DotNet
open Fake.IO

let configuration = DotNet.Custom <| Environment.environVarOrDefault "configuration" "Release"
let signOutput = Environment.hasEnvironVar "signOutput"

Target.create "Build" <| fun _ ->  
    "EFCore.FSharp.sln" |>
    DotNet.build (fun p -> 
        { p with
            Common = { p.Common with CustomParams = Some <| sprintf "/nologo /p:SignOutput=%O" signOutput }
            Configuration = configuration })

Target.create "Test" <| fun _ ->
    Path.combine "EFCore.FSharp.Test" "EFCore.FSharp.Test.fsproj" |>
    DotNet.test (fun p ->
        { p with
            Common = { p.Common with CustomParams = Some "--no-build --no-restore"}
            Configuration = configuration })

open Fake.Core.TargetOperators

"Build"
    ==> "Test"

Target.runOrDefault "Test"    