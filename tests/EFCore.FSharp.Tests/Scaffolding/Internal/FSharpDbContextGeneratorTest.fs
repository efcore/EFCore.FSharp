module EntityFrameworkCore.FSharp.Test.Scaffolding.Internal.FSharpDbContextGeneratorTest

open System
open Microsoft.Extensions.DependencyInjection.Extensions
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Scaffolding
open Expecto
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata
open ModelCodeGeneratorTestBase
open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.SqlServer.Metadata.Internal
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.SqlServer.Design.Internal

let normaliseLineEndings (str: string) =
    str.Replace("\r\n", "\n").Replace("\r", "\n")

let emptyModelDbContext =
    """namespace TestNamespace

open TestDbDomain
open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata
open EntityFrameworkCore.FSharp.Extensions

type TestDbContext =
    inherit DbContext

    new() = { inherit DbContext() }
    new(options : DbContextOptions<TestDbContext>) = { inherit DbContext(options) }

    override this.OnConfiguring(optionsBuilder: DbContextOptionsBuilder) =
        if not optionsBuilder.IsConfigured then
            optionsBuilder.UseSqlServer("Initial Catalog=TestDatabase") |> ignore
        ()

    override this.OnModelCreating(modelBuilder: ModelBuilder) =
        base.OnModelCreating(modelBuilder)

        modelBuilder.RegisterOptionTypes()
"""

let temporalDbContext =
    """namespace TestNamespace

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata
open EntityFrameworkCore.FSharp.Extensions

type TestDbContext =
    inherit DbContext

    new() = { inherit DbContext() }
    new(options : DbContextOptions<TestDbContext>) = { inherit DbContext(options) }

    [<DefaultValue>] val mutable private _Customer : DbSet<Customer>
    member this.Customer
        with get() = this._Customer
        and set v = this._Customer <- v


    override this.OnConfiguring(optionsBuilder: DbContextOptionsBuilder) =
        if not optionsBuilder.IsConfigured then
            optionsBuilder.UseSqlServer("Initial Catalog=TestDatabase") |> ignore
        ()

    override this.OnModelCreating(modelBuilder: ModelBuilder) =
        base.OnModelCreating(modelBuilder)

        modelBuilder.Entity<Customer>(fun entity ->

            entity.ToTable(
                            (fun tb ->
                                tb.IsTemporal(
                                                    (fun ttb ->
                                                        ttb
                                                            .HasPeriodStart("PeriodStart")
                                                            .HasColumnName("PeriodStart") |> ignore
                                                        ttb
                                                            .HasPeriodEnd("PeriodEnd")
                                                            .HasColumnName("PeriodEnd") |> ignore
                                                        )
                                ) |> ignore
                                )
            ) |> ignore

            entity.Property(fun e -> e.Id).HasValue(0)
                .UseIdentityColumn(1L, 1)
                |> ignore

            entity.Property(fun e -> e.Name)
                |> ignore

            entity.Property(fun e -> e.PeriodEnd).HasValue(DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified))
                |> ignore

            entity.Property(fun e -> e.PeriodStart).HasValue(DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified))
                |> ignore
        ) |> ignore

        modelBuilder.RegisterOptionTypes()
"""

let vistaSource =
    """namespace TestNamespace

open System

[<CLIMutable>]
type Vista = {
    Id : Int32
    Name : string
}
"""

let customerSource =
    """namespace TestNamespace

open System

[<CLIMutable>]
type Customer = {
    Id : Int32
    Name : string
}
"""

let TestFluentApiCall (modelBuilder: ModelBuilder) =
    modelBuilder.Model.SetAnnotation("Test:TestModelAnnotation", "foo")
    modelBuilder


let _testFluentApiCallMethodInfo =
    let a =
        Reflection.Assembly.GetExecutingAssembly()

    let modu =
        a.GetType("EntityFrameworkCore.FSharp.Test.Scaffolding.Internal.FSharpDbContextGeneratorTest")

    let methodInfo = modu.GetMethod("TestFluentApiCall")
    methodInfo


