module EntityFrameworkCore.FSharp.Test.Scaffolding.Internal.FSharpDbContextGeneratorTest

open Microsoft.EntityFrameworkCore.Scaffolding
open Expecto

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
        base.OnModelCreating(modelBuilder)

        modelBuilder.RegisterOptionTypes()
"""

[<Tests>]
let FSharpDbContextGeneratorTest =
    let testBase = ModelCodeGeneratorTestBase.ModelCodeGeneratorTestBase()

    testList "FSharpDbContextGeneratorTest" [
        test "Empty Model" {
            testBase.Test(
                (fun m -> ()),
                (ModelCodeGenerationOptions()),
                (fun code -> Expect.equal code.ContextFile.Code emptyModelDbContext "Should be equal"),
                (fun model -> Expect.isEmpty (model.GetEntityTypes()) "Should be empty")
            )
        }
    ]

