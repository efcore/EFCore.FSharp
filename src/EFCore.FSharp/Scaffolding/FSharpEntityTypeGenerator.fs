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
open EntityFrameworkCore.FSharp.Internal

open System.ComponentModel.DataAnnotations
open System.ComponentModel.DataAnnotations.Schema
open System.CodeDom.Compiler
open Microsoft.EntityFrameworkCore.Scaffolding

/// Create attributes of a given name
type internal AttributeWriter(name: string) =
    let parameters = List<string>()

    /// Add parameter to Attribute
    member __.AddParameter p = parameters.Add p

    /// Write attribute to string
    override __.ToString() =
        if Seq.isEmpty parameters then
            sprintf "[<%s>]" name
        else
            sprintf "[<%s(%s)>]" name (String.Join(", ", parameters))

type FSharpEntityTypeGenerator
    (annotationCodeGenerator: IAnnotationCodeGenerator, code: ICSharpHelper, config: ScaffoldOptions)
    =

    let createAttributeQuick =
        AttributeWriter
        >> string

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

    let writeProperty (name: string) (typeName: string) (annontationText: string option) =

        stringBuffer {
            $"[<DefaultValue>] val mutable private _{name} : {typeName}"
            ""
            annontationText
            $"member this.{name} with get() = this._{name} and set v = this._{name} <- v"
            ""
        }

    let rec getTypeName scaffoldNullableColumnsAs (t: Type) =

        if t.IsArray then
            (getTypeName scaffoldNullableColumnsAs (t.GetElementType()))
            + "[]"

        else if t.GetTypeInfo().IsGenericType then
            if t.GetGenericTypeDefinition() = typedefof<Nullable<_>> then
                match scaffoldNullableColumnsAs with
                | NullableTypes ->
                    "Nullable<"
                    + (getTypeName scaffoldNullableColumnsAs (Nullable.GetUnderlyingType(t)))
                    + ">"
                | OptionTypes ->
                    (getTypeName scaffoldNullableColumnsAs (Nullable.GetUnderlyingType(t)))
                    + " option"
            else
                let genericTypeDefName = t.Name.Substring(0, t.Name.IndexOf('`'))

                let genericTypeArguments =
                    String.Join(
                        ", ",
                        t.GenericTypeArguments
                        |> Seq.map (fun t' -> getTypeName scaffoldNullableColumnsAs t')
                    )

                genericTypeDefName
                + "<"
                + genericTypeArguments
                + ">"

        else
            match primitiveTypeNames.TryGetValue t with
            | true, value -> value
            | _ -> t.Name

    let generateRequiredAttribute (p: IProperty) =

        let isNullableOrOptionType (t: Type) =
            let typeInfo = t.GetTypeInfo()

            (typeInfo.IsValueType
             |> not)
            || (typeInfo.IsGenericType
                && (typeInfo.GetGenericTypeDefinition() = typedefof<Nullable<_>>
                    || typeInfo.GetGenericTypeDefinition() = typedefof<Option<_>>))

        if
            (not p.IsNullable)
            && (p.ClrType
                |> isNullableOrOptionType)
            && (p.IsPrimaryKey()
                |> not)
        then
            (nameof RequiredAttribute
             |> createAttributeQuick)
            |> Some
        else
            None

    let generateColumnAttribute (p: IProperty) =
        let columnName = p.GetColumnBaseName()
        let columnType = getConfiguredColumnType p

        let delimitedColumnName =
            if
                isNull columnName
                |> not
                && columnName
                   <> p.Name
            then
                FSharpUtilities.delimitString (columnName)
                |> Some
            else
                Option.None

        let delimitedColumnType =
            if
                isNull columnType
                |> not
            then
                FSharpUtilities.delimitString (columnType)
                |> Some
            else
                Option.None

        if
            delimitedColumnName.IsSome
            || delimitedColumnType.IsSome
        then
            let a =
                "ColumnAttribute"
                |> AttributeWriter

            match delimitedColumnName with
            | Some name ->
                name
                |> a.AddParameter
            | None -> ()

            match delimitedColumnType with
            | Some t ->
                (sprintf "TypeName = %s" t)
                |> a.AddParameter
            | None -> ()

            a
            |> string
            |> Some

        else
            None


    let generateMaxLengthAttribute (p: IProperty) =

        let ml = p.GetMaxLength()

        if ml.HasValue then
            let attrName =
                if p.ClrType = typedefof<string> then
                    "StringLengthAttribute"
                else
                    "MaxLengthAttribute"

            let a = AttributeWriter(attrName)
            a.AddParameter(code.Literal ml.Value)

            a
            |> string
            |> Some
        else
            None

    let generateKeyAttribute (property: IProperty) =
        if notNull (property.FindContainingPrimaryKey()) then
            (nameof KeyAttribute
             |> createAttributeQuick)
            |> Some
        else
            None

    let generateKeylessAttribute (entityType: IEntityType) =
        if isNull (entityType.FindPrimaryKey()) then
            (nameof KeylessAttribute
             |> createAttributeQuick)
            |> Some
        else
            None

    let generateTableAttribute (entityType: IEntityType) =

        let tableName = entityType.GetTableName()
        let schema = entityType.GetSchema()
        let defaultSchema = entityType.Model.GetDefaultSchema()

        let schemaParameterNeeded =
            notNull schema
            && schema
               <> defaultSchema

        let isView = notNull (entityType.GetViewName())

        let tableAttributeNeeded =
            (not isView)
            && (schemaParameterNeeded
                || notNull tableName
                   && tableName
                      <> entityType.GetDbSetName())

        if tableAttributeNeeded then
            let tableAttribute = AttributeWriter(nameof TableAttribute)

            tableAttribute.AddParameter(code.Literal(tableName))

            if schemaParameterNeeded then
                tableAttribute.AddParameter($"Schema = {code.Literal(schema)}")

            (string tableAttribute)
            |> Some
        else
            None

    let generateIndexAttributes (entityType: IEntityType) =

        let indexes =
            entityType.GetIndexes()
            |> Seq.filter (fun i ->
                ConfigurationSource.Convention
                <> ((i :?> IConventionIndex).GetConfigurationSource())
            )

        let attributes =
            indexes
            |> Seq.map (fun index ->
                let annotations =
                    annotationCodeGenerator.FilterIgnoredAnnotations(index.GetAnnotations())
                    |> annotationsToDictionary

                annotationCodeGenerator.RemoveAnnotationsHandledByConventions(index, annotations)

                if annotations.Count = 0 then
                    let indexAttribute = AttributeWriter(nameof IndexAttribute)

                    index.Properties
                    |> Seq.iter (fun p -> indexAttribute.AddParameter $"nameof({p.Name})")

                    if notNull index.Name then
                        indexAttribute.AddParameter $"Name = {code.Literal(index.Name)}"

                    if index.IsUnique then
                        indexAttribute.AddParameter $"IsUnique = {code.Literal(index.IsUnique)}"

                    string indexAttribute
                    |> Some

                else
                    None
            )

        let output = stringBuffer { attributes }

        if output.Length > 0 then Some output else None

    let generateForeignKeyAttribute (navigation: INavigation) =

        if
            navigation.IsOnDependent
            && navigation.ForeignKey.PrincipalKey.IsPrimaryKey()
        then
            let foreignKeyAttribute = AttributeWriter(nameof ForeignKeyAttribute)

            if navigation.ForeignKey.Properties.Count > 1 then
                let names =
                    navigation.ForeignKey.Properties
                    |> Seq.map (fun fk -> fk.Name)

                let param = String.Join(",", names)
                foreignKeyAttribute.AddParameter(code.Literal param)
            else
                foreignKeyAttribute.AddParameter(
                    code.Literal navigation.ForeignKey.Properties.[0].Name
                )

            (string foreignKeyAttribute)
            |> Some
        else
            None

    let generateInversePropertyAttribute (navigation: INavigation) =

        if
            navigation.ForeignKey.PrincipalKey.IsPrimaryKey()
            && notNull navigation.Inverse
        then
            let inverseNavigation = navigation.Inverse

            let inversePropertyAttribute = AttributeWriter(nameof InversePropertyAttribute)

            let nameMatches =
                navigation.DeclaringEntityType.GetPropertiesAndNavigations()
                |> Seq.exists (fun m -> m.Name = inverseNavigation.DeclaringEntityType.Name)

            let param =
                if nameMatches then
                    code.Literal inverseNavigation.Name
                else
                    $"\"{inverseNavigation.DeclaringEntityType.Name}.{inverseNavigation.Name}\""

            inversePropertyAttribute.AddParameter param

            (string inversePropertyAttribute)
            |> Some
        else
            None

    let generateEntityTypeDataAnnotations entityType =
        let annotations =
            stringBuffer {
                generateKeylessAttribute entityType
                generateTableAttribute entityType
                generateIndexAttributes entityType
            }

        if annotations.Length > 0 then Some annotations else None


    let generateConstructor (entityType: IEntityType) =

        let collectionNavigations =
            entityType.GetNavigations()
            |> Seq.filter (fun n -> n.IsCollection)

        if
            collectionNavigations
            |> Seq.isEmpty
        then
            None
        else
            stringBuffer {
                "do"

                indent {
                    for c in collectionNavigations do
                        $"this.{c.Name} <- HashSet<{c.TargetEntityType.Name}>() :> ICollection<{c.TargetEntityType.Name}>"

                    ""
                }
            }
            |> Some


    let generatePropertyDataAnnotations (p: IProperty) =

        let annotations =
            stringBuffer {
                generateKeyAttribute p
                generateRequiredAttribute p
                generateColumnAttribute p
                generateMaxLengthAttribute p

                let annotations =
                    annotationCodeGenerator.FilterIgnoredAnnotations(p.GetAnnotations())
                    |> annotationsToDictionary

                annotationCodeGenerator.RemoveAnnotationsHandledByConventions(p, annotations)

                for a in annotationCodeGenerator.GenerateDataAnnotationAttributes(p, annotations) do
                    let attributeWriter = AttributeWriter a.Type.Name

                    a.Arguments
                    |> Seq.iter (fun arg -> attributeWriter.AddParameter(code.UnknownLiteral arg))

                    string a
            }

        if annotations.Length > 0 then Some annotations else None

    let generateNavigationDataAnnotations (navigation: INavigation) =

        stringBuffer {
            generateForeignKeyAttribute navigation
            generateInversePropertyAttribute navigation
        }

    let generateProperties (entityType: IEntityType) scaffoldNullableColumnsAs useDataAnnotations =

        let props =
            entityType.GetProperties()
            |> Seq.sortBy (fun p -> p.GetColumnOrder().GetValueOrDefault(-1))

        stringBuffer {

            for p in props do
                let annotationText =
                    if useDataAnnotations then
                        generatePropertyDataAnnotations p
                    else
                        None

                let typeName = getTypeName scaffoldNullableColumnsAs p.ClrType

                writeProperty p.Name typeName annotationText
        }

    let generateSkipForeignKeyAttribute (navigation: ISkipNavigation) =

        if navigation.ForeignKey.PrincipalKey.IsPrimaryKey() then
            let foreignKeyAttribute = AttributeWriter(nameof ForeignKeyAttribute)

            let props =
                navigation.ForeignKey.Properties
                |> Seq.map (fun p -> p.Name)
                |> join ","

            foreignKeyAttribute.AddParameter(code.Literal props)

            (string foreignKeyAttribute)
            |> Some
        else
            None

    let generateSkipInversePropertyAttribute (navigation: ISkipNavigation) =

        if navigation.ForeignKey.PrincipalKey.IsPrimaryKey() then
            let inverseNavigation = navigation.Inverse

            if notNull inverseNavigation then
                let inversePropertyAttribute = AttributeWriter(nameof InversePropertyAttribute)

                let condition =
                    navigation.DeclaringEntityType.GetPropertiesAndNavigations()
                    |> Seq.exists (fun m -> m.Name = inverseNavigation.DeclaringEntityType.Name)

                if condition then
                    inversePropertyAttribute.AddParameter(code.Literal inverseNavigation.Name)
                else
                    inversePropertyAttribute.AddParameter(
                        $"nameof ({inverseNavigation.DeclaringEntityType.Name}.{inverseNavigation.Name})"
                    )

                (string inversePropertyAttribute)
                |> Some
            else
                None
        else
            None

    let generateSkipNavigationDataAnnotations (navigation: ISkipNavigation) =

        stringBuffer {
            generateSkipForeignKeyAttribute navigation
            generateSkipInversePropertyAttribute navigation
        }

    let generateSkipNavigationProperties
        (entityType: IEntityType)
        scaffoldNullableColumnsAs
        useDataAnnotations
        =

        let skipNavigations = entityType.GetSkipNavigations()

        if
            skipNavigations
            |> Seq.isEmpty
            |> not
        then

            stringBuffer {
                ""

                for p in skipNavigations do
                    let func =
                        if useDataAnnotations then
                            generateSkipNavigationDataAnnotations p
                            |> Some
                        else
                            None

                    let typeName = p.TargetEntityType.Name

                    let navigationType =
                        if p.IsCollection then
                            $"ICollection<{typeName}>"
                        else
                            typeName

                    writeProperty p.Name navigationType func
            }
            |> Some
        else
            None


    let generateNavigationProperties
        (entityType: IEntityType)
        scaffoldNullableColumnsAs
        useDataAnnotations
        =

        let sortedNavigations =
            entityType.GetNavigations()
            |> Seq.sortBy (fun n ->
                ((if n.IsOnDependent then 0 else 1), (if n.IsCollection then 1 else 0))
            )

        if
            not (
                sortedNavigations
                |> Seq.isEmpty
            )
        then

            stringBuffer {
                ""

                for p in sortedNavigations do
                    let func =
                        if useDataAnnotations then
                            generateNavigationDataAnnotations p
                            |> Some
                        else
                            None

                    let typeName = p.TargetEntityType.Name

                    let navigationType =
                        if p.IsCollection then
                            $"ICollection<{typeName}>"
                        else
                            typeName

                    writeProperty p.Name navigationType func
            }
            |> Some
        else
            None

    let generateClass (entityType: IEntityType) useDataAnnotations scaffoldNullableColumnsAs =

        indent {
            if useDataAnnotations then
                generateEntityTypeDataAnnotations entityType

            $"type {entityType.Name}() as this ="

            indent {
                generateConstructor entityType
                generateProperties entityType scaffoldNullableColumnsAs useDataAnnotations
                generateNavigationProperties entityType scaffoldNullableColumnsAs useDataAnnotations

                generateSkipNavigationProperties
                    entityType
                    scaffoldNullableColumnsAs
                    useDataAnnotations
            }
        }

    let generateRecordTypeEntry useDataAnnotations scaffoldNullableColumnsAs (p: IProperty) =

        stringBuffer {
            if useDataAnnotations then
                generatePropertyDataAnnotations p

            let typeName = getTypeName scaffoldNullableColumnsAs p.ClrType

            $"{p.Name}: {typeName}"
        }

    let generateNavigateTypeEntry (n: INavigation) (useDataAnnotations: bool) =

        stringBuffer {
            if useDataAnnotations then
                generateNavigationDataAnnotations n

            let referencedTypeName = n.TargetEntityType.Name

            let navigationType =
                if n.IsCollection then
                    sprintf "%s seq" referencedTypeName
                else
                    referencedTypeName

            $"{n.Name}: {navigationType}"
        }

    let generateSkipNavigateTypeEntry (n: ISkipNavigation) (useDataAnnotations: bool) =

        stringBuffer {
            if useDataAnnotations then
                generateSkipNavigationDataAnnotations n

            let referencedTypeName = n.TargetEntityType.Name

            let navigationType =
                if n.IsCollection then
                    sprintf "%s seq" referencedTypeName
                else
                    referencedTypeName

            $"{n.Name}: {navigationType}"
        }

    let generateRecord
        (entityType: IEntityType)
        (useDataAnnotations: bool)
        scaffoldNullableColumnsAs
        =

        let navProperties =
            entityType.GetNavigations()
            |> Seq.sortBy (fun n ->
                ((if n.IsOnDependent then 0 else 1), (if n.IsCollection then 1 else 0))
            )

        let skipNavigations = entityType.GetSkipNavigations()

        indent {
            "[<CLIMutable>]"
            $"type {entityType.Name} = {{"

            indent {
                // Properties
                for p in entityType.GetProperties() do
                    generateRecordTypeEntry useDataAnnotations scaffoldNullableColumnsAs p

                // Navigation Properties
                for n in navProperties do
                    generateNavigateTypeEntry n useDataAnnotations

                // SkipNavigation Properties
                for n in skipNavigations do
                    generateSkipNavigateTypeEntry n useDataAnnotations
            }

            "}"
            ""
        }

    let writeCode
        (entityType: IEntityType)
        (options: ModelCodeGenerationOptions)
        scaffoldTypesAs
        scaffoldNullableColumnsAs
        =

        let generate =
            match scaffoldTypesAs with
            | ClassType -> generateClass
            | RecordType -> generateRecord

        generate entityType options.UseDataAnnotations scaffoldNullableColumnsAs

    let mutable _entityType: IEntityType = null
    let mutable _options: ModelCodeGenerationOptions = null
    let mutable _namespaceHint: string = null
    let mutable _session: IDictionary<string, obj> = Dictionary<string, obj>()


    member this.EntityType
        with get () = _entityType
        and set v = _entityType <- v

    member this.Options
        with get () = _options
        and set v = _options <- v

    member this.NamespaceHint
        with get () = _namespaceHint
        and set v = _namespaceHint <- v

    member this.Session
        with get () = _session
        and set v = _session <- v

    member this.Errors = CompilerErrorCollection()

    member this.AddError errorMsg =
        let ce = new CompilerError()
        ce.ErrorText <- errorMsg

        this.Errors.Add(ce)
        |> ignore

    member this.AddWarning warningMsg =
        let ce = new CompilerError()
        ce.ErrorText <- warningMsg
        ce.IsWarning <- true

        this.Errors.Add(ce)
        |> ignore

    member this.Initialize() =
        if not this.Errors.HasErrors then
            if this.Session.ContainsKey("EntityType") then
                this.EntityType <- this.Session.["EntityType"] :?> IEntityType
            else
                this.AddError "EntityType is not set in the session"

            if this.Session.ContainsKey("Options") then
                this.Options <- this.Session.["Options"] :?> ModelCodeGenerationOptions
            else
                this.AddError "Options is not set in the session"

            if this.Session.ContainsKey("NamespaceHint") then
                this.NamespaceHint <- this.Session.["NamespaceHint"] :?> string
            else
                this.AddError "NamespaceHint is not set in the session"


    interface ITextTransformation with
        member this.TransformText() =

            if this.EntityType.IsSimpleManyToManyJoinEntityType() then
                // Don't scaffold these
                ""
            else
                let scaffoldTypesAs =
                    if notNull config then
                        config.ScaffoldTypesAs
                    else
                        RecordType

                let scaffoldNullableColumnsAs =
                    if notNull config then
                        config.ScaffoldNullableColumnsAs
                    else
                        OptionTypes

                writeCode this.EntityType this.Options scaffoldTypesAs scaffoldNullableColumnsAs
