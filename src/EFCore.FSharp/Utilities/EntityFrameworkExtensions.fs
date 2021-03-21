namespace EntityFrameworkCore.FSharp

open System.Collections.Generic

open Microsoft.EntityFrameworkCore.Design.Internal
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Migrations.Internal

module internal EntityFrameworkExtensions =

    let getConfiguredColumnType =
        RelationalPropertyExtensions.GetConfiguredColumnType

    let getPrimaryKey (p: IProperty) =
        (p :?> Property).PrimaryKey

    let sortNamespaces ns =
        let namespaceComparer = NamespaceComparer()
        ns |> List.sortWith (fun x y -> namespaceComparer.Compare(x, y))

    let getId =
        MigrationExtensions.GetId

    let findMapping (p:IProperty) =
        p.FindTypeMapping()

    let getDisplayName =
        EntityTypeExtensions.DisplayName

    let getDeclaredProperties =
        EntityTypeExtensions.GetDeclaredProperties

    let getDeclaredKeys =
        EntityTypeExtensions.GetDeclaredKeys

    let getDeclaredIndexes =
        EntityTypeExtensions.GetDeclaredIndexes

    let getData (b:bool) (entityType:IEntityType) =
        entityType.GetSeedData(b)

    let findDeclaredPrimaryKey =
        EntityTypeExtensions.FindDeclaredPrimaryKey

    let getDeclaredForeignKeys =
        EntityTypeExtensions.GetDeclaredForeignKeys

    let getDeclaredReferencingForeignKeys =
        EntityTypeExtensions.GetDeclaredReferencingForeignKeys

    let findOwnership (entityType : IEntityType) =
        (entityType :?> EntityType)
            |> EntityTypeExtensions.FindOwnership

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
