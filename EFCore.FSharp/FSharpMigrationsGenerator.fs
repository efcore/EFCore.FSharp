namespace Bricelam.EntityFrameworkCore.FSharp

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Migrations.Operations

type FSharpMigrationsGenerator(dependencies: MigrationsCodeGeneratorDependencies) =
    inherit MigrationsCodeGenerator(dependencies)

    override this.FileExtension = ".fs"

    override this.GenerateMigration(migrationNamespace: string, migrationName: string, upOperations: IReadOnlyList<MigrationOperation>, downOperations: IReadOnlyList<MigrationOperation>) = ""

    override this.GenerateMetadata(migrationNamespace: string, contextType: Type, migrationName: string, migrationId: string, targetModel: IModel) = ""

    override this.GenerateSnapshot(modelSnapshotNamespace: string, contextType: Type, modelSnapshotName: string, model: IModel) = ""
