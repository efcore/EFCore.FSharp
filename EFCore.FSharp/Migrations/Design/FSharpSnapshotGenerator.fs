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
    
    let private sort (entityTypes:IReadOnlyList<IEntityType>) =
        let entityTypeGraph = new Multigraph<IEntityType, int>()
        entityTypeGraph.AddVertices(entityTypes)

        entityTypes
            |> Seq.filter(fun e -> e.BaseType |> isNull |> not)
            |> Seq.iter(fun e -> entityTypeGraph.AddEdge(e.BaseType, e, 0))
        entityTypeGraph.TopologicalSort()

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

    let generateEntityTypes builderName entities (sb:IndentedStringBuilder) =
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
