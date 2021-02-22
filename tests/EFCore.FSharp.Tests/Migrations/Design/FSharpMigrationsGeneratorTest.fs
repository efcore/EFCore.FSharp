namespace EntityFrameworkCore.FSharp.Test.Migrations.Design

open System
open System.Collections.Generic

open System.Linq.Expressions
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.SqlServer.Design.Internal
open Microsoft.EntityFrameworkCore.Storage.ValueConversion
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.EntityFrameworkCore.TestUtilities

open EntityFrameworkCore.FSharp.Internal
open EntityFrameworkCore.FSharp.Migrations.Design
open EntityFrameworkCore.FSharp.Test.TestUtilities

open Expecto

type TestFSharpSnapshotGenerator (dependencies,
                                  mappingSource: IRelationalTypeMappingSource,
                                  annotationCodeGenerator: IAnnotationCodeGenerator) =
    inherit FSharpSnapshotGenerator(dependencies, mappingSource, annotationCodeGenerator)

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

type private RawEnum =
    | A = 0
    | B = 1

type MyContext() =
    class end

module FSharpMigrationsGeneratorTest =

    let getRequiredReferences() =
        let runtimeNames =
            [
                "mscorlib.dll"
                "System.Private.CoreLib.dll"
                "System.Runtime.dll"
                "netstandard.dll"
                "System.Runtime.Extensions.dll"
                "System.Console.dll"
                "System.Collections.dll"
                "System.Resources.ResourceManager.dll"
                "System.Collections.Concurrent.dll"
                "System.Threading.Tasks.dll"
                "System.Threading.dll"
                "System.Threading.ThreadPool.dll"
                "System.Threading.Thread.dll"
                "System.Diagnostics.TraceSource.dll"
                "System.Buffers.dll"
                "System.Globalization.dll"
                "System.IO.FileSystem.dll"
                "System.Runtime.InteropServices.dll"
                "System.Runtime.Numerics.dll"
                "System.Net.Requests.dll"
                "System.Linq.Expressions.dll"
                "System.Net.WebClient.dll"
                "System.ObjectModel.dll"
                "System.ComponentModel.dll"
                "System.Data.Common.dll"
            ]

        let thisAssembly = System.Reflection.Assembly.GetExecutingAssembly()

        let localNames =
            [
                "FSharp.Core.dll"
                "FSharp.Compiler.Service.dll"
                "Microsoft.EntityFrameworkCore.dll"
                "Microsoft.EntityFrameworkCore.Abstractions.dll"
                "Microsoft.EntityFrameworkCore.Design.dll"
                "Microsoft.EntityFrameworkCore.Proxies.dll"
                "Microsoft.EntityFrameworkCore.Relational.dll"
                "Microsoft.EntityFrameworkCore.Sqlite.dll"
                "Microsoft.EntityFrameworkCore.SqlServer.dll"
                "EntityFrameworkCore.FSharp.dll"
                $"{thisAssembly.GetName().Name}.dll"
            ]

        let runtimeDir =
            System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()

        let runtimeRefs =
            runtimeNames
            |> List.map(fun r -> runtimeDir + r)

        let localRefs =
            let location = thisAssembly.Location.Replace(thisAssembly.GetName().Name + ".dll", "")
            localNames
            |> List.map(fun s -> location + s)

        runtimeRefs @ localRefs |> List.toArray

    let compileModelSnapshot (modelSnapshotCode: string) (modelSnapshotTypeName: string) =
        let references = getRequiredReferences()

        let sources = [ modelSnapshotCode ]

        let build = { Sources = sources; TargetDir = null }
        let assembly = build.BuildInMemory references

        let snapshotType = assembly.GetType(modelSnapshotTypeName, throwOnError = true, ignoreCase = false)

        let contextTypeAttribute =
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<DbContextAttribute>(snapshotType)

        Expect.isNotNull contextTypeAttribute "Should not be null"
        Expect.equal contextTypeAttribute.ContextType.FullName typeof<MyContext>.FullName "Should be equal"

        Activator.CreateInstance(snapshotType) :?> ModelSnapshot


    let nl = Environment.NewLine

    let createMigrationsCodeGenerator() =

        let serverTypeMappingSource =
            SqlServerTypeMappingSource(
                TestServiceFactory.Instance.Create<TypeMappingSourceDependencies>(),
                TestServiceFactory.Instance.Create<RelationalTypeMappingSourceDependencies>())

        let sqlServerAnnotationCodeGenerator =
            SqlServerAnnotationCodeGenerator(
                AnnotationCodeGeneratorDependencies(serverTypeMappingSource));

        let annotationCodeGenerator =
            AnnotationCodeGenerator(AnnotationCodeGeneratorDependencies(serverTypeMappingSource))

        let codeHelper = FSharpHelper(serverTypeMappingSource)

        let generator =
            FSharpMigrationsGenerator(
                MigrationsCodeGeneratorDependencies(serverTypeMappingSource, sqlServerAnnotationCodeGenerator),
                FSharpMigrationsGeneratorDependencies(
                    codeHelper,
                    FSharpMigrationOperationGenerator(codeHelper),
                    FSharpSnapshotGenerator(codeHelper, serverTypeMappingSource, annotationCodeGenerator)))

        (serverTypeMappingSource, codeHelper, generator)

    let missingAnnotationCheck (createMetadataItem: ModelBuilder -> IMutableAnnotatable)
                               (invalidAnnotations:HashSet<string>)
                               (validAnnotations:IDictionary<string, (obj * string)>)
                               (generationDefault : string)
                               (test: TestFSharpSnapshotGenerator -> IMutableAnnotatable -> IndentedStringBuilder -> unit) =

        let sqlServerTypeMappingSource =
            SqlServerTypeMappingSource(
                TestServiceFactory.Instance.Create<TypeMappingSourceDependencies>(),
                TestServiceFactory.Instance.Create<RelationalTypeMappingSourceDependencies>())

        let codeHelper =
            FSharpHelper(sqlServerTypeMappingSource)

        let annotationCodeGenerator =
            AnnotationCodeGenerator(AnnotationCodeGeneratorDependencies(sqlServerTypeMappingSource))

        let generator = TestFSharpSnapshotGenerator(codeHelper, sqlServerTypeMappingSource, annotationCodeGenerator)

        let coreAnnotations =
            typeof<CoreAnnotationNames>.GetFields()
            |> Seq.filter (fun f -> f.FieldType = typeof<string>)
            |> Seq.toList

        coreAnnotations
        |> List.iter (fun field ->
            let annotationName = field.GetValue(null) |> string
            Expect.isTrue
                (CoreAnnotationNames.AllNames.Contains(annotationName))
                $"CoreAnnotations.AllNames doesn't contain {annotationName}")

        let rlNames = (typeof<RelationalAnnotationNames>).GetFields() |> Seq.toList

        let allAnnotations = (coreAnnotations @ rlNames) |> Seq.filter (fun f -> f.Name <> "Prefix")

        allAnnotations
        |> Seq.iter(fun f ->
            let annotationName = f.GetValue(null) |> string

            if not (invalidAnnotations.Contains(annotationName)) then
                let modelBuilder = RelationalTestHelpers.Instance.CreateConventionBuilder()
                let metadataItem = createMetadataItem modelBuilder

                let annotation =
                    if validAnnotations.ContainsKey(annotationName) then
                        fst validAnnotations.[annotationName]
                    else
                        null

                metadataItem.SetAnnotation(annotationName, annotation)

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
                        CoreAnnotationNames.Precision
                        CoreAnnotationNames.Scale
                        CoreAnnotationNames.Unicode
                        CoreAnnotationNames.ProductVersion
                        CoreAnnotationNames.ValueGeneratorFactory
                        CoreAnnotationNames.OwnedTypes
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
                        RelationalAnnotationNames.TableColumnMappings
                        RelationalAnnotationNames.ViewColumnMappings
                        RelationalAnnotationNames.SqlQueryColumnMappings
                        RelationalAnnotationNames.FunctionColumnMappings
                        RelationalAnnotationNames.RelationalOverrides
                        RelationalAnnotationNames.DefaultValueSql
                        RelationalAnnotationNames.ComputedColumnSql
                        RelationalAnnotationNames.DefaultValue
                        RelationalAnnotationNames.Name
                        RelationalAnnotationNames.Sequences
                        RelationalAnnotationNames.CheckConstraints
                        RelationalAnnotationNames.DefaultSchema
                        RelationalAnnotationNames.Filter
                        RelationalAnnotationNames.DbFunctions
                        RelationalAnnotationNames.MaxIdentifierLength
                        RelationalAnnotationNames.IsFixedLength
                        RelationalAnnotationNames.Collation
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
                                + "modelBuilder"
                                + nl
                                + @"    .HasComment(""My Comment"") |> ignore"
                                + nl)
                        )
                        (
                            CoreAnnotationNames.DefiningQuery,
                            (Expression.Lambda(Expression.Constant(null)) :> obj, "")
                        )
                        (
                            RelationalAnnotationNames.ViewName, ("MyView" :> obj,
                                nl
                                + "modelBuilder."
                                + "ToView"
                                + @"(""MyView"") |> ignore"
                                + nl)
                        )
                        (
                            RelationalAnnotationNames.FunctionName,
                            (null, "")
                        )
                        (
                            RelationalAnnotationNames.SqlQuery,
                            (null, "")
                        )
                    ] |> dict

                missingAnnotationCheck
                        (fun b -> (b.Entity<WithAnnotations>().Metadata :> IMutableAnnotatable))
                        notForEntityType
                        forEntityType
                        toTable
                        (fun g m b -> g.generateEntityTypeAnnotations "modelBuilder" (m :> obj :?> _) b |> ignore)
            }

            test "Test new annotations handled for properties" {
                let notForProperty =
                    [
                        CoreAnnotationNames.ProductVersion
                        CoreAnnotationNames.OwnedTypes
                        CoreAnnotationNames.ConstructorBinding
                        CoreAnnotationNames.ServiceOnlyConstructorBinding
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
                        RelationalAnnotationNames.ViewName
                        RelationalAnnotationNames.Schema
                        RelationalAnnotationNames.ViewSchema
                        RelationalAnnotationNames.DefaultSchema
                        RelationalAnnotationNames.DefaultMappings
                        RelationalAnnotationNames.TableMappings
                        RelationalAnnotationNames.ViewMappings
                        RelationalAnnotationNames.SqlQueryMappings
                        RelationalAnnotationNames.Name
                        RelationalAnnotationNames.Sequences
                        RelationalAnnotationNames.CheckConstraints
                        RelationalAnnotationNames.Filter
                        RelationalAnnotationNames.DbFunctions
                        RelationalAnnotationNames.MaxIdentifierLength
                    ] |> HashSet

                let columnMapping =
                    @$"{nl}.HasColumnType(""default_int_mapping"")"

                let forProperty =
                    [
                        ( CoreAnnotationNames.MaxLength, (256 :> obj, $"{nl}.HasMaxLength(256){columnMapping} |> ignore"))
                        ( CoreAnnotationNames.Precision, (4 :> obj, $"{nl}.HasPrecision(4){columnMapping} |> ignore") )
                        ( CoreAnnotationNames.Unicode, (false :> obj, $"{nl}.IsUnicode(false){columnMapping} |> ignore"))
                        (
                            CoreAnnotationNames.ValueConverter, (ValueConverter<int, int64>((fun v -> v |> int64), (fun v -> v |> int), null) :> obj,
                                nl+ @".HasColumnType(""default_long_mapping"") |> ignore")
                        )
                        (
                            CoreAnnotationNames.ProviderClrType,
                            (typeof<int64> :> obj, nl + @".HasColumnType(""default_long_mapping"") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.ColumnName,
                            ("MyColumn" :> obj, columnMapping + nl + @".HasColumnName(""MyColumn"") |> ignore")
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
                            RelationalAnnotationNames.IsFixedLength,
                            (true :> obj, columnMapping + nl + @".IsFixedLength(true) |> ignore")
                        )
                        (
                            RelationalAnnotationNames.Comment,
                            ("My Comment" :> obj, columnMapping + nl + @".HasComment(""My Comment"") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.Collation,
                            ("Some Collation" :> obj, $@"{columnMapping}{nl}.UseCollation(""Some Collation"") |> ignore")
                        )
                    ] |> dict

                missingAnnotationCheck
                    (fun b -> (b.Entity<WithAnnotations>().Property(fun e -> e.Id).Metadata :> IMutableAnnotatable))
                    notForProperty
                    forProperty
                    (columnMapping + " |> ignore")
                    (fun g m b -> g.generatePropertyAnnotations (m :> obj :?> _) b |> ignore)
            }

            test "Snapshot with enum discriminator uses converted values" {

                let sqlServerTypeMappingSource =
                    SqlServerTypeMappingSource(
                        TestServiceFactory.Instance.Create<TypeMappingSourceDependencies>(),
                        TestServiceFactory.Instance.Create<RelationalTypeMappingSourceDependencies>())

                let codeHelper =
                    FSharpHelper(sqlServerTypeMappingSource);

                let sqlServerAnnotationCodeGenerator =
                    SqlServerAnnotationCodeGenerator(
                        AnnotationCodeGeneratorDependencies(sqlServerTypeMappingSource))

                let generator =
                    FSharpMigrationsGenerator(
                        MigrationsCodeGeneratorDependencies(
                            sqlServerTypeMappingSource,
                            sqlServerAnnotationCodeGenerator),
                        FSharpMigrationsGeneratorDependencies(
                            codeHelper,
                            FSharpMigrationOperationGenerator(codeHelper),
                            FSharpSnapshotGenerator(
                                codeHelper, sqlServerTypeMappingSource, sqlServerAnnotationCodeGenerator)));

                let modelBuilder = RelationalTestHelpers.Instance.CreateConventionBuilder()

                modelBuilder.Model.RemoveAnnotation(CoreAnnotationNames.ProductVersion) |> ignore

                modelBuilder.Entity<WithAnnotations>(
                    fun eb ->
                        eb.HasDiscriminator<RawEnum>("EnumDiscriminator")
                            .HasValue(RawEnum.A)
                            .HasValue<Derived>(RawEnum.B) |> ignore
                        eb.Property<RawEnum>("EnumDiscriminator").HasConversion<int>() |> ignore
                        ) |> ignore

                modelBuilder.FinalizeModel() |> ignore

                let modelSnapshotCode =
                    generator.GenerateSnapshot(
                        "MyNamespace",
                        typeof<MyContext>,
                        "MySnapshot",
                        modelBuilder.Model)

                let snapshotModel = (compileModelSnapshot modelSnapshotCode "MyNamespace.MySnapshot").Model

                Expect.equal (snapshotModel.FindEntityType(typeof<WithAnnotations>).GetDiscriminatorValue()) ((int RawEnum.A) :> obj) "Should be equal"
                Expect.equal (snapshotModel.FindEntityType(typeof<Derived>).GetDiscriminatorValue()) ((int RawEnum.B) :> obj) "Should be equal"
            }

        ]
