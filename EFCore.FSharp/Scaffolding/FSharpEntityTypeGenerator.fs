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

module ScaffoldingTypes =
    type RecordOrType = | ClassType | RecordType
    type OptionOrNullable = | OptionTypes | NullableTypes

open ScaffoldingTypes

module FSharpEntityTypeGenerator =

    type private AttributeWriter(name:string) =

        let parameters = List<string>()

        member this.AddParameter p =
            parameters.Add p

        override this.ToString() =
            match parameters |> Seq.isEmpty with
            | true -> sprintf "[<%s>]" name
            | false -> sprintf "[<%s(%s)>]" name (String.Join(", ", parameters))

    let createAttributeQuick = AttributeWriter >> string

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

    let private generatePrimaryKeyAttribute (p:IProperty) (sb:IndentedStringBuilder) =

        let key = (p :?> Microsoft.EntityFrameworkCore.Metadata.Internal.Property).PrimaryKey

        if isNull key || key.Properties.Count <> 1 then
            sb
        else
            sb |> appendLine ("KeyAttribute" |> createAttributeQuick)

    let private generateRequiredAttribute (p:IProperty) (sb:IndentedStringBuilder) =

        let isNullableOrOptionType (t:Type) =
            let typeInfo = t.GetTypeInfo()
            (typeInfo.IsValueType |> not) ||
                (typeInfo.IsGenericType && (typeInfo.GetGenericTypeDefinition() = typedefof<Nullable<_>> || typeInfo.GetGenericTypeDefinition() = typedefof<Option<_>>))

        if (not p.IsNullable) && (p.ClrType |> isNullableOrOptionType) && (p.IsPrimaryKey() |> not) then
            sb |> appendLine ("RequiredAttribute" |> createAttributeQuick)
        else
            sb

    let private generateColumnAttribute (p:IProperty) (sb:IndentedStringBuilder) =
        sb

    let private generateMaxLengthAttribute (p:IProperty) (sb:IndentedStringBuilder) =

        let ml = p.GetMaxLength()

        if ml.HasValue then
            let attrName = 
               match p.ClrType = typedefof<string> with
                | true -> "StringLengthAttribute"
                | false -> "MaxLengthAttribute"

            let a = AttributeWriter(attrName)
            a.AddParameter (FSharpHelper.LiteralWriter.Literal ml.Value)

            sb |> append (a |> string)
        else
            sb

    let private generateTableAttribute (entityType : IEntityType) (sb:IndentedStringBuilder) =
        sb

    let GenerateEntityTypeDataAnnotations entityType =
        entityType |> generateTableAttribute


    let GenerateConstructor (entityType : IEntityType) (sb:IndentedStringBuilder) =
        sb |> appendLine "new() = { }"

    let GenerateProperties (entityType : IEntityType) (optionOrNullable:OptionOrNullable) (sb:IndentedStringBuilder) =
        // TODO: add key etc.
        sb |> appendLine "// Properties"

    let GenerateNavigationProperties (entityType : IEntityType) (optionOrNullable:OptionOrNullable) (sb:IndentedStringBuilder) =
        sb |> appendLine "// NavigationProperties"

    let GenerateClass (entityType : IEntityType) (useDataAnnotations:bool) (optionOrNullable:OptionOrNullable) (sb:IndentedStringBuilder) =

        sb
            |> appendLine ("type " + entityType.Name + "() =")
            |> indent
            |> GenerateConstructor entityType
            |> GenerateProperties entityType optionOrNullable
            |> GenerateNavigationProperties entityType optionOrNullable
            |> unindent   

    let private generateRecordTypeEntry useDataAnnotations optionOrNullable (p: IProperty) sb =

        if useDataAnnotations then
            sb
                |> generatePrimaryKeyAttribute p
                |> generateRequiredAttribute p
                |> generateColumnAttribute p
                |> generateMaxLengthAttribute p
                |> ignore
        
        sb |> append p.Name |> append ": " |> appendLine (getTypeName optionOrNullable p.ClrType)
        

    let private generateRecordProperties (properties :IProperty seq) (useDataAnnotations:bool) (sb:IndentedStringBuilder) =
        sb

    let GenerateRecord (entityType : IEntityType) (useDataAnnotations:bool) optionOrNullable (sb:IndentedStringBuilder) =

        let properties =
            entityType.GetProperties()
            |> Seq.map(fun p -> generateRecordTypeEntry useDataAnnotations optionOrNullable p)
            |> Seq.map(string)

        let navProperties =
            entityType
                    |> EntityTypeExtensions.GetNavigations
                    |> Seq.sortBy(fun n -> ((if n.IsDependentToPrincipal() then 0 else 1), (if n.IsCollection() then 1 else 0)))



        let navsIsEmpty = navProperties |> Seq.isEmpty

        sb
            |> appendLine ("type " + entityType.Name + " = {")
            |> indent
            |> appendLines properties navsIsEmpty
            |> appendLine " }"
            |> unindent
            |> appendLine ""
            

    let WriteCode (entityType: IEntityType) (useDataAnnotation: bool) createTypesAs optionOrNullable (sb:IndentedStringBuilder) =
        
        let generate =
            match createTypesAs with
            | ClassType -> GenerateClass
            | RecordType -> GenerateRecord
        
        sb
            |> generate entityType useDataAnnotation optionOrNullable