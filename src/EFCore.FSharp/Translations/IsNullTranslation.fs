module EntityFrameworkCore.FSharp.Translations.IsNullTranslation

open EntityFrameworkCore.FSharp
open Microsoft.EntityFrameworkCore.Query

let isNullMethodTranslator (sqlExp: ISqlExpressionFactory) =
    { new IMethodCallTranslator with
        member _.Translate(instance, method, arguments, logger) =
            if method.DeclaringType.IsValueType then
                null
            else
                let expression = arguments |> Seq.head

                match method.Name with
                | "IsNull" -> sqlExp.IsNull(expression) :> _
                | _ -> null }
