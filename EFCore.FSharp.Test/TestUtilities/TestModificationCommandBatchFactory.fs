namespace Bricelam.EntityFrameworkCore.FSharp.Test.TestUtilities

open Microsoft.EntityFrameworkCore.Update

type TestModificationCommandBatchFactory(commandBuilderFactory, sqlGenerationHelper, updateSqlGenerator, valueBufferFactoryFactory) =

    let mutable createCount = 0
    member this.CreateCount = createCount

    interface IModificationCommandBatchFactory with
        member this.Create () =
            SingularModificationCommandBatch(
                commandBuilderFactory,
                sqlGenerationHelper,
                updateSqlGenerator,
                valueBufferFactoryFactory) :> _

