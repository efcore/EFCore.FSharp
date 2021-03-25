module EntityFrameworkCore.FSharp.Test.ValueConverters.ValueConvertersTest

open System
open EntityFrameworkCore.FSharp
open Expecto

[<Tests>]
let OptionConverterTests =
    testList "OptionConverterTests" [
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

        test "Can create OptionConverter" {
            let oc = OptionConverter<string>()

            Expect.isNotNull (box oc) "Should not be null"
        }
    ]

type TestEnumLikeUnion = | First | Second

[<Tests>]
let EnumLikeUnionConverterTests =
    testList "EnumLikeUnionConverterTests" [

        test "Can convert to string from enum-like DU" {
            let c = Conversion.fromEnumLikeUnion<TestEnumLikeUnion>

            Expect.equal (c.Compile().Invoke(TestEnumLikeUnion.First)) "First" "Should be equal"
            Expect.equal (c.Compile().Invoke(TestEnumLikeUnion.Second)) "Second" "Should be equal"
        }

        test "Can convert from string to enum-like DU" {
            let c = Conversion.toEnumLikeUnion<TestEnumLikeUnion>

            Expect.equal (c.Compile().Invoke("First")) TestEnumLikeUnion.First "Should be equal"
            Expect.equal (c.Compile().Invoke("Second")) TestEnumLikeUnion.Second "Should be equal"

            Expect.throws (fun () -> c.Compile().Invoke("Third") |> ignore) "Could not parse Third to Union type of type TestEnumLikeUnion"
        }

        test "Can create EnumLikeUnionConverter" {
            let oc = EnumLikeUnionConverter<TestEnumLikeUnion>()

            Expect.isNotNull (box oc) "Should not be null"
        }

    ]
