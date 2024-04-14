namespace EntityFrameworkCore.FSharp

module Conversion =

    open FSharp.Reflection
    open Microsoft.FSharp.Linq.RuntimeHelpers
    open System
    open System.Linq.Expressions

    let toOption<'T> =
        <@
            Func<'T, 'T option>(fun (x: 'T) ->
                match box x with
                | null -> None
                | _ -> Some x
            )
        @>
        |> LeafExpressionConverter.QuotationToExpression
        |> unbox<Expression<Func<'T, 'T option>>>

    let fromOption<'T> =
        <@
            Func<'T option, 'T>(fun (x: 'T option) ->
                match x with
                | Some y -> y
                | None -> Unchecked.defaultof<'T>
            )
        @>
        |> LeafExpressionConverter.QuotationToExpression
        |> unbox<Expression<Func<'T option, 'T>>>

    let toSingleCaseUnion<'T, 'U> =
        <@
            Func<'T, 'U>(fun (x: 'T) ->
                FSharpValue.MakeUnion(
                    FSharpType.GetUnionCases(typedefof<'U>)
                    |> Array.exactlyOne,
                    [| x :> obj |]
                )
                :?> 'U
            )
        @>
        |> LeafExpressionConverter.QuotationToExpression
        |> unbox<Expression<Func<'T, 'U>>>

    let fromFromSingleCase<'T, 'U> =
        <@
            Func<'U, 'T>(fun (x: 'U) ->
                FSharpValue.GetUnionFields(x, x.GetType())
                |> snd
                |> Seq.head
                :?> 'T
            )
        @>
        |> LeafExpressionConverter.QuotationToExpression
        |> unbox<Expression<Func<'U, 'T>>>

type OptionConverter<'T>() =
    inherit
        Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<'T option, 'T>(
            Conversion.fromOption,
            Conversion.toOption
        )

type SingleCaseUnionConverter<'T, 'U>() =
    inherit
        Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<'U, 'T>(
            Conversion.fromFromSingleCase,
            Conversion.toSingleCaseUnion
        )
