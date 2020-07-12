namespace EntityFrameworkCore.FSharp.Test.TestUtilities

open Microsoft.EntityFrameworkCore.Metadata.Conventions
open Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.EntityFrameworkCore.Diagnostics
open System.Diagnostics
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Logging
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure

type FakeDiagnosticsLogger<'a when 'a :> LoggerCategory<'a> and 'a : (new : unit -> 'a)>() =
    
    interface ILogger with
        member this.BeginScope<'state> (state : 'state) = null
        member this.IsEnabled logLevel = true
        member this.Log<'state> (logLevel, eventId, (state : 'state), ex, formatter) =
            ()

    
    interface IDiagnosticsLogger<'a> with
        member this.Definitions: LoggingDefinitions = 
            raise (System.NotImplementedException())

        member this.DiagnosticSource: DiagnosticSource = 
            new DiagnosticListener("Fake") :> _

        member this.Interceptors: IInterceptors = 
            raise (System.NotImplementedException())

        member this.Logger: ILogger = 
            this :> _

        member this.Options: ILoggingOptions = 
            LoggingOptions() :> _

        member this.ShouldLogSensitiveData(): bool = 
            false

