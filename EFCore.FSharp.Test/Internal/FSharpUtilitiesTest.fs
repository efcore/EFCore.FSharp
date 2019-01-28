namespace Bricelam.EntityFrameworkCore.FSharp.Test.Internal

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore.Design
open Bricelam.EntityFrameworkCore.FSharp.Internal
open Xunit
open FsUnit.Xunit

module FSharpUtilitiesTest =

    [<Theory>]
    [<InlineData(typeof<int>, "int")>]
    [<InlineData(typeof<Nullable<int>>, "Nullable<int>")>]
    [<InlineData(typeof<int[]>, "int[]")>]
    [<InlineData(typeof<Dictionary<string, List<int>>>, "Dictionary<string, int ResizeArray>")>]
    [<InlineData(typeof<List<Nullable<int>>>, "Nullable<int> ResizeArray")>]
    [<InlineData(typeof<Nullable<int> list>, "Nullable<int> list")>]
    [<InlineData(typeof<int seq>, "int seq")>]
    [<InlineData(typeof<int list>, "int list")>]
    [<InlineData(typeof<int option>, "int option")>]
    [<InlineData(typeof<int option list>, "int option list")>]
    [<InlineData(typeof<int list option>, "int list option")>]
    let getTypeName(type', typeName) =
        type' |> FSharpUtilities.getTypeName |> should equal typeName

    [<Theory>]
    [<InlineData("", "\"\"")>]
    [<InlineData("SomeValue", "\"SomeValue\"")>]
    [<InlineData("Contains\\Backslash\"QuoteAnd\tTab", "\"Contains\\\\Backslash\\\"QuoteAnd\\tTab\"")>]
    [<InlineData("Contains\r\nNewlinesAnd\"Quotes", "@\"Contains\r\nNewlinesAnd\"\"Quotes\"")>]
    let delimitString(input, expectedOutput) =
        input |> FSharpUtilities.delimitString |> should equal expectedOutput

    [<Fact>]
    let ``Generate MethodCallCodeFragment works``() =
        let method = MethodCallCodeFragment("Test", true, 42)
        method |> FSharpUtilities.generate |> should equal ".Test(true, 42)"

    [<Fact>]
    let ``Generate MethodCallCodeFragment works when niladic``() =
        let method = MethodCallCodeFragment("Test")
        method |> FSharpUtilities.generate |> should equal ".Test()"