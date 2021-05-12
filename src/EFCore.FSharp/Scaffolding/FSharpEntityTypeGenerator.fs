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
open EntityFrameworkCore.FSharp
open EntityFrameworkCore.FSharp.EntityFrameworkExtensions
open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open EntityFrameworkCore.FSharp.Internal

open System.ComponentModel.DataAnnotations
open System.ComponentModel.DataAnnotations.Schema

/// Create attributes of a given name
type internal AttributeWriter(name:string) =
    let parameters = List<string>()

    /// Add parameter to Attribute
    member __.AddParameter p =
        parameters.Add p

    /// Write attribute to string
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

    let writeProperty name typeName func sb =
        sb
        |> appendLine (sprintf "[<DefaultValue>] val mutable private _%s : %s" name typeName)
        |> appendEmptyLine
        |> func
        |> appendLine (sprintf "member this.%s with get() = this._%s and set v = this._%s <- v" name name name)
        |> appendEmptyLine
        |> ignore

    let rec getTypeName scaffoldNullableColumnsAs (t:Type) =

        if t.IsArray then
            (getTypeName scaffoldNullableColumnsAs (t.GetElementType())) + "[]"

        else if t.GetTypeInfo().IsGenericType then
            if t.GetGenericTypeDefinition() = typedefof<Nullable<_>> then
                match scaffoldNullableColumnsAs with
                | NullableTypes ->  "Nullable<" + (getTypeName scaffoldNullableColumnsAs (Nullable.GetUnderlyingType(t))) + ">";
                | OptionTypes -> (getTypeName scaffoldNullableColumnsAs (Nullable.GetUnderlyingType(t))) + " option"
            else
                let genericTypeDefName = t.Name.Substring(0, t.Name.IndexOf('`'));
                let genericTypeArguments = String.Join(", ", t.GenericTypeArguments |> Seq.map(fun t' -> getTypeName scaffoldNullableColumnsAs t'))
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

    let generateForeignKeyAttribute (navigation:INavigation) sb =

        if navigation.IsOnDependent && navigation.ForeignKey.PrincipalKey.IsPrimaryKey() then
            let foreignKeyAttribute = AttributeWriter(nameof ForeignKeyAttribute)

            if navigation.ForeignKey.Properties.Count > 1 then
                let names = navigation.ForeignKey.Properties |> Seq.map(fun fk -> fk.Name)
                let param = String.Join(",", names)
                foreignKeyAttribute.AddParameter (code.Literal param)
            else
                foreignKeyAttribute.AddParameter (code.Literal navigation.ForeignKey.Properties.[0].Name)

            sb |> appendLine (string foreignKeyAttribute)
        else
            sb

    let generateInversePropertyAttribute (navigation:INavigation) sb =

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
                     $"\"{inverseNavigation.DeclaringEntityType.Name}.{inverseNavigation.Name}\""

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


    let generatePropertyDataAnnotations (p:IProperty) sb =

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

        sb

    let generateNavigationDataAnnotations(navigation:INavigation) sb =

        sb
        |> generateForeignKeyAttribute navigation
        |> generateInversePropertyAttribute navigation

    let generateProperties (entityType : IEntityType) scaffoldNullableColumnsAs useDataAnnotations sb =

        let props =
            entityType.GetProperties()
            |> Seq.sortBy ScaffoldingPropertyExtensions.GetColumnOrdinal

        props
        |> Seq.iter(fun p ->
            let func =
                if useDataAnnotations then
                    generatePropertyDataAnnotations p
                else
                    (fun s -> s)

            let typeName = getTypeName scaffoldNullableColumnsAs p.ClrType
            sb |> writeProperty p.Name typeName func
        )

        sb

    let generateNavigationProperties (entityType: IEntityType) scaffoldNullableColumnsAs useDataAnnotations sb =

        let sortedNavigations =
            entityType.GetNavigations()
            |> Seq.sortBy(fun n -> if n.IsOnDependent then 0 else 1)
            |> Seq.sortBy(fun n -> if n.IsCollection then 1 else 0)

        if not(sortedNavigations |> Seq.isEmpty) then
            sb |> appendEmptyLine |> ignore

        sortedNavigations
        |> Seq.iter(fun p ->

            let func =
                if useDataAnnotations then
                    generateNavigationDataAnnotations p
                else
                    (fun s -> s)

            let typeName =
                if isNull p.TargetEntityType.ClrType then
                    p.TargetEntityType.Name
                else
                    getTypeName scaffoldNullableColumnsAs p.TargetEntityType.ClrType
            let navigationType = if p.IsCollection then $"ICollection<{typeName}>" else typeName

            sb |> writeProperty p.Name navigationType func
        )

        sb

    let generateClass (entityType : IEntityType) useDataAnnotations scaffoldNullableColumnsAs sb =

        sb
            |>
                if useDataAnnotations then
                    generateEntityTypeDataAnnotations entityType
                else
                    id
            |> appendLine (sprintf "type %s() as this =" entityType.Name)
            |> indent
            |> generateConstructor entityType
            |> generateProperties entityType scaffoldNullableColumnsAs useDataAnnotations
            |> generateNavigationProperties entityType scaffoldNullableColumnsAs useDataAnnotations
            |> unindent

    let generateRecordTypeEntry useDataAnnotations scaffoldNullableColumnsAs (p: IProperty) sb =

        if useDataAnnotations then
            sb
                |> generatePropertyDataAnnotations p
                |> ignore

        let typeName = getTypeName scaffoldNullableColumnsAs p.ClrType
        sb |> appendLine (sprintf "%s: %s" p.Name typeName) |> ignore
        ()

    let writeRecordProperties (properties :IProperty seq) (useDataAnnotations:bool) scaffoldNullableColumnsAs sb =
        properties
        |> Seq.iter(fun p -> generateRecordTypeEntry useDataAnnotations scaffoldNullableColumnsAs p sb)

        sb

    let generateNavigateTypeEntry (n:INavigation) (useDataAnnotations:bool) sb =
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
        sb |> appendLine (sprintf "%s: %s" n.Name navigationType) |> ignore

    let writeNavigationProperties (nav:INavigation seq) (useDataAnnotations:bool) sb =
        nav |> Seq.iter(fun n -> generateNavigateTypeEntry n useDataAnnotations sb)
        sb

    let generateRecord (entityType : IEntityType) (useDataAnnotations:bool) scaffoldNullableColumnsAs sb =
        let properties =
            entityType.GetProperties()

        let navProperties =
            entityType
                    |> EntityTypeExtensions.GetNavigations
                    |> Seq.sortBy(fun n -> ((if n.IsOnDependent then 0 else 1), (if n.IsCollection then 1 else 0)))

        sb
            |> appendLine ("CLIMutable" |> createAttributeQuick)
            |> appendLine (sprintf "type %s = {" entityType.Name)
            |> indent
            |> writeRecordProperties properties useDataAnnotations scaffoldNullableColumnsAs
            |> writeNavigationProperties navProperties useDataAnnotations
            |> unindent
            |> appendLine "}"
            |> appendEmptyLine


    let writeCode (entityType: IEntityType) (useDataAnnotation: bool) scaffoldTypesAs scaffoldNullableColumnsAs sb =

        let generate =
            match scaffoldTypesAs with
            | ClassType -> generateClass
            | RecordType -> generateRecord

        sb
            |> indent
            |> generate entityType useDataAnnotation scaffoldNullableColumnsAs
            |> string

    interface ICSharpEntityTypeGenerator with
        member this.WriteCode(entityType, ``namespace``, useDataAnnotations) =
            let scaffoldTypesAs = if notNull config then config.ScaffoldTypesAs else RecordType
            let scaffoldNullableColumnsAs = if notNull config then config.ScaffoldNullableColumnsAs else OptionTypes
            writeCode entityType useDataAnnotations scaffoldTypesAs scaffoldNullableColumnsAs (IndentedStringBuilder())
