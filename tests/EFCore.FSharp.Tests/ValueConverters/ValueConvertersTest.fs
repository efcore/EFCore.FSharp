module EntityFrameworkCore.FSharp.Test.ValueConverters.ValueConvertersTest

open System
open EntityFrameworkCore.FSharp
open Expecto

[<Tests>]
let ValueConvertersTest =
    testList "ValueConvertersTest" [
        test "string -> string option" {
            let c = Conversion.toOption<string>
            Expect.equal (c.Compile().Invoke(null)) None "Should be equal"

            let g = "test"
            Expect.equal (c.Compile().Invoke(g)) (Some g) "Should be equal"
        }

        test "string option -> string" {
            let c = Conversion.fromOption<string>
            Expect.equal (c.Compile().Invoke(None)) null "Should be equal"

            let g = "test"
            Expect.equal (c.Compile().Invoke(Some g)) g "Should be equal"
        }

        test "Guid? -> Guid option" {
            let c = Conversion.toOption<Nullable<Guid>>
            Expect.equal (c.Compile().Invoke(Nullable())) None "Should be equal"

            let g = Nullable(Guid.NewGuid())
            Expect.equal (c.Compile().Invoke(g)) (Some g) "Should be equal"
        }

        test "Guid option -> Guid?" {
            let c = Conversion.fromOption<Nullable<Guid>>
            Expect.equal (c.Compile().Invoke(None)) (Nullable()) "Should be equal"

            let g = Nullable(Guid.NewGuid())
            Expect.equal (c.Compile().Invoke(Some g)) g "Should be equal"
        }
    ]
