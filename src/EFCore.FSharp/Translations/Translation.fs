namespace EntityFrameworkCore.FSharp.Translations

open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Query

type FSharpMemberTranslatorPlugin(sqlExpressionFactory) =
    interface IMemberTranslatorPlugin with
        member _.Translators = seq {
            OptionTranslation.optionMemberTranslator sqlExpressionFactory
            SingleCaseUnionTranslation.singleCaseUnionMemberTranslator sqlExpressionFactory
        }

type FSharpMethodCallTranslatorPlugin(sqlExpressionFactory) =
    interface IMethodCallTranslatorPlugin with
        member _.Translators = seq {
            OptionTranslation.optionMethodCallTranslator sqlExpressionFactory
            IsNullTranslation.isNullMethodTranslator sqlExpressionFactory
        }

type ExtensionInfo(extension) =
    inherit DbContextOptionsExtensionInfo(extension)
        override _.IsDatabaseProvider = false

        override _.GetServiceProviderHashCode() = 0L

        override _.PopulateDebugInfo debugInfo =
             debugInfo.["SqlServer: UseFSharp"] <- "1"

        override _.LogFragment = "using FSharp types"

type FSharpTypeOptionsExtension() =
     interface IDbContextOptionsExtension with
         member this.ApplyServices(services) =
             EntityFrameworkRelationalServicesBuilder(services)
                 .TryAddProviderSpecificServices(
                     fun x ->
                         x.TryAddSingletonEnumerable<IMemberTranslatorPlugin, FSharpMemberTranslatorPlugin>()
                          .TryAddSingletonEnumerable<IMethodCallTranslatorPlugin, FSharpMethodCallTranslatorPlugin>()
                         |> ignore)
             |> ignore

         member this.Info = ExtensionInfo(this :> IDbContextOptionsExtension) :> _
         member this.Validate _ = ()
