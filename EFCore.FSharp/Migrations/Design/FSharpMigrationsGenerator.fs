namespace Bricelam.EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.Internal

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Bricelam.EntityFrameworkCore.FSharp.Internal
open Microsoft.EntityFrameworkCore.Design.Internal

type FSharpMigrationsGenerator(dependencies: MigrationsCodeGeneratorDependencies) =
    inherit MigrationsCodeGenerator(dependencies)
    
    let writeCreateTableType (sb: IndentedStringBuilder) (op:CreateTableOperation) =
        sb
            |> appendEmptyLine
            |> append "type private " |> append op.Name |> appendLine "Table = {"
            |> indent
            |> appendLines (op.Columns |> Seq.map (fun c -> sprintf "%s: OperationBuilder<AddColumnOperation>" c.Name)) false
            |> unindent
            |> appendLine "}"
            |> ignore
        ()        

    let createTypesForOperations (operations: MigrationOperation seq) (sb: IndentedStringBuilder) =
        operations
            |> Seq.filter(fun op -> (op :? CreateTableOperation))
            |> Seq.map(fun op -> (op :?> CreateTableOperation))
            |> Seq.iter(fun op -> op |> writeCreateTableType sb)
        sb

    member private this.GetAllNamespaces (operations: MigrationOperation seq) =
        let defaultNamespaces =
            seq { yield "System";
                     yield "System.Collections.Generic";
                     yield "Microsoft.EntityFrameworkCore";
                     yield "Microsoft.EntityFrameworkCore.Infrastructure";
                     yield "Microsoft.EntityFrameworkCore.Metadata";
                     yield "Microsoft.EntityFrameworkCore.Migrations";
                     yield "Microsoft.EntityFrameworkCore.Migrations.Operations";
                     yield "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders"; }

        let allOperationNamespaces = base.GetNamespaces(operations)

        let namespaceComparer = NamespaceComparer()
        let namespaces =
            allOperationNamespaces
            |> Seq.append defaultNamespaces
            |> Seq.toList
            |> List.sortWith (fun x y -> namespaceComparer.Compare(x, y))
            |> Seq.distinct

        namespaces        

    override this.FileExtension = ".fs"
    override this.Language = "F#"

    override this.GenerateMigration(migrationNamespace: string, migrationName: string, upOperations: IReadOnlyList<MigrationOperation>, downOperations: IReadOnlyList<MigrationOperation>) =
        let sb = IndentedStringBuilder()

        let allOperations =  (upOperations |> Seq.append downOperations)
        let namespaces = allOperations |> this.GetAllNamespaces

        sb
            |> append "namespace " |> appendLine (FSharpHelper.Namespace [|migrationNamespace|])
            |> appendEmptyLine
            |> writeNamespaces namespaces
            |> appendEmptyLine
            |> createTypesForOperations allOperations // This will eventually become redundant with anon record types
            |> appendEmptyLine
            //|> append "[<DbContext(typeof<" |> append (contextType |> FSharpHelper.Reference) |> appendLine ">)>]"
            //|> append "[<Migration(" |> append (migrationId |> FSharpHelper.Literal) |> appendLine ")>]"
            |> append "type " |> append (migrationName |> FSharpHelper.Identifier) |> appendLine "() ="
            |> indent |> appendLine "inherit Migration()"
            |> appendEmptyLine
            |> appendLine "override this.Up(migrationBuilder:MigrationBuilder) ="
            |> indent |> FSharpMigrationOperationGenerator.Generate "migrationBuilder" upOperations
            |> appendEmptyLine
            |> unindent |> appendLine "override this.Down(migrationBuilder:MigrationBuilder) ="
            |> indent |> FSharpMigrationOperationGenerator.Generate "migrationBuilder" downOperations
            // TODO: implement this override here
            // |> unindent
            // |> appendEmptyLine            
            // |> appendLine "override this.BuildTargetModel(modelBuilder: ModelBuilder) ="
            // |> indent            
            // |> FSharpSnapshotGenerator.generate "modelBuilder" targetModel
            |> appendEmptyLine
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
            |> append "type " |> append (migrationName |> FSharpHelper.Identifier) |> appendLine " with"
            |> appendEmptyLine
            |> indent
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
