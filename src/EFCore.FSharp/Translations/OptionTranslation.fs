module EntityFrameworkCore.FSharp.Translations.OptionTranslation

open EntityFrameworkCore.FSharp
open Microsoft.EntityFrameworkCore.Query

let optionMemberTranslator (sqlExp: ISqlExpressionFactory) =
    { new IMemberTranslator with
        member _.Translate(instance, member', returnType, loger) =
            if not (SharedTypeExtensions.isOptionType member'.DeclaringType) then
                null
            else
                sqlExp.Convert(instance, returnType) :> _
    }

let optionMethodCallTranslator (sqlExp: ISqlExpressionFactory) =
    { new IMethodCallTranslator with
        member _.Translate(instance, method, arguments, loger) =
            if not (SharedTypeExtensions.isOptionType method.DeclaringType) then
                null
            else

                let expression =
                    arguments
                    |> Seq.head

                match method.Name with
                | "get_IsNone" -> sqlExp.IsNull(expression) :> _
                | "get_IsSome" -> sqlExp.IsNotNull(expression) :> _
                | _ -> null
    }
