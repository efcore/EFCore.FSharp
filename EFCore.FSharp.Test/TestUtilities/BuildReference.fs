﻿namespace Bricelam.EntityFrameworkCore.FSharp.Test.TestUtilities

open System
open System.IO
open Microsoft.CodeAnalysis
open Microsoft.Extensions.DependencyModel
open Fantomas
open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Text
open Microsoft.CodeAnalysis.CSharp
open FSharp.Compiler.Ast
open System

type BuildReference = {
    CopyLocal : bool
    References : MetadataReference seq
    Path: string } with
        static member ByName name copyLocal path =
            let references =
                DependencyContext.Default.CompileLibraries
                    |> Seq.collect(fun l -> l.ResolveReferencePaths())
                    |> Seq.filter(fun r -> Path.GetFileNameWithoutExtension(r) = name)
                    |> Seq.map(fun r -> MetadataReference.CreateFromFile(r))
                    |> Seq.map(fun r -> r :> MetadataReference)
                    |> Seq.toList
        
            if references.Length = 0 then
                failwithf "Assembly '%s' not found." name

            let p = 
                match path with 
                | Some p' -> p'
                | None -> null

            { References = references; CopyLocal = copyLocal; Path = p }

        static member ByPath path =
            let references = seq { (MetadataReference.CreateFromFile(path) :> MetadataReference) }
            { References = references; CopyLocal = false; Path = path }

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
                BuildReference.ByName "netstandard" false None
                BuildReference.ByName "System.Collections" false None
                BuildReference.ByName "System.ComponentModel.Annotations" false None
                BuildReference.ByName "System.Data.Common" false None
                BuildReference.ByName "System.Linq.Expressions" false None
                BuildReference.ByName "System.Runtime" false None
                BuildReference.ByName "System.Text.RegularExpressions" false None
            ]

        member this.BuildInMemory (references: string list) =
            let projectName = "TestProject"

            let checker = FSharpChecker.Create()

            let source = String.Join(Environment.NewLine, this.Sources)

            let sourceText = SourceText.ofString source

            let options = 
                { FSharpParsingOptions.Default with
                    SourceFiles = [|"empty.fs"|] }

            let parseResult = 
                checker.ParseFile("empty.fs", sourceText, options)
                |> Async.RunSynchronously
            
            let input = parseResult.ParseTree.Value

            let thisAssembly = 
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Name

            let allReferences = 
                thisAssembly :: references 
            
            let errors, _, assemblyOpt =
                checker.CompileToDynamicAssembly([input], projectName, allReferences, None)
                |> Async.RunSynchronously

            let assembly = 
                match assemblyOpt with
                | Some a -> a
                | None ->
                    let messages = errors |> Seq.map (fun e -> e.Message + Environment.NewLine)
                    invalidOp (String.Join(", ", messages))

            assembly
