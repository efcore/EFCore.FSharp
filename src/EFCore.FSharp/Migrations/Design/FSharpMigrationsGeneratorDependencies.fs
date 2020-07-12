namespace EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.Internal

open EntityFrameworkCore.FSharp.EntityFrameworkExtensions
open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open EntityFrameworkCore.FSharp.Internal
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Design

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