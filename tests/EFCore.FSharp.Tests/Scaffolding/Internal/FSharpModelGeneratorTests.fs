module EntityFrameworkCore.FSharp.Test.Scaffolding.Internal.FSharpModelGeneratorTests

open System.IO
open Microsoft.EntityFrameworkCore.Design
open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore.Scaffolding
open Microsoft.EntityFrameworkCore.Metadata.Internal
open EntityFrameworkCore.FSharp
open EntityFrameworkCore.FSharp.Test.TestUtilities
open Expecto
open Expecto.Tests

let createGenerator () =
    let services =
        ServiceCollection()
            .AddEntityFrameworkSqlServer()
            .AddEntityFrameworkDesignTimeServices()
            .AddSingleton<IAnnotationCodeGenerator, AnnotationCodeGenerator>()
            .AddSingleton<ProviderCodeGenerator, TestProviderCodeGenerator>()
            .AddSingleton<IProviderConfigurationCodeGenerator, TestProviderCodeGenerator>()

    (EFCoreFSharpServices() :> IDesignTimeServices).ConfigureDesignTimeServices(services)

    services
        .BuildServiceProvider()
        .GetRequiredService<IModelCodeGenerator>()

[<Tests>]
let FSharpModelGeneratorTests =

    testList "FSharpModelGeneratorTests" [

        test "Language Works" {
            let generator = createGenerator()

            let result = generator.Language

            Expect.equal result "F#" "Should be equal"
        }

        test "Write code works" {
            let generator = createGenerator()
            let modelBuilder = RelationalTestHelpers.Instance.CreateConventionBuilder()

            modelBuilder
                .Entity("TestEntity")
                .Property<int>("Id")
                .HasAnnotation(ScaffoldingAnnotationNames.ColumnOrdinal, 0) |> ignore

            let modelBuilderOptions =
                ModelCodeGenerationOptions(
                    ModelNamespace = "TestNamespace",
                    ContextNamespace = "ContextNameSpace",
                    ContextDir = Path.Combine("..", (sprintf "%s%c" "TestContextDir" Path.DirectorySeparatorChar)),
                    ContextName = "TestContext",
                    ConnectionString = "Data Source=Test")

            let result =
                generator.GenerateModel(
                    modelBuilder.Model,
                    modelBuilderOptions)

            let expectedContextFilePath = Path.Combine("..", "TestContextDir", "TestContext.fs")
            Expect.equal result.ContextFile.Path expectedContextFilePath "Should be equal"
            Expect.isNotEmpty result.ContextFile.Code "Should not be empty"

            Expect.equal result.AdditionalFiles.Count 1 "Should be equal"
            Expect.equal result.AdditionalFiles.[0].Path "TestDomain.fs" "Should be equal"
            Expect.isNotEmpty result.AdditionalFiles.[0].Code "Should not be empty"
        }

    ]

