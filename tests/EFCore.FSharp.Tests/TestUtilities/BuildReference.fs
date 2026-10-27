namespace EntityFrameworkCore.FSharp.Test.TestUtilities

open System
open System.IO
open System.Numerics
open System.Reflection
open Microsoft.CodeAnalysis
open Microsoft.Extensions.DependencyModel
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text

type BuildReference =
    { CopyLocal: bool
      References: MetadataReference seq
      Path: string }

    static member ByName name copyLocal path =
        let references =
            DependencyContext.Default.CompileLibraries
            |> Seq.collect (fun l -> l.ResolveReferencePaths())
            |> Seq.filter (fun r -> Path.GetFileNameWithoutExtension(r) = name)
            |> Seq.map (fun r -> MetadataReference.CreateFromFile(r))
            |> Seq.map (fun r -> r :> MetadataReference)
            |> Seq.toList

        if references.Length = 0 then
            failwithf "Assembly '%s' not found." name

        let p =
            match path with
            | Some p' -> p'
            | None -> null

        { References = references
          CopyLocal = copyLocal
          Path = p }

    static member ByPath path =
        let references =
            seq { (MetadataReference.CreateFromFile(path) :> MetadataReference) }

        { References = references
          CopyLocal = false
          Path = path }

type BuildFileResult =
    { TargetPath: string
      TargetDir: string
      TargetName: string }

    static member Create targetPath =
        { TargetPath = targetPath
          TargetDir = Path.GetDirectoryName(targetPath)
          TargetName = Path.GetFileNameWithoutExtension(targetPath) }

type BuildSource =
    { TargetDir: string
      Sources: string list }

    static let checker = FSharpChecker.Create()
    static let compilerLock = obj ()

    member this.BuildInMemory(references: string array) =
        let projectName = "TestProject"

        let source =
            String.Join(Environment.NewLine, this.Sources)

        let tmpDir =
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())

        Directory.CreateDirectory(tmpDir) |> ignore

        let sourceFile = Path.Combine(tmpDir, "source.fs")
        File.WriteAllText(sourceFile, source)

        let outputDll =
            Path.Combine(tmpDir, projectName + ".dll")

        let args =
            [| yield "fsc.exe"
               yield "--noframework"
               yield "--target:library"
               yield sprintf "--out:%s" outputDll
               for r in references do
                   yield sprintf "-r:%s" r
               yield sourceFile |]

        let errors, exitCode =
            lock compilerLock (fun () -> checker.Compile(args) |> Async.RunSynchronously)

        if exitCode <> 0 then
            let messages =
                errors
                |> Seq.map (fun e -> e.Message + Environment.NewLine)

            invalidOp (String.Join(Environment.NewLine, messages))

        let assemblyBytes = File.ReadAllBytes(outputDll)
        let assembly = Assembly.Load(assemblyBytes)

        // Clean up temp files
        try
            Directory.Delete(tmpDir, true)
        with
        | _ -> ()

        assembly
