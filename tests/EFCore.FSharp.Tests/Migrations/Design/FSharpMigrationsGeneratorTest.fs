namespace EntityFrameworkCore.FSharp.Test.Migrations.Design

open System
open System.Collections.Generic

open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.EntityFrameworkCore.TestUtilities

open EntityFrameworkCore.FSharp.Internal
open EntityFrameworkCore.FSharp.Migrations.Design
open EntityFrameworkCore.FSharp.Test.TestUtilities

open Expecto
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Storage.ValueConversion
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Migrations.Operations
open System.Text.RegularExpressions
open Microsoft.EntityFrameworkCore.ChangeTracking
open Microsoft.EntityFrameworkCore.Migrations

type TestFSharpSnapshotGenerator (dependencies, mappingSource : IRelationalTypeMappingSource) =
    inherit FSharpSnapshotGenerator(dependencies, mappingSource)

    member this.TestGenerateEntityTypeAnnotations builderName entityType stringBuilder =
        this.generateEntityTypeAnnotations builderName entityType stringBuilder

    member this.TestGeneratePropertyAnnotations property sb =
        this.generatePropertyAnnotations property sb

type WithAnnotations() =
    [<DefaultValue>]
    val mutable id : int
    member this.Id with get() = this.id and set v = this.id <- v

type Derived() =
    inherit WithAnnotations()

type RawEnum =
    | A = 0
    | B = 1

type MyContext() =
    class end

