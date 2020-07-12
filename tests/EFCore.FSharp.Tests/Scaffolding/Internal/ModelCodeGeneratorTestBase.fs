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
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal
open Microsoft.EntityFrameworkCore.SqlServer.Scaffolding.Internal
open Microsoft.EntityFrameworkCore.Scaffolding.Internal
open EntityFrameworkCore.FSharp.Scaffolding
open EntityFrameworkCore.FSharp.Scaffolding.Internal
open EntityFrameworkCore.FSharp.Migrations.Design
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure
open Microsoft.EntityFrameworkCore.Diagnostics
open Microsoft.EntityFrameworkCore.SqlServer.Diagnostics.Internal

type ModelCodeGeneratorTestBase() =

    let configureDesignTimeServices (services: IServiceCollection) =
        services
            .AddSingleton<LoggingDefinitions, SqlServerLoggingDefinitions>()
            .AddSingleton<IRelationalTypeMappingSource, SqlServerTypeMappingSource>()
            .AddSingleton<IDatabaseModelFactory, SqlServerDatabaseModelFactory>()
            .AddSingleton<IProviderConfigurationCodeGenerator, SqlServerCodeGenerator>()
            .AddSingleton<IAnnotationCodeGenerator, SqlServerAnnotationCodeGenerator>()
            .AddSingleton<ICSharpDbContextGenerator, FSharpDbContextGenerator>()
            .AddSingleton<IModelCodeGenerator, FSharpModelGenerator>()
            .AddSingleton<IMigrationsCodeGenerator, FSharpMigrationsGenerator>()
            .AddSingleton<IMigrationsScaffolder, FSharpMigrationsScaffolder>() |> ignore

        // let serviceMap (map : ServiceCollectionMap) =
        //     map.TryAdd(typeof<ProviderCodeGenerator>, typeof<TestProviderCodeGenerator>, ServiceLifetime.Singleton) |> ignore

        // EntityFrameworkRelationalServicesBuilder(services)
        //     .TryAddProviderSpecificServices(Action<ServiceCollectionMap>(serviceMap))

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
            ]

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
            ]

        let runtimeDir =
            System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()

        let runtimeRefs =
            runtimeNames
            |> List.map(fun r -> runtimeDir + r)

        let localRefs =
            let thisAssembly = System.Reflection.Assembly.GetExecutingAssembly()
            let location = thisAssembly.Location.Replace(thisAssembly.GetName().Name + ".dll", "")
            localNames
            |> List.map(fun s -> location + s)

        runtimeRefs @ localRefs |> List.toArray

    static member BuildNonValidatingConventionSet () =
        let services = ServiceCollection()

        let serviceProvider =
            services
                .AddEntityFrameworkSqlServer()
                .AddDbContext<DbContext>(fun o -> o.UseSqlServer("Server=.") |> ignore)
                .BuildServiceProvider()

        let serviceScope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope()
        let context = serviceScope.ServiceProvider.GetService<DbContext>()

        let conventionSet =
            (context :> IInfrastructure<IServiceProvider>)
                .Instance
                .GetRequiredService<IConventionSetBuilder>()
                .CreateConventionSet()

        ConventionSet.Remove(conventionSet.ModelFinalizedConventions, (typeof<ValidatingConvention>)) |> ignore

        conventionSet

    member this.Test((buildModel : ModelBuilder -> unit), (options : ModelCodeGenerationOptions), (assertScaffold : ScaffoldedModel -> unit), (assertModel : IModel -> unit)) =
        let modelBuilder = SqlServerTestHelpers.CreateConventionBuilder true
        modelBuilder.Model.RemoveAnnotation(CoreAnnotationNames.ProductVersion) |> ignore
        buildModel(modelBuilder)

        let _ = modelBuilder.Model.GetEntityTypeErrors()

        let model = modelBuilder.FinalizeModel()

        let services = ServiceCollection().AddEntityFrameworkDesignTimeServices()
        services |> configureDesignTimeServices

        let generator =
            services
                .BuildServiceProvider()
                .GetRequiredService<IModelCodeGenerator>()

        if isNull options.ModelNamespace then
            options.ModelNamespace <- "TestNamespace"

        options.ContextName <- "TestDbContext"
        options.ConnectionString <- "Initial Catalog=TestDatabase"

        let scaffoldedModel =
            generator.GenerateModel(
                model,
                options)

        assertScaffold(scaffoldedModel);

        let sources =
            scaffoldedModel.ContextFile.Code :: (scaffoldedModel.AdditionalFiles |> Seq.map (fun f -> f.Code) |> Seq.toList)
            |> List.rev

        let build = {
            TargetDir = null
            Sources = sources }

        let references =
            getRequiredReferences()

        let assembly = build.BuildInMemory references

        let context = assembly.CreateInstance("TestNamespace.TestDbContext") :?> DbContext

        assertModel(context.Model)

