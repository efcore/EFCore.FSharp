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

    let getDisplayName (e : IEntityType) =
        e.DisplayName

    let getDeclaredProperties (e : IEntityType) =
        e.GetDeclaredProperties()

    let getDeclaredKeys (e : IEntityType) =
        e.GetDeclaredKeys()

    let getDeclaredIndexes (e : IEntityType) =
        e.GetDeclaredIndexes()

    let getData (b:bool) (entityType:IEntityType) =
        entityType.GetSeedData(b)

    let findPrimaryKey (e : IEntityType) =
        e.FindPrimaryKey()

    let getDeclaredForeignKeys (e : IEntityType) =
        e.GetDeclaredForeignKeys()

    let getDeclaredReferencingForeignKeys (e : IEntityType) =
        e.GetDeclaredReferencingForeignKeys()

    let findOwnership (e : IEntityType) =
        e.FindOwnership()

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
