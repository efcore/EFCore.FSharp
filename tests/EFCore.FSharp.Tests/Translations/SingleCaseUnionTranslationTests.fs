module EFCore.FSharp.Tests.Translations.SingleCaseUnionTranslationTests

open System
open System.Linq
open System.ComponentModel.DataAnnotations
open EntityFrameworkCore.FSharp.DbContextHelpers
open EntityFrameworkCore.FSharp.Extensions
open Expecto
open Microsoft.EntityFrameworkCore

type PositiveInteger = PositiveInteger of int

[<CLIMutable>]
type Blog =
    { [<Key>]
      Id: Guid
      Title: string
      Votes: PositiveInteger }

type MyContext() =
    inherit DbContext()

    [<DefaultValue>]
    val mutable private _blogs: DbSet<Blog>

    member this.Blogs
        with get () = this._blogs
        and set v = this._blogs <- v

    override _.OnConfiguring(options: DbContextOptionsBuilder) : unit =
        options
            .UseInMemoryDatabase("MyContext")
            .UseFSharpTypes()
        |> ignore

    override _.OnModelCreating builder = builder.RegisterSingleUnionCases()

let createContext () =
    let ctx = new MyContext()
    ctx

let blogWithVotes =
    { Id = Guid.NewGuid()
      Title = "My Title"
      Votes = PositiveInteger 10 }

let saveBlogs ctx =
    addEntity ctx blogWithVotes |> ignore
    saveChanges ctx |> ignore

[<Tests>]
let OptionTranslationQueryTests =
    testList
        "SingleCaseUnionTranslationTests with query"
        [

          test "Filter votes property with exact value" {
              use ctx = createContext ()
              saveBlogs ctx

              let blog =
                  query {
                      for blog in ctx.Blogs do
                          where (blog.Votes = PositiveInteger 10)
                          select blog
                          headOrDefault
                  }

              Expect.equal blog blogWithVotes "Record in context should match"
          }


          test "Filter votes property with 'greater than' extracting property" {
              use ctx = createContext ()
              saveBlogs ctx

              let blog =
                  query {
                      for blog in ctx.Blogs do
                          let (PositiveInteger votes) = blog.Votes
                          where (votes > 0)
                          select blog
                          headOrDefault
                  }

              Expect.equal blog blogWithVotes "Record in context should match"
          } ]


[<Tests>]
let OptionTranslationLinqMethodsTests =
    testList
        "SingleCaseUnionTranslationTests with LINQ"
        [ test "Filter votes property with exact value" {
              use ctx = createContext ()
              saveBlogs ctx

              let blog =
                  ctx
                      .Blogs
                      .Where(fun b -> b.Votes = PositiveInteger 10)
                      .FirstOrDefault()

              Expect.equal blog blogWithVotes "Record in context should match"
          } ]
