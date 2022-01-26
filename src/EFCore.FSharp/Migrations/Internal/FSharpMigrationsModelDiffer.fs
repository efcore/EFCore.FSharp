namespace EntityFrameworkCore.FSharp.Migrations.Internal

open System.Runtime.InteropServices
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Migrations.Internal
open Microsoft.EntityFrameworkCore.Migrations.Operations
open EntityFrameworkCore.FSharp.SharedTypeExtensions
open Microsoft.EntityFrameworkCore.Metadata.Internal

type FSharpMigrationsModelDiffer
    (
        typeMappingSource,
        migrationsAnnotations,
        changeDetector,
        updateAdapterFactory,
        commandBatchPreparerDependencies
    ) =
    inherit MigrationsModelDiffer
        (
            typeMappingSource,
            migrationsAnnotations,
            changeDetector,
            updateAdapterFactory,
            commandBatchPreparerDependencies
        )

    let isNullableType (p: IProperty) =
        let clrType = p.ClrType
        let isPrimaryKey = p.IsPrimaryKey()

        let isNullable =
            (isOptionType clrType || isNullableType clrType)

        isNullable && not isPrimaryKey

    override _.Diff
        (
            source: IColumn,
            target: IColumn,
            diffContext: MigrationsModelDiffer.DiffContext
        ) : MigrationOperation seq =

        let sourceTypeProperty =
            (source.PropertyMappings |> Seq.head).Property

        let targetTypeProperty =
            (target.PropertyMappings |> Seq.head).Property

        (source :?> Column).IsNullable <- isNullableType sourceTypeProperty
        (target :?> Column).IsNullable <- isNullableType targetTypeProperty

        base.Diff(source, target, diffContext)

    override _.Add
        (
            source: IColumn,
            diffContext: MigrationsModelDiffer.DiffContext,
            [<Optional; DefaultParameterValue(false)>] ``inline``: bool
        ) : MigrationOperation seq =

        let sourceTypeProperty =
            (source.PropertyMappings |> Seq.head).Property

        (source :?> Column).IsNullable <- isNullableType sourceTypeProperty

        base.Add(source, diffContext, ``inline``)
