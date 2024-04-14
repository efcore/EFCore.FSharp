module EntityFrameworkCore.FSharp.Test.DbContextHelperTests

open System
open System.ComponentModel.DataAnnotations
open EntityFrameworkCore.FSharp.DbContextHelpers
open Expecto
open Microsoft.EntityFrameworkCore

[<CLIMutable>]
type Blog = {
    [<Key>]
    Id: Guid
    Title: string
    Content: string
}

[<CLIMutable>]
type CompositeType = { Key1: int; Key2: int; Value: string }

type MyContext() =
    inherit DbContext()

    [<DefaultValue>]
    val mutable private _blogs: DbSet<Blog>

    [<DefaultValue>]
    val mutable private _composite: DbSet<CompositeType>

    member this.Blogs
        with get () = this._blogs
        and set v = this._blogs <- v

    member this.Composite
        with get () = this._composite
        and set v = this._composite <- v

    override __.OnConfiguring(options: DbContextOptionsBuilder) : unit =
        options.UseSqlite(sprintf "Data Source=%s.db" (Guid.NewGuid().ToString()))
        |> ignore

    override __.OnModelCreating(modelBuilder) =
        modelBuilder
            .Entity<CompositeType>()
            .HasKey(
                [|
                    "Key1"
                    "Key2"
                |]
            )
        |> ignore

let createContext () =
    let ctx = new MyContext()

    ctx.Database.EnsureDeleted()
    |> ignore

    ctx.Database.EnsureCreated()
    |> ignore

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

            addEntity ctx original
            saveChanges ctx

            let modified = { original with Title = "My New Title" }

            updateEntity ctx (fun b -> b.Id) modified
            |> ignore

            let expected = {
                Id = original.Id
                Title = "My New Title"
                Content = "My original content"
            }

            let found = tryFindEntity<Blog> ctx original.Id

            let actual = Expect.wantSome found "Should not be None"

            Expect.equal actual expected "Record in context should match"
        }

        test "Helpers work as expected for compsoite keys" {

            let original = {
                Key1 = 1
                Key2 = 2
                Value = "My original content"
            }

            use ctx = createContext ()

            addEntity ctx original
            saveChanges ctx

            let modified = {
                original with
                    Value = "My New Content"
            }

            updateEntity
                ctx
                (fun b -> [
                    box b.Key1
                    box b.Key2
                ])
                modified
            |> ignore

            let expected = {
                Key1 = 1
                Key2 = 2
                Value = "My New Content"
            }

            let key: obj[] = [|
                original.Key1
                original.Key2
            |]

            let found = tryFindEntity<CompositeType> ctx key

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
                    do! addEntityAsync ctx original
                    do! saveChangesAsync ctx

                    let modified = { original with Title = "My New Title" }

                    let! _ = updateEntityAsync ctx (fun b -> b.Id) modified

                    return! tryFindEntityAsync<Blog> ctx original.Id
                }
                |> Async.RunSynchronously

            let actual = Expect.wantSome found "Should not be None"

            Expect.equal actual expected "Record in context should match"
        }

        test "Async Task helpers work as expected" {

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
                    do!
                        addEntityTaskAsync ctx original
                        |> Async.AwaitTask

                    do!
                        saveChangesTaskAsync ctx
                        |> Async.AwaitTask

                    let modified = { original with Title = "My New Title" }

                    let! _ = updateEntityAsync ctx (fun b -> b.Id :> obj) modified

                    return!
                        tryFindEntityTaskAsync<Blog> ctx original.Id
                        |> Async.AwaitTask
                }
                |> Async.RunSynchronously

            let actual = Expect.wantSome found "Should not be None"

            Expect.equal actual expected "Record in context should match"
        }


        test "tryFindEntity returns None if no matching entry found" {
            use ctx = createContext ()
            let found = tryFindEntity<Blog> ctx (Guid.NewGuid())

            Expect.isNone found "Should be None"
        }

        test "tryFindEntityAsync returns None if no matching entry found" {
            use ctx = createContext ()

            let found =
                tryFindEntityAsync<Blog> ctx (Guid.NewGuid())
                |> Async.RunSynchronously

            Expect.isNone found "Should be None"
        }
    ]

