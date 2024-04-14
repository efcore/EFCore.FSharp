namespace EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.Collections.Generic

open EntityFrameworkCore.FSharp.SharedTypeExtensions
open EntityFrameworkCore.FSharp.EntityFrameworkExtensions
open EntityFrameworkCore.FSharp.Utilities

open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Storage
open EntityFrameworkCore.FSharp

type FSharpSnapshotGenerator
    (
        code: ICSharpHelper,
        mappingSource: IRelationalTypeMappingSource,
        annotationCodeGenerator: IAnnotationCodeGenerator
    ) =

    let mutable _typeQualifiedCalls: string option = None

    let hasAnnotationMethodInfo =
        getRequiredRuntimeMethod (
            typeof<ModelBuilder>,
            "HasAnnotation",
            [|
                typeof<string>
                typeof<string>
            |]
        )

    let findValueConverter (p: IProperty) =
        let pValueConverter = p.GetValueConverter()

        if
            p.GetValueConverter()
            |> isNull
        then
            let typeMapping = p.FindTypeMapping()

            if
                typeMapping
                |> isNull
            then
                let mapping = mappingSource.FindMapping(p)

                if
                    mapping
                    |> isNull
                then
                    None
                elif
                    mapping.Converter
                    |> isNull
                then
                    None
                else
                    mapping.Converter
                    |> Some
            elif
                typeMapping.Converter
                |> isNull
            then
                None
            else
                typeMapping.Converter
                |> Some
        else
            pValueConverter
            |> Some

    let generateAnnotations
        (builderName: string)
        (annotatable: IAnnotatable)
        (annotations: Dictionary<string, IAnnotation>)
        (inChainedCall: bool)
        (leadingNewLine: bool)
        =

        let fluentApiCalls =
            annotationCodeGenerator.GenerateFluentApiCalls(annotatable, annotations)

        let mutable (chainedCall: MethodCallCodeFragment option) = None

        let typeQualifiedCalls = ResizeArray<MethodCallCodeFragment>()

        fluentApiCalls
        |> Seq.iter (fun call ->
            if
                notNull call.MethodInfo
                && call.MethodInfo.IsStatic
                && (isNull call.MethodInfo.DeclaringType
                    || call.MethodInfo.DeclaringType.Assembly
                       <> typeof<RelationalModelBuilderExtensions>.Assembly)
            then
                typeQualifiedCalls.Add call
            else
                chainedCall <-
                    match chainedCall with
                    | None -> Some call
                    | Some c -> Some(c.Chain(call))
        )

        // Append remaining raw annotations which did not get generated as Fluent API calls
        annotations.Values
        |> Seq.sortBy (fun a -> a.Name)
        |> Seq.iter (fun a ->
            let call = MethodCallCodeFragment(hasAnnotationMethodInfo, a.Name, a.Value)

            chainedCall <-
                match chainedCall with
                | None -> Some call
                | Some c -> Some(c.Chain(call))
        )


        let chainedCalls =
            match chainedCall with
            | Some c ->
                if inChainedCall then
                    stringBuffer {
                        (code.Fragment c)
                        + " |> ignore"
                    }
                else

                    stringBuffer {
                        if leadingNewLine then
                            ""

                        (code.Fragment c)
                        + " |> ignore"
                    }

            | None -> if inChainedCall then "|> ignore" else ""

        let buildTypeQualifiedCall call =
            code.Fragment(call, builderName, typeQualified = true)
            + " |> ignore"

        let typeQualifiedCalls =
            if typeQualifiedCalls.Count > 0 then
                stringBuffer {
                    ""

                    typeQualifiedCalls
                    |> Seq.map buildTypeQualifiedCall
                }
                |> Some
            else
                None

        _typeQualifiedCalls <- typeQualifiedCalls

        if chainedCalls.Trim().Length > 0 then
            Some(chainedCalls.Trim())
        else
            None

    let generateFluentApiForDefaultValue (property: IProperty) =
        match property.TryGetDefaultValue() with
        | true, defaultValue ->
            let valueConverter =
                if
                    defaultValue
                    <> (box DBNull.Value)
                then
                    let valueConverter =
                        property.GetValueConverter()
                        |> Option.ofObj

                    let typeMap =
                        if
                            property.FindTypeMapping()
                            |> Option.ofObj
                            |> Option.isSome
                        then
                            property.FindTypeMapping()
                            |> Option.ofObj
                        else
                            (mappingSource.FindMapping(property) :> CoreTypeMapping)
                            |> Option.ofObj

                    match valueConverter, typeMap with
                    | Some v, _ ->
                        v
                        |> Option.ofObj
                    | None, Some t ->
                        t.Converter
                        |> Option.ofObj
                    | _ -> None
                else
                    None

            let appendValueConverter =
                let value =
                    match valueConverter with
                    | Some vc -> vc.ConvertToProvider.Invoke(defaultValue)
                    | None -> defaultValue

                code.UnknownLiteral(value)

            $".HasDefaultValue({appendValueConverter})"
            |> Some

        | _ -> None

    let genPropertyAnnotations propertyBuilderName (property: IProperty) =
        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(property.GetAnnotations())
            |> annotationsToDictionary

        let columnType (p: IProperty) =
            let columnType =
                p.GetColumnType()
                ?= mappingSource.GetMapping(p).StoreType

            code.Literal columnType

        let removeAnnotation (annotation: string) =
            annotations.Remove(annotation)
            |> ignore

        let generateFluentApiForMaxLength =
            property.GetMaxLength()
            |> Option.ofNullable
            |> Option.map (fun i -> $".HasMaxLength({code.Literal i})")

        let generateFluentApiForPrecisionAndScale =
            property.GetPrecision()
            |> Option.ofNullable
            |> Option.map (fun i ->
                let scale =
                    if property.GetScale().HasValue then
                        $", {code.UnknownLiteral(property.GetScale().Value)}"
                    else
                        ""

                annotations.Remove(CoreAnnotationNames.Precision)
                |> ignore

                $".HasPrecision({code.UnknownLiteral i}{scale})"

            )

        let generateFluentApiForUnicode =
            property.IsUnicode()
            |> Option.ofNullable
            |> Option.map (fun b -> $".IsUnicode({code.Literal b})")

        stringBuffer {
            generateFluentApiForMaxLength
            generateFluentApiForPrecisionAndScale
            generateFluentApiForUnicode
            $".HasColumnType({columnType property})"

            removeAnnotation RelationalAnnotationNames.ColumnType

            generateFluentApiForDefaultValue property
            removeAnnotation RelationalAnnotationNames.DefaultValue

            generateAnnotations propertyBuilderName property annotations true true
        }

    let generateBaseType (entityTypeBuilderName: string) (baseType: IEntityType) =

        if notNull baseType then
            stringBuffer {
                ""
                $"{entityTypeBuilderName}.HasBaseType({code.Literal baseType.Name}) |> ignore"
            }
            |> Some
        else
            None

    let generateProperty (entityTypeBuilderName: string) (p: IProperty) =

        let converter = findValueConverter p

        let clrType =
            match converter with
            | Some c ->
                if isNullableType p.ClrType then
                    makeNullable p.IsNullable c.ProviderClrType
                elif isOptionType p.ClrType then
                    makeOptional p.IsNullable c.ProviderClrType
                else
                    c.ProviderClrType
            | None -> p.ClrType

        let isPropertyRequired =
            let isNullable =
                (isOptionType clrType
                 || isNullableType clrType)

            (p.IsPrimaryKey())
            || (not isNullable)

        let propertyBuilderTypeName =
            $"{entityTypeBuilderName}.Property<{code.Reference(clrType)}>({code.Literal(p.Name)})"

        let valueGenerated =
            if
                p.ValueGenerated
                <> ValueGenerated.Never
            then
                let v =
                    if p.ValueGenerated = ValueGenerated.OnAdd then
                        ".ValueGeneratedOnAdd()"
                    else if p.ValueGenerated = ValueGenerated.OnUpdate then
                        ".ValueGeneratedOnUpdate()"
                    else
                        ".ValueGeneratedOnAddOrUpdate()"

                Some v
            else
                None

        stringBuffer {
            ""
            propertyBuilderTypeName

            indent {
                if p.IsConcurrencyToken then
                    ".IsConcurrencyToken()"

                $".IsRequired(%b{isPropertyRequired})"
                valueGenerated
                genPropertyAnnotations propertyBuilderTypeName p
            }

            _typeQualifiedCalls
        }

    let generateProperties (entityTypeBuilderName: string) (properties: IProperty seq) =
        stringBuffer {
            for p in properties do
                generateProperty entityTypeBuilderName p
        }

    let generateKeyAnnotations (keyBuilderName: string) (key: IKey) =
        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(key.GetAnnotations())
            |> annotationsToDictionary

        generateAnnotations keyBuilderName key annotations true true

    let generateKey (entityTypeBuilderName: string) (key: IKey) (isPrimary: bool) =

        let keyBuilderName =
            let props =
                key.Properties
                |> Seq.map (fun p ->
                    (p.Name
                     |> code.Literal)
                )
                |> join ", "

            let methodName = if isPrimary then "HasKey" else "HasAlternateKey"

            sprintf "%s.%s(%s)" entityTypeBuilderName methodName props

        let keyAnnotations = generateKeyAnnotations keyBuilderName key

        stringBuffer {
            ""
            keyBuilderName

            if keyAnnotations.IsSome then
                indent { keyAnnotations }
                _typeQualifiedCalls
        }

    let generateKeys (entityTypeBuilderName: string) (keys: IKey seq) (pk: IKey) =

        let primaryKey =
            if notNull pk then
                generateKey entityTypeBuilderName pk true
                |> Some
            else
                None

        let otherKeys =
            if
                isNull pk
                || pk.DeclaringEntityType.IsOwned()
            then
                keys
                |> Seq.filter (fun k ->
                    k <> pk
                    && (k.GetReferencingForeignKeys()
                        |> Seq.isEmpty
                        || k.GetAnnotations()
                           |> Seq.exists (fun a ->
                               a.Name
                               <> RelationalAnnotationNames.UniqueConstraintMappings
                           ))
                )
                |> Seq.map (fun k -> generateKey entityTypeBuilderName k false)
            else
                Seq.empty

        stringBuffer {
            primaryKey
            otherKeys
        }

    let generateIndexAnnotations indexBuilderName (idx: IIndex) =
        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(idx.GetAnnotations())
            |> annotationsToDictionary

        generateAnnotations indexBuilderName idx annotations true true

    let generateIndex (entityTypeBuilderName: string) (idx: IIndex) =

        let indexParams =
            if isNull idx.Name then
                String.Join(
                    ", ",
                    (idx.Properties
                     |> Seq.map (fun p ->
                         p.Name
                         |> code.Literal
                     ))
                )
            else
                sprintf
                    "[| %s |], %s"
                    (String.Join(
                        "; ",
                        (idx.Properties
                         |> Seq.map (fun p ->
                             p.Name
                             |> code.Literal
                         ))
                    ))
                    (code.Literal idx.Name)

        let indexBuilderName = sprintf "%s.HasIndex(%s)" entityTypeBuilderName indexParams

        stringBuffer {
            ""
            indexBuilderName

            indent {
                if idx.IsUnique then
                    ".IsUnique()"

                generateIndexAnnotations indexBuilderName idx
            }

            _typeQualifiedCalls
        }

    let generateIndexes (entityTypeBuilderName: string) (indexes: IIndex seq) =

        stringBuffer {
            for idx in indexes do
                ""
                generateIndex entityTypeBuilderName idx
        }

    let processDataItem (props: IProperty seq) (data: IDictionary<string, obj>) =

        let writeProperty (p: IProperty) =
            match data.TryGetValue p.Name with
            | true, value ->
                if notNull value then
                    $"%s{code.Identifier p.Name} = %s{code.UnknownLiteral value}"
                else
                    ""
            | _ -> ""

        let propsToWrite =
            props
            |> Seq.map writeProperty
            |> join "; "

        $"{{| {propsToWrite} |}}"

    let processDataItems (data: IDictionary<string, obj> seq) (propsToOutput: IProperty seq) =

        stringBuffer {
            for d in data do
                processDataItem propsToOutput d
        }

    let generateSequence (builderName: string) (sequence: ISequence) =

        let clrType =
            if
                sequence.Type
                <> Sequence.DefaultClrType
            then
                $"<{code.Reference sequence.Type}>"
            else
                ""

        let schema =
            if
                String.IsNullOrEmpty(sequence.Schema)
                |> not
                && sequence.Model.GetDefaultSchema()
                   <> sequence.Schema
            then
                $", {code.Literal sequence.Schema}"
            else
                ""

        let lines =
            seq {
                if
                    sequence.StartValue
                    <> (Sequence.DefaultStartValue
                        |> int64)
                then
                    $".StartsAt({code.Literal sequence.StartValue})"

                if
                    sequence.IncrementBy
                    <> Sequence.DefaultIncrementBy
                then
                    $".IncrementsBy({code.Literal sequence.IncrementBy})"

                if
                    sequence.MinValue
                    <> Sequence.DefaultMinValue
                then
                    $".HasMin({code.Literal sequence.MinValue})"

                if
                    sequence.MaxValue
                    <> Sequence.DefaultMaxValue
                then
                    $".HasMax({code.Literal sequence.MaxValue})"

                if
                    sequence.IsCyclic
                    <> Sequence.DefaultIsCyclic
                then
                    ".IsCyclic()"
            }

        let sequenceDefinition =
            $"{builderName}.HasSequence{clrType}({code.Literal sequence.Name}{schema}"

        if
            lines
            |> Seq.isEmpty
        then

            stringBuffer {
                ""

                sequenceDefinition
                + ") |> ignore"
            }
        else
            stringBuffer {
                ""
                sequenceDefinition
                lines
                ") |> ignore"
            }

    member this.generateCheckConstraints (builderName: string) (entityType: IEntityType) =
        let generateCheckConstraint (c: ICheckConstraint) =
            let name = code.Literal c.Name
            let sql = code.Literal c.Sql

            if
                c.Name
                <> (c.GetDefaultName()
                    ?= c.ModelName)
            then
                $"{builderName}.HasCheckConstraint({name}, {sql}, (fun c -> c.HasName({c.Name}))) |> ignore"
            else
                $"{builderName}.HasCheckConstraint({name}, {sql}) |> ignore"

        stringBuffer {
            for c in entityType.GetCheckConstraints() do
                ""
                generateCheckConstraint c
        }

    member this.generateEntityTypeAnnotations
        (entityTypeBuilderName: string)
        (entityType: IEntityType)
        =

        let sb = IndentedStringBuilder()

        let annotationAndValueNotNull (annotation: IAnnotation) =
            notNull annotation
            && notNull annotation.Value

        let getAnnotationValue (annotation: IAnnotation) (defaultValue: unit -> string) =
            if annotationAndValueNotNull annotation then
                string annotation.Value
            else
                defaultValue ()

        let annotationList =
            entityType.GetAnnotations()
            |> Seq.toList

        let findInList (a: IAnnotation list) name =
            a
            |> List.tryFind (fun an -> an.Name = name)
            |> Option.toObj

        let discriminatorPropertyAnnotation =
            findInList annotationList CoreAnnotationNames.DiscriminatorProperty

        let discriminatorMappingCompleteAnnotation =
            findInList annotationList CoreAnnotationNames.DiscriminatorMappingComplete

        let discriminatorValueAnnotation =
            findInList annotationList CoreAnnotationNames.DiscriminatorValue

        let hasDiscriminator =
            annotationAndValueNotNull discriminatorPropertyAnnotation
            || annotationAndValueNotNull discriminatorMappingCompleteAnnotation
            || annotationAndValueNotNull discriminatorValueAnnotation

        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(entityType.GetAnnotations())
            |> annotationsToDictionary

        let tryGetAnnotationByName (name: string) =
            match annotations.TryGetValue name with
            | true, a -> a
            | _ -> null


        let tableNameAnnotation = tryGetAnnotationByName RelationalAnnotationNames.TableName

        if
            annotationAndValueNotNull tableNameAnnotation
            || (isNull entityType.BaseType)
        then

            let tableName = getAnnotationValue tableNameAnnotation entityType.GetTableName

            if
                notNull tableName
                || notNull tableNameAnnotation
            then
                sb.AppendLine("").Append(entityTypeBuilderName).Append(".ToTable(")
                |> ignore

                let schemaAnnotation = tryGetAnnotationByName RelationalAnnotationNames.Schema

                let schema = getAnnotationValue schemaAnnotation entityType.GetSchema

                if
                    isNull tableName
                    && (isNull schemaAnnotation
                        || isNull schema)
                then
                    sb.Append(sprintf "(string %s)" (code.UnknownLiteral tableName))
                    |> ignore
                else
                    sb.Append(code.UnknownLiteral tableName)
                    |> ignore

                if notNull tableNameAnnotation then
                    annotations.Remove(tableNameAnnotation.Name)
                    |> ignore

                let isExcludedAnnotation =
                    tryGetAnnotationByName RelationalAnnotationNames.IsTableExcludedFromMigrations

                if
                    notNull schema
                    || (notNull schemaAnnotation
                        && notNull tableName)
                then
                    if
                        isNull schema
                        && (notNull isExcludedAnnotation
                            && (isExcludedAnnotation.Value :?> Nullable<bool>).GetValueOrDefault()
                               <> true)
                    then
                        sb.Append(sprintf ", (string %s)" (code.UnknownLiteral schema))
                        |> ignore
                    elif notNull schema then
                        sb.Append(sprintf ", %s" (code.UnknownLiteral schema))
                        |> ignore

                if notNull isExcludedAnnotation then
                    if (isExcludedAnnotation.Value :?> Nullable<bool>).GetValueOrDefault() then
                        sb.Append ", (fun t -> t.ExcludeFromMigrations())"
                        |> ignore

                    annotations.Remove(isExcludedAnnotation.Name)
                    |> ignore


                sb.Append ") |> ignore"
                |> ignore

        annotations.Remove(RelationalAnnotationNames.Schema)
        |> ignore

        let viewNameAnnotation = tryGetAnnotationByName RelationalAnnotationNames.ViewName

        if
            (annotationAndValueNotNull viewNameAnnotation
             || isNull entityType.BaseType)
        then
            let viewName = getAnnotationValue viewNameAnnotation entityType.GetViewName

            if notNull viewName then
                sb
                    .AppendLine("")
                    .Append(
                        sprintf "%s.ToView(%s" entityTypeBuilderName (code.UnknownLiteral viewName)
                    )
                |> ignore

                if notNull viewNameAnnotation then
                    annotations.Remove(viewNameAnnotation.Name)
                    |> ignore

                let viewSchemaAnnotation =
                    tryGetAnnotationByName RelationalAnnotationNames.ViewSchema

                if annotationAndValueNotNull viewSchemaAnnotation then
                    let viewSchemaAnnotationValue = viewSchemaAnnotation.Value :?> string

                    sb.Append(", ").Append(code.UnknownLiteral viewSchemaAnnotationValue)
                    |> ignore

                    annotations.Remove(viewSchemaAnnotation.Name)
                    |> ignore

                sb.Append ") |> ignore"
                |> ignore

        annotations.Remove(RelationalAnnotationNames.ViewSchema)
        |> ignore

        annotations.Remove(RelationalAnnotationNames.ViewDefinitionSql)
        |> ignore

        let functionNameAnnotation =
            tryGetAnnotationByName RelationalAnnotationNames.FunctionName

        if
            annotationAndValueNotNull functionNameAnnotation
            || isNull entityType.BaseType
        then
            let functionName =
                getAnnotationValue functionNameAnnotation entityType.GetFunctionName

            if
                notNull functionName
                || notNull functionNameAnnotation
            then
                sb
                    .AppendLine("")
                    .Append(entityTypeBuilderName)
                    .Append(".ToFunction(")
                    .Append(code.UnknownLiteral functionName)
                    .Append(") |> ignore")
                |> ignore

                if notNull functionNameAnnotation then
                    annotations.Remove(functionNameAnnotation.Name)
                    |> ignore

        let sqlQueryAnnotation = tryGetAnnotationByName RelationalAnnotationNames.SqlQuery

        if
            annotationAndValueNotNull sqlQueryAnnotation
            || isNull entityType.BaseType
        then
            let sqlQuery = getAnnotationValue sqlQueryAnnotation entityType.GetSqlQuery

            if
                notNull sqlQuery
                || notNull sqlQueryAnnotation
            then
                sb
                    .AppendLine("")
                    .Append(entityTypeBuilderName)
                    .Append(".ToSqlQuery(")
                    .Append(code.UnknownLiteral sqlQuery)
                    .Append(") |> ignore")
                |> ignore

                if notNull sqlQueryAnnotation then
                    annotations.Remove(sqlQueryAnnotation.Name)
                    |> ignore

        if hasDiscriminator then

            sb.AppendLine("").Append(entityTypeBuilderName).Append(".HasDiscriminator")
            |> ignore

            if annotationAndValueNotNull discriminatorPropertyAnnotation then
                let discriminatorProperty =
                    entityType.FindProperty(discriminatorPropertyAnnotation.Value :?> string)

                let propertyClrType =
                    match
                        discriminatorProperty
                        |> findValueConverter
                    with
                    | Some c ->
                        c.ProviderClrType
                        |> makeNullable discriminatorProperty.IsNullable
                    | None -> discriminatorProperty.ClrType

                sb
                    .Append("<")
                    .Append(code.Reference(propertyClrType))
                    .Append(">(")
                    .Append(code.Literal(discriminatorPropertyAnnotation.Value :?> string))
                    .Append(")")
                |> ignore
            else
                sb.Append "()"
                |> ignore

            if annotationAndValueNotNull discriminatorMappingCompleteAnnotation then
                let value = discriminatorMappingCompleteAnnotation.Value

                sb.Append(".IsComplete(").Append(code.UnknownLiteral(value)).Append(")")
                |> ignore

            if annotationAndValueNotNull discriminatorValueAnnotation then
                let discriminatorProperty = entityType.FindDiscriminatorProperty()

                let defaultValue = discriminatorValueAnnotation.Value

                let value =
                    if notNull discriminatorProperty then
                        match
                            discriminatorProperty
                            |> findValueConverter
                        with
                        | Some c -> c.ConvertToProvider.Invoke(defaultValue)
                        | None -> defaultValue
                    else
                        defaultValue

                sb.Append(".HasValue(").Append(code.UnknownLiteral(value)).Append(")")
                |> ignore

            sb.Append " |> ignore"
            |> ignore

        stringBuffer {
            string sb
            generateAnnotations entityTypeBuilderName entityType annotations false true
            _typeQualifiedCalls
        }

    member private this.generateForeignKeyAnnotations entityTypeBuilderName (fk: IForeignKey) =

        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(fk.GetAnnotations())
            |> annotationsToDictionary

        generateAnnotations entityTypeBuilderName fk annotations true false

    member private this.generateForeignKey entityTypeBuilderName (fk: IForeignKey) =

        let literalPropNames (properties: seq<IProperty>) =
            properties
            |> Seq.map (fun p ->
                p.Name
                |> code.Literal
            )
            |> join ", "

        let definition =
            if not fk.IsOwnership then
                let dependent =
                    if isNull fk.DependentToPrincipal then
                        code.UnknownLiteral null
                    else
                        fk.DependentToPrincipal.Name
                        |> code.Literal

                $"{entityTypeBuilderName}.HasOne({code.Literal fk.PrincipalEntityType.Name}, {dependent})"
            else
                let dependent =
                    if notNull fk.DependentToPrincipal then
                        code.Literal fk.DependentToPrincipal.Name
                    else
                        ""

                $"{entityTypeBuilderName}.WithOwner({dependent})"

        let lines = ResizeArray<string>()

        let ptd =
            if notNull fk.PrincipalToDependent then
                code.Literal fk.PrincipalToDependent.Name
            else
                ""

        if
            fk.IsUnique
            && (not fk.IsOwnership)
        then

            lines.Add $".WithOne({ptd})"

            lines.Add
                $".HasForeignKey({code.Literal fk.DeclaringEntityType.Name}, {literalPropNames fk.Properties})"

            if
                fk.PrincipalKey
                <> fk.PrincipalEntityType.FindPrimaryKey()
            then

                lines.Add
                    $".HasPrincipalKey({code.Literal fk.PrincipalEntityType.Name}, {literalPropNames fk.PrincipalKey.Properties})"

        else
            if not fk.IsOwnership then
                lines.Add $".WithMany({ptd})"

            lines.Add $".HasForeignKey({literalPropNames fk.Properties})"

            if
                fk.PrincipalKey
                <> fk.PrincipalEntityType.FindPrimaryKey()
            then
                lines.Add $".HasPrincipalKey({literalPropNames fk.PrincipalKey.Properties})"

        if not fk.IsOwnership then
            if
                fk.DeleteBehavior
                <> DeleteBehavior.ClientSetNull
            then
                lines.Add $".OnDelete({code.Literal fk.DeleteBehavior})"

            if fk.IsRequired then
                lines.Add ".IsRequired()"

        stringBuffer {
            definition

            indent {
                lines
                this.generateForeignKeyAnnotations entityTypeBuilderName fk
            }

            _typeQualifiedCalls
        }

    member private this.generateForeignKeys entityTypeBuilderName (foreignKeys: IForeignKey seq) =

        stringBuffer {
            for fk in foreignKeys do
                this.generateForeignKey entityTypeBuilderName fk
        }

    member private this.generateOwnedTypes entityTypeBuilderName (ownerships: IForeignKey seq) =
        stringBuffer {
            for o in ownerships do
                this.generateEntityType entityTypeBuilderName o.DeclaringEntityType
        }

    member private this.generateRelationships
        (entityTypeBuilderName: string)
        (entityType: IEntityType)
        : string =

        let ownerships =
            entityType
            |> getDeclaredReferencingForeignKeys
            |> Seq.filter (fun fk -> fk.IsOwnership)

        stringBuffer {
            this.generateForeignKeys entityTypeBuilderName (getDeclaredForeignKeys entityType)
            this.generateOwnedTypes entityTypeBuilderName ownerships
        }

    member private this.generateNavigationAnnotations
        (navigationBuilderName: string)
        (navigation: INavigation)
        =
        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(navigation.GetAnnotations())
            |> annotationsToDictionary

        generateAnnotations navigationBuilderName navigation annotations true false

    member private this.generateNavigations
        (entityTypeBuilderName: string)
        (navigations: INavigation seq)
        =

        let createNavigation (n: INavigation) =
            let navigationBuilderName =
                $"{entityTypeBuilderName}.Navigation({code.Literal(n.Name)})"

            let isRequired =
                (not n.IsOnDependent)
                && (not n.IsCollection)
                && n.ForeignKey.IsRequiredDependent

            stringBuffer {
                ""
                navigationBuilderName

                if isRequired then
                    ""
                    ".IsRequired()"

                this.generateNavigationAnnotations navigationBuilderName n
                _typeQualifiedCalls
            }

        stringBuffer {
            for n in navigations do
                createNavigation n
        }

    member private this.generateData
        builderName
        (properties: IProperty seq)
        (data: IDictionary<string, obj> seq)
        =
        if Seq.isEmpty data then
            None
        else
            stringBuffer {
                ""
                $"{builderName}.HasData([|"
                indent { processDataItems data properties }
                " |]) |> ignore"
            }
            |> Some

    member private this.generateEntityType
        (builderName: string)
        (entityType: IEntityType)
        : string =

        let ownership = entityType.FindOwnership()

        let ownerNav =
            if isNull ownership then
                None
            else
                Some ownership.PrincipalToDependent.Name

        let declaration =
            match ownerNav with
            | None ->
                sprintf
                    ".Entity(%s"
                    (entityType.Name
                     |> code.Literal)
            | Some o ->
                if ownership.IsUnique then
                    sprintf
                        ".OwnsOne(%s, %s"
                        (entityType.Name
                         |> code.Literal)
                        (o
                         |> code.Literal)
                else
                    sprintf
                        ".OwnsMany(%s, %s"
                        (entityType.Name
                         |> code.Literal)
                        (o
                         |> code.Literal)

        let entityTypeBuilderName =
            if builderName.StartsWith("b", StringComparison.Ordinal) then
                let mutable counter = 1

                match builderName.Length > 1, Int32.TryParse(builderName.[1..]) with
                | true, (true, _) ->
                    counter <-
                        counter
                        + 1
                | _ -> ()

                "b"
                + if counter = 0 then "" else counter.ToString()
            else
                "b"

        stringBuffer {
            ""
            $"{builderName}{declaration}, (fun {entityTypeBuilderName} ->"

            indent {
                generateBaseType entityTypeBuilderName entityType.BaseType

                generateProperties
                    entityTypeBuilderName
                    (entityType
                     |> getDeclaredProperties)

                generateKeys
                    entityTypeBuilderName
                    (entityType
                     |> getDeclaredKeys)
                    (if isNull entityType.BaseType then
                         entityType.FindPrimaryKey()
                     else
                         null)

                generateIndexes
                    entityTypeBuilderName
                    (entityType
                     |> getDeclaredIndexes)

                this.generateEntityTypeAnnotations entityTypeBuilderName entityType
                this.generateCheckConstraints entityTypeBuilderName entityType

                ownerNav
                |> Option.map (fun _ -> this.generateRelationships entityTypeBuilderName entityType)

                this.generateData
                    entityTypeBuilderName
                    (entityType.GetProperties())
                    (entityType
                     |> getData true)
            }

            ")) |> ignore"
        }

    member private this.generateEntityTypeRelationships builderName (entityType: IEntityType) =

        stringBuffer {
            $"{builderName}.Entity({code.Literal entityType.Name}, (fun b ->"
            indent { this.generateRelationships "b" entityType }
            ")) |> ignore"
        }

    member private this.generateEntityTypeNavigations builderName (entityType: IEntityType) =

        let navigations =
            entityType.GetDeclaredNavigations()
            |> Seq.filter (fun n ->
                (not n.IsOnDependent)
                && (not n.ForeignKey.IsOwnership)
            )

        stringBuffer {
            $"{builderName}.Entity({code.Literal entityType.Name}, (fun b ->"
            indent { this.generateNavigations "b" navigations }
            ")) |> ignore"
        }

    member private this.generateEntityTypes builderName (entities: IEntityType seq) =

        let entitiesToWrite =
            entities
            |> Seq.filter (
                findOwnership
                >> isNull
            )

        let relationships =
            entitiesToWrite
            |> Seq.filter (fun e ->
                (e
                 |> getDeclaredForeignKeys
                 |> Seq.isEmpty
                 |> not)
                || (e
                    |> getDeclaredReferencingForeignKeys
                    |> Seq.exists (fun fk -> fk.IsOwnership))
            )

        let navigations =
            entitiesToWrite
            |> Seq.filter (fun e ->
                e.GetDeclaredNavigations()
                |> Seq.exists (fun n ->
                    (not n.IsOnDependent)
                    && (not n.ForeignKey.IsOwnership)
                )
            )


        stringBuffer {
            for e in entitiesToWrite do
                this.generateEntityType builderName e

            for r in relationships do
                this.generateEntityTypeRelationships builderName r

            for n in navigations do
                this.generateEntityTypeNavigations builderName n
        }

    member _.generatePropertyAnnotations property = genPropertyAnnotations property

    interface Microsoft.EntityFrameworkCore.Migrations.Design.ICSharpSnapshotGenerator with
        member this.Generate(builderName, model, sb) =
            let annotations =
                annotationCodeGenerator.FilterIgnoredAnnotations(model.GetAnnotations())
                |> annotationsToDictionary

            let productVersion = model.GetProductVersion()

            if notNull productVersion then
                annotations.Add(
                    CoreAnnotationNames.ProductVersion,
                    Annotation(CoreAnnotationNames.ProductVersion, productVersion)
                )

            let snapshotCode =
                stringBuffer {
                    generateAnnotations builderName model annotations false false

                    for sequence in model.GetSequences() do
                        generateSequence builderName sequence

                    this.generateEntityTypes builderName (model.GetEntityTypesInHierarchicalOrder())
                }

            sb.Append(snapshotCode)
            |> ignore
