# Many to many relationships

To configure a many to many relationship directly on a type we need to use recursive types.

```fsharp
open System.Linq
open Microsoft.EntityFrameworkCore
open EntityFrameworkCore.FSharp.Extensions

// Use ResizeArray to avoid Unhandled exception. System.NotSupportedException: Collection was of a fixed size.
[<CLIMutable>]
type Post =
    { PostId : int
      Title : string
      Content : string
      Tags : Tag ResizeArray } 
and [<CLIMutable>] Tag =
    { TagId : string
      Posts : Post ResizeArray }

type MyContext () =
  inherit DbContext()
    
  [<DefaultValue>]
  val mutable posts : DbSet<Post>
  member this.Posts with get() = this.posts and set v = this.posts <- v
  
  [<DefaultValue>]
  val mutable tags : DbSet<Tag>
  member this.Tags with get() = this.tags and set v = this.tags <- v
  
  override _.OnModelCreating builder =
      builder.RegisterOptionTypes() // enables option values for all entities

  override __.OnConfiguring(options: DbContextOptionsBuilder) : unit =
      options.UseSqlite("Data Source=blogging.db") |> ignore

let ctx = new MyContext()

let tag = { TagId = "Sunset"; Posts = [] |> ResizeArray }
let post =
    { PostId = 100
      Title = "title"
      Content = "Content"
      Tags = [ tag ] |> ResizeArray }

ctx.Posts.Add post |> ignore
ctx.SaveChanges() |> ignore

let posts = ctx.Posts.Include(fun p -> p.Tags).ToList() |> Seq.toList // from c# to f# list
```

A limitation is that the types have to be declared in the same file.
