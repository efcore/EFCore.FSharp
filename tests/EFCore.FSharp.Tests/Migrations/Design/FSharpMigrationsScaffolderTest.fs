namespace EntityFrameworkCore.FSharp.Test.Migrations.Design

open System
open System.Collections.Generic
open System.IO
open System.Threading
open System.Threading.Tasks
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.ChangeTracking.Internal
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Design.Internal
open Microsoft.EntityFrameworkCore.Diagnostics
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Infrastructure.Internal
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Migrations
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Migrations.Internal
open Microsoft.EntityFrameworkCore.SqlServer.Design.Internal
open Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.EntityFrameworkCore.TestUtilities
open Microsoft.EntityFrameworkCore.Update
open Microsoft.EntityFrameworkCore.Update.Internal
open Microsoft.Extensions.DependencyInjection

open EntityFrameworkCore.FSharp.Internal
open EntityFrameworkCore.FSharp.Migrations.Design
open EntityFrameworkCore.FSharp.Test.TestUtilities

open Expecto
open Microsoft.EntityFrameworkCore.Internal
open EntityFrameworkCore.FSharp.Test.TestUtilities.FakeProvider
open Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure
open EntityFrameworkCore.FSharp.Test.TestUtilities

type ContextWithSnapshot () =
    inherit DbContext()

type GenericContext<'a> () =
    inherit DbContext()

[<DbContext(typeof<ContextWithSnapshot>)>]
type ContextWithSnapshotModelSnapshot () =
    inherit ModelSnapshot()

    override __.BuildModel (modelBuilder) = ()

