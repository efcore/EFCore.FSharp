module EntityFrameworkCore.FSharp.Test.ValueConverters.ValueConvertersTest

open System
open EntityFrameworkCore.FSharp.ValueConverters
open Expecto

[<Tests>]
let OptionConverterTests =
    testList "OptionConverterTests" [
        test "string -> string option" {
            let c = Conversion.toOption<string>.Compile()
            Expect.equal (c.Invoke(null)) None "Should be equal"

            let g = "test"
            Expect.equal (c.Invoke(g)) (Some g) "Should be equal"
        }

        test "string option -> string" {
            let c = Conversion.fromOption<string>.Compile()
            Expect.equal (c.Invoke(None)) null "Should be equal"

            let g = "test"
            Expect.equal (c.Invoke(Some g)) g "Should be equal"
        }

        test "Guid? -> Guid option" {
            let c = Conversion.toOption<Nullable<Guid>>.Compile()
            Expect.equal (c.Invoke(Nullable())) None "Should be equal"

            let g = Nullable(Guid.NewGuid())
            Expect.equal (c.Invoke(g)) (Some g) "Should be equal"
        }

        test "Guid option -> Guid?" {
            let c = Conversion.fromOption<Nullable<Guid>>.Compile()
            Expect.equal (c.Invoke(None)) (Nullable()) "Should be equal"

            let g = Nullable(Guid.NewGuid())
            Expect.equal (c.Invoke(Some g)) g "Should be equal"
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
            let c = Conversion.fromEnumLikeUnion<TestEnumLikeUnion>.Compile()

            Expect.equal (c.Invoke(TestEnumLikeUnion.First)) "First" "Should be equal"
            Expect.equal (c.Invoke(TestEnumLikeUnion.Second)) "Second" "Should be equal"
        }

        test "Can convert from string to enum-like DU" {
            let c = Conversion.toEnumLikeUnion<TestEnumLikeUnion>.Compile()

            Expect.equal (c.Invoke("First")) TestEnumLikeUnion.First "Should be equal"
            Expect.equal (c.Invoke("Second")) TestEnumLikeUnion.Second "Should be equal"

            Expect.throws (fun () -> c.Invoke("Third") |> ignore) "Could not parse Third to Union type of type TestEnumLikeUnion"
        }

        test "Can create EnumLikeUnionConverter" {
            let oc = EnumLikeUnionConverter<TestEnumLikeUnion>()

            Expect.isNotNull (box oc) "Should not be null"
        }

    ]
