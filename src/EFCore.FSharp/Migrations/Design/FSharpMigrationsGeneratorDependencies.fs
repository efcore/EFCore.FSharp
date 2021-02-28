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

    member this.With (fSharpHelper : ICSharpHelper) =
        FSharpMigrationsGeneratorDependencies (fSharpHelper, this.FSharpMigrationOperationGenerator, this.FSharpSnapshotGenerator)

    member this.With (fSharpMigrationOperationGenerator : ICSharpMigrationOperationGenerator) =
        FSharpMigrationsGeneratorDependencies (this.FSharpHelper, fSharpMigrationOperationGenerator, this.FSharpSnapshotGenerator)

    member this.With (fSharpSnapshotGenerator : ICSharpSnapshotGenerator) =
        FSharpMigrationsGeneratorDependencies (this.FSharpHelper, this.FSharpMigrationOperationGenerator, fSharpSnapshotGenerator)
