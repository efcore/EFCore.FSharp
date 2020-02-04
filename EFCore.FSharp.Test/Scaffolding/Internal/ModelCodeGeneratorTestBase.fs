namespace Bricelam.EntityFrameworkCore.FSharp.Test.Scaffolding.Internal

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
open Bricelam.EntityFrameworkCore.FSharp.Test.TestUtilities
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal
open Microsoft.EntityFrameworkCore.SqlServer.Scaffolding.Internal
open Microsoft.EntityFrameworkCore.Scaffolding.Internal
open Bricelam.EntityFrameworkCore.FSharp.Scaffolding
open Bricelam.EntityFrameworkCore.FSharp.Scaffolding.Internal
open Bricelam.EntityFrameworkCore.FSharp.Migrations.Design
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
            [
                "Microsoft.EntityFrameworkCore"
                "Microsoft.EntityFrameworkCore.Relational"
                "Microsoft.EntityFrameworkCore.SqlServer"
            ]

        let assembly = build.BuildInMemory references
        let dbType = assembly.GetType("TestNamespace.TestDbContext")

        let context = assembly.CreateInstance("TestNamespace.TestDbContext") :?> DbContext
        let compiledModel = context.Model
        assertModel(compiledModel)

