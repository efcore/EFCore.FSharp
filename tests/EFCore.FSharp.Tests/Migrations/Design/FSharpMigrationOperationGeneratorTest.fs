namespace EntityFrameworkCore.FSharp.Test.Migrations.Design

open System
open System.Data.Common
open System.Data.SqlTypes
open System.Linq
open System.Linq.Expressions
open System.Numerics
open System.Reflection
open Microsoft.EntityFrameworkCore.Design.Internal
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.EntityFrameworkCore.Storage.ValueConversion
open Microsoft.EntityFrameworkCore.TestUtilities
open Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal
open NetTopologySuite
open NetTopologySuite.Geometries
open NetTopologySuite.IO
open Expecto

open EntityFrameworkCore.FSharp.Internal
open EntityFrameworkCore.FSharp.Migrations.Design
open EntityFrameworkCore.FSharp.Test.TestUtilities
open Microsoft.EntityFrameworkCore.Migrations
open System.Text
open System.Data
open Microsoft.Data.SqlClient

type GeometryValueCoverter<'geometry when 'geometry :> Geometry>
    (
        reader: SqlServerBytesReader,
        writer: SqlServerBytesWriter
    ) =
    inherit ValueConverter<'geometry, SqlBytes>
        (
            (fun g -> SqlBytes(writer.Write g)),
            (fun b -> reader.Read(b.Value) :?> 'geometry)
        )

module Helpers =
    let createConverter (geometryServices: NtsGeometryServices) (storeType: string) =
        let isGeography =
            String.Equals(storeType, "geography", StringComparison.OrdinalIgnoreCase)

        let reader =
            SqlServerBytesReader(geometryServices, IsGeography = isGeography)

        let writer =
            SqlServerBytesWriter(IsGeography = isGeography)

        GeometryValueCoverter<'geometry>(reader, writer)

type SqlServerGeometryTypeMapping<'geometry when 'geometry :> Geometry>
    (
        geometryServices: NtsGeometryServices,
        storeType: string
    ) =
    inherit RelationalGeometryTypeMapping<'geometry, SqlBytes>
        (
            (Helpers.createConverter geometryServices storeType),
            storeType
        )

    let isGeography =
        String.Equals(storeType, "geography", StringComparison.OrdinalIgnoreCase)

    let getSqlBytes =
        typeof<SqlDataReader>.GetRuntimeMethod ("GetSqlBytes", [| typeof<int> |])

    let createSqlDbTypeAccessor paramType =
        let paramParam =
            Expression.Parameter(typeof<DbParameter>, "parameter")

        let valueParam =
            Expression.Parameter(typeof<SqlDbType>, "value")

        Expression
            .Lambda<Action<DbParameter, SqlDbType>>(
                Expression.Call(
                    Expression.Convert(paramParam, paramType),
                    paramType.GetProperty("SqlDbType").SetMethod,
                    valueParam
                ),
                paramParam,
                valueParam
            )
            .Compile()

    let createUdtTypeNameAccessor (paramType) =

        let paramParam =
            Expression.Parameter(typeof<DbParameter>, "parameter")

        let valueParam =
            Expression.Parameter(typeof<string>, "value")

        Expression
            .Lambda<Action<DbParameter, string>>(
                Expression.Call(
                    Expression.Convert(paramParam, paramType),
                    paramType.GetProperty("UdtTypeName").SetMethod,
                    valueParam
                ),
                paramParam,
                valueParam
            )
            .Compile()

    override __.Clone(parameters: RelationalTypeMapping.RelationalTypeMappingParameters) =
        SqlServerGeometryTypeMapping<'geometry>(geometryServices, storeType) :> RelationalTypeMapping

    override __.GenerateNonNullSqlLiteral(value) =
        let builder = new StringBuilder()
        let geometry = value :?> Geometry

        let defaultSrid =
            (geometry = (Point.Empty :> Geometry))
            || (geometry.SRID = (if isGeography then 4326 else 0))

        let g =
            if isGeography then
                "geography"
            else
                "geometry"

        let m =
            if defaultSrid then
                "Parse"
            else
                "STGeomFromText"

        let a =
            (WKTWriter.ForMicrosoftSqlServer())
                .Write(geometry)

        builder
            .Append(g)
            .Append("::")
            .Append(m)
            .Append("('")
            .Append(a)
            .Append("'")
        |> ignore

        if (not defaultSrid) then
            builder.Append(", ").Append(geometry.SRID)
            |> ignore

        builder.Append(")") |> ignore

        builder.ToString()

    override __.GetDataReaderMethod() = getSqlBytes

    override __.AsText value = (value :?> Geometry).AsText()

    override __.GetSrid value = (value :?> Geometry).SRID

    override __.WKTReaderType = typeof<NetTopologySuite.IO.WKTReader>

    override __.ConfigureParameter parameter =
        let t = parameter.GetType()

        if parameter.Value = (DBNull.Value :> obj) then
            parameter.Value <- SqlBytes.Null

        let sqlDbTypeSetter = createSqlDbTypeAccessor t
        let udtTypeNameSetter = createUdtTypeNameAccessor t

        sqlDbTypeSetter.Invoke(parameter, SqlDbType.Udt)

        udtTypeNameSetter.Invoke(
            parameter,
            (if isGeography then
                 "geography"
             else
                 "geometry")
        )

type SqlServerNetTopologySuiteTypeMappingSourcePlugin(geometryServices) =

    let spatialStoresTypes = Set.ofList [ "geometry"; "geography" ]

    let notNull a = not (isNull a)

    interface IRelationalTypeMappingSourcePlugin with
        member __.FindMapping(mappingInfo) =
            let clrType = mappingInfo.ClrType
            let storeTypeName = mappingInfo.StoreTypeName

            if
                typeof<Geometry>.IsAssignableFrom (clrType)
                || (notNull storeTypeName
                    && spatialStoresTypes.Contains(storeTypeName))
            then
                let genericType =
                    if notNull clrType then
                        clrType
                    else
                        typeof<Geometry>

                let storeName =
                    if notNull storeTypeName then
                        storeTypeName
                    else
                        "geography"

                let instance =
                    Activator.CreateInstance(
                        typedefof<SqlServerGeometryTypeMapping<_>>.MakeGenericType (genericType),
                        geometryServices,
                        storeName
                    )

                (instance :?> RelationalTypeMapping)
            else
                null


module FSharpMigrationOperationGeneratorTest =

    let _eol = Environment.NewLine

    let join separator (lines: string seq) = String.Join(separator, lines)

    let createGenerator () =

        let typeMappingSourceDependencies =
            TestServiceFactory.Instance.Create<TypeMappingSourceDependencies>()

        let relationalTypeMappingSourceDependencies =
            TestServiceFactory.Instance.Create<RelationalTypeMappingSourceDependencies>()

        FSharpMigrationOperationGenerator(
            FSharpHelper(
                SqlServerTypeMappingSource(typeMappingSourceDependencies, relationalTypeMappingSourceDependencies)
            )
        )
        :> ICSharpMigrationOperationGenerator

    let Test<'a when 'a :> MigrationOperation> (operation: 'a) (expectedCode: string) (``assert``: ('a -> unit)) =

        let generator =
            FSharpMigrationOperationGenerator(
                FSharpHelper(
                    SqlServerTypeMappingSource(
                        TestServiceFactory.Instance.Create<TypeMappingSourceDependencies>(),
                        RelationalTypeMappingSourceDependencies(
                            [| SqlServerNetTopologySuiteTypeMappingSourcePlugin(NtsGeometryServices.Instance) |]
                        )
                    )
                )
            )

        let builder = IndentedStringBuilder()

        builder.AppendLine "open System" |> ignore

        builder.AppendLine "open Microsoft.EntityFrameworkCore.Migrations"
        |> ignore

        builder.AppendLine "open NetTopologySuite.Geometries"
        |> ignore

        builder.AppendLine "" |> ignore

        builder.AppendLine "module OperationsFactory ="
        |> ignore

        builder.IncrementIndent() |> ignore
        builder.AppendLine "" |> ignore

        builder.AppendLine "let Create(mb: MigrationBuilder) ="
        |> ignore

        let expected = IndentedStringBuilder()

        expected.AppendLines(builder.ToString(), false)
        |> ignore

        expected.IncrementIndent() |> ignore
        expected.IncrementIndent() |> ignore

        expected.AppendLines(expectedCode, false)
        |> ignore


        builder.IncrementIndent() |> ignore

        (generator :> ICSharpMigrationOperationGenerator)
            .Generate("mb", [| operation |], builder)

        let code = builder.ToString()

        Expect.equal (code.Trim()) ((expected.ToString()).Trim()) "Should be equal"

        let build = { TargetDir = ""; Sources = [ code ] }

        let references =
            [| "System.Collections"
               "System.Net.Requests"
               "System.Net.WebClient"
               "System.Runtime"
               "System.Runtime.Numerics"
               "Microsoft.EntityFrameworkCore.Relational"
               "NetTopologySuite" |]

        let assembly = build.BuildInMemory(references)

        let factoryType =
            assembly.GetTypes()
            |> Seq.find (fun m -> m.Name = "OperationsFactory")

        let createMethod = factoryType.GetMethod("Create")
        let mb = MigrationBuilder(activeProvider = null)
        createMethod.Invoke(null, [| mb |]) |> ignore
        let result = mb.Operations.Cast<'a>().Single()

        ``assert`` result

    [<Tests>]
    let FSharpMigrationOperationGeneratorTest =
        testList
            "FSharpMigrationOperationGeneratorTest"
            [

              test "Generate separates operations by a blank line" {
                  let generator = createGenerator ()
                  let builder = IndentedStringBuilder()

                  generator.Generate(
                      "mb",
                      [| SqlOperation(Sql = "-- Don't stand so")
                         SqlOperation(Sql = "-- close to me") |],
                      builder
                  )

                  let expected =
                      "mb.Sql(\"-- Don't stand so\") |> ignore"
                      + _eol
                      + _eol
                      + "mb.Sql(\"-- close to me\") |> ignore"
                      + _eol
                      + _eol

                  let actual = builder.ToString()

                  Expect.equal expected actual "Should be equal"
              }

              test "AddColumnOperation required args" {
                  let op =
                      AddColumnOperation(Name = "Id", Table = "Post", ClrType = typeof<int>)

                  let expected =
                      seq {
                          "mb.AddColumn<int>("
                          "    name = \"Id\""
                          "    ,table = \"Post\""
                          "    ,nullable = false"
                          "    ) |> ignore"
                      }
                      |> join _eol

                  let ``assert`` (o: AddColumnOperation) =
                      Expect.equal o.Name "Id" "Should be equal"
                      Expect.equal o.Table "Post" "Should be equal"
                      Expect.equal o.ClrType typeof<int> "Should be equal"

                  Test<AddColumnOperation> op expected ``assert``
              }

              test "CreateTableOperation optional args" {
                  let op =
                      CreateTableOperation(Name = "MyTable", Schema = "MySchema")

                  op.Columns.Add
                  <| AddColumnOperation(Name = "Id", Table = "MyTable", ClrType = typeof<Guid>)

                  let expected =
                      seq {
                          "mb.CreateTable("
                          "    name = \"MyTable\""
                          "    ,schema = \"MySchema\""
                          "    ,columns = (fun table -> "
                          "    {|"
                          "        Id ="
                          "            table.Column<Guid>("
                          "                nullable = false"
                          "            )"
                          "    |})"
                          ") |> ignore"
                      }
                      |> join _eol

                  let ``assert`` (o: CreateTableOperation) =
                      Expect.equal o.Name "MyTable" "Should be equal"
                      Expect.equal o.Schema "MySchema" "Should be equal"
                      Expect.equal o.Columns.Count 1 "Should have one column"

                      let c = o.Columns.[0]

                      Expect.equal c.ClrType typeof<Guid> "Should be equal"

                  Test<CreateTableOperation> op expected ``assert``
              }

              test "AddForeignKey" {
                  let op =
                      AddForeignKeyOperation(
                          Name = "FK_Test",
                          Table = "MyTable",
                          Columns = [| "MyColumn" |],
                          PrincipalTable = "PrincipalTable",
                          PrincipalColumns = [| "Id" |]
                      )

                  let expected =
                      seq {
                          "mb.AddForeignKey("
                          "    name = \"FK_Test\""
                          "    ,table = \"MyTable\""
                          "    ,column = \"MyColumn\""
                          "    ,principalTable = \"PrincipalTable\""
                          "    ,principalColumn = \"Id\""
                          ") |> ignore"
                      }
                      |> join _eol

                  let ``assert`` (o: AddForeignKeyOperation) =
                      Expect.equal o.Name "FK_Test" "Should be equal"
                      Expect.equal o.Table "MyTable" "Should be equal"
                      Expect.equal o.PrincipalTable "PrincipalTable" "Should be equal"

                  Test<AddForeignKeyOperation> op expected ``assert``
              }

              test "AddPrimaryKey" {
                  let op =
                      AddPrimaryKeyOperation(Name = "PK_Test", Table = "MyTable", Columns = [| "MyColumn" |])

                  let expected =
                      seq {
                          "mb.AddPrimaryKey("
                          "    name = \"PK_Test\""
                          "    ,table = \"MyTable\""
                          "    ,column = \"MyColumn\""
                          ") |> ignore"
                      }
                      |> join _eol

                  let ``assert`` (o: AddPrimaryKeyOperation) =
                      Expect.equal o.Name "PK_Test" "Should be equal"
                      Expect.equal o.Table "MyTable" "Should be equal"

                  Test<AddPrimaryKeyOperation> op expected ``assert``
              }

              test "AddUniqueConstraint" {
                  let op =
                      AddUniqueConstraintOperation(Name = "UQ_Test", Table = "MyTable", Columns = [| "MyColumn" |])

                  let expected =
                      seq {
                          "mb.AddUniqueConstraint("
                          "    name = \"UQ_Test\""
                          "    ,table = \"MyTable\""
                          "    ,column = \"MyColumn\""
                          ") |> ignore"
                      }
                      |> join _eol

                  let ``assert`` (o: AddUniqueConstraintOperation) =
                      Expect.equal o.Name "UQ_Test" "Should be equal"
                      Expect.equal o.Table "MyTable" "Should be equal"

                  Test<AddUniqueConstraintOperation> op expected ``assert``
              }

              ]
