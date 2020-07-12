namespace EntityFrameworkCore.FSharp.Test.TestUtilities

open Microsoft.Data.SqlClient
open Microsoft.EntityFrameworkCore.Diagnostics
open Microsoft.EntityFrameworkCore.SqlServer.Diagnostics.Internal
open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.DependencyInjection.Extensions
open Microsoft.EntityFrameworkCore.Infrastructure
open System
open Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata.Conventions

type SqlServerTestHelpers() =
    static member CreateConventionBuilder skipValidation =
        let createServiceProvider (customServices: IServiceCollection option) (addProviderServices: IServiceCollection -> IServiceCollection) =
            let services = ServiceCollection()
            addProviderServices services |> ignore

            match customServices with 
            | Some c -> 
                c
                |> Seq.iter(fun s -> services.Add(s) |> ignore)
            | None -> ()

            services.BuildServiceProvider()

        let useProviderOptions (optionsBuilder: DbContextOptionsBuilder) = 
            optionsBuilder.UseSqlServer(new SqlConnection("Database=DummyDatabase"));

        let createOptions serviceProvider =
            let optionsBuilder = 
                DbContextOptionsBuilder()
                    .UseInternalServiceProvider(serviceProvider)

            useProviderOptions optionsBuilder |> ignore

            optionsBuilder.Options

        let createContext() = 
            let services = 
                createServiceProvider None (fun s -> s.AddEntityFrameworkSqlServer()) |> createOptions        
            new DbContext(services)

        let createContextServices() = 
            (createContext() :> (IInfrastructure<IServiceProvider>)).Instance

        let conventionSet = 
            createContextServices().GetRequiredService<IConventionSetBuilder>().CreateConventionSet();

        if skipValidation then
            ConventionSet.Remove(conventionSet.ModelFinalizedConventions, typeof<ValidatingConvention>) |> ignore

        ModelBuilder(conventionSet)
