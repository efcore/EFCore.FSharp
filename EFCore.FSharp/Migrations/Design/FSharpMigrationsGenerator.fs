namespace Bricelam.EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.Collections.Generic
open System.Reflection
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.Internal

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Bricelam.EntityFrameworkCore.FSharp.Internal
open Microsoft.EntityFrameworkCore.Design.Internal
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal

module FSharpMigrationsGenerator =
    let private getColumnNamespaces (columnOperation: ColumnOperation) =
        let ns = columnOperation.ClrType.GetNamespaces()

        let ns' =
            match columnOperation :? AlterColumnOperation with
            | true ->
                (columnOperation :?> AlterColumnOperation).OldColumn.ClrType.GetNamespaces()
            | false -> Seq.empty<String>

        ns |> Seq.append ns'

    let private getAnnotationNamespaces (items: IAnnotatable seq) = 
        let ignoredAnnotations = 
            [
                RelationshipDiscoveryConvention.NavigationCandidatesAnnotationName
                RelationshipDiscoveryConvention.AmbiguousNavigationsAnnotationName
                InversePropertyAttributeConvention.InverseNavigationsAnnotationName
            ]

        items
            |> Seq.collect (fun i -> i.GetAnnotations())
            |> Seq.filter (fun a -> a.Value |> notNull)
            |> Seq.filter (fun a -> (ignoredAnnotations |> List.contains a.Name) |> not)
            |> Seq.collect (fun a -> a.Value.GetType().GetNamespaces())
            |> Seq.toList

    let private getAnnotatables (ops: MigrationOperation seq) : IAnnotatable seq =
        
        let list = List<IAnnotatable>()
        ops |> Seq.map(fun o -> o :> IAnnotatable) |> list.AddRange

        ops
            |> Seq.filter(fun o -> o :? CreateTableOperation)
            |> Seq.map(fun o -> o :?> CreateTableOperation)
            |> Seq.iter(fun c -> 
                c.Columns |> Seq.map(fun o -> o :> IAnnotatable) |> list.AddRange

                if c.PrimaryKey |> notNull then
                    (c.PrimaryKey :> IAnnotatable) |> list.Add

                c.UniqueConstraints |> Seq.map(fun o -> o :> IAnnotatable) |> list.AddRange
                c.ForeignKeys |> Seq.map(fun o -> o :> IAnnotatable) |> list.AddRange
                )

        list |> Seq.cast

    let private getAnnotatablesByModel (model : IModel) =
        
        let g o = o :> IAnnotatable

        let e = 
            (model.GetEntityTypes())
            |> Seq.collect (fun e -> 

                [(e :> IAnnotatable)]
                @  (e.GetDeclaredProperties() |> Seq.map g |> Seq.toList)
                @  (e.GetDeclaredKeys() |> Seq.map g |> Seq.toList)
                @  (e.GetDeclaredForeignKeys() |> Seq.map g |> Seq.toList)
                @  (e.GetDeclaredIndexes() |> Seq.map g |> Seq.toList)
                )
            |> Seq.toList            

        [(model :> IAnnotatable)] @ e

    let private getOperationNamespaces (ops: MigrationOperation seq) =
        let columnOperations =
            ops
                |> Seq.filter(fun o -> o :? ColumnOperation)
                |> Seq.map(fun o -> o :?> ColumnOperation)
                |> Seq.collect(getColumnNamespaces)

        let columnsInCreateTableOperations =
            ops
                 |> Seq.filter(fun o -> o :? CreateTableOperation)
                 |> Seq.map(fun o -> o :?> CreateTableOperation)
                 |> Seq.collect(fun o -> o.Columns)
                 |> Seq.collect(getColumnNamespaces)

        let annotatables = ops |> getAnnotatables |> getAnnotationNamespaces

        columnOperations |> Seq.append columnsInCreateTableOperations |> Seq.append annotatables

    let private getModelNamspaces (model: IModel) =
        
        let namespaces =
            model.GetEntityTypes()
                |> Seq.collect
                    (fun e -> e.GetDeclaredProperties()
                                |> Seq.collect (fun p ->
                                                    let mapping = p.FindMapping()
                                                    let ns =
                                                        if  mapping |> isNull ||
                                                            mapping.Converter |> isNull ||
                                                            mapping.Converter.ProviderClrType |> isNull then
                                                            p.ClrType
                                                        else
                                                            mapping.Converter.ProviderClrType
                                                    ns.GetNamespaces()))
                |> Seq.toList                                

        let annotationNamespaces = model |> getAnnotatablesByModel |> getAnnotationNamespaces

        namespaces @ annotationNamespaces
    
    let private writeCreateTableType (sb: IndentedStringBuilder) (op:CreateTableOperation) =
        sb
            |> appendEmptyLine
            |> append "type private " |> append op.Name |> appendLine "Table = {"
            |> indent
            |> appendLines (op.Columns |> Seq.map (fun c -> sprintf "%s: OperationBuilder<AddColumnOperation>" c.Name)) false
            |> unindent
            |> appendLine "}"
            |> ignore
        ()        

    let private createTypesForOperations (operations: MigrationOperation seq) (sb: IndentedStringBuilder) =
        operations
            |> Seq.filter(fun op -> (op :? CreateTableOperation))
            |> Seq.map(fun op -> (op :?> CreateTableOperation))
            |> Seq.iter(fun op -> op |> writeCreateTableType sb)
        sb

    let private getAllNamespaces (operations: MigrationOperation seq) =
        let defaultNamespaces =
            seq { yield "System";
                     yield "System.Collections.Generic";
                     yield "Microsoft.EntityFrameworkCore";
                     yield "Microsoft.EntityFrameworkCore.Infrastructure";
                     yield "Microsoft.EntityFrameworkCore.Metadata";
                     yield "Microsoft.EntityFrameworkCore.Migrations";
                     yield "Microsoft.EntityFrameworkCore.Migrations.Operations";
                     yield "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders";
                     yield "Microsoft.EntityFrameworkCore.Storage.ValueConversion" }

        let allOperationNamespaces = operations |> getOperationNamespaces

        let namespaceComparer = NamespaceComparer()
        let namespaces =
            allOperationNamespaces
            |> Seq.append defaultNamespaces
            |> Seq.toList
            |> List.sortWith (fun x y -> namespaceComparer.Compare(x, y))
            |> Seq.distinct

        namespaces        

    let FileExtension = ".fs"

    let GenerateMigration (migrationNamespace) (migrationName) (migrationId: string) (contextType:Type) (upOperations) (downOperations) (model) =
        let sb = IndentedStringBuilder()

        let allOperations =  (upOperations |> Seq.append downOperations)
        let namespaces = allOperations |> getAllNamespaces |> Seq.append [contextType.Namespace]

        sb
            |> append "namespace " |> appendLine (FSharpHelper.Namespace [|migrationNamespace|])
            |> appendEmptyLine
            |> writeNamespaces namespaces
            |> appendEmptyLine
            |> createTypesForOperations allOperations // This will eventually become redundant with anon record types
            |> appendEmptyLine
            |> append "[<DbContext(typeof<" |> append (contextType |> FSharpHelper.Reference) |> appendLine ">)>]"
            |> append "[<Migration(" |> append (migrationId |> FSharpHelper.Literal) |> appendLine ")>]"
            |> append "type " |> append (migrationName |> FSharpHelper.Identifier) |> appendLine "() ="
            |> indent |> appendLine "inherit Migration()"
            |> appendEmptyLine
            |> appendLine "override this.Up(migrationBuilder:MigrationBuilder) ="
            |> indent |> FSharpMigrationOperationGenerator.Generate "migrationBuilder" upOperations
            |> appendEmptyLine
            |> unindent |> appendLine "override this.Down(migrationBuilder:MigrationBuilder) ="
            |> indent |> FSharpMigrationOperationGenerator.Generate "migrationBuilder" downOperations
            |> unindent
            |> appendEmptyLine            
            |> appendLine "override this.BuildTargetModel(modelBuilder: ModelBuilder) ="
            |> indent            
            |> FSharpSnapshotGenerator.generate "modelBuilder" model
            |> appendEmptyLine
            |> string

    let GenerateSnapshot (modelSnapshotNamespace: string) (contextType: Type) (modelSnapshotName: string) (model: IModel) =
        let sb = IndentedStringBuilder()

        let defaultNamespaces =
            seq {
                 yield "System"
                 yield "Microsoft.EntityFrameworkCore"
                 yield "Microsoft.EntityFrameworkCore.Infrastructure"
                 yield "Microsoft.EntityFrameworkCore.Metadata"
                 yield "Microsoft.EntityFrameworkCore.Migrations"
                 yield "Microsoft.EntityFrameworkCore.Storage.ValueConversion"

                 if contextType.Namespace |> String.IsNullOrEmpty |> not then
                    yield contextType.Namespace
            }
            |> Seq.toList

        let modelNamespaces = model |> getModelNamspaces

        let namespaceComparer = NamespaceComparer()
        let namespaces =
            (defaultNamespaces @ modelNamespaces)
            |> List.sortWith (fun x y -> namespaceComparer.Compare(x, y))
            |> Seq.distinct

        sb
            |> append "namespace " |> appendLine (FSharpHelper.Namespace [|modelSnapshotNamespace|])
            |> appendEmptyLine
            |> writeNamespaces namespaces
            |> appendEmptyLine
            |> append "[<DbContext(typeof<" |> append (contextType |> FSharpHelper.Reference) |> appendLine ">)>]"
            |> append "type " |> append (modelSnapshotName |> FSharpHelper.Identifier) |> appendLine "() ="
            |> indent |> appendLine "inherit ModelSnapshot()"
            |> appendEmptyLine
            |> appendLine "override this.BuildModel(modelBuilder: ModelBuilder) ="
            |> indent            
            |> FSharpSnapshotGenerator.generate "modelBuilder" model
            |> appendEmptyLine
            |> unindent
            |> string
