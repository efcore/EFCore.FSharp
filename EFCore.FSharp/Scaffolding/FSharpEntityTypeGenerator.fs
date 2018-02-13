namespace Bricelam.EntityFrameworkCore.FSharp.Scaffolding

open System
open System.Collections.Generic
open System.Reflection
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Internal

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities

module ScaffoldingTypes =
    type RecordOrType = | ClassType | RecordType
    type OptionOrNullable = | OptionTypes | NullableTypes

open ScaffoldingTypes

module FSharpEntityTypeGenerator =

    let private primitiveTypeNames = new Dictionary<Type, string>()
    primitiveTypeNames.Add(typedefof<bool>, "bool")
    primitiveTypeNames.Add(typedefof<byte>, "byte")
    primitiveTypeNames.Add(typedefof<byte[]>, "byte[]")
    primitiveTypeNames.Add(typedefof<sbyte>, "sbyte")
    primitiveTypeNames.Add(typedefof<int>, "int")
    primitiveTypeNames.Add(typedefof<char>, "char")
    primitiveTypeNames.Add(typedefof<float32>, "float32")
    primitiveTypeNames.Add(typedefof<double>, "double")
    primitiveTypeNames.Add(typedefof<string>, "string")
    primitiveTypeNames.Add(typedefof<decimal>, "decimal")

    let rec private getTypeName (optionOrNullable:OptionOrNullable) (t:Type) =

        if t.IsArray then
            (getTypeName optionOrNullable (t.GetElementType())) + "[]"

        else if t.GetTypeInfo().IsGenericType then
            if t.GetGenericTypeDefinition() = typedefof<Nullable<_>> then
                match optionOrNullable with
                | NullableTypes ->  "Nullable<" + (getTypeName optionOrNullable (Nullable.GetUnderlyingType(t))) + ">";
                | OptionTypes -> (getTypeName optionOrNullable (Nullable.GetUnderlyingType(t))) + " option"
            else
                let genericTypeDefName = t.Name.Substring(0, t.Name.IndexOf('`'));
                let genericTypeArguments = String.Join(", ", t.GenericTypeArguments |> Seq.map(fun t' -> getTypeName optionOrNullable t'))
                genericTypeDefName + "<" + genericTypeArguments + ">";
      
        else
            match primitiveTypeNames.TryGetValue t with
            | true, value -> value
            | _ -> t.Name


    let GenerateTableAttribute (entityType : IEntityType) (sb:IndentedStringBuilder) =
        sb

    let GenerateEntityTypeDataAnnotations entityType =
        entityType |> GenerateTableAttribute


    let GenerateConstructor (entityType : IEntityType) (sb:IndentedStringBuilder) =
        sb |> appendLine "new() = { }"

    let GenerateProperties (entityType : IEntityType) (optionOrNullable:OptionOrNullable) (sb:IndentedStringBuilder) =
        // TODO: add key etc.
        sb |> appendLine "// Properties"

    let GenerateNavigationProperties (entityType : IEntityType) (optionOrNullable:OptionOrNullable) (sb:IndentedStringBuilder) =
        sb |> appendLine "// NavigationProperties"

    let GenerateClass (entityType : IEntityType) (optionOrNullable:OptionOrNullable) (sb:IndentedStringBuilder) =

        sb
            |> appendLine ("type " + entityType.Name + "() =")
            |> indent
            |> GenerateConstructor entityType
            |> GenerateProperties entityType optionOrNullable
            |> GenerateNavigationProperties entityType optionOrNullable
            |> unindent   

    let private generateRecordTypeEntry optionOrNullable (p: IProperty) =
        // TODO: add key etc.
        p.Name + ": " + (getTypeName optionOrNullable p.ClrType)

    let GenerateRecord (entityType : IEntityType) optionOrNullable (sb:IndentedStringBuilder) =

        let properties = entityType.GetProperties() |> Seq.map(fun p -> generateRecordTypeEntry optionOrNullable p)

        sb
            |> appendLine ("type " + entityType.Name + " = {")
            |> indent
            |> appendLines properties true
            |> unindent
            |> appendLine "}"
            |> appendLine ""
            

    let WriteCode (entityType: IEntityType) (useDataAnnotation: bool) createTypesAs optionOrNullable (sb:IndentedStringBuilder) =
        
        let generate =
            match createTypesAs with
            | ClassType -> GenerateClass
            | RecordType -> GenerateRecord
        
        sb
            |> generate entityType optionOrNullable