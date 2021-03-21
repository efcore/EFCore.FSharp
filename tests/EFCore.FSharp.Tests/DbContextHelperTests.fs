module EntityFrameworkCore.FSharp.Test.DbContextHelperTests


open System
open System.ComponentModel.DataAnnotations
open EntityFrameworkCore.FSharp.DbContextHelpers
open Expecto
open Microsoft.EntityFrameworkCore

[<CLIMutable>]
type Blog = {
    [<Key>]
    Id : Guid
    Title : string
    Content : string
}

type MyContext () =
    inherit DbContext()

    [<DefaultValue>]
    val mutable private _blogs : DbSet<Blog>
    member this.Blogs with get() = this._blogs and set v = this._blogs <- v

    override __.OnConfiguring(options: DbContextOptionsBuilder) : unit =
           options.UseSqlite("Data Source=dbContextHelperTests.db") |> ignore

let createContext b =
    let ctx = new MyContext()

    ctx.Database.EnsureDeleted() |> ignore
    ctx.Database.EnsureCreated() |> ignore

    ctx.Blogs.Add b |> ignore
    ctx.SaveChanges() |> ignore
    ctx

[<Tests>]
let DbContextHelperTests =
    testList "DbContextHelperTests" [

        test "updateEntity works as expected" {

            let original = {
                Id = (Guid.NewGuid())
                Title = "My Title"
                Content = "My original content"
            }

            use ctx = createContext original

            let modified = { original with Title = "My New Title" }

            updateEntity ctx (fun b -> b.Id :> obj) modified |> ignore

            let expected = {
                Id = original.Id
                Title = "My New Title"
                Content = "My original content"
            }

            let actual = ctx.Blogs.Find original.Id

            Expect.equal actual expected "Record in context should match"
        }
    ]
