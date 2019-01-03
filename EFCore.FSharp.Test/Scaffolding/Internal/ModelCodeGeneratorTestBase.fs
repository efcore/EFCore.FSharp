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
open Bricelam.EntityFrameworkCore.FSharp.Test.TestUtilities

type ModelCodeGeneratorTestBase() =

    static member BuildNonValidatingConventionSet () =
        let serviceProvider =
            ServiceCollection()
                .AddEntityFrameworkSqlServer()
                .AddDbContext<DbContext>(fun o -> o.UseSqlServer("Server=.") |> ignore)
                .BuildServiceProvider()

        let serviceScope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope()        
        let context = serviceScope.ServiceProvider.GetService<DbContext>()
            
        CompositeConventionSetBuilder(context.GetService<IEnumerable<IConventionSetBuilder>>().ToList())
            .AddConventions(context.GetService<ICoreConventionSetBuilder>().CreateConventionSet())
        

    member this.Test(buildModel : ModelBuilder -> unit, options : ModelCodeGenerationOptions, assertScaffold : ScaffoldedModel -> unit, assertModel : IModel -> unit) =
        let modelBuilder = ModelBuilder(ModelCodeGeneratorTestBase.BuildNonValidatingConventionSet())
        modelBuilder.Model.RemoveAnnotation(CoreAnnotationNames.ProductVersionAnnotation) |> ignore
        buildModel(modelBuilder)

        let model = modelBuilder.FinalizeModel()

        let services = ServiceCollection().AddEntityFrameworkDesignTimeServices()
        SqlServerDesignTimeServices().ConfigureDesignTimeServices(services)

        let generator =
            services
                .BuildServiceProvider()
                .GetRequiredService<IModelCodeGenerator>()

        let scaffoldedModel =
            generator.GenerateModel(
                model,
                "TestNamespace",
                String.Empty,
                "TestDbContext",
                "Initial Catalog=TestDatabase",
                options)
        assertScaffold(scaffoldedModel);

        let sources =
            (scaffoldedModel.AdditionalFiles |> Seq.map (fun f -> f.Code) |> Seq.toList)
            @ [ scaffoldedModel.ContextFile.Code ]

        let build = {
            TargetDir = ""
            Sources = sources }
            

        let assembly = build.BuildInMemory()
        let context = (assembly.CreateInstance("TestNamespace.TestDbContext")) :?> DbContext
        let compiledModel = context.Model
        assertModel(compiledModel)

