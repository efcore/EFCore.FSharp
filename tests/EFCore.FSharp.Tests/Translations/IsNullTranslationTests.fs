module EntityFrameworkCore.FSharp.Test.IsNullTranslationTests

open System
open System.Linq
open System.ComponentModel.DataAnnotations
open EntityFrameworkCore.FSharp.DbContextHelpers
open EntityFrameworkCore.FSharp.Extensions
open Expecto
open Microsoft.EntityFrameworkCore

[<CLIMutable>]
type Blog =
    { [<Key>]
      Id: Guid
      Title: string
      Content: string }

type MyContext() =
    inherit DbContext()

    [<DefaultValue>]
    val mutable private _blogs: DbSet<Blog>

    member this.Blogs
        with get () = this._blogs
        and set v = this._blogs <- v

    override _.OnConfiguring(options: DbContextOptionsBuilder) : unit =
        options
            .UseSqlite($"Data Source={Guid.NewGuid().ToString()}.db")
            .UseFSharpTypes()
        |> ignore

    override _.OnModelCreating builder = builder.RegisterOptionTypes()

let createContext () =
    let ctx = new MyContext()
    ctx.Database.EnsureDeleted() |> ignore
    ctx.Database.EnsureCreated() |> ignore
    ctx

let blogWithContent =
    { Id = Guid.NewGuid()
      Title = "My Title"
      Content = "Some text" }

let blogWithoutContent =
    { Id = Guid.NewGuid()
      Title = "My Title"
      Content = null }

let saveBlogs ctx =
    [ blogWithContent; blogWithoutContent ]
    |> List.iter (addEntity ctx)

    saveChanges ctx

[<Tests>]
let OptionTranslationQueryTests =
    testList
        "IsNullTranslationTests with query"
        [

          test "Filter content nullable property with isNull should return a blog with content" {
              use ctx = createContext ()
              saveBlogs ctx

              let blog =
                  query {
                      for blog in ctx.Blogs do
                          where (not (isNull blog.Content))
                          select blog
                          headOrDefault
                  }

              Expect.equal blog blogWithContent "Record in context should match"
          }


          test "Filter content nullable property with IsNone should return a blog without content" {
              use ctx = createContext ()
              saveBlogs ctx

              let blog =
                  query {
                      for blog in ctx.Blogs do
                          where (isNull blog.Content)
                          select blog
                          headOrDefault
                  }

              Expect.equal blog blogWithoutContent "Record in context should match"
          }

          test "Filter content nullable property by value" {
              use ctx = createContext ()
              saveBlogs ctx

              let blog =
                  query {
                      for blog in ctx.Blogs do
                          where (blog.Content = "Some text")
                          select blog
                          headOrDefault
                  }

              Expect.equal blog blogWithContent "Record in context should match"
          } ]


[<Tests>]
let OptionTranslationLinqMethodsTests =
    testList
        "IsNullTranslationTests with LINQ Methods"
        [

          test "Filter content nullable property with IsSome should return a blog with content" {
              use ctx = createContext ()
              saveBlogs ctx

              let blog =
                  ctx
                      .Blogs
                      .Where(fun x -> not (isNull x.Content))
                      .FirstOrDefault()

              Expect.equal blog blogWithContent "Record in context should match"
          }


          test "Filter content nullable property with IsNone should return a blog without content" {
              use ctx = createContext ()
              saveBlogs ctx

              let blog =
                  ctx
                      .Blogs
                      .Where(fun x -> isNull x.Content)
                      .FirstOrDefault()

              Expect.equal blog blogWithoutContent "Record in context should match"
          }

          test "Filter content nullable property by value" {
              use ctx = createContext ()
              saveBlogs ctx

              let blog =
                  ctx
                      .Blogs
                      .Where(fun x -> x.Content = "Some text")
                      .FirstOrDefault()

              Expect.equal blog blogWithContent "Record in context should match"
          } ]
