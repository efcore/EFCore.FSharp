namespace EntityFrameworkCore.FSharp.Test.TestUtilities

open Microsoft.Data.SqlClient
open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.TestUtilities

type SqlServerTestHelpers private () =

    inherit TestHelpers()

    static member Instance = SqlServerTestHelpers()

    override _.AddProviderServices services = services.AddEntityFrameworkSqlServer()

    override _.UseProviderOptions optionsBuilder =
        optionsBuilder.UseSqlServer(new SqlConnection("Database=DummyDatabase"))
        |> ignore
