namespace Bricelam.EntityFrameworkCore.FSharp

open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Scaffolding
open Microsoft.Extensions.DependencyInjection

type EFCoreFSharpServices() =
    interface IDesignTimeServices with
        member this.ConfigureDesignTimeServices(services: IServiceCollection) =
            services
                .AddSingleton<IModelCodeGenerator, FSharpModelGenerator>()
                .AddSingleton<IMigrationsCodeGenerator, FSharpMigrationsGenerator>() |> ignore