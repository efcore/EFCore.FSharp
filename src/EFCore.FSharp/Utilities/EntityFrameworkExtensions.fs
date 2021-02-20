namespace EntityFrameworkCore.FSharp

open System.Collections.Generic

open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata.Internal

module internal EntityFrameworkExtensions =

    let getConfiguredColumnType =
        RelationalPropertyExtensions.GetConfiguredColumnType

    let getPrimaryKey (p: IProperty) =
        (p :?> Property).PrimaryKey

    let sortNamespaces ns =
        let namespaceComparer = Microsoft.EntityFrameworkCore.Design.Internal.NamespaceComparer()
        ns |> List.sortWith (fun x y -> namespaceComparer.Compare(x, y))

    let getId =
        Microsoft.EntityFrameworkCore.Migrations.Internal.MigrationExtensions.GetId

    let findMapping (p:IProperty) =
        p.FindTypeMapping()

    let getDisplayName =
        Microsoft.EntityFrameworkCore.EntityTypeExtensions.DisplayName

    let getDeclaredProperties =
        Microsoft.EntityFrameworkCore.EntityTypeExtensions.GetDeclaredProperties

    let getDeclaredKeys =
        Microsoft.EntityFrameworkCore.EntityTypeExtensions.GetDeclaredKeys

    let getDeclaredIndexes =
        Microsoft.EntityFrameworkCore.EntityTypeExtensions.GetDeclaredIndexes

    let getData (b:bool) (entityType:IEntityType) =
        entityType.GetSeedData(b)

    let findDeclaredPrimaryKey =
        EntityTypeExtensions.FindDeclaredPrimaryKey

    let getDeclaredForeignKeys =
        Microsoft.EntityFrameworkCore.EntityTypeExtensions.GetDeclaredForeignKeys

    let getDeclaredReferencingForeignKeys =
        Microsoft.EntityFrameworkCore.EntityTypeExtensions.GetDeclaredReferencingForeignKeys

    let findOwnership (entityType : IEntityType) =
        (entityType :?> EntityType)
            |> Microsoft.EntityFrameworkCore.EntityTypeExtensions.FindOwnership

    let entityDbSetName (e : IEntityType) =
        e.GetDbSetName()

    let modelEntityTypeErrors (m : IModel) =
        m.GetEntityTypeErrors()

    let toAnnotatable (a : IAnnotatable) =
        a

    let annotationsToDictionary (annotations: IAnnotation seq) =
        annotations
        |> Seq.map (fun a -> a.Name, a)
        |> readOnlyDict
        |> Dictionary
