namespace EntityFrameworkCore.FSharp.Test.TestUtilities

open Microsoft.EntityFrameworkCore.Update

type TestModificationCommandBatchFactory(commandBuilderFactory, sqlGenerationHelper, updateSqlGenerator, valueBufferFactoryFactory) =

    let mutable createCount = 0
    member this.CreateCount = createCount

    interface IModificationCommandBatchFactory with
        member this.Create () =
            let dependencies = 
                ModificationCommandBatchFactoryDependencies(commandBuilderFactory,
                                                            sqlGenerationHelper,
                                                            updateSqlGenerator,
                                                            valueBufferFactoryFactory, null, null)
            SingularModificationCommandBatch(dependencies) :> _

