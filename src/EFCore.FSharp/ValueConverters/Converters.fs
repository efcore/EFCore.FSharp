namespace EntityFrameworkCore.FSharp

module Conversion =

    open Microsoft.FSharp.Linq.RuntimeHelpers
    open System
    open System.Linq.Expressions
    open FSharp.Reflection

    let toOption<'T> =
        <@ Func<'T, 'T option>(fun (x : 'T) -> match box x with null -> None | _ -> Some x) @>
        |> LeafExpressionConverter.QuotationToExpression
        |> unbox<Expression<Func<'T, 'T option>>>

    let fromOption<'T> =
        <@ Func<'T option, 'T>(fun (x : 'T option) -> match x with Some y -> y | None -> Unchecked.defaultof<'T>) @>
        |> LeafExpressionConverter.QuotationToExpression
        |> unbox<Expression<Func<'T option, 'T>>>

    let toEnumLikeUnion<'a> =
        <@ Func<string, 'a>(fun x ->
            match FSharpType.GetUnionCases typeof<'a> |> Seq.filter (fun case -> case.Name = x) |> Seq.tryHead with
            | Some case -> FSharpValue.MakeUnion(case,[||]) :?> 'a
            | _ -> failwithf "Could not parse %s to Union type of type %A" x (typeof<'a>)
        ) @>
        |> LeafExpressionConverter.QuotationToExpression
        |> unbox<Expression<Func<string, 'a>>>

    let fromEnumLikeUnion<'a> =
        <@ Func<'a, string>(fun x -> match FSharpValue.GetUnionFields(x, typeof<'a>) with | case, _ -> case.Name) @>
        |> LeafExpressionConverter.QuotationToExpression
        |> unbox<Expression<Func<'a, string>>>

type OptionConverter<'T> () =
    inherit Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<'T option, 'T>
        (Conversion.fromOption, Conversion.toOption)

type EnumLikeUnionConverter<'a> () =
    inherit Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<'a, string>
        (Conversion.fromEnumLikeUnion, Conversion.toEnumLikeUnion)
