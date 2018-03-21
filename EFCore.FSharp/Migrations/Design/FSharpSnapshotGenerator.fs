namespace Bricelam.EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.Collections.Generic
open System.Linq
open System.Reflection
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Internal

open Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Bricelam.EntityFrameworkCore.FSharp.Internal
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata.Internal


module FSharpSnapshotGenerator =
    
    let private generateFluentApiForAnnotation (annotations: List<IAnnotation> byref) (annotationName:string) (annotationValueFunc: (IAnnotation -> obj) option) (fluentApiMethodName:string) (genericTypesFunc: (IAnnotation -> IReadOnlyList<Type>)option) (sb:IndentedStringBuilder) =

        let annotationValueFunc' =
            match annotationValueFunc with
            | Some a -> a
            | None -> (fun a -> if isNull a then null else a.Value)

        let annotation = annotations |> Seq.tryFind (fun a -> a.Name = annotationName)
        let annotationValue = annotation |> Option.map annotationValueFunc'
        
        let genericTypesFunc' =
            match genericTypesFunc with
            | Some a -> a
            | None -> (fun _ -> List<Type>() :> IReadOnlyList<Type>)

        let genericTypes = annotation |> Option.map genericTypesFunc'
        let hasGenericTypes =
            match genericTypes with
            | Some gt -> ((gt |> Seq.isEmpty |> not) && (gt |> Seq.forall(isNull >> not)))
            | None -> false

        if (annotationValue.IsSome && (annotationValue.Value |> isNull |> not)) || hasGenericTypes then
            sb
            |> appendEmptyLine
            |> append "."
            |> append fluentApiMethodName
            |> ignore

            if hasGenericTypes then
                sb
                    |> append "<"
                    |> append (String.Join(",", (genericTypes.Value |> Seq.map(FSharpHelper.Reference))))
                    |> ignore

            sb
                |> append "("
                |> ignore

            if annotationValue.IsSome && annotationValue.Value |> isNull |> not then
                sb |> append (annotationValue.Value |> FSharpHelper.UnknownLiteral) |> ignore

            sb
                |> append ")"
                |> ignore            

            annotation.Value |> annotations.Remove |> ignore

            sb
        else
            sb
    
    let private sort (entityTypes:IEntityType list) =
        let entityTypeGraph = new Multigraph<IEntityType, int>()
        entityTypeGraph.AddVertices(entityTypes)

        entityTypes
            |> Seq.filter(fun e -> e.BaseType |> isNull |> not)
            |> Seq.iter(fun e -> entityTypeGraph.AddEdge(e.BaseType, e, 0))
        entityTypeGraph.TopologicalSort() |> Seq.toList

    let ignoreAnnotationTypes (annotations:List<IAnnotation>) (annotation:string) (sb:IndentedStringBuilder) =
        
        let annotationsToRemove =
            annotations |> Seq.filter (fun a -> a.Name.StartsWith(annotation, StringComparison.OrdinalIgnoreCase))

        annotationsToRemove |> Seq.iter (annotations.Remove >> ignore)

        sb

    let generateAnnotation (annotation:IAnnotation) (sb:IndentedStringBuilder) =
        let name = annotation.Name |> FSharpHelper.Literal
        let value = annotation.Value |> FSharpHelper.UnknownLiteral
        
        sb
            |> append (sprintf ".HasAnnotation(%s, %s)" name value)
            |> ignore

    let generateAnnotations (annotations:List<IAnnotation>) (sb:IndentedStringBuilder) =

        annotations
        |> Seq.iter(fun a ->
            sb
                |> appendEmptyLine
                |> generateAnnotation a)

        sb |> appendEmptyLine |> appendLine "|> ignore"

    let generateBaseType (funcId: string) (baseType: IEntityType) (sb:IndentedStringBuilder) =

        if (baseType |> notNull) then
            sb
                |> appendEmptyLine
                |> append funcId
                |> append ".HasBaseType("
                |> append (baseType.Name |> FSharpHelper.Literal)
                |> appendLine ")"
        else
            sb

    let generateProperties (funcId: string) properties (sb:IndentedStringBuilder) =
        sb

    let generateKey (funcId: string) (key:IKey) (isPrimary:bool) (sb:IndentedStringBuilder) =
        sb

    let generateKeys (funcId: string) (declaredKeys: IKey seq) (pk:IKey) (sb:IndentedStringBuilder) =
        
        if pk |> notNull then
            sb |> generateKey funcId pk true |> ignore

        

        sb

    let generateIndex (funcId: string) (index:IIndex) (sb:IndentedStringBuilder) =
        sb
            |> appendEmptyLine

            |> ignore

    let generateIndexes (funcId: string) (indexes:IIndex seq) (sb:IndentedStringBuilder) =
        
        indexes |> Seq.iter (fun ix -> sb |> generateIndex funcId ix)        
        sb

    let generateEntityTypeAnnotations (funcId: string) (entityType:IEntityType) (sb:IndentedStringBuilder) =
        sb

    let generateForeignKeys funcId (foreignKeys: IForeignKey seq) sb =
        sb

    let generateOwnedTypes funcId (foreignKeys: IForeignKey seq) (sb:IndentedStringBuilder) =
        sb

    let generateRelationships (funcId: string) (entityType:IEntityType) (sb:IndentedStringBuilder) =
        sb
            |> generateForeignKeys funcId (entityType.GetDeclaredForeignKeys())
            |> generateOwnedTypes funcId (entityType.GetDeclaredReferencingForeignKeys() |> Seq.filter(fun fk -> fk.IsOwnership))

    let generateSeedData properties data (sb:IndentedStringBuilder) =
        sb


    let generateEntityType (builderName:string) (entityType: IEntityType) (sb:IndentedStringBuilder) =

        let ownership = entityType.FindOwnership()

        let ownerNav =
            match ownership |> isNull with
            | true -> None
            | false -> ownership.PrincipalToDependent.Name |> Some

        let declaration =
            match ownerNav with
            | None -> (sprintf ".Entity(%s" (entityType.Name |> FSharpHelper.Literal))
            | Some o -> (sprintf ".OwnsOne(%s, %s" (entityType.Name |> FSharpHelper.Literal) (o |> FSharpHelper.Literal))

        let funcId = "b"

        sb
            |> appendEmptyLine
            |> append builderName
            |> append declaration
            |> append ", (fun " |> append funcId |> appendLine " ->"
            |> indent
            |> generateBaseType funcId entityType.BaseType
            |> generateProperties funcId (entityType.GetDeclaredProperties())
            |>
                match ownerNav with
                | None -> append ""
                | Some _ -> generateKeys funcId (entityType.GetDeclaredKeys()) (entityType.FindDeclaredPrimaryKey())
            |> generateIndexes funcId (entityType.GetDeclaredIndexes())
            |> generateEntityTypeAnnotations funcId entityType
            |>
                match ownerNav with
                | None -> append ""
                | Some _ -> generateRelationships funcId entityType
            |> generateSeedData (entityType.GetProperties()) (entityType.GetSeedData(true))
            |> appendLine ")) |> ignore"
            |> unindent
            |> ignore

    let generateEntityTypeRelationships builderName (entityType: IEntityType) (sb:IndentedStringBuilder) =
        
        sb
            |> appendEmptyLine
            |> append builderName
            |> append ".Entity("
            |> append (entityType.Name |> FSharpHelper.Literal)
            |> appendLine(", (fun b ->")
            |> indent
            |> generateRelationships "b" entityType
            |> appendLine ")) |> ignore"
            |> unindent
            |> ignore
            |> ignore


    let generateEntityTypes builderName (entities: IEntityType list) (sb:IndentedStringBuilder) =

        let entitiesToWrite =
            entities |> Seq.filter (fun e -> (e.HasDefiningNavigation() |> not) && (e.FindOwnership() |> isNull))

        entitiesToWrite
            |> Seq.iter(fun e -> generateEntityTypeRelationships builderName e sb)

        let relationships =
            entitiesToWrite
            |> Seq.filter(fun e -> (e.GetDeclaredForeignKeys() |> Seq.isEmpty |> not) || (e.GetDeclaredReferencingForeignKeys() |> Seq.exists(fun fk -> fk.IsOwnership)))

        relationships |> Seq.iter(fun e -> generateEntityTypeRelationships builderName e sb)

        sb
        
    let generate (builderName:string) (model:IModel) (sb:IndentedStringBuilder) =

        let mutable annotations = model.GetAnnotations().ToList()

        if annotations |> Seq.isEmpty |> not then
            sb
                |> append builderName
                |> indent
                |> generateFluentApiForAnnotation &annotations RelationalAnnotationNames.DefaultSchema Option.None "HasDefaultSchema" Option.None
                |> ignoreAnnotationTypes annotations RelationalAnnotationNames.DbFunction
                |> ignoreAnnotationTypes annotations RelationalAnnotationNames.MaxIdentifierLength
                |> ignoreAnnotationTypes annotations CoreAnnotationNames.OwnedTypesAnnotation
                |> generateAnnotations annotations
                |> unindent
                |> ignore

        let sortedEntities = model.GetEntityTypes() |> Seq.filter(fun et -> not et.IsQueryType) |> Seq.toList |> sort
        sb |> generateEntityTypes builderName sortedEntities
