namespace Bricelam.EntityFrameworkCore.FSharp.Test.TestUtilities

open System
open System.IO
open Microsoft.CodeAnalysis
open Microsoft.Extensions.DependencyModel
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.FSharp.Compiler.Ast
open Fantomas

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

        static member private ReferenceNames =
            [
                "System.Collections"
                "System.ComponentModel.Annotations"
                "System.Data.Common"
                "System.Linq.Expressions"
                "System.Runtime"
                "System.Text.RegularExpressions"
                "Microsoft.EntityFrameworkCore"
                "Microsoft.EntityFrameworkCore.Relational"
                "Microsoft.EntityFrameworkCore.SqlServer"

            ]

        member this.Build () =
            let projectName = Path.GetRandomFileName()
            let references = ResizeArray<MetadataReference>()

            BuildSource.References
                |> Seq.iter(fun r -> references.AddRange(r.References) )

            // TODO: complete

        member this.BuildInMemory () =
            let projectName = "TestProject"

            let checker = FSharpChecker.Create()

            let loadRefs =
                BuildSource.ReferenceNames
                |> Seq.map(fun r -> sprintf """#r "%s" """ r)
            
            let src = String.Join(Environment.NewLine, loadRefs) + Environment.NewLine + Environment.NewLine + (this.Sources |>Seq.head)

            let ast = CodeFormatter.parse true src
            
            let errors, code, assOpt = checker.CompileToDynamicAssembly([ast], projectName, BuildSource.ReferenceNames, None) |> Async.RunSynchronously
            

            let assembly = 
                match assOpt with
                | Some a -> a
                | None ->
                    let messages = errors |> Seq.map (fun e -> e.Message)
                    invalidOp (String.Join(", ", messages))

            assembly
