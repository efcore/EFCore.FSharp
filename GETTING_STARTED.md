# Notes

This guide is for a simple single-project setup rather than a production-ready topology, however it should hopefully assist with the basics.

# Prerequisites

This guide assumes:

- You have the `.NET Core SDK` installed (tested with version 3.1, though it may work with other versions)
- You have `SQLite` installed and a basic knowledge of how to explore a SQLite database

# Setup

## Build the NuGet package

This package will be published to NuGet at some point. In the meantime, it must be built manually.

1. Clone this repository
1. From the command line, run `./build.sh` (Linux/Mac) or `.\build.cmd` (Windows)
1. The built NuGet package will be located in the `dist` folder in the root of the repository, named something like `EntityFrameworkCore.FSharp.3.1.5-alpha1.nupkg`

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

## Add references to our NuGet package and Entity Framework provider-specific NuGet package

1. Create a directory to store the NuGet package we created earlier and copy it in

    ```
    md packages-local
    cp {LocationOfLocalRepo}/dist/EntityFrameworkCore.FSharp.3.1.5-alpha1.nupkg ./packages-local
    ```

1. In our `paket.dependencies` file, reference this and the NuGet package for our desired Entity Framework provider

    ```
    source ./packages-local
    nuget EntityFrameworkCore.FSharp
    nuget Microsoft.EntityFrameworkCore.Sqlite
    ```

1. Include these packages by adding the following lines to `paket.references`

    ```
    EntityFrameworkCore.FSharp
    Microsoft.EntityFrameworkCore.Sqlite
    ```

1. Run the `Paket` tool to install dependencies

    `paket install`

## Add Design Time Services interface to our codebase

1. Create a file e.g. `DesignTimeServices.fs` and add this to your project (`.fsproj`) file
1. Paste in the following content

    ```fsharp
    module DesignTimeServices

    open Microsoft.Extensions.DependencyInjection
    open Microsoft.EntityFrameworkCore.Design
    open EntityFrameworkCore.FSharp

    type DesignTimeServices() =
        interface IDesignTimeServices with 
            member __.ConfigureDesignTimeServices(serviceCollection: IServiceCollection) = 
                let fSharpServices= EFCoreFSharpServices() :> IDesignTimeServices
                fSharpServices.ConfigureDesignTimeServices serviceCollection
                ()
    ```

# Create the database

We will use a very simple structure. Initially, a blog will simply have an ID and a URL. We will then update our model to allow each blog to have multiple blog posts.

For this example we will use record types, but "normal" classes will also work if this better suits your needs.

## Create the model

1. Create a file for our model e.g. `BloggingModel.fs` and add it to the project (`.fsproj`) file
1. Paste in the following content. Note that our record type is marked `CLIMutable`, and that we initialise our DbSet using `Unchecked.defaultof`

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
        member __.Blogs with get() = __.blogs and set v = __.blogs <- v

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
        <Compile Include="DesignTimeServices.fs" />
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
        member __.Blogs : DbSet<Blog> = Unchecked.defaultof<DbSet<Blog>>
        member __.Posts : DbSet<Post> = Unchecked.defaultof<DbSet<Post>>

        override __.OnConfiguring(options: DbContextOptionsBuilder) : unit =
            options.UseSqlite("Data Source=blogging.db") |> ignore
    ```

1. Add a new migration

    `dotnet ef migrations add AddPostsTable`

1. When we look at the `AddPostsTable` migration in our `Migrations` folder, we should see in the `Up` method that it has successfully inferred the foreign key relationship. Once we've added this file to our `.fsproj`, we can run migrations:

    `dotnet ef database update`

We should now be able to see the new `Posts` table in our database, complete with a foreign key relationship to the `Blogs` table.
