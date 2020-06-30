namespace EntityFrameworkCore.FSharp.Test.TestUtilities

open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Migrations
open Microsoft.EntityFrameworkCore.Migrations.Operations

type TestRelationalMigrationSqlGenerator(dependencies) =
    inherit MigrationsSqlGenerator(dependencies)

    override this.Generate(operation : RenameTableOperation, model : IModel, builder : MigrationCommandListBuilder) =
        ()

    override this.Generate(operation : RenameSequenceOperation, model : IModel, builder : MigrationCommandListBuilder) =
        ()

    override this.Generate(operation : RenameColumnOperation, model : IModel, builder : MigrationCommandListBuilder) =
        ()

    override this.Generate(operation : EnsureSchemaOperation, model : IModel, builder : MigrationCommandListBuilder) =
        ()

    override this.Generate(operation : RenameIndexOperation, model : IModel, builder : MigrationCommandListBuilder) =
        ()

    override this.Generate(operation : AlterColumnOperation, model : IModel, builder : MigrationCommandListBuilder) =
        ()
