namespace EntityFrameworkCore.FSharp

open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Scaffolding
open Microsoft.EntityFrameworkCore.Scaffolding.Internal
open Microsoft.Extensions.DependencyInjection

open EntityFrameworkCore.FSharp
open EntityFrameworkCore.FSharp.Scaffolding
open EntityFrameworkCore.FSharp.Scaffolding.Internal
open EntityFrameworkCore.FSharp.Migrations.Design
open EntityFrameworkCore.FSharp.Internal

type EFCoreFSharpServices(scaffoldOptions: ScaffoldOptions) =

    new() = EFCoreFSharpServices(ScaffoldOptions.Default)

    static member Default =
        EFCoreFSharpServices() :> IDesignTimeServices

    static member WithScaffoldOptions scaffoldOptions =
        EFCoreFSharpServices scaffoldOptions :> IDesignTimeServices

    interface IDesignTimeServices with
        member __.ConfigureDesignTimeServices(services: IServiceCollection) =
            services
                .AddSingleton<ScaffoldOptions>(scaffoldOptions)
                .AddSingleton<ICSharpHelper, FSharpHelper>()
                .AddSingleton<ICSharpEntityTypeGenerator, FSharpEntityTypeGenerator>()
                .AddSingleton<ICSharpDbContextGenerator, FSharpDbContextGenerator>()
                .AddSingleton<IModelCodeGenerator, FSharpModelGenerator>()
                .AddSingleton<ICSharpMigrationOperationGenerator, FSharpMigrationOperationGenerator>()
                .AddSingleton<ICSharpSnapshotGenerator, FSharpSnapshotGenerator>()
                .AddSingleton<IMigrationsCodeGenerator, FSharpMigrationsGenerator>()
                .AddSingleton<IMigrationsScaffolder, FSharpMigrationsScaffolder>()
                .AddSingleton<FSharpMigrationsGeneratorDependencies>()
            |> ignore
