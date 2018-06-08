namespace Bricelam.EntityFrameworkCore.FSharp

open System
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Infrastructure

module internal EntityFrameworkExtensions =

    let getConfiguredColumnType =
        Microsoft.EntityFrameworkCore.Internal.RelationalPropertyExtensions.GetConfiguredColumnType

    let getPrimaryKey (p:IProperty) =
        (p :?> Microsoft.EntityFrameworkCore.Metadata.Internal.Property).PrimaryKey

    let getNamespaces =
        Microsoft.EntityFrameworkCore.Internal.TypeExtensions.GetNamespaces

    let sortNamespaces ns =
        let namespaceComparer = Microsoft.EntityFrameworkCore.Design.Internal.NamespaceComparer()
        ns |> List.sortWith (fun x y -> namespaceComparer.Compare(x, y))

    let getId =
        Microsoft.EntityFrameworkCore.Migrations.Internal.MigrationExtensions.GetId

    let findMapping =
        Microsoft.EntityFrameworkCore.Metadata.Internal.PropertyExtensions.FindMapping

    let getDisplayName =
        Microsoft.EntityFrameworkCore.Metadata.Internal.EntityTypeExtensions.DisplayName

    let getDeclaredProperties =
        Microsoft.EntityFrameworkCore.Metadata.Internal.EntityTypeExtensions.GetDeclaredProperties

    let getDeclaredKeys =
        Microsoft.EntityFrameworkCore.Metadata.Internal.EntityTypeExtensions.GetDeclaredKeys

    let getDeclaredIndexes =
        Microsoft.EntityFrameworkCore.Metadata.Internal.EntityTypeExtensions.GetDeclaredIndexes

    let getData (b:bool) entityType =
        Microsoft.EntityFrameworkCore.Metadata.Internal.EntityTypeExtensions.GetData(entityType, b)

    let findDeclaredPrimaryKey =
        Microsoft.EntityFrameworkCore.Metadata.Internal.EntityTypeExtensions.FindDeclaredPrimaryKey

    let getDeclaredForeignKeys =
        Microsoft.EntityFrameworkCore.Metadata.Internal.EntityTypeExtensions.GetDeclaredForeignKeys
    
    let getDeclaredReferencingForeignKeys =
        Microsoft.EntityFrameworkCore.Metadata.Internal.EntityTypeExtensions.GetDeclaredReferencingForeignKeys

    let findOwnership (entityType : IEntityType) =    
        (entityType :?> Microsoft.EntityFrameworkCore.Metadata.Internal.EntityType)
            |> Microsoft.EntityFrameworkCore.Metadata.Internal.EntityTypeExtensions.FindOwnership

    let scaffoldEntity (e : IEntityType) =
        Microsoft.EntityFrameworkCore.Metadata.Internal.ScaffoldingMetadataExtensions.Scaffolding e

    let scaffoldModel (m : IModel) =
        Microsoft.EntityFrameworkCore.Metadata.Internal.ScaffoldingMetadataExtensions.Scaffolding m

    let toAnnotatable (a : IAnnotatable) =
        a :> IAnnotatable