module EntityFrameworkCore.FSharp.Test.Scaffolding.Internal.FSharpDbContextGeneratorTest

open System
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Scaffolding
open Expecto
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata

let normaliseLineEndings (str: string) =
    str.Replace("\r\n", "\n").Replace("\r", "\n")

let emptyModelDbContext = """namespace TestNamespace

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata
open EntityFrameworkCore.FSharp.Extensions

open TestDbDomain

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

        modelBuilder
            |> registerOptionTypes
            |> registerEnumLikeUnionTypes
            |> ignore

        base.OnModelCreating modelBuilder
"""

[<Tests>]
let FSharpDbContextGeneratorTest =
    let testBase = ModelCodeGeneratorTestBase.ModelCodeGeneratorTestBase()

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

