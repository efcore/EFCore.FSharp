namespace EntityFrameworkCore.FSharp

module Conversion =

  open Microsoft.FSharp.Linq.RuntimeHelpers
  open System
  open System.Linq.Expressions

  let toOption<'T> =
    <@ Func<'T, 'T option>(fun (x : 'T) -> match box x with null -> None | _ -> Some x) @>
    |> LeafExpressionConverter.QuotationToExpression
    |> unbox<Expression<Func<'T, 'T option>>>

  let fromOption<'T> =
    <@ Func<'T option, 'T>(fun (x : 'T option) -> match x with Some y -> y | None -> Unchecked.defaultof<'T>) @>
    |> LeafExpressionConverter.QuotationToExpression
    |> unbox<Expression<Func<'T option, 'T>>>

type OptionConverter<'T> () =
  inherit Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<'T option, 'T>
            (Conversion.fromOption, Conversion.toOption)
