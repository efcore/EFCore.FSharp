module EntityFrameworkCore.FSharp.Translations.SingleCaseUnionTranslation

open EntityFrameworkCore.FSharp
open Microsoft.EntityFrameworkCore.Query

let singleCaseUnionMemberTranslator (sqlExp: ISqlExpressionFactory) =
    { new IMemberTranslator with
        member _.Translate(instance, member', returnType, loger) =
            if SharedTypeExtensions.isSingleCaseUnion member'.DeclaringType then
                sqlExp.Convert(instance, returnType) :> _
            else
                instance }
