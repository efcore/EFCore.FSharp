module EntityFrameworkCore.FSharp.Test.Utilities

open EntityFrameworkCore.FSharp.StringBuffer
open Expecto

[<Tests>]
let StringBufferTests =

    testList
        "StringBuffer tests"
        [ test "Indent works correctly" {

            let expected =
                (seq {
                    "let square x ="
                    "    x * x"
                    ""
                 }
                 |> join System.Environment.NewLine)

            let actual =
                stringBuilder {
                    "let square x ="
                    indent { "x * x" }
                }

            Expect.equal actual expected "Should match"
          }

          test "Nested indents work correctly" {

              let expected =
                  (seq {
                      "module Test ="
                      "    let square x ="
                      "        x * x"
                      ""
                   }
                   |> join System.Environment.NewLine)

              let actual =
                  stringBuilder {
                      "module Test ="

                      indent {
                          "let square x ="
                          indent { "x * x" }
                      }
                  }

              Expect.equal actual expected "Should match"
          } ]
