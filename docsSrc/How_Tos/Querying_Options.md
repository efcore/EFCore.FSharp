# Querying option types

    [hide]
    #r "Microsoft.EntityFrameworkCore.Sqlite.dll"

    open System
    open System.ComponentModel.DataAnnotations
    open System.Linq
    open Microsoft.EntityFrameworkCore
    open EntityFrameworkCore.FSharp
    open EntityFrameworkCore.FSharp.DbContextHelpers

## Configuring

EF Core by default does not know anything about the way of F# deals with nullable values which is the Option type.

This project has a way to enable query for columns defined as an option, first you need to call `.UseFSharpTypes()` on the 
context configuration builder


```fsharp
open EntityFrameworkCore.FSharp.Extensions

[<CLIMutable>]
type Blog = {
    [<Key>]
    Id : Guid
    Title : string
    Content : string option
}

type MyContext () =
    inherit DbContext()

    [<DefaultValue>]
    val mutable private _blogs : DbSet<Blog>
    member this.Blogs with get() = this._blogs and set v = this._blogs <- v

    override _.OnModelCreating builder =
        builder.RegisterOptionTypes() // enables option values for all entities

    override _.OnConfiguring(options: DbContextOptionsBuilder) : unit =
           options.UseSqlite("Data Source=dbName.db")
                  .UseFSharpTypes()
           |> ignore

```

Note that the  `.UseFSharpTypes()` will exist independent of the database, the `UseSqlite` is just for the example. 
If you are using an SQL server with `UseSqlServer` or `UsePostgres` for Postgres, or any other database it will exist.

## Querying

With that, you will be able to query your entity by an optional column.

Querying entities with `Some` content
    
```fsharp
let queryWhereSome (ctx: MyContext) =

    // Querying entities with `Some` content
    let blogs =
        query {
            for blog in ctx.Blogs do
            where blog.Content.IsSome
            select blog
        }

    // or using Linq extensions
    let blog =
        ctx.Blogs.Where(fun x -> x.Content.IsSome)

    ()
```


Querying entities with `None` content

```fsharp
let queryWhereNone (ctx: MyContext) =

    let blogs =
        query {
            for blog in ctx.Blogs do
            where blog.Content.IsNone
            select blog
        }

    // or using Linq extensions
    let blog =
        ctx.Blogs.Where(fun x -> x.Content.IsNone)

    ()
```

Querying optional values by value

```fsharp
let queryWhereValue (ctx: MyContext) =

    let blogs =
        query {
            for blog in ctx.Blogs do
            where (blog.Content.Value = "Some text")
            select blog
        }

    // or using Linq extensions
    let blog =
        ctx.Blogs.Where(fun x -> x.Content.Value = "Some text")

    ()
```
