namespace Bricelam.EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.Internal

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Bricelam.EntityFrameworkCore.FSharp.Internal

type FSharpMigrationsGenerator(dependencies: MigrationsCodeGeneratorDependencies) =
    inherit MigrationsCodeGenerator(dependencies)
    
    let getNamespaces (operations: MigrationOperation seq) =
        seq { yield "System // This should be the namespaces needed by the MigrationOperations" }


    override this.FileExtension = ".fs"
    override this.Language = "F#"

    override this.GenerateMigration(migrationNamespace: string, migrationName: string, upOperations: IReadOnlyList<MigrationOperation>, downOperations: IReadOnlyList<MigrationOperation>) =
        let sb = IndentedStringBuilder()

        let defaultNamespaces =
            seq { yield "System";
                     yield "System.Collections.Generic";
                     yield "Microsoft.EntityFrameworkCore.Migrations"; }

        sb
            |> append "namespace " |> appendLine (FSharpHelper.Namespace [|migrationNamespace|])
            |> appendEmptyLine
            |> writeNamespaces (defaultNamespaces |> Seq.append (upOperations |> Seq.append downOperations |> getNamespaces))
            |> appendEmptyLine
            |> append "type " |> append (migrationName |> FSharpHelper.Identifier) |> appendLine " ="
            |> indent |> appendLine "inherit Migration"
            |> indent |> appendLine "override this.Up(migrationBuilder:MigrationBuilder) ="
            |> indent |> FSharpMigrationOperationGenerator.Generate "migrationBuilder" upOperations
            |> appendEmptyLine
            |> unindent |> appendLine "override this.Down(migrationBuilder:MigrationBuilder) ="
            |> indent |> FSharpMigrationOperationGenerator.Generate "migrationBuilder" downOperations
            |> string

    override this.GenerateMetadata(migrationNamespace: string, contextType: Type, migrationName: string, migrationId: string, targetModel: IModel) =
        let sb = IndentedStringBuilder()

        let defaultNamespaces =
            ["System";
             "Microsoft.EntityFrameworkCore";
             "Microsoft.EntityFrameworkCore.Infrastructure";
             "Microsoft.EntityFrameworkCore.Metadata";
             "Microsoft.EntityFrameworkCore.Migrations";
             contextType.Namespace]

        sb
            |> append "namespace " |> appendLine (FSharpHelper.Namespace [|migrationNamespace|])
            |> appendEmptyLine
            |> writeNamespaces defaultNamespaces
            |> appendEmptyLine
            |> append "[<DbContext(typeof<" |> append (contextType |> FSharpHelper.Reference) |> appendLine ">)>]"
            |> append "[<Migration(" |> append (migrationId |> FSharpHelper.Literal) |> appendLine ")>]"
            |> append "type " |> append (migrationName |> FSharpHelper.Identifier) |> appendLine "with"
            |> appendEmptyLine
            |> appendLine "override this.BuildTargetModel(modelBuilder: ModelBuilder) ="
            |> indent            
            |> FSharpSnapshotGenerator.generate "modelBuilder" targetModel
            |> appendEmptyLine
            |> unindent
            |> string

    override this.GenerateSnapshot(modelSnapshotNamespace: string, contextType: Type, modelSnapshotName: string, model: IModel) =
        let sb = IndentedStringBuilder()

        let defaultNamespaces =
            ["System";
             "Microsoft.EntityFrameworkCore";
             "Microsoft.EntityFrameworkCore.Infrastructure";
             "Microsoft.EntityFrameworkCore.Metadata";
             "Microsoft.EntityFrameworkCore.Migrations";
             contextType.Namespace]

        sb
            |> append "namespace " |> appendLine (FSharpHelper.Namespace [|modelSnapshotNamespace|])
            |> appendEmptyLine
            |> writeNamespaces defaultNamespaces
            |> appendEmptyLine
            |> append "[<DbContext(typeof<" |> append (contextType |> FSharpHelper.Reference) |> appendLine ">)>]"
            |> append "type " |> append (modelSnapshotName |> FSharpHelper.Identifier) |> appendLine "() ="
            |> indent |> appendLine "inherit ModelSnapshot()"
            |> appendEmptyLine
            |> appendLine "let hasAnnotation name value (modelBuilder:ModelBuilder) ="
            |> appendLineIndent "modelBuilder.HasAnnotation(name, value)"
            |> appendEmptyLine
            |> appendLine "override this.BuildModel(modelBuilder: ModelBuilder) ="
            |> indent            
            |> FSharpSnapshotGenerator.generate "modelBuilder" model
            |> appendEmptyLine
            |> unindent
            |> string
