# Use RecordHelpers

EFCore.FSharp also includes a number of helper methods for allowing a more F#-like experience while interacting with a `DbContext`.

They can be found in `EntityFrameworkCore.FSharp.DbContextHelpers`

## Example Usages

Given a context with a `Blog` defined as such

    [hide]
    open System
    open System.ComponentModel.DataAnnotations
    open Microsoft.EntityFrameworkCore
    open EntityFrameworkCore.FSharp
    open EntityFrameworkCore.FSharp.DbContextHelpers

```fsharp
[<CLIMutable>]
type Blog = {
    [<Key>]
    Id : Guid
    Title : string
    Content : string
}

type MyContext () =
    inherit DbContext()

    member val Blogs : DbSet<Blog> = null with get, set
```

We can use the helper methods like so

```fsharp

let interactWithContext (ctx:MyContext) =

    let originalBlogPost = {
        Id = (Guid.NewGuid())
        Title = "My Title"
        Content = "My original content"
    }

    originalBlogPost |> addEntity ctx |> ignore
    saveChanges ctx |> ignore

    let blogPostReadFromDb =
        match tryFindEntity<Blog> ctx originalBlogPost.Id with
        | Some b -> b
        | None -> failwithf "Could not entity of type %A with identifier %A" typeof<Blog> originalBlogPost.Id

    let modifiedBlogPost = { blogPostReadFromDb with Content = "My updated content" }

    // This method is needed as we are "updating" the database with what is technically a new object
    // We specify the key, allowing us to accommodate composite keys 
    updateEntity ctx (fun b -> b.Id :> obj) modifiedBlogPost |> ignore

    saveChanges ctx |> ignore

    let updatedBlogFromDb =
        match tryFindEntity<Blog> ctx originalBlogPost.Id with
        | Some b -> b
        | None -> failwithf "Could not find entity of type %A with identifier %A" typeof<Blog> originalBlogPost.Id

    updatedBlogFromDb.Content = "My updated content" // This will be true
```

An advantage of the generalised `updateEntity` function is that it allows us to use partial application to easily create methods for specific entities. For example:

```fsharp

let ctx = new MyContext()

// Partially applied function, all that it needs now is the Blog entity
let updateBlog = updateEntity ctx (fun (b:Blog) -> b.Id :> obj)

let myBlog = ctx.Blogs |> Seq.head

// No need to specify context or key, that's already been done
updateBlog { myBlog with Content = "Updated content" }

```

## Async methods

All methods in `EntityFrameworkCore.FSharp.DbContextHelpers` also have Async variants with support for `async { ... }` expressions
