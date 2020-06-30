module EntityFrameworkCore.FSharp.Test.Internal

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore.Design
open EntityFrameworkCore.FSharp.Internal
open Expecto

let getTypeNameTestCases = [
    (typeof<int>, "int")
    (typeof<Nullable<int>>, "Nullable<int>")
    (typeof<int[]>, "int[]")
    (typeof<Dictionary<string, List<int>>>, "Dictionary<string, int ResizeArray>")
    (typeof<List<Nullable<int>>>, "Nullable<int> ResizeArray")
    (typeof<Nullable<int> list>, "Nullable<int> list")
    (typeof<int seq>, "int seq")
    (typeof<int list>, "int list")
    (typeof<int option>, "int option")
    (typeof<int option list>, "int option list")
    (typeof<int list option>, "int list option")
]

let delimitStringTestCases = [
    ("", "\"\"")
    ("SomeValue", "\"SomeValue\"")
    ("Contains\\Backslash\"QuoteAnd\tTab", "\"Contains\\\\Backslash\\\"QuoteAnd\\tTab\"")
    ("Contains\r\nNewlinesAnd\"Quotes", "@\"Contains\r\nNewlinesAnd\"\"Quotes\"")
]

[<Tests>]
let FSharpUtilitiesTest =

    testList "FSharpUtilitiesTest" [
        testList "getTypeName" [
            test "int" {
                 getTypeNameTestCases |> Seq.iter(fun (type', expected) ->
                        let actual = FSharpUtilities.getTypeName type'
                        Expect.equal actual expected "Should be equal"
                 )
            }
        ]

        testList "delimitString" [
            test "delimitString" {
                delimitStringTestCases |> Seq.iter(fun (input, expected) ->
                        let actual = FSharpUtilities.delimitString input
                        Expect.equal actual expected "Should be equal"
                 )
            }
        ]

        testList "MethodCallCodeFragment" [
            test "MethodCallCodeFragment with parameters" {
                let method = MethodCallCodeFragment("Test", true, 42)
                let actual = method |> FSharpUtilities.generate
                Expect.equal actual ".Test(true, 42)" "Should be equal"
            }

            test "MethodCallCodeFragment when niladic" {
                let method = MethodCallCodeFragment("Test")
                let actual = method |> FSharpUtilities.generate
                Expect.equal actual ".Test()" "Should be equal"
            }
        ]
    ]
