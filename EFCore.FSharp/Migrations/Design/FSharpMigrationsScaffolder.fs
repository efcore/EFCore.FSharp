namespace Bricelam.EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.Linq
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Migrations.Internal

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open System.Reflection
open Microsoft.EntityFrameworkCore.Migrations
open Microsoft.EntityFrameworkCore.Infrastructure
open System.Text
open System.IO

type FSharpMigrationsScaffolder(dependencies: MigrationsScaffolderDependencies) =

    let contextType = dependencies.CurrentDbContext.Context.GetType()
    let activeProvider = dependencies.DatabaseProvider.Name

    let getConstructibleTypes (assembly:Assembly) =
        assembly.DefinedTypes
            |> Seq.filter(fun t -> t.IsAbstract |> not && t.IsGenericTypeDefinition |> not)

    let containsForeignMigrations migrationsNamespace =
        dependencies.MigrationsAssembly.Assembly
            |> getConstructibleTypes
            |> Seq.filter(fun t -> t.Namespace = migrationsNamespace && t.IsSubclassOf(typeof<Migration>))
            |> Seq.map(fun t -> t.GetCustomAttribute<DbContextAttribute>())
            |> Seq.exists(fun a -> a |> notNull && a.ContextType <> contextType)

    let getNamespace lastMigration defaultNamespace =

        let siblingType =    
            match lastMigration |> isNull with
            | false -> lastMigration.GetType()
            | true -> null

        match siblingType |> isNull with
        | true -> defaultNamespace
        | false ->
            let lastNamespace = siblingType.Namespace
            if lastNamespace <> defaultNamespace then
                dependencies.OperationReporter.WriteVerbose(DesignStrings.ReusingNamespace(siblingType.ShortDisplayName()))
                lastNamespace
            else
                defaultNamespace

    let getSubNamespace (rootNamespace:string) (ns:string) =
        if ns = rootNamespace then
            String.Empty
        else
            if ns.StartsWith(rootNamespace + ".", StringComparison.Ordinal) then
                ns.Substring(rootNamespace.Length + 1)
            else
                ns

    let tryGetProjectFile projectDir fileName =
        Directory.EnumerateFiles(projectDir, fileName, SearchOption.AllDirectories).FirstOrDefault()

    let getDirectory projectDir siblingFileName (subnamespace:string) =
        let defaultDirectory = Path.Combine(projectDir, Path.Combine(subnamespace.Split('.')))

        if siblingFileName |> notNull then
            let siblingPath = tryGetProjectFile projectDir siblingFileName
            if siblingPath |> notNull then
                let lastDirectory = Path.GetDirectoryName(siblingPath)
                if (defaultDirectory.Equals(lastDirectory, StringComparison.OrdinalIgnoreCase)) |> not then
                    dependencies.OperationReporter.WriteVerbose(DesignStrings.ReusingNamespace(siblingFileName));
                    lastDirectory
                else
                    defaultDirectory
            else
                defaultDirectory
        else
            defaultDirectory                                                

    let getOperations (modelSnapshot:ModelSnapshot) =

        let lastModel =
            match modelSnapshot |> isNull with
            | true -> dependencies.SnapshotModelProcessor.Process(null)
            | false -> dependencies.SnapshotModelProcessor.Process(modelSnapshot.Model)

        let upOperations = dependencies.MigrationsModelDiffer.GetDifferences(lastModel, dependencies.Model) |> Seq.toList
        let downOperations = 
            match upOperations |> List.isEmpty with
            | true -> []
            | false -> dependencies.MigrationsModelDiffer.GetDifferences(dependencies.Model, lastModel) |> Seq.toList

        (upOperations, downOperations)

    let scaffoldMigration (migrationName:string) (rootNamespace: string) (subNamespace:string) (language: string) : ScaffoldedMigration =
        
        if dependencies.MigrationsAssembly.FindMigrationId(migrationName) |> notNull then
            migrationName |> DesignStrings.DuplicateMigrationName |> OperationException |> raise
        
        let subNamespaceDefaulted, subNamespace' =
            match subNamespace |> String.IsNullOrEmpty with
            | true -> true, "Migrations"
            | false -> false, subNamespace 

        let lastMigration = dependencies.MigrationsAssembly.Migrations.LastOrDefault()
        let migrationNamespace =
            match subNamespaceDefaulted with
            | true -> getNamespace lastMigration.Value (rootNamespace + "." + subNamespace')
            | false -> rootNamespace + "." + subNamespace'


        let genericMarkIndex = contextType.Name.IndexOf('`')
        let sanitizedContextName =
            match genericMarkIndex with
            | -1 -> contextType.Name
            | _ -> contextType.Name.Substring(0, genericMarkIndex)

        let migrationNamespace' =
            if migrationNamespace |> containsForeignMigrations then
                if subNamespaceDefaulted then
                    let builder =
                        StringBuilder()
                            .Append(rootNamespace)
                            .Append(".Migrations.")

                    if (sanitizedContextName.EndsWith("Context", StringComparison.Ordinal)) then
                        builder.Append(sanitizedContextName.Substring(0, sanitizedContextName.Length - 7)) |> ignore
                    else
                        builder
                            .Append(sanitizedContextName)
                            .Append("Migrations") |> ignore

                    builder.ToString()
                else
                    dependencies.OperationReporter.WriteWarning(DesignStrings.ForeignMigrations(migrationNamespace))
                    migrationNamespace
            else
                migrationNamespace

        let modelSnapshot = dependencies.MigrationsAssembly.ModelSnapshot
        
        let upOperations, downOperations = getOperations modelSnapshot

        let migrationId = dependencies.MigrationsIdGenerator.GenerateId(migrationName)
        let modelSnapshotNamespace = getNamespace modelSnapshot migrationNamespace'
        
        let modelSnapshotName =
            let defaultModelSnapshotName = sanitizedContextName + "ModelSnapshot"
            match modelSnapshot |> isNull with
            | true -> defaultModelSnapshotName
            | false ->
                let lastModelSnapshotName = modelSnapshot.GetType().Name
                if (lastModelSnapshotName <> defaultModelSnapshotName) then
                    dependencies.OperationReporter.WriteVerbose(DesignStrings.ReusingSnapshotName(lastModelSnapshotName))
                    lastModelSnapshotName
                else
                    defaultModelSnapshotName            
        
        if upOperations |> Seq.exists(fun o -> o.IsDestructiveChange) then
            dependencies.OperationReporter.WriteWarning(DesignStrings.DestructiveOperation)

        let migrationCode =
            FSharpMigrationsGenerator.GenerateMigration
                migrationNamespace
                migrationName
                migrationId
                contextType
                upOperations
                downOperations
                dependencies.Model
        
        let migrationMetadataCode = "// This file can be ignored"
        
        let modelSnapshotCode =
            FSharpMigrationsGenerator.GenerateSnapshot
                modelSnapshotNamespace
                contextType
                modelSnapshotName
                dependencies.Model

        ScaffoldedMigration(
                FSharpMigrationsGenerator.FileExtension,
                lastMigration.Key,
                migrationCode,
                migrationId,
                migrationMetadataCode,
                (getSubNamespace rootNamespace migrationNamespace),
                modelSnapshotCode,
                modelSnapshotName,
                (getSubNamespace rootNamespace modelSnapshotNamespace))

    let removeMigration (projectDir:string) (rootNamespace: string) (force:bool) (language: string) : MigrationFiles =
        
        let files = MigrationFiles()

        let modelSnapshot = dependencies.MigrationsAssembly.ModelSnapshot

        if modelSnapshot |> isNull then
            DesignStrings.NoSnapshot |> OperationException |> raise
        
        let migrations =
            dependencies.MigrationsAssembly.Migrations
                |> Seq.map(fun m -> dependencies.MigrationsAssembly.CreateMigration(m.Value, activeProvider))
                |> Seq.toList

        let mutable model:IModel = null

        if migrations |> Seq.isEmpty |> not then
            let migration = migrations.[(migrations.Length - 1)]
            let migrationId = migration.GetId()
            model <- migration.TargetModel

            if (dependencies.MigrationsModelDiffer.HasDifferences(model, dependencies.SnapshotModelProcessor.Process(modelSnapshot.Model))) |> not then
                let mutable applied = false

                try
                    applied <- dependencies.HistoryRepository.GetAppliedMigrations() |> Seq.exists(fun e -> e.MigrationId.Equals(migrationId, StringComparison.OrdinalIgnoreCase))
                with
                | ex ->
                    if force then
                        ex |> string |> dependencies.OperationReporter.WriteVerbose
                        ((migrationId), ex.Message) |> DesignStrings.ForceRemoveMigration |> dependencies.OperationReporter.WriteWarning

                if applied then
                    if force then
                        let target = 
                            if migrations.Length > 1 then
                                migrations.[migrations.Length - 2].GetId()
                            else
                                Migration.InitialDatabase
                        dependencies.Migrator.Migrate(target)                    
                    else
                        migrationId |> DesignStrings.RevertMigration |> OperationException |> raise

                let migrationFileName = migrationId + FSharpMigrationsGenerator.FileExtension
                let migrationFile = tryGetProjectFile projectDir migrationFileName
                if (migrationFile |> notNull) then
                    migrationId |> DesignStrings.RemovingMigration |> dependencies.OperationReporter.WriteInformation
                    File.Delete(migrationFile);
                    files.MigrationFile <- migrationFile;
                else
                    (migrationFileName, migration.GetType().ShortDisplayName()) |> DesignStrings.NoMigrationFile |> dependencies.OperationReporter.WriteWarning
                
                let migrationMetadataFileName = migrationId + ".Designer" + FSharpMigrationsGenerator.FileExtension
                let migrationMetadataFile = tryGetProjectFile projectDir migrationMetadataFileName
                
                if (migrationMetadataFile |> notNull) then
                    File.Delete(migrationMetadataFile)
                    files.MetadataFile <- migrationMetadataFile
                else
                    migrationMetadataFile |> DesignStrings.NoMigrationMetadataFile |> dependencies.OperationReporter.WriteVerbose
                
                model <-
                    if migrations.Length > 1 then
                        migrations.[migrations.Length - 2].TargetModel
                    else
                        null

            else
                DesignStrings.ManuallyDeleted |> dependencies.OperationReporter.WriteVerbose

        
        let modelSnapshotType = modelSnapshot.GetType()
        let modelSnapshotName = modelSnapshotType.Name
        let modelSnapshotFileName = modelSnapshotName + FSharpMigrationsGenerator.FileExtension
        let modelSnapshotFile = tryGetProjectFile projectDir modelSnapshotFileName
        
        if (model |> isNull) then
            if (modelSnapshotFile |> isNull |> not) then
                dependencies.OperationReporter.WriteInformation(DesignStrings.RemovingSnapshot)
                File.Delete(modelSnapshotFile)
                files.SnapshotFile <- modelSnapshotFile
            else
                dependencies.OperationReporter.WriteWarning(
                    DesignStrings.NoSnapshotFile(
                        modelSnapshotFileName,
                        modelSnapshotType.ShortDisplayName()))
        else
            let modelSnapshotNamespace = modelSnapshotType.Namespace
            let modelSnapshotCode =
                FSharpMigrationsGenerator.GenerateSnapshot
                    modelSnapshotNamespace
                    contextType
                    modelSnapshotName
                    dependencies.Model

            let modelSnapshotFile' =
                match modelSnapshotFile |> isNull with
                | true ->
                    Path.Combine(
                        (getDirectory projectDir null (getSubNamespace rootNamespace modelSnapshotNamespace)),
                        modelSnapshotFileName)
                | false -> modelSnapshotFile
           
            DesignStrings.RevertingSnapshot |> dependencies.OperationReporter.WriteInformation
            File.WriteAllText(modelSnapshotFile', modelSnapshotCode, Encoding.UTF8)        

        files

    let save (projectDir: string) (migration: ScaffoldedMigration) (outputDir: string) : MigrationFiles =
        let lastMigrationFileName = migration.PreviousMigrationId + migration.FileExtension
        let migrationDirectory =
            if outputDir |> notNull then
                outputDir
            else
                getDirectory projectDir lastMigrationFileName migration.MigrationSubNamespace
        let migrationFile = Path.Combine(migrationDirectory, migration.MigrationId + migration.FileExtension)
        let migrationMetadataFile = Path.Combine(migrationDirectory, migration.MigrationId + ".Designer" + migration.FileExtension)
        let modelSnapshotFileName = migration.SnapshotName + migration.FileExtension
        let modelSnapshotDirectory =
            if outputDir |> notNull then
                outputDir
            else
                getDirectory projectDir modelSnapshotFileName migration.SnapshotSubnamespace
        let modelSnapshotFile = Path.Combine(modelSnapshotDirectory, modelSnapshotFileName)

        dependencies.OperationReporter.WriteVerbose(DesignStrings.WritingMigration(migrationFile))
        Directory.CreateDirectory(migrationDirectory) |> ignore
        File.WriteAllText(migrationFile, migration.MigrationCode, Encoding.UTF8)
        File.WriteAllText(migrationMetadataFile, migration.MetadataCode, Encoding.UTF8)

        dependencies.OperationReporter.WriteVerbose(DesignStrings.WritingSnapshot(modelSnapshotFile))
        Directory.CreateDirectory(modelSnapshotDirectory) |> ignore
        File.WriteAllText(modelSnapshotFile, migration.SnapshotCode, Encoding.UTF8)

        let files = MigrationFiles()
        files.MigrationFile <- migrationFile
        files.MetadataFile <- migrationMetadataFile
        files.SnapshotFile <- modelSnapshotFile

        files

    interface IMigrationsScaffolder with

        member __.ScaffoldMigration(migrationName:string, rootNamespace: string, subNamespace:string, language: string) =
            scaffoldMigration migrationName rootNamespace subNamespace language

        member __.RemoveMigration(projectDir:string, rootNamespace: string, force:bool, language: string) =
            removeMigration projectDir rootNamespace force language

        member __.Save(projectDir: string, migration: ScaffoldedMigration, outputDir: string) =
            save projectDir migration outputDir