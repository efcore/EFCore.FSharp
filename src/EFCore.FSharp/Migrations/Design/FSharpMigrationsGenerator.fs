namespace EntityFrameworkCore.FSharp.Migrations.Design

open System
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.Internal

open EntityFrameworkCore.FSharp.EntityFrameworkExtensions
open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.EntityFrameworkCore.Storage.ValueConversion
open Microsoft.EntityFrameworkCore.ChangeTracking.Internal
open EntityFrameworkCore.FSharp.SharedTypeExtensions

type FSharpMigrationsGenerator(dependencies, fSharpDependencies : FSharpMigrationsGeneratorDependencies) = 
    inherit MigrationsCodeGenerator(dependencies)

    let code = fSharpDependencies.FSharpHelper
    let generator = fSharpDependencies.FSharpMigrationOperationGenerator
    let snapshot = fSharpDependencies.FSharpSnapshotGenerator

    let getColumnNamespaces (columnOperation: ColumnOperation) =
        let ns = getNamespaces columnOperation.ClrType

        let ns' =
            if columnOperation :? AlterColumnOperation then
                (columnOperation :?> AlterColumnOperation).OldColumn.ClrType |> getNamespaces
            else
                Seq.empty<String>

        ns |> Seq.append ns'

    let findValueConverter (property: IProperty) =
        let coreType = 
            property.[CoreAnnotationNames.TypeMapping] :?> CoreTypeMapping

        if isNull coreType then
            dependencies.RelationalTypeMappingSource.FindMapping(property).Converter
        else
            coreType.Converter

    let getAnnotationNamespaces (items: IAnnotatable seq) = 
        let ignoredAnnotations = 
            [
                CoreAnnotationNames.NavigationCandidates
                CoreAnnotationNames.AmbiguousNavigations
                CoreAnnotationNames.InverseNavigations
                ChangeDetector.SkipDetectChangesAnnotation
                CoreAnnotationNames.OwnedTypes
                CoreAnnotationNames.ChangeTrackingStrategy
                CoreAnnotationNames.BeforeSaveBehavior
                CoreAnnotationNames.AfterSaveBehavior
                CoreAnnotationNames.TypeMapping
                CoreAnnotationNames.ValueComparer
                CoreAnnotationNames.KeyValueComparer
                CoreAnnotationNames.StructuralValueComparer
                CoreAnnotationNames.ConstructorBinding
                CoreAnnotationNames.NavigationAccessMode
                CoreAnnotationNames.PropertyAccessMode
                CoreAnnotationNames.ProviderClrType
                CoreAnnotationNames.ValueConverter
                CoreAnnotationNames.ValueGeneratorFactory
                CoreAnnotationNames.DefiningQuery
                CoreAnnotationNames.QueryFilter
                RelationalAnnotationNames.CheckConstraints
            ]

        let ignoreAnnotationTypes = 
            [
                RelationalAnnotationNames.DbFunction 
                RelationalAnnotationNames.SequencePrefix        
            ]

        let getProviderType (annotatable: IAnnotatable) (t: Type) = 
            match annotatable with
            | :? IProperty -> 
                let p = annotatable :?> IProperty
                if (t |> unwrapNullableType) = (p.ClrType |> unwrapNullableType) then
                    let valueConverter = findValueConverter p
                    
                    if isNull valueConverter then 
                        t
                    else 
                        valueConverter.ProviderClrType
                else 
                    t
            | _ -> t

        items
            |> Seq.collect (fun i -> 
                i.GetAnnotations()
                |> Seq.map(fun a -> {| Annotatable = i; Annotation = a|}))            
            |> Seq.filter (fun a -> a.Annotation.Value |> notNull)
            |> Seq.filter (fun a -> (ignoredAnnotations |> List.contains a.Annotation.Name) |> not)
            |> Seq.filter (fun a -> 
                ignoreAnnotationTypes
                |> Seq.exists (fun p -> a.Annotation.Name.StartsWith(p, StringComparison.Ordinal))
                |> not)
            |> Seq.collect (fun a -> getProviderType a.Annotatable (a.Annotation.Value.GetType()) |> getNamespaces)
            |> Seq.toList

    let getAnnotatables (ops: MigrationOperation seq) : IAnnotatable seq =
        
        let list = ops |> Seq.map toAnnotatable |> ResizeArray

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

    let getAnnotatablesByModel (model : IModel) =
        
        let e = 
            (model.GetEntityTypes())
            |> Seq.collect (fun e -> 
                
                (toAnnotatable e)
                ::  (e.GetProperties() |> Seq.map toAnnotatable |> Seq.toList)
                @  (e.GetKeys() |> Seq.map toAnnotatable |> Seq.toList)
                @  (e.GetForeignKeys() |> Seq.map toAnnotatable |> Seq.toList)
                @  (e.GetIndexes() |> Seq.map toAnnotatable |> Seq.toList)
                )
            |> Seq.toList            

        (toAnnotatable model) :: e
           

    let getOperationNamespaces (ops: MigrationOperation seq) =
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

        let insertUpdateDeleteNamespaces =
            let getDataNamespaces (values: obj[,]) =
                seq {            
                    for row in 0..values.GetLength(0) do
                        for col in 0..values.GetLength(1) do 
                            let value = values.[row, col]
                            if isNull value |> not then
                                for ns' in value.GetType().GetNamespaces() do
                                    ns'
                }

            let insert = 
                ops 
                |> Seq.filter(fun o -> o :? InsertDataOperation)
                |> Seq.map(fun o -> (o :?> InsertDataOperation).Values)

            let update = 
                ops 
                |> Seq.filter(fun o -> o :? UpdateDataOperation)
                |> Seq.collect(fun o -> 
                    let o' = (o :?> UpdateDataOperation)
                    [| o'.KeyValues; o'.Values|])

            let delete = 
                ops 
                |> Seq.filter(fun o -> o :? DeleteDataOperation)
                |> Seq.map(fun o -> (o :?> DeleteDataOperation).KeyValues)

            insert
            |> Seq.append update
            |> Seq.append delete
            |> Seq.collect (fun o -> getDataNamespaces o)

        let annotatables = 
            ops 
            |> getAnnotatables 
            |> getAnnotationNamespaces

        columnOperations 
        |> Seq.append columnsInCreateTableOperations 
        |> Seq.append insertUpdateDeleteNamespaces
        |> Seq.append annotatables        

    let getModelNamespaces (model: IModel) =        
        let namespaces =
            model.GetEntityTypes()
            |> Seq.collect (fun e -> 
                e.AsEntityType().GetDeclaredProperties()
                |> Seq.collect (fun p ->
                                    let converter = findValueConverter p
                                    let ns =
                                        if  converter |> isNull ||
                                            converter.ProviderClrType |> isNull then
                                            p.ClrType
                                        else
                                            converter.ProviderClrType
                                    getNamespaces ns))
            |> Seq.toList                                

        let annotationNamespaces = model |> getAnnotatablesByModel |> getAnnotationNamespaces

        namespaces @ annotationNamespaces
    
    let writeCreateTableType (sb: IndentedStringBuilder) (op:CreateTableOperation) =
        sb
            |> appendEmptyLine
            |> append "type private " |> append op.Name |> appendLine "Table = {"
            |> indent
            |> appendLines (op.Columns |> Seq.map (fun c -> sprintf "%s: OperationBuilder<AddColumnOperation>" c.Name)) false
            |> unindent
            |> appendLine "}"
            |> ignore

    let createTypesForOperations (operations: MigrationOperation seq) (sb: IndentedStringBuilder) =
        operations
            |> Seq.filter(fun op -> (op :? CreateTableOperation))
            |> Seq.map(fun op -> (op :?> CreateTableOperation))
            |> Seq.iter(fun op -> op |> writeCreateTableType sb)
        sb

    let getAllNamespaces (operations: MigrationOperation seq) =
        let defaultNamespaces =
            seq {
                "System"
                "System.Collections.Generic"
                "Microsoft.EntityFrameworkCore"
                "Microsoft.EntityFrameworkCore.Infrastructure"
                "Microsoft.EntityFrameworkCore.Metadata"
                "Microsoft.EntityFrameworkCore.Migrations"
                "Microsoft.EntityFrameworkCore.Migrations.Operations"
                "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders"
                "Microsoft.EntityFrameworkCore.Storage.ValueConversion" 
            }

        let allOperationNamespaces = operations |> getOperationNamespaces

        let namespaces =
            allOperationNamespaces
            |> Seq.append defaultNamespaces
            |> Seq.toList
            |> sortNamespaces
            |> Seq.distinct

        namespaces

    let generateMigration (migrationNamespace) (migrationName) (migrationId: string) (contextType:Type) (upOperations) (downOperations) (model) =
        let sb = IndentedStringBuilder()

        let allOperations = (upOperations |> Seq.append downOperations)
        let namespaces =
            allOperations
            |> getAllNamespaces
            |> Seq.append [contextType.Namespace]
            |> Seq.filter (isNull >> not)

        sb
        |> appendAutoGeneratedTag
        |> append "namespace " |> appendLine (code.Namespace [|migrationNamespace|])
        |> appendEmptyLine
        |> writeNamespaces namespaces
        |> appendEmptyLine
        |> createTypesForOperations allOperations // This will eventually become redundant with anon record types
        |> appendEmptyLine
        |> append "[<DbContext(typeof<" |> append (contextType |> code.Reference) |> appendLine ">)>]"
        |> append "[<Migration(" |> append (migrationId |> code.Literal) |> appendLine ")>]"
        |> append "type " |> append (migrationName |> code.Identifier) |> appendLine "() ="
        |> indent |> appendLine "inherit Migration()"
        |> appendEmptyLine
        |> appendLine "override this.Up(migrationBuilder:MigrationBuilder) ="
        |> indent |> ignore
        
        generator.Generate("migrationBuilder", upOperations, sb)

        sb        
        |> appendEmptyLine
        |> unindent |> appendLine "override this.Down(migrationBuilder:MigrationBuilder) ="
        |> indent |> ignore
        
        generator.Generate("migrationBuilder", downOperations, sb)

        sb
        |> unindent
        |> appendEmptyLine            
        |> appendLine "override this.BuildTargetModel(modelBuilder: ModelBuilder) ="
        |> indent  
        |> ignore          
        
        snapshot.Generate("modelBuilder", model, sb)

        sb
        |> appendEmptyLine
        |> string

    let generateSnapshot (modelSnapshotNamespace: string) (contextType: Type) (modelSnapshotName: string) (model: IModel) =
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

        let modelNamespaces = model |> getModelNamespaces

        let namespaces =
            (defaultNamespaces @ modelNamespaces)
            |> sortNamespaces
            |> Seq.distinct

        sb
            |> append "namespace " |> appendLine (code.Namespace [|modelSnapshotNamespace|])
            |> appendEmptyLine
            |> writeNamespaces namespaces
            |> appendEmptyLine
            |> append "[<DbContext(typeof<" |> append (contextType |> code.Reference) |> appendLine ">)>]"
            |> append "type " |> append (modelSnapshotName |> code.Identifier) |> appendLine "() ="
            |> indent |> appendLine "inherit ModelSnapshot()"
            |> appendEmptyLine
            |> appendLine "override this.BuildModel(modelBuilder: ModelBuilder) ="
            |> indent
            |> ignore

        snapshot.Generate("modelBuilder", model, sb)
        
        sb
            |> appendEmptyLine
            |> unindent
            |> string

    // Due to api shape we're currently forced to work around the fact EF expects 2 files per migration
    let mutable tempUpOperations = list.Empty
    let mutable tempDownOperations = list.Empty
    let mutable tempMigrationName = String.Empty

    override __.Language with get() = "F#"
    override __.FileExtension with get() = ".fs"

    // Defined in the order of when it's called during migration add
    override this.GenerateMigration (migrationNamespace, migrationName, upOperations, downOperations) =
        tempUpOperations <- Seq.toList upOperations
        tempDownOperations <- Seq.toList downOperations 
        tempMigrationName <- migrationName
        "// intentionally empty"
    
    override this.GenerateMetadata (migrationNamespace, contextType, migrationName, migrationId, targetModel) = 
        if tempMigrationName = migrationName then
            generateMigration migrationNamespace migrationName migrationId contextType tempUpOperations tempDownOperations targetModel
        else 
            invalidOp "Migration isn't the same as previously saved during GenerateMigration, DEV: did the order of operations change?"

    override this.GenerateSnapshot (modelSnapshotNamespace, contextType, modelSnapshotName, model) = 
        generateSnapshot modelSnapshotNamespace contextType modelSnapshotName model
