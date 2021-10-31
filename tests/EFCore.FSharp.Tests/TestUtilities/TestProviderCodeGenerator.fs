namespace EntityFrameworkCore.FSharp.Test.TestUtilities

open System
open System.Reflection
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Scaffolding
open Microsoft.EntityFrameworkCore

type TestProviderCodeGenerator(dependencies) =
    inherit ProviderCodeGenerator(dependencies)

    let getRequiredRuntimeMethod (t: Type, name: string, parameters: Type []) =
        let result =
            t.GetTypeInfo().GetRuntimeMethod(name, parameters)

        if isNull result then
            invalidOp $"Could not find method '{name}' on type '{t}'"
        else
            result

    let _useTestProviderMethodInfo: MethodInfo =
        let t = typeof<TestProviderCodeGenerator>

        let parameters =
            [| typeof<DbContextOptionsBuilder>
               typeof<string>
               typeof<Action<obj>> |]

        getRequiredRuntimeMethod (t, "UseTestProvider", parameters)

    static member UseTestProvider
        (
            optionsBuilder: DbContextOptionsBuilder,
            connectionString: string,
            optionsAction: Action<obj>
        ) =
        raise (NotSupportedException())

    override this.GenerateUseProvider(connectionString, providerOptions) =

        let options =
            if isNull providerOptions then
                [| (connectionString :> obj) |]
            else
                [| (connectionString :> obj)
                   (NestedClosureCodeFragment("x", providerOptions) :> obj) |]

        MethodCallCodeFragment(_useTestProviderMethodInfo, options)
