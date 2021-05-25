# Notes

This guide is for a simple single-project setup rather than a production-ready topology, however it should hopefully assist with the basics.

# Prerequisites

This guide assumes:

- You have the `.NET Core SDK` installed (tested with version 3.1, though it may work with other versions)
- You have `SQLite` installed and a basic knowledge of how to explore a SQLite database

# Setup

## Create a new dotnet project and install dotnet tools

We will use `Paket` for package management, though you can apply the same principles with `NuGet`.

1. Create a new folder for the project and open the directory

    ```
    md FsEfTest
    cd FsEfTest
    ```

1. Create a new F# console application project

    `dotnet new console -lang F#`

1. Add a tool manifest, then add the `Entity Framework` and `Paket` tools

    ```
    dotnet new tool-manifest
    dotnet tool install dotnet-ef
    dotnet tool install paket
    ```

1. Convert the project to use `Paket`

    `paket convert-from-nuget`

## Installing the package
paket

```bash
paket install EntityFrameworkCore.FSharp
```

dotnet CLI

```bash
dotnet add package EntityFrameworkCore.FSharp
```

# Create the database

We will use a very simple structure. Initially, a blog will simply have an ID and a URL. We will then update our model to allow each blog to have multiple blog posts.

For this example we will use record types, but "normal" classes will also work if this better suits your needs.

## Create the model

1. Create a file for our model e.g. `BloggingModel.fs` and add it to the project (`.fsproj`) file
1. Paste in the following content. Note that our record type is marked `CLIMutable`, and that our DbSet has a backing field and is initialised with the `DefaultValue` attribute.

    ```fsharp
    module BloggingModel

    open System.ComponentModel.DataAnnotations
    open Microsoft.EntityFrameworkCore

    [<CLIMutable>]
    type Blog = {
        [<Key>] Id: int
        Url: string
    }

    type BloggingContext() =  
        inherit DbContext()
        
        [<DefaultValue>] val mutable blogs : DbSet<Blog>
        member this.Blogs with get() = this.blogs and set v = this.blogs <- v

        override _.OnModelCreating builder =
            builder.RegisterOptionTypes() // enables option values for all entities

        override __.OnConfiguring(options: DbContextOptionsBuilder) : unit =
            options.UseSqlite("Data Source=blogging.db") |> ignore
    ```

## Add and run migrations

1. From the root of the project folder, run

    `dotnet ef migrations add Initial`

1. Now run migrations

    `dotnet ef database update`

1. Oh... that didn't work. No migrations were applied. Let's try again with verbose output

    `dotnet ef database update -v`

1. The `-v` option is useful for diagnosing issues. We should see that the `BloggingContext` is found and the `Migrations` folder was created successfully, with our snapshot and migration. Now we simply need to add references to these to our `.fsproj` file manually - in the correct order - so we will end up with something like this

    ```xml
    <ItemGroup>
        <Compile Include="BloggingModel.fs" />
        <Compile Include="Migrations/BloggingContextModelSnapshot.fs" />
        <Compile Include="Migrations/20200711141125_Initial.fs" />
        <Compile Include="Program.fs" />
    </ItemGroup>
    ```

1. Now, we can run migrations

    `dotnet ef database update`

If we explore our SQLite database, we should now see the `Blogs` table has been created.

# Update the model

Let's update our model so that there is a relationship between blogs and posts.

1. Update the `BloggingModel.fs` to have a `Posts` record type

    ```fsharp
    [<CLIMutable>] 
    type Blog = { [<Key>] Id: int; Url: string }

    [<CLIMutable>] 
    type Post = {
        [<Key>] Id: int
        Title: string
        BlogId: int
        Blog: Blog
    }

    type BloggingContext() =  
        inherit DbContext()

        [<DefaultValue>] val mutable blogs : DbSet<Blog>
        member this.Blogs with get() = this.blogs and set v = this.blogs <- v

        [<DefaultValue>] val mutable posts : DbSet<Post>
        member this.Posts with get() = this.posts and set v = this.posts <- v

        override _.OnModelCreating builder =
            builder.RegisterOptionTypes() // enables option values for all entities

        override __.OnConfiguring(options: DbContextOptionsBuilder) : unit =
            options.UseSqlite("Data Source=blogging.db") |> ignore
    ```

1. Add a new migration

    `dotnet ef migrations add AddPostsTable`

1. When we look at the `AddPostsTable` migration in our `Migrations` folder, we should see in the `Up` method that it has successfully inferred the foreign key relationship. Once we've added this file to our `.fsproj`, we can run migrations:

    `dotnet ef database update`

We should now be able to see the new `Posts` table in our database, complete with a foreign key relationship to the `Blogs` table.
