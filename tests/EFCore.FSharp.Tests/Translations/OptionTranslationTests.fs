module EntityFrameworkCore.FSharp.Test.OptionTranslationTests

open System
open System.Linq
open System.ComponentModel.DataAnnotations
open EntityFrameworkCore.FSharp.DbContextHelpers
open EntityFrameworkCore.FSharp.Extensions
open Expecto
open Microsoft.EntityFrameworkCore

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

    override _.OnConfiguring(options: DbContextOptionsBuilder) : unit =
           options.UseSqlite($"Data Source={Guid.NewGuid().ToString()}.db")
                  .UseFSharpTypes()
           |> ignore

    override _.OnModelCreating builder =
        builder.RegisterOptionTypes()

let createContext () =
    let ctx = new MyContext()
    ctx.Database.EnsureDeleted() |> ignore
    ctx.Database.EnsureCreated() |> ignore
    ctx

let blogWithContent = { Id = Guid.NewGuid(); Title = "My Title"; Content = Some "Some text" }
let blogWithoutContent = { Id = Guid.NewGuid(); Title = "My Title"; Content = None }

let saveBlogs ctx =
    [blogWithContent
     blogWithoutContent]
    |> List.iter (addEntity ctx >> ignore)
    saveChanges ctx |> ignore

[<Tests>]
let OptionTranslationQueryTests =
    testList "OptionTranslationTests with query" [

        test "Filter content optional property with IsSome should return a blog with content" {
           use ctx = createContext ()
           saveBlogs ctx

           let blog =
               query {
                   for blog in ctx.Blogs do
                   where blog.Content.IsSome
                   select blog
                   headOrDefault
               }

           Expect.equal blog blogWithContent "Record in context should match"
        }


        test "Filter content optional property with IsNone should return a blog without content" {
           use ctx = createContext ()
           saveBlogs ctx

           let blog =
               query {
                   for blog in ctx.Blogs do
                   where blog.Content.IsNone
                   select blog
                   headOrDefault
               }

           Expect.equal blog blogWithoutContent "Record in context should match"
        }

        test "Filter content optional property by value" {
           use ctx = createContext ()
           saveBlogs ctx
           let blog =
               query {
                   for blog in ctx.Blogs do
                   where (blog.Content.Value = "Some text")
                   select blog
                   headOrDefault
               }

           Expect.equal blog blogWithContent "Record in context should match"
        }
    ]


[<Tests>]
let OptionTranslationLinqMethodsTests =
    testList "OptionTranslationTests with LINQ Methods" [

        test "Filter content optional property with IsSome should return a blog with content" {
           use ctx = createContext ()
           saveBlogs ctx

           let blog = ctx.Blogs.Where(fun x -> x.Content.IsSome).FirstOrDefault()

           Expect.equal blog blogWithContent "Record in context should match"
        }


        test "Filter content optional property with IsNone should return a blog without content" {
           use ctx = createContext ()
           saveBlogs ctx

           let blog = ctx.Blogs.Where(fun x -> x.Content.IsNone).FirstOrDefault()

           Expect.equal blog blogWithoutContent "Record in context should match"
        }

        test "Filter content optional property by value" {
           use ctx = createContext ()
           saveBlogs ctx
           let blog = ctx.Blogs.Where(fun x -> x.Content.Value = "Some text").FirstOrDefault()

           Expect.equal blog blogWithContent "Record in context should match"
        }
    ]




