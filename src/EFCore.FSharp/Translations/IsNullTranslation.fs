module EntityFrameworkCore.FSharp.Translations.IsNullTranslation

open EntityFrameworkCore.FSharp
open Microsoft.EntityFrameworkCore.Query

let isNullMethodTranslator (sqlExp: ISqlExpressionFactory) =
    { new IMethodCallTranslator with
        member _.Translate(instance, method, arguments, logger) =
            if method.DeclaringType.IsValueType then
                null
            else
                let expression = arguments |> Seq.tryHead
                match expression with
                | Some expression
                    when method.Name = "IsNull" ->
                        sqlExp.IsNull(expression) :> _
                | _ -> null }
