namespace EntityFrameworkCore.FSharp.Test.TestUtilities

open Microsoft.EntityFrameworkCore.Diagnostics
open System.Diagnostics
open Microsoft.Extensions.Logging
open Microsoft.EntityFrameworkCore.Diagnostics.Internal

type FakeDiagnosticsLogger<'a when 'a :> LoggerCategory<'a> and 'a: (new: unit -> 'a)>() =

    interface ILogger with
        member this.BeginScope<'state>(state: 'state) = null
        member this.IsEnabled logLevel = true
        member this.Log<'state>(logLevel, eventId, (state: 'state), ex, formatter) = ()


    interface IDiagnosticsLogger<'a> with
        member this.Definitions: LoggingDefinitions = raise (System.NotImplementedException())

        member this.DiagnosticSource: DiagnosticSource = new DiagnosticListener("Fake") :> _

        member this.Interceptors: IInterceptors = raise (System.NotImplementedException())

        member this.Logger: ILogger = this :> _

        member this.Options: ILoggingOptions = LoggingOptions() :> _

        member this.ShouldLogSensitiveData() : bool = false

        member this.DbContextLogger = failwith "todo"

        member this.DispatchEventData
            (
                definition,
                eventData,
                diagnosticSourceEnabled,
                simpleLogEnabled
            ) =
            failwith "todo"

        member this.NeedsEventData(definition, diagnosticSourceEnabled, simpleLogEnabled) =
            failwith "todo"

        member this.NeedsEventData
            (
                definition,
                interceptor,
                diagnosticSourceEnabled,
                simpleLogEnabled
            ) =
            failwith "todo"

        member this.ShouldLog(definition) = failwith "todo"
