namespace EntityFrameworkCore.FSharp.Migrations.Design

open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Internal
open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open System.Text
open System.IO

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

        MigrationFiles (
            MigrationFile = migrationFile,
            MetadataFile = migrationMetadataFile,
            SnapshotFile = modelSnapshotFile
        )