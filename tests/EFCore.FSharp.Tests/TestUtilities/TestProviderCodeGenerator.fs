namespace EntityFrameworkCore.FSharp.Test.TestUtilities

open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Scaffolding

type TestProviderCodeGenerator(dependencies) =
    inherit ProviderCodeGenerator(dependencies)

    override this.GenerateUseProvider(connectionString, providerOptions) =
        
        let options =
            if isNull providerOptions then
                [| (connectionString :> obj) |]
            else            
                [| (connectionString :> obj); (NestedClosureCodeFragment("x", providerOptions) :> obj) |]
        
        MethodCallCodeFragment("UseTestProvider", options)

