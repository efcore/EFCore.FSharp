# Single case union types (aka Simple types)
It's common to use single case discriminated unions to meaningfully represent data values.
But EF does not know anything about this kind of type. Luckily this repository has some ways to help you to deal with they.


    [hide]
    #r "Microsoft.EntityFrameworkCore.Sqlite.dll"

    open System
    open System.ComponentModel.DataAnnotations
    open System.Linq
    open Microsoft.EntityFrameworkCore
    open EntityFrameworkCore.FSharp
    open EntityFrameworkCore.FSharp.DbContextHelpers

## Configuring

We have two approaches to deal with single case union types which are a converter or an extension that searches for all Single Case Unions in your entities.



```fsharp
open EntityFrameworkCore.FSharp.Extensions

type PositiveInteger = PositiveInteger of int

[<CLIMutable>]
type Blog = {
    [<Key>]
    Id : Guid
    Title : string
    Votes: PositiveInteger
}

type MyContext () =
    inherit DbContext()

    [<DefaultValue>]
    val mutable private _blogs : DbSet<Blog>
    member this.Blogs with get() = this._blogs and set v = this._blogs <- v

    override _.OnModelCreating builder =
       
        // setting manually each property
        builder.Entity<Blog>()
            .Property(fun x -> x.Votes)
            .HasConversion(SingleCaseUnionConverter<int, PositiveInteger>())
        |> ignore
        
        // OR
        
        // enables single clase unions for all entities
        builder.RegisterSingleUnionCases() 

    override _.OnConfiguring(options: DbContextOptionsBuilder) : unit =
           options
             .UseSqlite( "Data Source=dbName.db")
             .UseFSharpTypes() // enable queries for F# types
           |> ignore

```

## Querying

You can query for equality without any problem

    [hide]
    let ctx = new MyContext()

```fsharp
let blog =
   query {
       for blog in ctx.Blogs do
       where (blog.Votes = PositiveInteger 10)
       select blog
       headOrDefault
   }
   
 // or  
let blog' = ctx.Blogs.Where(fun b -> b.Votes = PositiveInteger 10).FirstOrDefault()
```


For querying with other types of operation you will need to unwrap the value inside the query

```fsharp
let blogQuery =
   query {
       for blog in ctx.Blogs do
       let (PositiveInteger votes) = blog.Votes
       where (votes > 0)
       select blog
       headOrDefault
   }
```

## Private Constructor

This extension doesn't support private union case constructors. 
