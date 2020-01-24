namespace Bricelam.EntityFrameworkCore.FSharp.Test.Scaffolding.Internal

open System
open System.Collections.Generic
open System.Linq
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Conventions
open Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.SqlServer.Design.Internal

open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore.Scaffolding
open Bricelam.EntityFrameworkCore.FSharp
open Bricelam.EntityFrameworkCore.FSharp.Test.TestUtilities
open System.Linq.Expressions
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal
open Microsoft.EntityFrameworkCore.SqlServer.Scaffolding.Internal
open Microsoft.EntityFrameworkCore.Scaffolding.Internal
open Bricelam.EntityFrameworkCore.FSharp.Scaffolding
open Bricelam.EntityFrameworkCore.FSharp.Scaffolding.Internal
open Bricelam.EntityFrameworkCore.FSharp.Migrations.Design
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure

type ModelCodeGeneratorTestBase() =

    let configureDesignTimeServices (services: IServiceCollection) =
        services
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
        let modelBuilder = ModelBuilder(ModelCodeGeneratorTestBase.BuildNonValidatingConventionSet())
        modelBuilder.Model.RemoveAnnotation(CoreAnnotationNames.ProductVersion) |> ignore
        buildModel(modelBuilder)

        let model = modelBuilder.FinalizeModel()

        let services = ServiceCollection().AddEntityFrameworkDesignTimeServices()        
        services |> configureDesignTimeServices

        let generator =
            services
                .BuildServiceProvider()
                .GetRequiredService<IModelCodeGenerator>()

        let scaffoldedModel =
            generator.GenerateModel(
                model,
                options)
        assertScaffold(scaffoldedModel);

        let sources =
             [ scaffoldedModel.ContextFile.Code ]

        let build = {
            TargetDir = ""
            Sources = sources }
            

        let assembly = build.BuildInMemory()
        let context = (assembly.CreateInstance("TestNamespace.TestDbContext")) :?> DbContext
        let compiledModel = context.Model
        assertModel(compiledModel)

