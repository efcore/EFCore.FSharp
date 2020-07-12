namespace EntityFrameworkCore.FSharp.Test.TestUtilities

open Microsoft.EntityFrameworkCore.Query.ExpressionTranslators
open Microsoft.EntityFrameworkCore.Query.Sql

type TestRelationalCompositeMemberTranslator (dependencies) =
    inherit RelationalCompositeMemberTranslator(dependencies)

type TestRelationalCompositeMethodCallTranslator (dependencies) =
    inherit RelationalCompositeMethodCallTranslator(dependencies)

type TestQuerySqlGenerator (dependencies, selectExpression) =
    inherit DefaultQuerySqlGenerator(dependencies, selectExpression)

type TestQuerySqlGeneratorFactory (dependencies) =
    inherit QuerySqlGeneratorFactoryBase(dependencies)

    override this.CreateDefault selectExpression =
        TestQuerySqlGenerator(dependencies, selectExpression) :> _