namespace EntityFrameworkCore.FSharp.Test.Migrations.Design

open System
open System.Collections.Generic

open System.Linq.Expressions
open System.Text
open System.Text.RegularExpressions

open Microsoft.Extensions.DependencyInjection

open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.ChangeTracking
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Migrations
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.SqlServer.Design.Internal
open Microsoft.EntityFrameworkCore.Storage.ValueConversion
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.EntityFrameworkCore.TestUtilities
open Microsoft.EntityFrameworkCore.ValueGeneration

open EntityFrameworkCore.FSharp.Internal
open EntityFrameworkCore.FSharp.Migrations.Design
open EntityFrameworkCore.FSharp.Test.TestUtilities

open Expecto
open Microsoft.EntityFrameworkCore.Design.Internal
open EntityFrameworkCore.FSharp

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

    let _eol = Environment.NewLine

    let join separator (lines : string seq) =
        String.Join(separator, lines)

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
                "System.Text.RegularExpressions.dll"
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

        try
            let snapshotType = assembly.GetType(modelSnapshotTypeName, throwOnError = true, ignoreCase = false)

            let contextTypeAttribute =
                System.Reflection.CustomAttributeExtensions.GetCustomAttribute<DbContextAttribute>(snapshotType)

            Expect.isNotNull contextTypeAttribute "Should not be null"
            Expect.equal contextTypeAttribute.ContextType.FullName typeof<MyContext>.FullName "Should be equal"

            Activator.CreateInstance(snapshotType) :?> ModelSnapshot
        with
            | exn ->
                let msg = sprintf "Could not build the following code {%s}:\n {%s}" modelSnapshotTypeName modelSnapshotCode
                raise (System.Exception(msg, exn))


    let _nl = Environment.NewLine

    let createMigrationsCodeGenerator() =

        let testAssembly = (typeof<BuildReference>).Assembly
        let reporter = TestOperationReporter()

        let services =
            DesignTimeServicesBuilder(testAssembly, testAssembly, reporter, [||])
                .CreateServiceCollection(SqlServerTestHelpers.Instance.CreateContext())

        let designTimeServices = EFCoreFSharpServices.Default

        designTimeServices.ConfigureDesignTimeServices(services)

        services
            .BuildServiceProvider(validateScopes = true)
            .GetRequiredService<IMigrationsCodeGenerator>()

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

                modelBuilder.FinalizeModel(designTime = true) |> ignore

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

                Expect.equal (actual.Trim()) (expected.Trim()) $"Should be equal, but failed on {annotationName}"
            )

        ()

    type EntityWithConstructorBinding(id: int) =
        member this.Id with get() = id

    let myDbFunction(): int = failwith "Not implemented"

    [<Flags>]
    type Enum1 =
        | Default = 0
        | One = 1
        | Two = 2

    [<CLIMutable>]
    type EntityWithEveryPrimitive = {
        Boolean: bool
        Byte: byte
        ByteArray: byte[]
        Char: char
        DateTime: DateTime
        DateTimeOffset: DateTimeOffset
        Decimal: decimal
        Double: double
        Enum: Enum1
        StringEnum: Enum1
        Guid: Guid
        Int16: int16
        Int32: int
        Int64: int64
        NullableBoolean: Nullable<bool>
        NullableByte: Nullable<byte>
        NullableChar: Nullable<char>
        NullableDateTime: Nullable<DateTime>
        NullableDateTimeOffset: Nullable<DateTimeOffset>
        NullableDecimal: Nullable<decimal>
        NullableDouble: Nullable<double>
        NullableEnum: Nullable<Enum1>
        NullableStringEnum: Nullable<Enum1>
        NullableGuid: Nullable<Guid>
        NullableInt16: Nullable<int16>
        NullableInt32: Nullable<int>
        NullableInt64: Nullable<int64>
        NullableSByte: Nullable<sbyte>
        NullableSingle: Nullable<float>
        NullableTimeSpan: Nullable<TimeSpan>
        NullableUInt16: Nullable<uint16>
        NullableUInt32: Nullable<uint>
        NullableUInt64: Nullable<uint64>
        SByte: sbyte
        Single: float
        String: string
        TimeSpan: TimeSpan
        UInt16: uint16
        UInt32: uint
        UInt64: uint64
        mutable privateSetter: int
    } with
        member this.PrivateSetter
            with get() = this.privateSetter
            and private set v = this.privateSetter <- v

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
                        CoreAnnotationNames.ValueGeneratorFactoryType
                        CoreAnnotationNames.ValueConverter
                        CoreAnnotationNames.ValueConverterType
                        CoreAnnotationNames.ValueComparer
                        CoreAnnotationNames.ValueComparerType
                        CoreAnnotationNames.KeyValueComparer
                        CoreAnnotationNames.StructuralValueComparer
                        CoreAnnotationNames.BeforeSaveBehavior
                        CoreAnnotationNames.AfterSaveBehavior
                        CoreAnnotationNames.ProviderClrType
                        CoreAnnotationNames.EagerLoaded
                        CoreAnnotationNames.DuplicateServiceProperties
                        RelationalAnnotationNames.ColumnName
                        RelationalAnnotationNames.ColumnOrder
                        RelationalAnnotationNames.ColumnType
                        RelationalAnnotationNames.TableColumnMappings
                        RelationalAnnotationNames.ViewColumnMappings
                        RelationalAnnotationNames.SqlQueryColumnMappings
                        RelationalAnnotationNames.FunctionColumnMappings
                        RelationalAnnotationNames.DefaultColumnMappings
                        RelationalAnnotationNames.TableMappings
                        RelationalAnnotationNames.ViewMappings
                        RelationalAnnotationNames.FunctionMappings
                        RelationalAnnotationNames.SqlQueryMappings
                        RelationalAnnotationNames.DefaultMappings
                        RelationalAnnotationNames.ForeignKeyMappings
                        RelationalAnnotationNames.TableIndexMappings
                        RelationalAnnotationNames.UniqueConstraintMappings
                        RelationalAnnotationNames.RelationalOverrides
                        RelationalAnnotationNames.DefaultValueSql
                        RelationalAnnotationNames.ComputedColumnSql
                        RelationalAnnotationNames.DefaultValue
                        RelationalAnnotationNames.Name
                        RelationalAnnotationNames.SequencePrefix
                        RelationalAnnotationNames.Sequences
                        RelationalAnnotationNames.CheckConstraints
                        RelationalAnnotationNames.DefaultSchema
                        RelationalAnnotationNames.Filter
                        RelationalAnnotationNames.DbFunction
                        RelationalAnnotationNames.DbFunctions
                        RelationalAnnotationNames.MaxIdentifierLength
                        RelationalAnnotationNames.IsFixedLength
                        RelationalAnnotationNames.Collation
                        RelationalAnnotationNames.IsStored
                        RelationalAnnotationNames.RelationalModel
                        RelationalAnnotationNames.ModelDependencies
                    ] |> HashSet

                let _toTable = _nl + @"entityTypeBuilder.ToTable(""WithAnnotations"") |> ignore" + _nl

                let forEntityType =
                    [
                        (
                            RelationalAnnotationNames.TableName,
                            (box "MyTable", _nl + @"entityTypeBuilder.ToTable(""MyTable"") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.Schema, (box "MySchema",
                                _nl + @"entityTypeBuilder.ToTable(""WithAnnotations"", ""MySchema"") |> ignore")
                        )
                        (
                            CoreAnnotationNames.DiscriminatorProperty, (box "Id",
                                _toTable
                                + @"entityTypeBuilder.HasDiscriminator<int>(""Id"") |> ignore")
                        )
                        (
                            CoreAnnotationNames.DiscriminatorValue, (box "MyDiscriminatorValue",
                                _toTable
                                + @"entityTypeBuilder.HasDiscriminator().HasValue(""MyDiscriminatorValue"") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.Comment, (box "My Comment",
                                _toTable
                                + @"entityTypeBuilder.HasComment(""My Comment"") |> ignore")
                        )
                        (
                            CoreAnnotationNames.DefiningQuery,
                            (box (Expression.Lambda(Expression.Constant(null))), "")
                        )
                        (
                            RelationalAnnotationNames.ViewName,
                            (box "MyView", _nl + @"entityTypeBuilder.ToView(""MyView"") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.FunctionName,
                            (box null, _nl + "entityTypeBuilder.ToFunction(null) |> ignore")
                        )
                        (
                            RelationalAnnotationNames.SqlQuery,
                            (box null, _nl + "entityTypeBuilder.ToSqlQuery(null) |> ignore")
                        )
                    ] |> dict

                missingAnnotationCheck
                        (fun b -> (b.Entity<WithAnnotations>().Metadata :> IMutableAnnotatable))
                        notForEntityType
                        forEntityType
                        _toTable
                        (fun g m b -> g.generateEntityTypeAnnotations "entityTypeBuilder" (m :> obj :?> _) b |> ignore)
            }

            test "Test new annotations handled for properties" {
                let notForProperty =
                    [
                        CoreAnnotationNames.ProductVersion
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
                        RelationalAnnotationNames.IsTableExcludedFromMigrations
                        RelationalAnnotationNames.ViewName
                        RelationalAnnotationNames.Schema
                        RelationalAnnotationNames.ViewSchema
                        RelationalAnnotationNames.ViewDefinitionSql
                        RelationalAnnotationNames.FunctionName
                        RelationalAnnotationNames.SqlQuery
                        RelationalAnnotationNames.DefaultSchema
                        RelationalAnnotationNames.DefaultMappings
                        RelationalAnnotationNames.TableColumnMappings
                        RelationalAnnotationNames.ViewColumnMappings
                        RelationalAnnotationNames.SqlQueryColumnMappings
                        RelationalAnnotationNames.FunctionColumnMappings
                        RelationalAnnotationNames.DefaultColumnMappings
                        RelationalAnnotationNames.TableMappings
                        RelationalAnnotationNames.ViewMappings
                        RelationalAnnotationNames.FunctionMappings
                        RelationalAnnotationNames.SqlQueryMappings
                        RelationalAnnotationNames.ForeignKeyMappings
                        RelationalAnnotationNames.TableIndexMappings
                        RelationalAnnotationNames.UniqueConstraintMappings
                        RelationalAnnotationNames.Name
                        RelationalAnnotationNames.Sequences
                        RelationalAnnotationNames.SequencePrefix
                        RelationalAnnotationNames.CheckConstraints
                        RelationalAnnotationNames.Filter
                        RelationalAnnotationNames.DbFunction
                        RelationalAnnotationNames.DbFunctions
                        RelationalAnnotationNames.MaxIdentifierLength
                        RelationalAnnotationNames.RelationalModel
                        RelationalAnnotationNames.ModelDependencies
                    ] |> HashSet

                let columnMapping =
                    @$"{_nl}.HasColumnType(""default_int_mapping""){_nl}"

                let columnMappingWithDefaultValue =
                    $"{columnMapping}.HasDefaultValue(0)"

                let forProperty =
                    [
                        ( CoreAnnotationNames.MaxLength, (box 256, $"{_nl}.HasMaxLength(256){columnMappingWithDefaultValue} |> ignore"))
                        ( CoreAnnotationNames.Precision, (box 4, $"{_nl}.HasPrecision(4){columnMappingWithDefaultValue} |> ignore") )
                        ( CoreAnnotationNames.Scale, (box null, $"{columnMappingWithDefaultValue} |> ignore") )
                        ( CoreAnnotationNames.Unicode, (box false, $"{_nl}.IsUnicode(false){columnMappingWithDefaultValue} |> ignore"))
                        (
                            CoreAnnotationNames.ValueConverter, (box (ValueConverter<int, int64>((fun v -> v |> int64), (fun v -> v |> int), null)),
                                _nl+ $".HasColumnType(\"default_long_mapping\"){_nl}.HasDefaultValue(0L) |> ignore")
                        )
                        (
                            CoreAnnotationNames.ProviderClrType,
                            (box typeof<int64>, $"{_nl}.HasColumnType(\"default_long_mapping\"){_nl}.HasDefaultValue(0L) |> ignore")
                        )
                        (
                            RelationalAnnotationNames.ColumnName,
                            (box "MyColumn", columnMappingWithDefaultValue + _nl + ".HasColumnName(\"MyColumn\") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.ColumnType,
                            (box "int", _nl + $".HasColumnType(\"int\"){_nl}.HasDefaultValue(0) |> ignore")
                        )
                        (
                            RelationalAnnotationNames.DefaultValueSql,
                            (box "some SQL", columnMappingWithDefaultValue + _nl + ".HasDefaultValueSql(\"some SQL\") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.ComputedColumnSql,
                            (box "some SQL", columnMappingWithDefaultValue + _nl + ".HasComputedColumnSql(\"some SQL\") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.DefaultValue,
                            (box "1", columnMapping + ".HasDefaultValue(\"1\") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.DefaultValue,
                            (box 0, columnMapping + ".HasDefaultValue(0) |> ignore")
                        )
                        (
                            RelationalAnnotationNames.IsFixedLength,
                            (box true, columnMappingWithDefaultValue + _nl + ".IsFixedLength() |> ignore")
                        )
                        (
                            RelationalAnnotationNames.Comment,
                            (box "My Comment", columnMappingWithDefaultValue + _nl + ".HasComment(\"My Comment\") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.Collation,
                            (box "Some Collation", $"{columnMappingWithDefaultValue}{_nl}.UseCollation(\"Some Collation\") |> ignore")
                        )
                        (
                            RelationalAnnotationNames.IsStored,
                            (box null, $"{columnMappingWithDefaultValue}{_nl}.HasAnnotation(\"{RelationalAnnotationNames.IsStored}\", null) |> ignore")
                        )
                    ] |> dict

                missingAnnotationCheck
                    (fun b -> (b.Entity<WithAnnotations>().Property(fun e -> e.Id).Metadata :> IMutableAnnotatable))
                    notForProperty
                    forProperty
                    (columnMappingWithDefaultValue + " |> ignore")
                    (fun g m b -> g.generatePropertyAnnotations "propertyBuilder" (m :> obj :?> _) b |> ignore)
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

                let model = modelBuilder.FinalizeModel(designTime = true)

                let modelSnapshotCode =
                    generator.GenerateSnapshot(
                        "MyNamespace",
                        typeof<MyContext>,
                        "MySnapshot",
                        model)

                let snapshotModel = (compileModelSnapshot modelSnapshotCode "MyNamespace.MySnapshot").Model

                Expect.equal (snapshotModel.FindEntityType(typeof<WithAnnotations>).GetDiscriminatorValue()) ((int RawEnum.A) :> obj) "Should be equal"
                Expect.equal (snapshotModel.FindEntityType(typeof<Derived>).GetDiscriminatorValue()) ((int RawEnum.B) :> obj) "Should be equal"
            }

            test "Migrations compile" {
                let generator = createMigrationsCodeGenerator()

                let sqlOperation = SqlOperation(Sql = "-- TEST")
                sqlOperation.["Some:EnumValue"] <- RegexOptions.Multiline

                let migrationCode =
                    generator.GenerateMigration(
                        "MyNamespace",
                        "MyMigration",
                        [
                            sqlOperation
                            AlterColumnOperation (
                                Name = "C1",
                                Table = "C2",
                                ClrType = typeof<Database>,
                                OldColumn = AddColumnOperation ( ClrType = typeof<Property> )
                            )
                            AddColumnOperation (
                                Name = "C3",
                                Table = "T1",
                                ClrType = typeof<PropertyEntry>
                            )

                            let insertValues: obj[,]  = Array2D.create 2 3 (1 :> obj)
                            insertValues.[0,1] <- null

                            InsertDataOperation (
                                Table = "T1",
                                Columns = [| "Id"; "C2"; "C3" |],
                                Values = insertValues
                            )
                        ],
                        [])

                Expect.equal migrationCode "// intentionally empty" "No support for partial class. Code is built in GenerateMetadata"

                let modelBuilder = SqlServerTestHelpers.Instance.CreateConventionBuilder(configure = (fun c -> c.RemoveAllConventions()))

                modelBuilder.HasAnnotation("Some:EnumValue", RegexOptions.Multiline) |> ignore
                modelBuilder.HasAnnotation(RelationalAnnotationNames.DbFunctions, SortedDictionary<string, IDbFunction>()) |> ignore
                modelBuilder.Entity("T1", fun eb ->
                                        eb.Property<int>("Id") |> ignore
                                        eb.Property<string>("C2").IsRequired() |> ignore
                                        eb.Property<int>("C3") |> ignore
                                        eb.HasKey("Id")  |> ignore) |> ignore
                modelBuilder.HasAnnotation(CoreAnnotationNames.ProductVersion, null) |> ignore
                let model = modelBuilder.FinalizeModel(designTime = true)


                let migrationMetadataCode =
                    generator.GenerateMetadata(
                        "MyNamespace",
                        typeof<MyContext>,
                        "MyMigration",
                        "20150511161616_MyMigration",
                        model)

                let expectedCode =
                    seq {
                        "// <auto-generated />"
                        "namespace MyNamespace"
                        ""
                        "open System"
                        "open System.Text.RegularExpressions"
                        "open EntityFrameworkCore.FSharp.Test.Migrations.Design"
                        "open Microsoft.EntityFrameworkCore"
                        "open Microsoft.EntityFrameworkCore.ChangeTracking"
                        "open Microsoft.EntityFrameworkCore.Infrastructure"
                        "open Microsoft.EntityFrameworkCore.Metadata"
                        "open Microsoft.EntityFrameworkCore.Metadata.Internal"
                        "open Microsoft.EntityFrameworkCore.Migrations"
                        "open Microsoft.EntityFrameworkCore.Storage"
                        "open Microsoft.EntityFrameworkCore.Storage.ValueConversion"
                        ""
                        "[<DbContext(typeof<MyContext>)>]"
                        "[<Migration(\"20150511161616_MyMigration\")>]"
                        "type MyMigration() ="
                        "    inherit Migration()"
                        ""
                        "    override this.Up(migrationBuilder:MigrationBuilder) ="
                        "        migrationBuilder.Sql(\"-- TEST\").Annotation(\"Some:EnumValue\", RegexOptions.Multiline) |> ignore"
                        ""
                        "        migrationBuilder.AlterColumn<Database>("
                        "            name = \"C1\""
                        "            ,table = \"C2\""
                        "            ,nullable = false"
                        "            ,oldClrType = typedefof<Property>"
                        "            ,oldNullable = false"
                        "            ) |> ignore"
                        ""
                        "        migrationBuilder.AddColumn<PropertyEntry>("
                        "            name = \"C3\""
                        "            ,table = \"T1\""
                        "            ,nullable = false"
                        "            ) |> ignore"
                        ""
                        "        migrationBuilder.InsertData("
                        "            table = \"T1\","
                        "            columns = [| \"Id\"; \"C2\"; \"C3\" |],"
                        "            values = array2D [ [ 1 :> obj; null; 1 :> obj ]; [ 1 :> obj; 1 :> obj; 1 :> obj ] ]"
                        "        ) |> ignore"
                        ""
                        ""
                        "    override this.Down(migrationBuilder:MigrationBuilder) ="
                        "        ()"
                        ""
                        "    override this.BuildTargetModel(modelBuilder: ModelBuilder) ="
                        "        modelBuilder.HasAnnotation(\"Some:EnumValue\", RegexOptions.Multiline) |> ignore"
                        ""
                        "        modelBuilder.Entity(\"T1\", (fun b ->"
                        ""
                        "            b.Property<int>(\"Id\")"
                        "                .IsRequired(true)"
                        "                .HasColumnType(\"int\")"
                        "                .HasDefaultValue(0) |> ignore"
                        ""
                        "            b.Property<string>(\"C2\")"
                        "                .IsRequired(true)"
                        "                .HasColumnType(\"nvarchar(max)\") |> ignore"
                        ""
                        "            b.Property<int>(\"C3\")"
                        "                .IsRequired(true)"
                        "                .HasColumnType(\"int\")"
                        "                .HasDefaultValue(0) |> ignore"
                        ""
                        "            b.HasKey(\"Id\") |> ignore"
                        ""
                        "            b.ToTable(\"T1\") |> ignore"
                        "        )) |> ignore"
                    } |> join _eol

                Expect.equal (migrationMetadataCode.Trim()) (expectedCode.Trim()) ""

                let build = { Sources = [ migrationMetadataCode ]; TargetDir = null }

                let references =
                    getRequiredReferences()
                    |> Array.append ([|
                        "System.Text.RegularExpressions"
                        "Microsoft.EntityFrameworkCore"
                        "Microsoft.EntityFrameworkCore.Relational"
                    |])

                let assembly = build.BuildInMemory(references)

                let migrationType = assembly.GetType("MyNamespace.MyMigration", throwOnError = true, ignoreCase = false)
                let attribute =
                    migrationType.GetCustomAttributes(false)
                    |> Seq.choose (fun t ->
                        match t with
                        | :? DbContextAttribute as a -> Some a
                        | _ -> None)
                    |> Seq.head

                let migration = Activator.CreateInstance(migrationType) :?> Migration

                Expect.equal attribute.ContextType.FullName (typeof<MyContext>).FullName $"Expected context type {nameof MyContext}"
                Expect.equal migration.UpOperations.Count 4 "Expected 4 up operations"
                Expect.equal (migration.DownOperations.Count) 0 "Expected 0 down operations"
                Expect.hasCountOf (migration.TargetModel.GetEntityTypes()) 1u (fun _ -> true) "Expected one entity"
            }

            test "Snapshots compile" {
                let generator = createMigrationsCodeGenerator()
                let modelBuilder = RelationalTestHelpers.Instance.CreateConventionBuilder()
                modelBuilder.Model.RemoveAnnotation(CoreAnnotationNames.ProductVersion) |> ignore
                modelBuilder.Entity<EntityWithConstructorBinding>(fun x ->
                    x.Property(fun e -> e.Id) |> ignore
                    x.Property<Guid>("PropertyWithValueGenerator").HasValueGenerator<GuidValueGenerator>() |> ignore
                ) |> ignore

                modelBuilder.HasDbFunction(myDbFunction) |> ignore

                let model = modelBuilder.Model
                model.["Some:EnumValue"] <- RegexOptions.Multiline

                let entityType = model.AddEntityType("Cheese")
                let property1 = entityType.AddProperty("Pickle", typeof<StringBuilder>)

                property1.SetValueConverter(
                    ValueConverter<StringBuilder, string>(
                        (fun v -> v.ToString()),
                        (fun v -> StringBuilder(v)),
                        (ConverterMappingHints(size = 10))))

                let property2 = entityType.AddProperty("Ham", typeof<RawEnum>)
                property2.SetValueConverter(
                    ValueConverter<RawEnum, string>(
                        (fun v -> v.ToString()),
                        (fun v -> Enum.Parse(typeof<RawEnum>, v) :?> _),
                        (ConverterMappingHints(size = 10))))

                entityType.SetPrimaryKey(property2) |> ignore

                let finalModel = modelBuilder.FinalizeModel(designTime = true)

                let modelSnapshotCode =
                    generator.GenerateSnapshot(
                        "MyNamespace",
                        typeof<MyContext>,
                        "MySnapshot",
                        finalModel)

                let expectedCode =
                    seq {
                        "// <auto-generated />"
                        "namespace MyNamespace"
                        ""
                        "open System"
                        "open System.Text.RegularExpressions"
                        "open EntityFrameworkCore.FSharp.Test.Migrations.Design"
                        "open Microsoft.EntityFrameworkCore"
                        "open Microsoft.EntityFrameworkCore.Infrastructure"
                        "open Microsoft.EntityFrameworkCore.Metadata"
                        "open Microsoft.EntityFrameworkCore.Migrations"
                        "open Microsoft.EntityFrameworkCore.Storage.ValueConversion"
                        ""
                        "[<DbContext(typeof<MyContext>)>]"
                        "type MySnapshot() ="
                        "    inherit ModelSnapshot()"
                        ""
                        "    override this.BuildModel(modelBuilder: ModelBuilder) ="
                        "        modelBuilder.HasAnnotation(\"Some:EnumValue\", RegexOptions.Multiline) |> ignore"
                        ""
                        "        modelBuilder.Entity(\"Cheese\", (fun b ->"
                        ""
                        "            b.Property<string>(\"Ham\")"
                        "                .IsRequired(true)"
                        "                .HasColumnType(\"just_string(10)\")"
                        "                .HasDefaultValue(\"A\") |> ignore"
                        ""
                        "            b.Property<string>(\"Pickle\")"
                        "                .IsRequired(false)"
                        "                .HasColumnType(\"just_string(10)\") |> ignore"
                        ""
                        "            b.HasKey(\"Ham\") |> ignore"
                        ""
                        "            b.ToTable(\"Cheese\") |> ignore"
                        "        )) |> ignore"
                        ""
                        "        modelBuilder.Entity(\"EntityFrameworkCore.FSharp.Test.Migrations.Design.FSharpMigrationsGeneratorTest+EntityWithConstructorBinding\", (fun b ->"
                        ""
                        "            b.Property<int>(\"Id\")"
                        "                .IsRequired(true)"
                        "                .ValueGeneratedOnAdd()"
                        "                .HasColumnType(\"default_int_mapping\")"
                        "                .HasDefaultValue(0) |> ignore"
                        ""
                        "            b.Property<Guid>(\"PropertyWithValueGenerator\")"
                        "                .IsRequired(true)"
                        "                .HasColumnType(\"default_guid_mapping\")"
                        "                .HasDefaultValue(Guid(\"00000000-0000-0000-0000-000000000000\")) |> ignore"
                        ""
                        "            b.HasKey(\"Id\") |> ignore"
                        ""
                        "            b.ToTable(\"EntityWithConstructorBinding\") |> ignore"
                        "        )) |> ignore"
                    } |> join _eol

                Expect.equal (modelSnapshotCode.Trim()) (expectedCode.Trim()) ""

                let snapshot = compileModelSnapshot modelSnapshotCode "MyNamespace.MySnapshot"
                Expect.equal (snapshot.Model.GetEntityTypes() |> Seq.length) 2 "Expect 2 entity types"
            }

            test "Snapshot with default values are round tripped" {
                let generator = createMigrationsCodeGenerator()
                let modelBuilder = RelationalTestHelpers.Instance.CreateConventionBuilder();
                modelBuilder.Entity<EntityWithEveryPrimitive>(fun eb ->
                    eb.Property(fun e -> e.Boolean).HasDefaultValue(false) |> ignore
                    eb.Property(fun e -> e.Byte).HasDefaultValue(Byte.MinValue) |> ignore
                    eb.Property(fun e -> e.ByteArray).HasDefaultValue([| 0uy |]) |> ignore
                    eb.Property(fun e -> e.Char).HasDefaultValue('0') |> ignore
                    eb.Property(fun e -> e.DateTime).HasDefaultValue(DateTime.MinValue) |> ignore
                    eb.Property(fun e -> e.DateTimeOffset).HasDefaultValue(DateTimeOffset.MinValue) |> ignore
                    eb.Property(fun e -> e.Decimal).HasDefaultValue(Decimal.MinValue) |> ignore
                    eb.Property(fun e -> e.Double).HasDefaultValue(Double.MinValue) |> ignore //double.NegativeInfinity
                    eb.Property(fun e -> e.Enum).HasDefaultValue(Enum1.Default) |> ignore
                    eb.Property(fun e -> e.NullableEnum).HasDefaultValue(Enum1.Default).HasConversion<string>() |> ignore
                    eb.Property(fun e -> e.Guid).HasDefaultValue(Guid.NewGuid()) |> ignore
                    eb.Property(fun e -> e.Int16).HasDefaultValue(Int16.MaxValue) |> ignore
                    eb.Property(fun e -> e.Int32).HasDefaultValue(Int32.MaxValue) |> ignore
                    eb.Property(fun e -> e.Int64).HasDefaultValue(Int64.MaxValue) |> ignore
                    eb.Property(fun e -> e.Single).HasDefaultValue(Single.Epsilon) |> ignore
                    eb.Property(fun e -> e.SByte).HasDefaultValue(SByte.MinValue) |> ignore
                    eb.Property(fun e -> e.String).HasDefaultValue("'\"'@\r\\\n") |> ignore
                    eb.Property(fun e -> e.TimeSpan).HasDefaultValue(TimeSpan.MaxValue) |> ignore
                    eb.Property(fun e -> e.UInt16).HasDefaultValue(UInt16.MinValue) |> ignore
                    eb.Property(fun e -> e.UInt32).HasDefaultValue(UInt32.MinValue) |> ignore
                    eb.Property(fun e -> e.UInt64).HasDefaultValue(UInt64.MinValue) |> ignore
                    eb.Property(fun e -> e.NullableBoolean).HasDefaultValue(true) |> ignore
                    eb.Property(fun e -> e.NullableByte).HasDefaultValue(Byte.MaxValue) |> ignore
                    eb.Property(fun e -> e.NullableChar).HasDefaultValue('\'') |> ignore
                    eb.Property(fun e -> e.NullableDateTime).HasDefaultValue(DateTime.MaxValue) |> ignore
                    eb.Property(fun e -> e.NullableDateTimeOffset).HasDefaultValue(DateTimeOffset.MaxValue) |> ignore
                    eb.Property(fun e -> e.NullableDecimal).HasDefaultValue(Decimal.MaxValue) |> ignore
                    eb.Property(fun e -> e.NullableDouble).HasDefaultValue(0.6822871999174) |> ignore
                    eb.Property(fun e -> e.NullableEnum).HasDefaultValue(Enum1.One ||| Enum1.Two) |> ignore
                    eb.Property(fun e -> e.NullableStringEnum).HasDefaultValue(Enum1.One).HasConversion<string>() |> ignore
                    eb.Property(fun e -> e.NullableGuid).HasDefaultValue(Guid()) |> ignore
                    eb.Property(fun e -> e.NullableInt16).HasDefaultValue(Int16.MinValue) |> ignore
                    eb.Property(fun e -> e.NullableInt32).HasDefaultValue(Int32.MinValue) |> ignore
                    eb.Property(fun e -> e.NullableInt64).HasDefaultValue(Int64.MinValue) |> ignore
                    eb.Property(fun e -> e.NullableSingle).HasDefaultValue(0.3333333) |> ignore
                    eb.Property(fun e -> e.NullableSByte).HasDefaultValue(SByte.MinValue) |> ignore
                    eb.Property(fun e -> e.NullableTimeSpan).HasDefaultValue(TimeSpan.MinValue.Add(TimeSpan())) |> ignore
                    eb.Property(fun e -> e.NullableUInt16).HasDefaultValue(UInt16.MaxValue) |> ignore
                    eb.Property(fun e -> e.NullableUInt32).HasDefaultValue(UInt32.MaxValue) |> ignore
                    eb.Property(fun e -> e.NullableUInt64).HasDefaultValue(UInt64.MaxValue) |> ignore

                    eb.HasKey(fun e -> e.Boolean :> obj) |> ignore
                    ) |> ignore

                let model = modelBuilder.FinalizeModel(designTime = true)

                let modelSnapshotCode =
                    generator.GenerateSnapshot(
                        "MyNamespace",
                        typeof<MyContext>,
                        "MySnapshot",
                        model)

                let snapshot = compileModelSnapshot modelSnapshotCode "MyNamespace.MySnapshot"
                let entityType = snapshot.Model.GetEntityTypes() |> Seq.head

                Expect.equal (entityType.DisplayName()) (typeof<EntityWithEveryPrimitive>.FullName + " (Dictionary<string, object>)") ""

                (modelBuilder.Model.GetEntityTypes() |> Seq.head).GetProperties()
                |> Seq.iter (fun property ->
                    let expected = property.GetDefaultValue()
                    let defaultValue = entityType.FindProperty(property.Name).GetDefaultValue()

                    let actual =
                        match expected |> Option.ofObj, defaultValue |> Option.ofObj with
                        | Some expected, Some actual' when expected.GetType().IsEnum ->
                                match actual' with
                                | :? String as a -> Enum.Parse(expected.GetType(), a)
                                | _ -> Enum.ToObject(expected.GetType(), actual')
                        | Some expected, Some actual' when actual'.GetType() <> expected.GetType() ->
                            Convert.ChangeType(actual', expected.GetType())
                        | _ -> defaultValue

                    if actual |> isNull |> not && expected |> isNull |> not then
                        Expect.equal
                            actual
                            expected
                            $"""Comparison failed for {if actual.GetType() = typeof<Nullable<_>> then $"Nullable<{(Nullable.GetUnderlyingType(actual.GetType()))}>" else property.ClrType.Name}"""
                )
            }

            test "Namespaces imported for insert data" {
                let generator = createMigrationsCodeGenerator()

                let _ =
                    generator.GenerateMigration(
                        "MyNamespace",
                        "MyMigration",
                        [
                            let values = Array2D.create 2 2 (1 :> obj)
                            values.[0, 1] <- null
                            values.[1, 0] <- 2 :> _
                            values.[1, 0] <- RegexOptions.Multiline :> _
                            InsertDataOperation (
                                Table = "MyTable",
                                Columns = [| "Id"; "MyColumn" |],
                                Values = values
                            )
                        ],
                        []
                    )

                let modelBuilder = RelationalTestHelpers.Instance.CreateConventionBuilder()
                let model = modelBuilder.Model.FinalizeModel()

                let migration =
                    generator.GenerateMetadata(
                        "MyNamespace",
                        typeof<MyContext>,
                        "MyMigration",
                        "20150511161616_MyMigration",
                        model
                    )

                Expect.stringContains migration "open System.Text.RegularExpressions" ""
            }

            test "Namespaces imported for update data values" {
                let generator = createMigrationsCodeGenerator()

                let _ =
                    generator.GenerateMigration(
                        "MyNamespace",
                        "MyMigration",
                        [
                            UpdateDataOperation(
                                Table = "MyTable",
                                KeyColumns = [| "Id" |],
                                KeyValues = (Array2D.create 1 1 (1 :> _)),
                                Columns = [| "MyColumn" |],
                                Values = Array2D.create 1 1 (RegexOptions.Multiline :> _)
                            )
                        ],
                        []
                    )

                let modelBuilder = RelationalTestHelpers.Instance.CreateConventionBuilder()
                let model = modelBuilder.Model.FinalizeModel()

                let migration =
                    generator.GenerateMetadata(
                        "MyNamespace",
                        typeof<MyContext>,
                        "MyMigration",
                        "20150511161616_MyMigration",
                        model
                    )

                Expect.stringContains migration "open System.Text.RegularExpressions" ""
            }

            test "Namespaces imported for update data KeyValues" {
                let generator = createMigrationsCodeGenerator()

                let _ =
                    generator.GenerateMigration(
                        "MyNamespace",
                        "MyMigration",
                        [
                            UpdateDataOperation(
                                Table = "MyTable",
                                KeyColumns = [| "Id" |],
                                KeyValues = (Array2D.create 1 1 (RegexOptions.Multiline :> _)),
                                Columns = [| "MyColumn" |],
                                Values = Array2D.create 1 1 (1 :> _)
                            )
                        ],
                        []
                    )

                let modelBuilder = RelationalTestHelpers.Instance.CreateConventionBuilder()
                let model = modelBuilder.Model.FinalizeModel()

                let migration =
                    generator.GenerateMetadata(
                        "MyNamespace",
                        typeof<MyContext>,
                        "MyMigration",
                        "20150511161616_MyMigration",
                        model
                    )

                Expect.stringContains migration "open System.Text.RegularExpressions" ""
            }

            test "Namespaces imported for delete data" {
                let generator = createMigrationsCodeGenerator()

                let _ =
                    generator.GenerateMigration(
                        "MyNamespace",
                        "MyMigration",
                        [
                            DeleteDataOperation(
                                Table = "MyTable",
                                KeyColumns = [| "Id" |],
                                KeyValues = (Array2D.create 1 1 (RegexOptions.Multiline :> _))
                            )
                        ],
                        []
                    )

                let modelBuilder = RelationalTestHelpers.Instance.CreateConventionBuilder()
                let model = modelBuilder.Model.FinalizeModel()

                let migration =
                    generator.GenerateMetadata(
                        "MyNamespace",
                        typeof<MyContext>,
                        "MyMigration",
                        "20150511161616_MyMigration",
                        model
                    )

                Expect.stringContains migration "open System.Text.RegularExpressions" ""
            }
        ]
