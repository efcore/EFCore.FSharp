module EntityFrameworkCore.FSharp.Translations.OptionTranslation

open EntityFrameworkCore.FSharp
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Query

let memberTranslator(sqlExp: ISqlExpressionFactory ) = {
    new IMemberTranslator with
        member _.Translate(instance, member', returnType, loger) =
           if not (SharedTypeExtensions.isOptionType member'.DeclaringType) then
               null
           else
               sqlExp.Convert(instance, returnType) :> _
}

let methodCallTranslator(sqlExp: ISqlExpressionFactory ) = {
    new IMethodCallTranslator with
        member _.Translate(instance, method, arguments, loger) =
           if not (SharedTypeExtensions.isOptionType method.DeclaringType) then
                null
           else

           let expression = arguments |> Seq.head
           match method.Name with
           | "get_IsNone"-> sqlExp.IsNull(expression) :> _
           | "get_IsSome"-> sqlExp.IsNotNull(expression) :> _
           | _ -> null
}

type OptionMemberTranslatorPlugin(sqlExpressionFactory) =
    interface IMemberTranslatorPlugin with
        member _.Translators = seq {
            memberTranslator sqlExpressionFactory
        }

type OptionMethodCallTranslatorPlugin(sqlExpressionFactory) =
    interface IMethodCallTranslatorPlugin with
        member _.Translators = seq {
            methodCallTranslator sqlExpressionFactory
        }

type ExtensionInfo(extension) =
    inherit DbContextOptionsExtensionInfo(extension)
        override _.IsDatabaseProvider = false

        override _.GetServiceProviderHashCode() = 0L

        override _.PopulateDebugInfo debugInfo =
             debugInfo.["SqlServer: UseFSharp"] <- "1"

        override _.LogFragment = "using FSharp option type"

type FsharpTypeOptionsExtension() =
     interface IDbContextOptionsExtension with
         member this.ApplyServices(services) =
             EntityFrameworkRelationalServicesBuilder(services)
                 .TryAddProviderSpecificServices(
                     fun x ->
                         x.TryAddSingletonEnumerable<IMemberTranslatorPlugin, OptionMemberTranslatorPlugin>()
                          .TryAddSingletonEnumerable<IMethodCallTranslatorPlugin, OptionMethodCallTranslatorPlugin>()
                         |> ignore)
             |> ignore

         member this.Info = ExtensionInfo(this :> IDbContextOptionsExtension) :> _
         member this.Validate _ = ()


