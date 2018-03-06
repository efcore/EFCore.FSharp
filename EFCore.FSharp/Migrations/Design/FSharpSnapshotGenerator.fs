namespace Bricelam.EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.Collections.Generic
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
    
    let private generateFluentApiForAnnotation (annotations: IAnnotation list byref) (annotationName:string) (annotationValueFunc: (IAnnotation -> obj) option) (fluentApiMethodName:string) (genericTypesFunc: (IAnnotation -> IReadOnlyList<Type>)option) (sb:IndentedStringBuilder) =

        let annotationValueFunc' =
            match annotationValueFunc with
            | Some a -> a
            | None -> (fun a -> if isNull a then null else a.Value)


        sb
    
    let private sort (entityTypes:IReadOnlyList<IEntityType>) =
        let entityTypeGraph = new Multigraph<IEntityType, int>()
        entityTypeGraph.AddVertices(entityTypes)

        entityTypes
            |> Seq.filter(fun e -> e.BaseType |> isNull |> not)
            |> Seq.iter(fun e -> entityTypeGraph.AddEdge(e.BaseType, e, 0))
        entityTypeGraph.TopologicalSort()

    let ignoreAnnotationTypes (annotations:IReadOnlyList<IAnnotation>) (annotation:string) (sb:IndentedStringBuilder) =
        sb           

    let generateAnnotation (annotation:IAnnotation) (sb:IndentedStringBuilder) =
        sb

    let generateAnnotations (annotations:IReadOnlyList<IAnnotation>) (sb:IndentedStringBuilder) =
        sb

    let generateEntityTypes builderName entities (sb:IndentedStringBuilder) =
        sb
        
    let generate (builderName:string) (model:IModel) (sb:IndentedStringBuilder) =

        let mutable annotations = model.GetAnnotations() |> Seq.toList

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
