namespace Bricelam.EntityFrameworkCore.FSharp.Test.TestUtilities

open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Scaffolding

type TestProviderCodeGenerator(dependencies) =
    inherit ProviderCodeGenerator(dependencies)

    override this.GenerateUseProvider(connectionString) =
        
        let options = [| (connectionString :> obj) |]        
        MethodCallCodeFragment("UseTestProvider", options)
    
    override this.GenerateUseProvider(connectionString, providerOptions) =
        
        let options =
            match isNull providerOptions with
            | true -> [| (connectionString :> obj) |]
            | false -> [| (connectionString :> obj); (NestedClosureCodeFragment("x", providerOptions) :> obj) |]
        
        MethodCallCodeFragment("UseTestProvider", options)


type TestScaffoldingProviderCodeGenerator () =
    interface IScaffoldingProviderCodeGenerator with

        member this.GenerateUseProvider(connectionString, language) =
            ""

