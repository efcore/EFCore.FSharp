namespace Bricelam.EntityFrameworkCore.FSharp.Scaffolding

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

module FSharpSnapshotGenerator =
    
    let private generateFluentApiForAnnotation (annotations: IAnnotation list byref) (annotationName:string) (annotationValueFunc: (IAnnotation -> obj) option) (fluentApiMethodName:string) (genericTypesFunc: (IAnnotation -> IReadOnlyList<Type>)option) (sb:IndentedStringBuilder) =

        let annotationValueFunc' =
            match annotationValueFunc with
            | Some a -> a
            | None -> (fun a -> if isNull a then null else a.Value)


        sb
    
    let generate (builderName:string) (model:IModel) (sb:IndentedStringBuilder) =

        let mutable annotations = model.GetAnnotations() |> Seq.toList

        if annotations |> Seq.isEmpty |> not then
            sb
                |> append builderName
                |> generateFluentApiForAnnotation &annotations RelationalAnnotationNames.DefaultSchema Option.None "HasDefaultSchema" Option.None
        else
            sb |> append ""        


        sb
