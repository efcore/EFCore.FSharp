namespace Bricelam.EntityFrameworkCore.FSharp.Test.Scaffolding.Internal

open Microsoft.EntityFrameworkCore.Design
open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore.Scaffolding
open Bricelam.EntityFrameworkCore.FSharp
open Bricelam.EntityFrameworkCore.FSharp.Test.TestUtilities
open Xunit;
open FsUnit.Xunit

module FSharpModelGeneratorTests =
    open Microsoft.EntityFrameworkCore.Scaffolding

    let private CreateGenerator () =
            
        let services = 
            ServiceCollection()
                .AddEntityFrameworkSqlServer()
                .AddEntityFrameworkDesignTimeServices()
                .AddSingleton<IScaffoldingProviderCodeGenerator, TestScaffoldingProviderCodeGenerator>()
                .AddSingleton<IAnnotationCodeGenerator, AnnotationCodeGenerator>()
                .AddSingleton<ProviderCodeGenerator, TestProviderCodeGenerator>()
                .AddSingleton<IProviderConfigurationCodeGenerator, TestProviderCodeGenerator>()

        (EFCoreFSharpServices() :> IDesignTimeServices).ConfigureDesignTimeServices(services)

        services
            .BuildServiceProvider()
            .GetRequiredService<IModelCodeGenerator>()            

    [<Fact>]
    let ``Language works`` () =
        let generator = CreateGenerator()

        let result = generator.Language

        result |> should equal "F#"
