namespace Bricelam.EntityFrameworkCore.FSharp.Test.TestUtilities

open Microsoft.EntityFrameworkCore.Metadata.Conventions
open Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.EntityFrameworkCore.Diagnostics
open System.Diagnostics
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Logging
open Microsoft.EntityFrameworkCore.Internal

type FakeDiagnosticsLogger<'a when 'a :> LoggerCategory<'a> and 'a : (new : unit -> 'a)>() =
    
    interface ILogger with
        member this.BeginScope<'state> (state : 'state) = null
        member this.IsEnabled logLevel = true
        member this.Log<'state> (logLevel, eventId, (state : 'state), ex, formatter) =
            ()

    
    interface IDiagnosticsLogger<'a> with

        member this.DiagnosticSource = new DiagnosticListener("Fake") :> _
        member this.GetLogBehavior (eventId, logLevel) = WarningBehavior.Log
        member this.Logger = this :> _
        member this.Options = LoggingOptions() :> _
        member this.ShouldLogSensitiveData () = false

    

type TestRelationalConventionSetBuilder (dependencies) =
    inherit RelationalConventionSetBuilder(dependencies)

    static member Build () =
        TestRelationalConventionSetBuilder(
            RelationalConventionSetBuilderDependencies(
                TestRelationalTypeMappingSource(
                    TestServiceFactory.create<TypeMappingSourceDependencies>([]),
                    TestServiceFactory.create<RelationalTypeMappingSourceDependencies>([])
                ),
                FakeDiagnosticsLogger<DbLoggerCategory.Model>(),
                null,
                null,
                null))
            .AddConventions(
                TestServiceFactory.create<CoreConventionSetBuilder>([])
                    .CreateConventionSet());



