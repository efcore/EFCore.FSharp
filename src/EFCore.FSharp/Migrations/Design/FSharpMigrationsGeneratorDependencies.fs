namespace EntityFrameworkCore.FSharp.Migrations.Design

open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Migrations.Design

type FSharpMigrationsGeneratorDependencies
    (
        fSharpHelper : ICSharpHelper,
        fSharpMigrationOperationGenerator : ICSharpMigrationOperationGenerator,
        fSharpSnapshotGenerator : ICSharpSnapshotGenerator
    ) =

    member this.FSharpHelper = fSharpHelper
    member this.FSharpMigrationOperationGenerator = fSharpMigrationOperationGenerator
    member this.FSharpSnapshotGenerator = fSharpSnapshotGenerator
