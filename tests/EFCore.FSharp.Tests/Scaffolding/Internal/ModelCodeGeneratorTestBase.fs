module EntityFrameworkCore.FSharp.Test.Scaffolding.Internal.ModelCodeGeneratorTestBase

open System
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Conventions
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.SqlServer.Design.Internal

open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore.Scaffolding
open EntityFrameworkCore.FSharp.Test.TestUtilities
open EntityFrameworkCore.FSharp
open Microsoft.EntityFrameworkCore.Design.Internal

[<AbstractClass>]
type ModelCodeGeneratorTestBase() =

    let createServices () =
        let testAssembly =
            (typeof<ModelCodeGeneratorTestBase>).Assembly

        let reporter = TestOperationReporter()

        let services =
            DesignTimeServicesBuilder(testAssembly, testAssembly, reporter, [||])
                .CreateServiceCollection("Microsoft.EntityFrameworkCore.SqlServer")

        services

    let getRequiredReferences () =
        let runtimeNames =
            [ "mscorlib.dll"
              "netstandard.dll"
              "System.Buffers.dll"
              "System.Collections.Concurrent.dll"
              "System.Collections.dll"
              "System.ComponentModel.dll"
              "System.ComponentModel.Primitives.dll"
              "System.Console.dll"
              "System.Data.Common.dll"
              "System.Diagnostics.TraceSource.dll"
              "System.Globalization.dll"
              "System.IO.FileSystem.dll"
              "System.Linq.Expressions.dll"
              "System.Net.Requests.dll"
              "System.Net.WebClient.dll"
              "System.ObjectModel.dll"
              "System.Private.CoreLib.dll"
              "System.Resources.ResourceManager.dll"
              "System.Runtime.dll"
              "System.Runtime.Extensions.dll"
              "System.Runtime.InteropServices.dll"
              "System.Runtime.Numerics.dll"
              "System.Threading.dll"
              "System.Threading.Tasks.dll"
              "System.Threading.Thread.dll"
              "System.Threading.ThreadPool.dll" ]

        let localNames =
            [ "FSharp.Core.dll"
              "FSharp.Compiler.Service.dll"
              "Microsoft.EntityFrameworkCore.dll"
              "Microsoft.EntityFrameworkCore.Abstractions.dll"
              "Microsoft.EntityFrameworkCore.Design.dll"
              "Microsoft.EntityFrameworkCore.Proxies.dll"
              "Microsoft.EntityFrameworkCore.Relational.dll"
              "Microsoft.EntityFrameworkCore.Sqlite.dll"
              "Microsoft.EntityFrameworkCore.SqlServer.dll"
              "EntityFrameworkCore.FSharp.dll" ]

        let runtimeDir =
            System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()

        let runtimeRefs =
            runtimeNames |> List.map (fun r -> runtimeDir + r)

        let localRefs =
            let thisAssembly =
                System.Reflection.Assembly.GetExecutingAssembly()

            let location =
                thisAssembly.Location.Replace(thisAssembly.GetName().Name + ".dll", "")

            localNames |> List.map (fun s -> location + s)

        runtimeRefs @ localRefs |> List.toArray

    abstract member AddModelServices: (IServiceCollection -> unit)
    abstract member AddScaffoldingServices: (IServiceCollection -> unit)

    member this.Test
        (buildModel: ModelBuilder -> 'a)
        (options: ModelCodeGenerationOptions)
        (assertScaffold: ScaffoldedModel -> unit)
        (assertModel: IModel -> unit)
        (additionalSources: string list)
        =

        // Add F# overrides
        let efCoreFSharpServices = EFCoreFSharpServices.Default

        let addModelServices = this.AddModelServices

        let modelBuilder =
            SqlServerTestHelpers.Instance.CreateConventionBuilder(
                addServices =
                    fun services ->
                        efCoreFSharpServices.ConfigureDesignTimeServices services
                        addModelServices services
                        services
            )

        (modelBuilder: ModelBuilder)
            .Model.RemoveAnnotation(CoreAnnotationNames.ProductVersion)
        |> ignore

        let _ = buildModel (modelBuilder)

        let model = modelBuilder.FinalizeModel()

        let services = createServices ()
        efCoreFSharpServices.ConfigureDesignTimeServices services

        this.AddScaffoldingServices services

        let generator =
            services
                .BuildServiceProvider(validateScopes = true)
                .GetRequiredService<IModelCodeGenerator>()

        if isNull options.ModelNamespace then
            options.ModelNamespace <- "TestNamespace"

        options.ContextName <- "TestDbContext"
        options.ConnectionString <- "Initial Catalog=TestDatabase"
        options.SuppressConnectionStringWarning <- true

        let scaffoldedModel = generator.GenerateModel(model, options)

        assertScaffold scaffoldedModel

        let sources =
            scaffoldedModel.ContextFile.Code
            :: (scaffoldedModel.AdditionalFiles
                |> Seq.map (fun f -> f.Code)
                |> Seq.toList)
            @ additionalSources
            |> List.rev

        let build = { TargetDir = null; Sources = sources }

        let references = getRequiredReferences ()

        let assembly = build.BuildInMemory references

        let context =
            (assembly: System.Reflection.Assembly)
                .CreateInstance("TestNamespace.TestDbContext")
            :?> DbContext

        assertModel context.Model
