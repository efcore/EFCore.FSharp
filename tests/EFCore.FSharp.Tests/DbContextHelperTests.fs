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
           options.UseSqlite(sprintf "Data Source=%s.db" (Guid.NewGuid().ToString())) |> ignore

let createContext () =
    let ctx = new MyContext()

    ctx.Database.EnsureDeleted() |> ignore
    ctx.Database.EnsureCreated() |> ignore

    ctx

[<Tests>]
let DbContextHelperTests =
    testList "DbContextHelperTests" [

        test "Helpers work as expected" {

            let original = {
                Id = (Guid.NewGuid())
                Title = "My Title"
                Content = "My original content"
            }

            use ctx = createContext ()

            addEntity ctx original |> ignore
            saveChanges ctx |> ignore

            let modified = { original with Title = "My New Title" }

            updateEntity ctx (fun b -> b.Id :> obj) modified |> ignore

            let expected = {
                Id = original.Id
                Title = "My New Title"
                Content = "My original content"
            }

            let found = tryFindEntity<Blog> ctx original.Id

            let actual = Expect.wantSome found "Should not be None"
            Expect.equal actual expected "Record in context should match"
        }

        test "Async helpers work as expected" {

            let original = {
                Id = (Guid.NewGuid())
                Title = "My Title"
                Content = "My original content"
            }

            let expected = {
                Id = original.Id
                Title = "My New Title"
                Content = "My original content"
            }

            use ctx = createContext ()

            let found =
                async {
                    let! _ = addEntityAsync ctx original
                    let! _ = saveChangesAsync ctx

                    let modified = { original with Title = "My New Title" }

                    let! _ = updateEntityAsync ctx (fun b -> b.Id :> obj) modified

                    return! tryFindEntityAsync<Blog> ctx original.Id
                } |> Async.RunSynchronously

            let actual = Expect.wantSome found "Should not be None"
            Expect.equal actual expected "Record in context should match"
        }

        test "tryFindEntity returns None if no matching entry found" {
            use ctx = createContext()
            let found = tryFindEntity<Blog> ctx (Guid.NewGuid())

            Expect.isNone found "Should be None"
        }

        test "tryFindEntityAsync returns None if no matching entry found" {
            use ctx = createContext()
            let found = tryFindEntityAsync<Blog> ctx (Guid.NewGuid()) |> Async.RunSynchronously

            Expect.isNone found "Should be None"
        }
    ]