type TestModelAnnotationProvider(dependencies) =
    inherit SqlServerAnnotationProvider(dependencies)

    override _.For(database: IRelationalModel, designTime: bool) =
        let baseResult = base.For(database, designTime)

        seq {
            yield! baseResult

            if database.["Test:TestModelAnnotation"] :? string then
                let annotationValue =
                    database.["Test:TestModelAnnotation"] :?> string

                yield (Annotation("Test:TestModelAnnotation", annotationValue)) :> IAnnotation
        }

type TestModelAnnotationCodeGenerator(dependencies) =
    inherit SqlServerAnnotationCodeGenerator(dependencies)

    override _.GenerateFluentApi(model: IModel, annotation: IAnnotation) =
        match annotation.Name with
        | "Test:TestModelAnnotation" -> MethodCallCodeFragment(_testFluentApiCallMethodInfo)
        | _ -> base.GenerateFluentApi(model, annotation)


[<Tests>]
let FSharpDbContextGeneratorTest =

    let testBase =
        { new ModelCodeGeneratorTestBase() with
            override _.AddModelServices =
                fun (services) ->
                    services.Replace(
                        ServiceDescriptor.Singleton<IRelationalAnnotationProvider, TestModelAnnotationProvider>()
                    )
                    |> ignore

            override _.AddScaffoldingServices =
                fun (services) ->
                    services.Replace(
                        ServiceDescriptor.Singleton<IAnnotationCodeGenerator, TestModelAnnotationCodeGenerator>()
                    )
                    |> ignore }

    testList
        "FSharpDbContextGeneratorTest"
        [ test "Empty Model" {

              let buildModel (m: ModelBuilder) = ()
              let options = ModelCodeGenerationOptions()

              let assertScaffold (code: ScaffoldedModel) =
                  Expect.equal
                      (normaliseLineEndings code.ContextFile.Code)
                      (normaliseLineEndings emptyModelDbContext)
                      "Should be equal"

              let assertModel (model: IModel) =
                  Expect.isEmpty (model.GetEntityTypes()) "Should be empty"

              testBase.Test buildModel options assertScaffold assertModel []
          }

        //   test "Views work" {

        //       let buildModel (m: ModelBuilder) = m.Entity("Vista").ToView("Vista")

        //       let options =
        //           ModelCodeGenerationOptions(UseDataAnnotations = true)

        //       let assertScaffold (code: ScaffoldedModel) =
        //           Expect.stringContains code.ContextFile.Code "entity.ToView(\"Vista\")" "Should contain view"

        //       let assertModel (model: IModel) =
        //           let entityType =
        //               model.FindEntityType("TestNamespace.Vista")

        //           Expect.isNotNull
        //               (entityType.FindAnnotation(RelationalAnnotationNames.ViewDefinitionSql))
        //               "Should not be null"

        //           Expect.equal (entityType.GetViewName()) "Vista" "Should be equal"
        //           Expect.isNull (entityType.GetViewSchema()) "Should be null"
        //           Expect.isNull (entityType.GetTableName()) "Should be null"
        //           Expect.isNull (entityType.GetSchema()) "Should be null"

        //       let additionalSources = [ vistaSource ]

        //       testBase.Test buildModel options assertScaffold assertModel additionalSources

        //   }

        //   test "Temporal Tables work" {

        //       let buildModel (m: ModelBuilder) =
        //           m.Entity(
        //               "Customer",
        //               fun e ->
        //                   e.Property<int>("Id") |> ignore
        //                   e.Property<string>("Name") |> ignore
        //                   e.HasKey("Id") |> ignore

        //                   e.ToTable(fun tb -> tb.IsTemporal() |> ignore)
        //                   |> ignore
        //           )

        //       let options =
        //           ModelCodeGenerationOptions(UseDataAnnotations = false)

        //       let assertScaffold (code: ScaffoldedModel) =
        //           Expect.equal
        //               (normaliseLineEndings code.ContextFile.Code)
        //               (normaliseLineEndings temporalDbContext)
        //               "Should be equal"

        //       let assertModel (model: IModel) = ()

        //       let additionalSources = [ customerSource ]

        //       testBase.Test buildModel options assertScaffold assertModel additionalSources

        //   }
         ]