module FSharpMigrationsGeneratorTest =

    let compileModelSnapshot (modelSnapshotCode: string) (modelSnapshotTypeName: string) =
        let references =
            [|
                "Microsoft.EntityFrameworkCore"
                "Microsoft.EntityFrameworkCore.Relational"
            |]

        let sources =
            [
                modelSnapshotCode
            ]

        let build = { Sources = sources; TargetDir = null }
        let assembly = build.BuildInMemory references

        let snapshotType = assembly.GetType(modelSnapshotTypeName, throwOnError = true, ignoreCase = false)

        let contextTypeAttribute =
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<DbContextAttribute>(snapshotType)

        Expect.isNotNull contextTypeAttribute "Should not be null"
        Expect.equal contextTypeAttribute.ContextType typeof<MyContext> "Should be equal"

        Activator.CreateInstance(snapshotType) :?> ModelSnapshot


    let nl = Environment.NewLine

    let createMigrationsCodeGenerator() =
        let serverTypeMappingSource =
            SqlServerTypeMappingSource(
                TestServiceFactory.Instance.Create<TypeMappingSourceDependencies>(),
                TestServiceFactory.Instance.Create<RelationalTypeMappingSourceDependencies>())

        let codeHelper = FSharpHelper(serverTypeMappingSource)

        let generator =
            FSharpMigrationsGenerator(
                MigrationsCodeGeneratorDependencies(serverTypeMappingSource),
                FSharpMigrationsGeneratorDependencies(
                    codeHelper,
                    FSharpMigrationOperationGenerator(codeHelper),
                    FSharpSnapshotGenerator(codeHelper, serverTypeMappingSource)))

        (serverTypeMappingSource, codeHelper, generator)

    let missingAnnotationCheck (createMetadataItem: ModelBuilder -> IMutableAnnotatable) (invalidAnnotations:HashSet<string>) (validAnnotations:IDictionary<string, (obj * string)>) (generationDefault : string) (test: TestFSharpSnapshotGenerator -> IMutableAnnotatable -> IndentedStringBuilder -> unit) =

        let typeMappingSource =
            SqlServerTypeMappingSource(
                TestServiceFactory.Instance.Create<TypeMappingSourceDependencies>(),
                TestServiceFactory.Instance.Create<RelationalTypeMappingSourceDependencies>())

        let codeHelper =
            new FSharpHelper(typeMappingSource)

        let generator = TestFSharpSnapshotGenerator(codeHelper, typeMappingSource);

        let caNames =
            (typeof<CoreAnnotationNames>).GetFields()
            |> Seq.filter(fun f -> f.FieldType = typeof<string>)
            |> Seq.toList

        let rlNames = (typeof<RelationalAnnotationNames>).GetFields() |> Seq.toList

        let allAnnotations = (caNames @ rlNames) |> Seq.filter (fun f -> f.Name <> "Prefix")

        allAnnotations
        |> Seq.iter(fun f ->
            let annotationName = f.GetValue(null) |> string

            if not (invalidAnnotations.Contains(annotationName)) then
                let modelBuilder = RelationalTestHelpers.Instance.CreateConventionBuilder()
                let metadataItem = createMetadataItem modelBuilder

                metadataItem.[annotationName] <-
                    if validAnnotations.ContainsKey(annotationName) then
                        fst validAnnotations.[annotationName]
                    else
                        null

                modelBuilder.FinalizeModel() |> ignore

                let sb = IndentedStringBuilder()

                try
                    test generator metadataItem sb
                with
                    | exn ->
                        let msg = sprintf "Annotation '%s' was not handled by the code generator: {%s}" annotationName exn.Message
                        Expect.isTrue false msg

                let actual = sb.ToString()

                let expected =
                    if validAnnotations.ContainsKey(annotationName) then
                        snd validAnnotations.[annotationName]
                    else
                        generationDefault

                Expect.equal actual expected "Should be equal"
            )

        ()

    [<Tests>]
    let FSharpMigrationsGeneratorTest =
        testList "FSharpMigrationsGeneratorTest" [

            test "Test new annotations handled for entity types" {
                let notForEntityType =
                    [
                        CoreAnnotationNames.MaxLength
                        CoreAnnotationNames.Unicode
                        CoreAnnotationNames.ProductVersion
                        CoreAnnotationNames.ValueGeneratorFactory
                        CoreAnnotationNames.OwnedTypes
                        CoreAnnotationNames.TypeMapping
                        CoreAnnotationNames.ValueConverter
                        CoreAnnotationNames.ValueComparer
                        CoreAnnotationNames.KeyValueComparer
                        CoreAnnotationNames.StructuralValueComparer
                        CoreAnnotationNames.BeforeSaveBehavior
                        CoreAnnotationNames.AfterSaveBehavior
                        CoreAnnotationNames.ProviderClrType
                        CoreAnnotationNames.EagerLoaded
                        CoreAnnotationNames.DuplicateServiceProperties
                        RelationalAnnotationNames.ColumnName
                        RelationalAnnotationNames.ColumnType
                        RelationalAnnotationNames.DefaultValueSql
                        RelationalAnnotationNames.ComputedColumnSql
                        RelationalAnnotationNames.DefaultValue
                        RelationalAnnotationNames.Name
                        RelationalAnnotationNames.SequencePrefix
                        RelationalAnnotationNames.CheckConstraints
                        RelationalAnnotationNames.DefaultSchema
                        RelationalAnnotationNames.Filter
                        RelationalAnnotationNames.DbFunction
                        RelationalAnnotationNames.MaxIdentifierLength
                        RelationalAnnotationNames.IsFixedLength
                    ] |> HashSet

                let toTable = nl + @"modelBuilder.ToTable(""WithAnnotations"") |> ignore" + nl

                let forEntityType =
                    [
                        (
                            RelationalAnnotationNames.TableName, ("MyTable" :> obj,
                                nl + "modelBuilder.ToTable" + @"(""MyTable"") |> ignore" + nl)
                        )
                        (
                            RelationalAnnotationNames.Schema, ("MySchema" :> obj,
                                nl
                                + "modelBuilder."
                                + "ToTable"
                                + @"(""WithAnnotations"",""MySchema"") |> ignore"
                                + nl)
                        )
                        (
                            CoreAnnotationNames.DiscriminatorProperty, ("Id" :> obj,
                                toTable
                                + nl
                                + "modelBuilder.HasDiscriminator"
                                + @"<int>(""Id"") |> ignore"
                                + nl)
                        )
                        (
                            CoreAnnotationNames.DiscriminatorValue, ("MyDiscriminatorValue" :> obj,
                                toTable
                                + nl
                                + "modelBuilder.HasDiscriminator"
                                + "()."
                                + "HasValue"
                                + @"(""MyDiscriminatorValue"") |> ignore"
                                + nl)
                        )
                        (
                            RelationalAnnotationNames.Comment, ("My Comment" :> obj,
                                toTable
                                + nl
                                + "modelBuilder.HasComment"
                                + @"(""My Comment"") |> ignore"
                                + nl)
                        )
                    ] |> dict

                missingAnnotationCheck
                        (fun b -> (b.Entity<WithAnnotations>().Metadata :> IMutableAnnotatable))
                        notForEntityType
                        forEntityType
                        toTable
                        (fun g m b -> g.generateEntityTypeAnnotations "modelBuilder" (m :> obj :?> _) b |> ignore)
            }

            test "Test new annotations handled for property types" {
                let notForProperty =
                    [
                        CoreAnnotationNames.ProductVersion
                        CoreAnnotationNames.OwnedTypes
                        CoreAnnotationNames.ConstructorBinding
                        CoreAnnotationNames.NavigationAccessMode
                        CoreAnnotationNames.EagerLoaded
                        CoreAnnotationNames.QueryFilter
                        CoreAnnotationNames.DefiningQuery
                        CoreAnnotationNames.DiscriminatorProperty
                        CoreAnnotationNames.DiscriminatorValue
                        CoreAnnotationNames.InverseNavigations
                        CoreAnnotationNames.NavigationCandidates
                        CoreAnnotationNames.AmbiguousNavigations
                        CoreAnnotationNames.DuplicateServiceProperties
                        RelationalAnnotationNames.TableName
                        RelationalAnnotationNames.Schema
                        RelationalAnnotationNames.DefaultSchema
                        RelationalAnnotationNames.Name
                        RelationalAnnotationNames.SequencePrefix
                        RelationalAnnotationNames.CheckConstraints
                        RelationalAnnotationNames.Filter
                        RelationalAnnotationNames.DbFunction
                        RelationalAnnotationNames.MaxIdentifierLength
                    ] |> HashSet

                let columnMapping =
                    nl + @".HasColumnType(""default_int_mapping"")"

                let forProperty =
                    [
                        ( CoreAnnotationNames.MaxLength, (256 :> obj, columnMapping + nl + ".HasMaxLength(256) |> ignore"))
                        ( CoreAnnotationNames.Unicode, (false :> obj, columnMapping + nl + ".IsUnicode(false) |> ignore"))
                        (
                            CoreAnnotationNames.ValueConverter, (new ValueConverter<int, int64>((fun v -> v |> int64), (fun v -> v |> int), null) :> obj,
                                nl+ @".HasColumnType(""default_long_mapping"") |> ignore")
                        )
                        (
                            CoreAnnotationNames.ProviderClrType,
                            (typeof<int64> :> obj, nl + @".HasColumnType(""default_long_mapping"") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.ColumnName,
                            ("MyColumn" :> obj, nl + @".HasColumnName(""MyColumn"")" + columnMapping + " |> ignore")
                        )
                        (
                            RelationalAnnotationNames.ColumnType,
                            ("int" :> obj, nl + @".HasColumnType(""int"") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.DefaultValueSql,
                            ("some SQL" :> obj, columnMapping + nl + @".HasDefaultValueSql(""some SQL"") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.ComputedColumnSql,
                            ("some SQL" :> obj, columnMapping + nl + @".HasComputedColumnSql(""some SQL"") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.DefaultValue,
                            ("1" :> obj, columnMapping + nl + @".HasDefaultValue(""1"") |> ignore")
                        )
                        (
                            CoreAnnotationNames.TypeMapping,
                            (new LongTypeMapping("bigint") :> obj, nl + @".HasColumnType(""bigint"") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.IsFixedLength,
                            (true :> obj, columnMapping + nl + @".IsFixedLength(true) |> ignore")
                        )
                        (
                            RelationalAnnotationNames.Comment,
                            ("My Comment" :> obj, columnMapping + nl + @".HasComment(""My Comment"") |> ignore")
                        )
                    ] |> dict

                missingAnnotationCheck
                    (fun b -> (b.Entity<WithAnnotations>().Property(fun e -> e.Id).Metadata :> IMutableAnnotatable))
                    notForProperty
                    forProperty
                    (columnMapping + " |> ignore")
                    (fun g m b -> g.generatePropertyAnnotations (m :> obj :?> _) b |> ignore)
            }

            // test "Snapshot with enum discriminator uses converted values" {
            //     let (serverTypeMappingSource, _, generator) = createMigrationsCodeGenerator()

            //     let modelBuilder = RelationalTestHelpers.Instance.CreateConventionBuilder()

            //     modelBuilder.Model.RemoveAnnotation(CoreAnnotationNames.ProductVersion) |> ignore

            //     modelBuilder.Entity<WithAnnotations>(fun eb ->
            //         eb.HasDiscriminator<RawEnum>("EnumDiscriminator")
            //             .HasValue(RawEnum.A)
            //             .HasValue<Derived>(RawEnum.B) |> ignore

            //         eb.Property<RawEnum>("EnumDiscriminator").HasConversion<int>() |> ignore)
            //         |> ignore

            //     modelBuilder.FinalizeModel() |> ignore

            //     let modelSnapshotCode =
            //         generator.GenerateSnapshot(
            //             "MyNamespace",
            //             typeof<MyContext>,
            //             "MySnapshot",
            //             modelBuilder.Model)

            //     let snapshotModel = (compileModelSnapshot modelSnapshotCode "MyNamespace.MySnapshot").Model

            //     Expect.equal (snapshotModel.FindEntityType(typeof<WithAnnotations>).GetDiscriminatorValue()) ((int RawEnum.A) :> obj) "Should be equal"
            //     Expect.equal (snapshotModel.FindEntityType(typeof<Derived>).GetDiscriminatorValue()) ((int RawEnum.B) :> obj) "Should be equal"
            // }

        ]