[<Tests>]
let DbSetTests =
    testList "DbContextHelperTests dbSet tests" [

        test "toListAsync should return a list of blogs" {
            use ctx = createContext ()

            let blog = {
                Id = (Guid.NewGuid())
                Title = "My Title"
                Content = "My original content"
            }

            addEntity ctx blog
            saveChanges ctx

            let result =
                toListAsync ctx.Blogs
                |> Async.RunSynchronously

            Expect.equal [ blog ] result "Should be same"
        }


        test "tryFirstAsync should return the blog" {
            use ctx = createContext ()

            let blog = {
                Id = (Guid.NewGuid())
                Title = "My Title"
                Content = "My original content"
            }

            addEntity ctx blog
            saveChanges ctx

            let result =
                tryFirstAsync ctx.Blogs
                |> Async.RunSynchronously

            let actual = Expect.wantSome result "should have one"
            Expect.equal blog actual "Should be same"
        }

        test "tryFirstAsync should return None" {
            use ctx = createContext ()

            let result =
                tryFirstAsync ctx.Blogs
                |> Async.RunSynchronously

            Expect.isNone result "should have none"
        }

        test "tryFirst should return the blog" {
            use ctx = createContext ()

            let blog = {
                Id = (Guid.NewGuid())
                Title = "My Title"
                Content = "My original content"
            }

            addEntity ctx blog
            saveChanges ctx

            let result = tryFirst ctx.Blogs

            let actual = Expect.wantSome result "should have one"
            Expect.equal blog actual "Should be same"
        }

        test "tryFirst should return None" {
            use ctx = createContext ()
            let result = tryFirst ctx.Blogs
            Expect.isNone result "should have none"
        }

        test "tryFilterFirstAsync should return the blog" {
            use ctx = createContext ()
            let id = Guid.NewGuid()

            let blog = {
                Id = id
                Title = "My Title"
                Content = "My original content"
            }

            addEntity ctx blog
            saveChanges ctx

            let result =
                tryFilterFirstAsync <@ fun x -> x.Id = id @> ctx.Blogs
                |> Async.RunSynchronously

            let actual = Expect.wantSome result "should have one"
            Expect.equal blog actual "Should be same"
        }

        test "tryFilterFirstAsync should return None" {
            use ctx = createContext ()
            let id = Guid.NewGuid()

            let result =
                tryFilterFirstAsync <@ fun x -> x.Id = id @> ctx.Blogs
                |> Async.RunSynchronously

            Expect.isNone result "should have none"
        }

        test "tryFilterFirst should return the blog" {
            use ctx = createContext ()
            let id = Guid.NewGuid()

            let blog = {
                Id = id
                Title = "My Title"
                Content = "My original content"
            }

            addEntity ctx blog
            saveChanges ctx

            let result = tryFilterFirst <@ fun x -> x.Id = id @> ctx.Blogs

            let actual = Expect.wantSome result "should have one"
            Expect.equal blog actual "Should be same"
        }

        test "tryFilterFirst should return None" {
            use ctx = createContext ()
            let id = Guid.NewGuid()

            let result = tryFilterFirst <@ fun x -> x.Id = id @> ctx.Blogs

            Expect.isNone result "should have none"
        }


        test "TryFirstAsync extension should return the blog" {
            use ctx = createContext ()

            let blog = {
                Id = (Guid.NewGuid())
                Title = "My Title"
                Content = "My original content"
            }

            addEntity ctx blog
            saveChanges ctx

            let result =
                ctx.Blogs.TryFirstAsync()
                |> Async.RunSynchronously

            let actual = Expect.wantSome result "should have one"
            Expect.equal blog actual "Should be same"
        }

        test "TryFirstAsync extension should return None" {
            use ctx = createContext ()

            let result =
                ctx.Blogs.TryFirstAsync()
                |> Async.RunSynchronously

            Expect.isNone result "should have none"
        }


        test "TryFirstTaskAsync extension should return the blog" {
            use ctx = createContext ()

            let blog = {
                Id = (Guid.NewGuid())
                Title = "My Title"
                Content = "My original content"
            }

            addEntity ctx blog
            saveChanges ctx

            let result =
                ctx.Blogs.TryFirstTaskAsync()
                |> Async.AwaitTask
                |> Async.RunSynchronously

            let actual = Expect.wantSome result "should have one"
            Expect.equal blog actual "Should be same"
        }

        test "TryFirstTaskAsync extension should return None" {
            use ctx = createContext ()

            let result =
                ctx.Blogs.TryFirstTaskAsync()
                |> Async.AwaitTask
                |> Async.RunSynchronously

            Expect.isNone result "should have none"
        }


        test "TryFirst extension should return the blog" {
            use ctx = createContext ()

            let blog = {
                Id = (Guid.NewGuid())
                Title = "My Title"
                Content = "My original content"
            }

            addEntity ctx blog
            saveChanges ctx

            let result = ctx.Blogs.TryFirst()

            let actual = Expect.wantSome result "should have one"
            Expect.equal blog actual "Should be same"
        }

        test "TryFirst extension should return None" {
            use ctx = createContext ()
            let result = ctx.Blogs.TryFirst()
            Expect.isNone result "should have none"
        }

        test "TryFirstAsync extension with filter should return the blog" {
            use ctx = createContext ()
            let id = Guid.NewGuid()

            let blog = {
                Id = id
                Title = "My Title"
                Content = "My original content"
            }

            addEntity ctx blog
            saveChanges ctx

            let result =
                ctx.Blogs.TryFirstAsync(fun x -> x.Id = id)
                |> Async.RunSynchronously

            let actual = Expect.wantSome result "should have one"
            Expect.equal blog actual "Should be same"
        }


        test "TryFirstTaskAsync extension with filter should return the blog" {
            use ctx = createContext ()
            let id = Guid.NewGuid()

            let blog = {
                Id = id
                Title = "My Title"
                Content = "My original content"
            }

            addEntity ctx blog
            saveChanges ctx

            let result =
                ctx.Blogs.TryFirstTaskAsync(fun x -> x.Id = id)
                |> Async.AwaitTask
                |> Async.RunSynchronously

            let actual = Expect.wantSome result "should have one"
            Expect.equal blog actual "Should be same"
        }

        test "TryFirstAsync extension with filter should return None" {
            use ctx = createContext ()
            let id = Guid.NewGuid()

            let result =
                ctx.Blogs.TryFirstAsync(fun x -> x.Id = id)
                |> Async.RunSynchronously

            Expect.isNone result "should have none"
        }

        test "TryFirstTaskAsync extension with filter should return None" {
            use ctx = createContext ()
            let id = Guid.NewGuid()

            let result =
                ctx.Blogs.TryFirstTaskAsync(fun x -> x.Id = id)
                |> Async.AwaitTask
                |> Async.RunSynchronously

            Expect.isNone result "should have none"
        }

        test "TryFirst extension with filter should return the blog" {
            use ctx = createContext ()
            let id = Guid.NewGuid()

            let blog = {
                Id = id
                Title = "My Title"
                Content = "My original content"
            }

            addEntity ctx blog
            saveChanges ctx

            let result = ctx.Blogs.TryFirst(fun x -> x.Id = id)

            let actual = Expect.wantSome result "should have one"
            Expect.equal blog actual "Should be same"
        }

        test "TryFirst extension with filter should return None" {
            use ctx = createContext ()
            let id = Guid.NewGuid()
            let result = ctx.Blogs.TryFirst(fun x -> x.Id = id)
            Expect.isNone result "should have none"
        }
    ]
