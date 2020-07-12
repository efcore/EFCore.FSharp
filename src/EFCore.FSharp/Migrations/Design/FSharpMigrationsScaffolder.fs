namespace EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.IO
open System.Text
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Internal
open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open System
open System
open Microsoft.EntityFrameworkCore


type FSharpMigrationsScaffolder(dependencies) = 
    inherit MigrationsScaffolder(dependencies)

    // Copy of (modulo custom code changes) https://github.com/aspnet/EntityFrameworkCore/blob/d8b7ebbfabff3d2e8560c24b1ff14d1f4244ca6a/src/EFCore.Design/Migrations/Design/MigrationsScaffolder.cs#L365
    override this.Save(projectDir, migration, outputDir) = 
        let lastMigrationFileName = migration.PreviousMigrationId + migration.FileExtension
        let migrationDirectory =
            if outputDir |> notNull then
                outputDir
            else
                this.GetDirectory(projectDir, lastMigrationFileName, migration.MigrationSubNamespace)
        let migrationFile = Path.Combine(migrationDirectory, migration.MigrationId + migration.FileExtension)
        let migrationMetadataFile = Path.Combine(migrationDirectory, migration.MigrationId + ".Designer" + migration.FileExtension)
        let modelSnapshotFileName = migration.SnapshotName + migration.FileExtension
        let modelSnapshotDirectory =
            if outputDir |> notNull then
                outputDir
            else
                this.GetDirectory(projectDir, modelSnapshotFileName, migration.SnapshotSubnamespace)
        let modelSnapshotFile = Path.Combine(modelSnapshotDirectory, modelSnapshotFileName)

        dependencies.OperationReporter.WriteVerbose(DesignStrings.WritingMigration(migrationFile))
        Directory.CreateDirectory(migrationDirectory) |> ignore

        (* Custom code 
           This is all we need to support an F# compatible file structure
           A makeover of the API (GenerateMigration ... -> migrationCode: string * metadataCode: string) would be best
           That should include this Save method taking into account it sometimes can receive nothing for metadataCode
        *)
        if migration.MigrationCode = "// intentionally empty" then
            File.WriteAllText(migrationFile, migration.MetadataCode, Encoding.UTF8)
        else 
            File.WriteAllText(migrationFile, migration.MigrationCode, Encoding.UTF8)
            File.WriteAllText(migrationMetadataFile, migration.MetadataCode, Encoding.UTF8)            
        (* End custom code *)

        dependencies.OperationReporter.WriteVerbose(DesignStrings.WritingSnapshot(modelSnapshotFile))
        Directory.CreateDirectory(modelSnapshotDirectory) |> ignore
        File.WriteAllText(modelSnapshotFile, migration.SnapshotCode, Encoding.UTF8)

        let projectFiles =
            Directory.GetFiles(projectDir)
            |> Seq.filter (fun f -> f.EndsWith ".fsproj")
            |> Seq.toList

        match projectFiles with
        | [] -> dependencies.OperationReporter.WriteVerbose(sprintf "Unable to find .fsproj file in %s" projectDir)

        | [ projectFile ] ->
            let projectDirectory = Path.GetDirectoryName projectFile
            let projectContents = File.ReadAllLines projectFile

            let allDbContextTypes =
                dependencies.MigrationsAssembly.Assembly.GetTypes()
                |> Seq.filter (fun t -> typeof<DbContext>.IsAssignableFrom t)
                |> Seq.map (fun t -> sprintf "type %s" t.Name)
            
            let declaringFiles =
                projectContents
                |> Seq.filter (fun l -> l.Contains("<Compile Include=\""))
                |> Seq.map (fun l -> l.Replace("<Compile Include=\"", "").Replace("/>", "").TrimStart().TrimEnd().TrimEnd('"'))
                |> Seq.map (fun l -> (l, File.ReadAllText l))
                |> Seq.filter (fun (_, c) -> allDbContextTypes |> Seq.exists (fun t -> c.Contains t))
                |> Seq.map fst
                |> Seq.toList

            let rec calculateInsertPoint (compileIncludesToFind: string list) (searchLines: (int * string) seq) (acc: int) =
                match compileIncludesToFind with
                | [ ] -> acc + 1
                | _ ->
                    let line = compileIncludesToFind.Head
                    let location =
                        searchLines
                        |> Seq.skipWhile (fun (_, l) -> l.Contains(sprintf "\"%s\"" line) |> not)
                        |> Seq.map fst
                        |> Seq.head
                        
                    let newLocation = if location > acc then location else acc
                    calculateInsertPoint (compileIncludesToFind.Tail) searchLines newLocation

            let indexedProjectContents =
                projectContents
                |> Seq.mapi (fun i l -> (i, l))

            let insertionPoint =
                calculateInsertPoint declaringFiles indexedProjectContents 0

            let indentation =
                (projectContents
                |> Seq.toArray).[insertionPoint + 1]
                |> Seq.takeWhile (fun c -> c = ' ')
                |> String.Concat

            let compileIncludes =
                [ migrationFile; modelSnapshotFile; if migration.MigrationCode <> "// intentionally empty" then migrationMetadataFile; ]
                |> Seq.filter (isNull >> not)
                |> Seq.map (fun s -> s.Replace(projectDirectory, "").TrimStart('\\'))
                |> Seq.map (fun s -> sprintf "%s<Compile Include=\"%s\" />" indentation s)
                |> Seq.toList

            let startProjectFile =
                projectContents
                |> Seq.take insertionPoint
                |> Seq.toList

            let endProjectFile =
                projectContents
                |> Seq.skip insertionPoint
                |> Seq.toList

            let newProjectContents =
                [ startProjectFile; compileIncludes; endProjectFile ]
                |> List.concat
                
            File.WriteAllLines(projectFile, newProjectContents)

        | _ -> dependencies.OperationReporter.WriteVerbose(sprintf "Ambiguous .fsproj file in %s. Please manually add the generated files." projectDir)

        MigrationFiles (
            MigrationFile = migrationFile,
            MetadataFile = migrationMetadataFile,
            SnapshotFile = modelSnapshotFile
        )
