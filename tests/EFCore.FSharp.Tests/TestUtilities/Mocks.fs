namespace EntityFrameworkCore.FSharp.Test.TestUtilities

open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.EntityFrameworkCore.Design.Internal
open Microsoft.EntityFrameworkCore.Migrations
open Microsoft.EntityFrameworkCore.Storage

type MockHistoryRepository() =
    interface IHistoryRepository with

        member __.GetBeginIfExistsScript(migrationId) = null

        member __.GetBeginIfNotExistsScript(migrationId) = null

        member __.GetCreateScript() = null

        member __.GetCreateIfNotExistsScript() = null

        member __.GetEndIfScript() = null

        member __.Exists() = false

        member __.ExistsAsync(cancellationToken) = Task.FromResult(false)

        member __.GetAppliedMigrations() = null

        member __.GetAppliedMigrationsAsync(cancellationToken) =
            Task.FromResult<IReadOnlyList<HistoryRow>>(null)

        member __.GetDeleteScript(migrationId) = null

        member __.GetInsertScript(row) = null

type MockProvider() =
    interface IDatabaseProvider with
        member __.Name = "Mock.Provider"
        member __.IsConfigured(options) = true

type TestOperationReporter() =

    let messages = ResizeArray<string>()

    member __.Messages = messages

    member __.Clear() = messages.Clear()

    interface IOperationReporter with

        member __.WriteInformation(message) = messages.Add("info: " + message)

        member __.WriteVerbose(message) = messages.Add("verbose: " + message)

        member __.WriteWarning(message) = messages.Add("warn: " + message)

        member __.WriteError(message) = messages.Add("error: " + message)
