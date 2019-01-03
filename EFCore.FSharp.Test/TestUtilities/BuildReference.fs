namespace Bricelam.EntityFrameworkCore.FSharp.Test.TestUtilities

open System.IO
open Microsoft.CodeAnalysis
open Microsoft.Extensions.DependencyModel
open Microsoft.FSharp.Compiler.SourceCodeServices
open System

type BuildReference = {
    CopyLocal : bool
    References : MetadataReference seq } with
        static member ByName name copyLocal =
            let references =
                DependencyContext.Default.CompileLibraries
                    |> Seq.collect(fun l -> l.ResolveReferencePaths())
                    |> Seq.filter(fun r -> Path.GetFileNameWithoutExtension(r) = name)
                    |> Seq.map(fun r -> MetadataReference.CreateFromFile(r))
                    |> Seq.map(fun r -> r :> MetadataReference)
        
            { References = references; CopyLocal = copyLocal }

type BuildFileResult = {
    TargetPath : string
    TargetDir : string
    TargetName : string } with        

        static member Create targetPath =
            {
                TargetPath = targetPath;
                TargetDir = Path.GetDirectoryName(targetPath)
                TargetName = Path.GetFileNameWithoutExtension(targetPath) }

type BuildSource = {
    TargetDir : string
    Sources : string list } with

        static member private References =
            [
                BuildReference.ByName "netstandard" false
                BuildReference.ByName "System.Collections" false
                BuildReference.ByName "System.ComponentModel.Annotations" false
                BuildReference.ByName "System.Data.Common" false
                BuildReference.ByName "System.Linq.Expressions" false
                BuildReference.ByName "System.Runtime" false
                BuildReference.ByName "System.Text.RegularExpressions" false
                BuildReference.ByName "Microsoft.EntityFrameworkCore" false
                BuildReference.ByName "Microsoft.EntityFrameworkCore.Relational" false
                BuildReference.ByName "Microsoft.EntityFrameworkCore.SqlServer" false

            ]

        member this.Build () =
            let projectName = Path.GetRandomFileName()
            let references = ResizeArray<MetadataReference>()

            BuildSource.References
                |> Seq.iter(fun r -> references.AddRange(r.References) )

            // TODO: complete

        member this.BuildInMemory () =
            let projectName = Path.GetRandomFileName()
            let references = BuildSource.References |> Seq.collect (fun r -> r.References)

            let checker = FSharpChecker.Create()

            let dllName = Path.ChangeExtension(projectName, ".dll")
            let errors, code, assembly = checker.CompileToDynamicAssembly([| "-o"; dllName; |], execute=None) |> Async.RunSynchronously
            

            match assembly with
            | Some a -> a
            | None ->
                let messages = errors |> Seq.map (fun e -> e.Message)
                invalidOp (String.Join(", ", messages))
