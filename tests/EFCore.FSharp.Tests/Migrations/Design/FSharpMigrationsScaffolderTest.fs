namespace EntityFrameworkCore.FSharp.Test.Migrations.Design

open System.Collections.Generic
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

type MockHistoryRepository () =
    interface IHistoryRepository with

        member __.GetBeginIfExistsScript(migrationId) = null

        member __.GetBeginIfNotExistsScript(migrationId) = null

        member __.GetCreateScript() = null

        member __.GetCreateIfNotExistsScript() = null

        member __.GetEndIfScript() = null

        member __.Exists() = false

        member __.ExistsAsync(cancellationToken) = Task.FromResult(false)

        member __.GetAppliedMigrations() = null

        member __.GetAppliedMigrationsAsync(cancellationToken) = Task.FromResult<IReadOnlyList<HistoryRow>>(null)

        member __.GetDeleteScript(migrationId) = null

        member __.GetInsertScript(row) = null

type MockProvider () =
    interface IDatabaseProvider with
        member __.Name = "Mock.Provider"
        member __.IsConfigured (options) = true

type TestOperationReporter  () =

    let messages = ResizeArray<string>();

    member __.Messages = messages

    member __.Clear() = messages.Clear()

    interface IOperationReporter with

        member __.WriteInformation(message) = messages.Add("info: " + message)

        member __.WriteVerbose(message) = messages.Add("verbose: " + message)

        member __.WriteWarning(message) = messages.Add("warn: " + message)

        member __.WriteError(message) = messages.Add("error: " + message)

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
        let model = Model()
        model.[RelationalAnnotationNames.RelationalModel] <- RelationalModel(model)

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
                SnapshotModelProcessor(reporter, services.GetRequiredService<IConventionSetBuilder>()),
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
                    services.GetRequiredService<IConventionSetBuilder>(),
                    services.GetRequiredService<IDiagnosticsLogger<DbLoggerCategory.Migrations>>(),
                    services.GetRequiredService<IDiagnosticsLogger<DbLoggerCategory.Database.Command>>(),
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

    ]
