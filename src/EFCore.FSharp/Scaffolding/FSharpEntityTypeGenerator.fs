namespace EntityFrameworkCore.FSharp.Scaffolding

open System
open System.Collections.Generic
open System.Reflection
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Scaffolding.Internal
open EntityFrameworkCore.FSharp.EntityFrameworkExtensions
open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open EntityFrameworkCore.FSharp.Internal


type RecordOrType = | ClassType | RecordType
type OptionOrNullable = | OptionTypes | NullableTypes

[<AllowNullLiteral>]
type ScaffoldOptions() = 
    member val RecordOrType = RecordType with get,set
    member val OptionOrNullable = OptionTypes with get,set

    static member Default = ScaffoldOptions()
    
open System.ComponentModel.DataAnnotations
open System.ComponentModel.DataAnnotations.Schema
open Microsoft.Extensions.Options

type internal AttributeWriter(name:string) =
    let parameters = List<string>()
    member __.AddParameter p =
        parameters.Add p
    override __.ToString() =
        if Seq.isEmpty parameters then
            sprintf "[<%s>]" name
        else
            sprintf "[<%s(%s)>]" name (String.Join(", ", parameters))

type FSharpEntityTypeGenerator(annotationCodeGenerator : IAnnotationCodeGenerator, code : ICSharpHelper, config: ScaffoldOptions) =
    let createAttributeQuick = AttributeWriter >> string
    let primitiveTypeNames =
        seq {
            yield (typedefof<bool>, "bool")
            yield (typedefof<byte>, "byte")
            yield (typedefof<byte[]>, "byte[]")
            yield (typedefof<sbyte>, "sbyte")
            yield (typedefof<int>, "int")
            yield (typedefof<char>, "char")
            yield (typedefof<float32>, "float32")
            yield (typedefof<double>, "double")
            yield (typedefof<string>, "string")
            yield (typedefof<decimal>, "decimal")
        }
        |> dict

    let writeProperty name ``type`` sb =
        sb
        |> appendLine (sprintf "[<DefaultValue>] val mutable private _%s : %s" name ``type``)
        |> appendLine (sprintf "member this.%s" name)
        |> indent
        |> appendLine (sprintf "with get() = this._%s" name)
        |> appendLine (sprintf "and set v = this._%s <- v" name)
        |> unindent
        |> appendEmptyLine
        |> ignore

    let rec getTypeName optionOrNullable (t:Type) =

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

    let generateRequiredAttribute (p:IProperty) sb =

        let isNullableOrOptionType (t:Type) =
            let typeInfo = t.GetTypeInfo()
            (typeInfo.IsValueType |> not) ||
                (typeInfo.IsGenericType && (typeInfo.GetGenericTypeDefinition() = typedefof<Nullable<_>> || typeInfo.GetGenericTypeDefinition() = typedefof<Option<_>>))

        if (not p.IsNullable) && (p.ClrType |> isNullableOrOptionType) && (p.IsPrimaryKey() |> not) then
            sb |> appendLine (nameof RequiredAttribute |> createAttributeQuick)
        else
            sb

    let generateColumnAttribute (p:IProperty) sb =
        let columnName = p.GetColumnBaseName()
        let columnType = getConfiguredColumnType p

        let delimitedColumnName = if isNull columnName |> not && columnName <> p.Name then FSharpUtilities.delimitString(columnName) |> Some else Option.None
        let delimitedColumnType = if isNull columnType |> not then FSharpUtilities.delimitString(columnType) |> Some else Option.None

        if delimitedColumnName.IsSome || delimitedColumnType.IsSome then
            let a = "ColumnAttribute" |> AttributeWriter

            match delimitedColumnName with
            | Some name -> name |> a.AddParameter
            | None -> ()

            match delimitedColumnType with
            | Some t -> (sprintf "Type = %s" t) |> a.AddParameter
            | None -> ()

            sb |> appendLine (a |> string)

        else
            sb


    let generateMaxLengthAttribute (p:IProperty) sb =

        let ml = p.GetMaxLength()

        if ml.HasValue then
            let attrName =
               if p.ClrType = typedefof<string> then "StringLengthAttribute" else "MaxLengthAttribute"

            let a = AttributeWriter(attrName)
            a.AddParameter (code.Literal ml.Value)

            sb |> append (string a)
        else
            sb

    let generateKeyAttribute (property : IProperty) sb =
        if notNull (property.FindContainingPrimaryKey()) then
            sb |> appendLine (nameof KeyAttribute |> createAttributeQuick)
        else
            sb

    let generateKeylessAttribute (entityType : IEntityType) sb =
        if isNull (entityType.FindPrimaryKey()) then
            sb |> appendLine (nameof KeylessAttribute |> createAttributeQuick)
        else
            sb

    let generateTableAttribute (entityType : IEntityType) sb =

        let tableName = entityType.GetTableName()
        let schema = entityType.GetSchema()
        let defaultSchema = entityType.Model.GetDefaultSchema()

        let schemaParameterNeeded = notNull schema && schema <> defaultSchema
        let isView = notNull (entityType.GetViewName())
        let tableAttributeNeeded = (not isView) && (schemaParameterNeeded || notNull tableName && tableName <> entityType.GetDbSetName())

        if tableAttributeNeeded then
            let tableAttribute = AttributeWriter(nameof TableAttribute)
            
            tableAttribute.AddParameter(code.Literal(tableName));
            
            if schemaParameterNeeded then
                tableAttribute.AddParameter($"Schema = {code.Literal(schema)}")
            
            sb |> appendLine (string tableAttribute)
        else
            sb

    let generateIndexAttributes (entityType: IEntityType) sb =

        let indexes =
            entityType.GetIndexes()
            |> Seq.filter(fun i -> ConfigurationSource.Convention <> ((i :?> IConventionIndex).GetConfigurationSource()))

        indexes |> Seq.iter(fun index ->
            let annotations =
                annotationCodeGenerator.FilterIgnoredAnnotations(index.GetAnnotations())
                |> annotationsToDictionary

            annotationCodeGenerator.RemoveAnnotationsHandledByConventions(index, annotations)

            if annotations.Count = 0 then
                let indexAttribute = AttributeWriter(nameof IndexAttribute)

                index.Properties |> Seq.iter(fun p -> indexAttribute.AddParameter $"nameof({p.Name})")

                if notNull index.Name then
                    indexAttribute.AddParameter $"Name = {code.Literal(index.Name)}"

                if index.IsUnique then
                    indexAttribute.AddParameter $"IsUnique = {code.Literal(index.IsUnique)}"

                sb |> appendLine (string indexAttribute) |> ignore
        )

        sb

    let generateForeignKeyAttribute (navigation:INavigation) (sb:IndentedStringBuilder) =

        if navigation.IsOnDependent && navigation.ForeignKey.PrincipalKey.IsPrimaryKey() then
            let foreignKeyAttribute = AttributeWriter(nameof ForeignKeyAttribute)

            if navigation.ForeignKey.Properties.Count > 1 then
                let names = navigation.ForeignKey.Properties |> Seq.map(fun fk -> fk.Name)
                let param = String.Join(",", names)
                foreignKeyAttribute.AddParameter (code.Literal param)
            else
                foreignKeyAttribute.AddParameter $"nameof{navigation.ForeignKey.Properties.[0].Name}"

            sb |> appendLine (string foreignKeyAttribute)
        else
            sb

    let generateInversePropertyAttribute (navigation:INavigation) (sb:IndentedStringBuilder) =

        if navigation.ForeignKey.PrincipalKey.IsPrimaryKey() && notNull navigation.Inverse then
            let inverseNavigation = navigation.Inverse
            let inversePropertyAttribute = AttributeWriter(nameof InversePropertyAttribute)

            let nameMatches =
                navigation.DeclaringEntityType.GetPropertiesAndNavigations()
                |> Seq.exists(fun m -> m.Name = inverseNavigation.DeclaringEntityType.Name)

            let param =
                if nameMatches then
                    code.Literal inverseNavigation.Name
                else
                     $"nameof({inverseNavigation.DeclaringEntityType.Name}.{inverseNavigation.Name})"

            inversePropertyAttribute.AddParameter param

            sb |> appendLine (string inversePropertyAttribute)

        else
            sb

    let generateEntityTypeDataAnnotations entityType sb =
        sb
        |> generateKeylessAttribute entityType
        |> generateTableAttribute entityType
        |> generateIndexAttributes entityType


    let generateConstructor (entityType : IEntityType) sb =

        let collectionNavigations = entityType.GetNavigations() |> Seq.filter(fun n -> n.IsCollection)

        if collectionNavigations |> Seq.isEmpty then
            sb
        else
            sb
            |> appendLine "do"
            |> indent
            |> appendLines (collectionNavigations |> Seq.map(fun c -> sprintf "this.%s <- HashSet<%s>() :> ICollection<%s>" c.Name c.TargetEntityType.Name c.TargetEntityType.Name)) false
            |> appendEmptyLine
            |> unindent


    let generatePropertyDataAnnotations (p:IProperty) (sb:IndentedStringBuilder) =

        sb
        |> generateKeyAttribute p
        |> generateRequiredAttribute p
        |> generateColumnAttribute p
        |> generateMaxLengthAttribute p
        |> ignore

        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(p.GetAnnotations())
            |> annotationsToDictionary

        annotationCodeGenerator.RemoveAnnotationsHandledByConventions(p, annotations)

        annotationCodeGenerator.GenerateDataAnnotationAttributes(p, annotations)
        |> Seq.iter(fun a ->
            let attributeWriter = AttributeWriter a.Type.Name
            a.Arguments |> Seq.iter(fun arg -> attributeWriter.AddParameter(code.UnknownLiteral arg))
            sb |> appendLine (string a) |> ignore
        )
        
        ()

    let generateNavigationDataAnnotations(navigation:INavigation) (sb:IndentedStringBuilder) =

        sb
        |> generateForeignKeyAttribute navigation
        |> generateInversePropertyAttribute navigation
        |> ignore

    let generateProperties (entityType : IEntityType) (optionOrNullable:OptionOrNullable) useDataAnnotations sb =

        let props =
            entityType.GetProperties()
            |> Seq.sortBy ScaffoldingPropertyExtensions.GetColumnOrdinal

        props
        |> Seq.iter(fun p ->
            if useDataAnnotations then
                sb |> generatePropertyDataAnnotations p

            sb |> writeProperty p.Name p.ClrType.FullName
        )

        sb

    let generateNavigationProperties (entityType : IEntityType) (optionOrNullable:OptionOrNullable) useDataAnnotations sb =
        
        let sortedNavigations =
            entityType.GetNavigations()
            |> Seq.sortBy(fun n -> if n.IsOnDependent then 0 else 1)
            |> Seq.sortBy(fun n -> if n.IsCollection then 1 else 0)

        if not(sortedNavigations |> Seq.isEmpty) then
            sb |> appendEmptyLine |> ignore

        sortedNavigations
        |> Seq.iter(fun p ->
            if useDataAnnotations then
                sb |> generateNavigationDataAnnotations p

            let name = p.TargetEntityType.Name
            let navigationType = if p.IsCollection then $"ICollection<{name}>" else name
            
            sb |> writeProperty p.Name navigationType
        )

        sb

    let generateClass (entityType : IEntityType) useDataAnnotations optionOrNullable sb =

        sb
            |>
                if useDataAnnotations then
                    generateEntityTypeDataAnnotations entityType
                else
                    id
            |> appendLine (sprintf "type %s() as this =" entityType.Name)
            |> indent
            |> generateConstructor entityType
            |> generateProperties entityType optionOrNullable useDataAnnotations
            |> generateNavigationProperties entityType optionOrNullable useDataAnnotations
            |> unindent

    let generateRecordTypeEntry useDataAnnotations optionOrNullable (p: IProperty) sb =

        if useDataAnnotations then
            sb
                |> generatePropertyDataAnnotations p
                |> ignore

        let typeName = getTypeName optionOrNullable p.ClrType
        sb |> appendLine (sprintf "mutable %s: %s" p.Name typeName) |> ignore
        ()

    let writeRecordProperties (properties :IProperty seq) (useDataAnnotations:bool) (skipFinalNewLine: bool) optionOrNullable sb =
        properties
        |> Seq.iter(fun p -> generateRecordTypeEntry useDataAnnotations optionOrNullable p sb)

        sb

    let generateForeignKeyAttribute (n:INavigation) sb =

        if n.IsOnDependent && n.ForeignKey.PrincipalKey.IsPrimaryKey() then
            let a = "ForeignKeyAttribute" |> AttributeWriter
            let props = n.ForeignKey.Properties |> Seq.map (fun n' -> n'.Name)
            String.Join(",", props) |> FSharpUtilities.delimitString |> a.AddParameter
            sb |> appendLine (a |> string)
        else
            sb

    let generateInversePropertyAttribute (n:INavigation) sb =
        if n.ForeignKey.PrincipalKey.IsPrimaryKey() then
            let inverse = n.Inverse
            if isNull inverse then
                sb
            else
                let a = "InversePropertyAttribute" |> AttributeWriter
                inverse.Name |> FSharpUtilities.delimitString |> a.AddParameter
                sb |> appendLine (a |> string)
        else
            sb

    let generateNavigateTypeEntry (n:INavigation) (useDataAnnotations:bool) (skipFinalNewLine: bool) optionOrNullable sb =
        if useDataAnnotations then
            sb
                |> generateNavigationDataAnnotations n
                |> ignore

        let referencedTypeName = n.TargetEntityType.Name
        let navigationType =
            if n.IsCollection then
                sprintf "ICollection<%s>" referencedTypeName
            else
                referencedTypeName
        sb |> appendLine (sprintf "mutable %s: %s" n.Name navigationType) |> ignore

    let writeNavigationProperties (nav:INavigation seq) (useDataAnnotations:bool) (skipFinalNewLine: bool) optionOrNullable sb =
        nav |> Seq.iter(fun n -> generateNavigateTypeEntry n useDataAnnotations skipFinalNewLine optionOrNullable sb)
        sb

    let generateRecord (entityType : IEntityType) (useDataAnnotations:bool) optionOrNullable sb =
        let properties =
            entityType.GetProperties()

        let navProperties =
            entityType
                    |> EntityTypeExtensions.GetNavigations
                    |> Seq.sortBy(fun n -> ((if n.IsOnDependent then 0 else 1), (if n.IsCollection then 1 else 0)))

        let navsIsEmpty = navProperties |> Seq.isEmpty

        sb
            |> appendLine ("CLIMutable" |> createAttributeQuick)
            |> appendLine (sprintf "type %s = {" entityType.Name)
            |> indent
            |> writeRecordProperties properties useDataAnnotations navsIsEmpty optionOrNullable
            |> writeNavigationProperties navProperties useDataAnnotations true optionOrNullable
            |> unindent
            |> appendLine "}"
            |> appendEmptyLine


    let writeCode (entityType: IEntityType) (useDataAnnotation: bool) createTypesAs optionOrNullable sb =

        let generate =
            match createTypesAs with
            | ClassType -> generateClass
            | RecordType -> generateRecord

        sb
            |> indent
            |> generate entityType useDataAnnotation optionOrNullable
            |> string

    interface ICSharpEntityTypeGenerator with
        member this.WriteCode(entityType, ``namespace``, useDataAnnotations) =
            let createTypesAs = if notNull config then config.RecordOrType else RecordType
            let optionOrNullable = if notNull config then config.OptionOrNullable else OptionTypes
            writeCode entityType useDataAnnotations createTypesAs optionOrNullable (IndentedStringBuilder())
