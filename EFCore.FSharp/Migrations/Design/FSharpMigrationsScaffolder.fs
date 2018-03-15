namespace Bricelam.EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Design

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Bricelam.EntityFrameworkCore.FSharp.Internal
open Microsoft.EntityFrameworkCore.Design.Internal

type FSharpMigrationsScaffolder(dependencies: MigrationsScaffolderDependencies) =

    let contextType = dependencies.CurrentDbContext.Context.GetType()
    let activeProvider = dependencies.DatabaseProvider.Name

    let scaffoldMigration (migrationName:string) (rootNamespace: string) (subNamespace:string) (language: string) : ScaffoldedMigration =
        
        if dependencies.MigrationsAssembly.FindMigrationId(migrationName) |> notNull then
            raise (migrationName |> DesignStrings.DuplicateMigrationName |> OperationException)
        
        null

    let removeMigration (projectDir:string) (rootNamespace: string) (force:bool) (language: string) : MigrationFiles =
        null

    let save (projectDir: string) (migration: ScaffoldedMigration) (outputDir: string) : MigrationFiles =
        null

    interface IMigrationsScaffolder with

        member this.ScaffoldMigration(migrationName:string, rootNamespace: string, subNamespace:string, language: string) =
            scaffoldMigration migrationName rootNamespace subNamespace language

        member this.RemoveMigration(projectDir:string, rootNamespace: string, force:bool, language: string) =
            removeMigration projectDir rootNamespace force language

        member this.Save(projectDir: string, migration: ScaffoldedMigration, outputDir: string) =
            save projectDir migration outputDir