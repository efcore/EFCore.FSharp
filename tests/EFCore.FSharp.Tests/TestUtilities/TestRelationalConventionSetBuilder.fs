namespace EntityFrameworkCore.FSharp.Test.TestUtilities

open Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure

type TestRelationalConventionSetBuilder(dependencies, relationalDependencies) =
    inherit RelationalConventionSetBuilder(dependencies, relationalDependencies)

