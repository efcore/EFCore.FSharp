namespace Bricelam.EntityFrameworkCore.FSharp

open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Scaffolding
open Microsoft.EntityFrameworkCore.Scaffolding.Internal
open Microsoft.Extensions.DependencyInjection

open Bricelam.EntityFrameworkCore.FSharp.Scaffolding
open Bricelam.EntityFrameworkCore.FSharp.Scaffolding.Internal
open Bricelam.EntityFrameworkCore.FSharp.Migrations.Design

type EFCoreFSharpServices() =
    interface IDesignTimeServices with
        member this.ConfigureDesignTimeServices(services: IServiceCollection) =
            services
                .AddSingleton<ICSharpDbContextGenerator, FSharpDbContextGenerator>()
                .AddSingleton<IModelCodeGenerator, FSharpModelGenerator>()
                .AddSingleton<IMigrationsScaffolder, FSharpMigrationsScaffolder>() |> ignore