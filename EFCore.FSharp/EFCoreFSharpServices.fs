namespace Bricelam.EntityFrameworkCore.FSharp

open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Scaffolding
open Microsoft.Extensions.DependencyInjection

open Bricelam.EntityFrameworkCore.FSharp.Scaffolding

type EFCoreFSharpServices() =
    interface IDesignTimeServices with
        member this.ConfigureDesignTimeServices(services: IServiceCollection) =
            services
                .AddSingleton<IFSharpDbContextGenerator, FSharpDbContextGenerator>()
                .AddSingleton<IModelCodeGenerator, FSharpModelGenerator>()
                .AddSingleton<IMigrationsCodeGenerator, FSharpMigrationsGenerator>() |> ignore