module FSharpMigrationsScaffolderTest =

    let createMigrationScaffolder<'context when 'context :> DbContext and 'context : (new : unit -> 'context)> () =
        let currentContext = CurrentDbContext(new 'context())
        let idGenerator = MigrationsIdGenerator()

        let sqlServerTypeMappingSource =
                SqlServerTypeMappingSource(
                    TestServiceFactory.Instance.Create<TypeMappingSourceDependencies>(),
                    TestServiceFactory.Instance.Create<RelationalTypeMappingSourceDependencies>())

        let sqlServerAnnotationCodeGenerator =
                SqlServerAnnotationCodeGenerator(
                    AnnotationCodeGeneratorDependencies(sqlServerTypeMappingSource))

        let code = FSharpHelper(sqlServerTypeMappingSource)

        let reporter = TestOperationReporter()
        let migrationAssembly =
            MigrationsAssembly(
                currentContext,
                DbContextOptions<'context>().WithExtension(FakeRelationalOptionsExtension()),
                idGenerator,
                FakeDiagnosticsLogger<DbLoggerCategory.Migrations>())
        let historyRepository = MockHistoryRepository()

        let services = RelationalTestHelpers.Instance.CreateContextServices()
        let model = Model().FinalizeModel()
        model.AddRuntimeAnnotation(RelationalAnnotationNames.RelationalModel, RelationalModel(model)) |> ignore

        FSharpMigrationsScaffolder(
            MigrationsScaffolderDependencies(
                currentContext,
                model,
                migrationAssembly,
                MigrationsModelDiffer(
                    TestRelationalTypeMappingSource(
                        TestServiceFactory.Instance.Create<TypeMappingSourceDependencies>(),
                        TestServiceFactory.Instance.Create<RelationalTypeMappingSourceDependencies>()),
                    MigrationsAnnotationProvider(
                        MigrationsAnnotationProviderDependencies()),
                    services.GetRequiredService<IChangeDetector>(),
                    services.GetRequiredService<IUpdateAdapterFactory>(),
                    services.GetRequiredService<CommandBatchPreparerDependencies>()),
                idGenerator,
                MigrationsCodeGeneratorSelector(
                    [|
                        CSharpMigrationsGenerator(
                            MigrationsCodeGeneratorDependencies(
                                sqlServerTypeMappingSource,
                                sqlServerAnnotationCodeGenerator),
                            CSharpMigrationsGeneratorDependencies(
                                code,
                                CSharpMigrationOperationGenerator(
                                    CSharpMigrationOperationGeneratorDependencies(
                                        code)),
                                CSharpSnapshotGenerator(
                                    CSharpSnapshotGeneratorDependencies(
                                        code, sqlServerTypeMappingSource, sqlServerAnnotationCodeGenerator))))
                    |]),
                historyRepository,
                reporter,
                MockProvider(),
                SnapshotModelProcessor(reporter, services.GetRequiredService<IModelRuntimeInitializer>()),
                Migrator(
                    migrationAssembly,
                    historyRepository,
                    services.GetRequiredService<IDatabaseCreator>(),
                    services.GetRequiredService<IMigrationsSqlGenerator>(),
                    services.GetRequiredService<IRawSqlCommandBuilder>(),
                    services.GetRequiredService<IMigrationCommandExecutor>(),
                    services.GetRequiredService<IRelationalConnection>(),
                    services.GetRequiredService<ISqlGenerationHelper>(),
                    services.GetRequiredService<ICurrentDbContext>(),
                    services.GetRequiredService<IModelRuntimeInitializer>(),
                    services.GetRequiredService<IDiagnosticsLogger<DbLoggerCategory.Migrations>>(),
                    services.GetRequiredService<IRelationalCommandDiagnosticsLogger>(),
                    services.GetRequiredService<IDatabaseProvider>())))

    [<Tests>]
    let MigrationsScaffolderTests =
        testList "" [

            test "ScaffoldMigration reuses model snapshot" {
                let scaffolder = createMigrationScaffolder<ContextWithSnapshot>()

                let migration = scaffolder.ScaffoldMigration("EmptyMigration", "WebApplication1")

                Expect.equal (nameof ContextWithSnapshotModelSnapshot)  migration.SnapshotName "Should be equal"
                Expect.equal typeof<ContextWithSnapshotModelSnapshot>.Namespace  migration.SnapshotSubnamespace  "Should be equal"
            }

            // test "ScaffoldMigration handles generic contexts" {
            //     let scaffolder = createMigrationScaffolder<GenericContext<int>>()

            //     let migration = scaffolder.ScaffoldMigration("EmptyMigration", "WebApplication1")

            //     Expect.equal "GenericContextModelSnapshot" migration.SnapshotName "Should be equal"
            // }

            test "ScaffoldMigration can override namespace" {
                let scaffolder = createMigrationScaffolder<ContextWithSnapshot>()

                let migration = scaffolder.ScaffoldMigration("EmptyMigration", null, "OverrideNamespace.OverrideSubNamespace")

                Expect.stringContains migration.MigrationCode "namespace OverrideNamespace.OverrideSubNamespace" "Should contain namespace"
                Expect.equal "OverrideNamespace.OverrideSubNamespace" migration.MigrationSubNamespace "Should be equal"

                Expect.stringContains migration.SnapshotCode "namespace OverrideNamespace.OverrideSubNamespace" "Should contain namespace"
                Expect.equal "OverrideNamespace.OverrideSubNamespace" migration.SnapshotSubnamespace "Should be equal"
            }

            test "ScaffoldMigration save works as expected" {
                let projectDir =  Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())

                Directory.CreateDirectory(projectDir) |> ignore

                let scaffolder = createMigrationScaffolder<ContextWithSnapshot>()
                let migration = scaffolder.ScaffoldMigration("EmptyMigration", "WebApplication1")

                let saveResult = scaffolder.Save(projectDir, migration, null)

                Expect.isTrue (File.Exists saveResult.MigrationFile) "MigrationFile should exist"
                Expect.isTrue (File.Exists saveResult.MetadataFile) "MetadataFile should exist"
                Expect.isTrue (File.Exists saveResult.SnapshotFile) "SnapshotFile should exist"

                Directory.Delete(projectDir, true)
            }

    ]
