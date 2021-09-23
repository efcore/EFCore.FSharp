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

let emptyModelDbContext = """namespace TestNamespace

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata
open EntityFrameworkCore.FSharp.Extensions

type TestDbContext =
    inherit DbContext

    new() = { inherit DbContext() }
    new(options : DbContextOptions<TestDbContext>) =
        { inherit DbContext(options) }

    override this.OnConfiguring(optionsBuilder: DbContextOptionsBuilder) =
        if not optionsBuilder.IsConfigured then
            optionsBuilder.UseSqlServer("Initial Catalog=TestDatabase") |> ignore
            ()

    override this.OnModelCreating(modelBuilder: ModelBuilder) =
        base.OnModelCreating(modelBuilder)

        modelBuilder.RegisterOptionTypes()
"""

let TestFluentApiCall (modelBuilder: ModelBuilder) =
    modelBuilder.Model.SetAnnotation("Test:TestModelAnnotation", "foo")
    modelBuilder


let _testFluentApiCallMethodInfo =
    let a = System.Reflection.Assembly.GetExecutingAssembly()
    let modu = a.GetType("EntityFrameworkCore.FSharp.Test.Scaffolding.Internal.FSharpDbContextGeneratorTest")
    let methodInfo = modu.GetMethod("TestFluentApiCall")
    methodInfo


type TestModelAnnotationProvider(dependencies) =
    inherit SqlServerAnnotationProvider(dependencies)

    override _.For(database: IRelationalModel, designTime: bool) =
        let baseResult = base.For(database, designTime)
        seq {
            yield! baseResult

            if database.["Test:TestModelAnnotation"] :? string then
                let annotationValue = database.["Test:TestModelAnnotation"] :?> string
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

    let testBase = { new ModelCodeGeneratorTestBase() with
        override _.AddModelServices = fun (services) ->
            services.Replace(ServiceDescriptor.Singleton<IRelationalAnnotationProvider, TestModelAnnotationProvider>()) |> ignore

        override _.AddScaffoldingServices = fun (services) ->
            services.Replace(ServiceDescriptor.Singleton<IAnnotationCodeGenerator, TestModelAnnotationCodeGenerator>()) |> ignore
    }

    testList "FSharpDbContextGeneratorTest" [
        test "Empty Model" {

            let buildModel (m: ModelBuilder) = ()
            let options = ModelCodeGenerationOptions()

            let assertScaffold (code: ScaffoldedModel) =
                Expect.equal (normaliseLineEndings code.ContextFile.Code) (normaliseLineEndings emptyModelDbContext) "Should be equal"

            let assertModel (model: IModel) =
                Expect.isEmpty (model.GetEntityTypes()) "Should be empty"

            testBase.Test buildModel options assertScaffold assertModel
        }
    ]

