namespace Bricelam.EntityFrameworkCore.FSharp

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.Internal

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities

type FSharpMigrationsGenerator(dependencies: MigrationsCodeGeneratorDependencies) =
    inherit MigrationsCodeGenerator(dependencies)
    
    let getNamespaces (operations: MigrationOperation seq) =
        seq { yield "" }


    override this.FileExtension = ".fs"

    override this.GenerateMigration(migrationNamespace: string, migrationName: string, upOperations: IReadOnlyList<MigrationOperation>, downOperations: IReadOnlyList<MigrationOperation>) =
        let sb = IndentedStringBuilder()

        sb.AppendLine("namespace " + migrationNamespace) |> ignore //TODO: implement Code.Namespace(modelSnapshotNamespace)

        let defaultNamespaces =
            seq { yield "System";
                     yield "System.Collections.Generic";
                     yield "Microsoft.EntityFrameworkCore.Migrations"; }

        sb
            |> writeNamespaces (defaultNamespaces |> Seq.append (upOperations |> Seq.append downOperations |> getNamespaces))
            |> append "type " |> appendLine migrationName
            |> indent |> appendLine "inherit Migration"
            |> indent |> appendLine "override this.Up(migrationBuilder:MigrationBuilder) ="
            |> indent |> FSharpMigrationOperationGenerator.Generate "migrationBuilder" upOperations
            |> appendLine ""
            |> unindent |> appendLine "override this.Down(migrationBuilder:MigrationBuilder) ="
            |> indent |> FSharpMigrationOperationGenerator.Generate "migrationBuilder" downOperations
            |> string

    override this.GenerateMetadata(migrationNamespace: string, contextType: Type, migrationName: string, migrationId: string, targetModel: IModel) =
        let sb = IndentedStringBuilder()

        sb.AppendLine("namespace " + migrationNamespace) |> ignore //TODO: implement Code.Namespace(modelSnapshotNamespace)

        let defaultNamespaces =
            ["System";
             "Microsoft.EntityFrameworkCore";
             "Microsoft.EntityFrameworkCore.Infrastructure";
             "Microsoft.EntityFrameworkCore.Metadata";
             "Microsoft.EntityFrameworkCore.Migrations";
             contextType.Namespace]

        sb
            |> writeNamespaces defaultNamespaces
            // TODO: implement
            |> appendLine "// Metadata"
            |> string

    override this.GenerateSnapshot(modelSnapshotNamespace: string, contextType: Type, modelSnapshotName: string, model: IModel) =
        let sb = IndentedStringBuilder()

        sb.AppendLine("namespace " + modelSnapshotNamespace) |> ignore //TODO: implement Code.Namespace(modelSnapshotNamespace)

        let defaultNamespaces =
            ["System";
             "Microsoft.EntityFrameworkCore";
             "Microsoft.EntityFrameworkCore.Infrastructure";
             "Microsoft.EntityFrameworkCore.Metadata";
             "Microsoft.EntityFrameworkCore.Migrations";
             contextType.Namespace]

        sb
            |> writeNamespaces defaultNamespaces
            // TODO: implement
            |> appendLine "// Snapshot"
            |> string
