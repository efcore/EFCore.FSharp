namespace EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.Collections.Generic

open EntityFrameworkCore.FSharp.SharedTypeExtensions
open EntityFrameworkCore.FSharp.EntityFrameworkExtensions
open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open EntityFrameworkCore.FSharp.Utilities

open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Storage

type FSharpSnapshotGenerator
    (
        code: ICSharpHelper,
        mappingSource: IRelationalTypeMappingSource,
        annotationCodeGenerator: IAnnotationCodeGenerator
    ) =

    let hasAnnotationMethodInfo =
        getRequiredRuntimeMethod (typeof<ModelBuilder>, "HasAnnotation", [| typeof<string>; typeof<string> |])

    let getAnnotations (o: IAnnotatable) = ResizeArray(o.GetAnnotations())

    let appendMethodCall condition methodCall sb =
        if condition then
            sb |> appendEmptyLine |> append methodCall
        else
            sb

    let findValueConverter (p: IProperty) =
        let pValueConverter = p.GetValueConverter()

        if p.GetValueConverter() |> isNull then
            let typeMapping = p.FindTypeMapping()

            if typeMapping |> isNull then
                let mapping = mappingSource.FindMapping(p)

                if mapping |> isNull then
                    None
                elif mapping.Converter |> isNull then
                    None
                else
                    mapping.Converter |> Some
            elif typeMapping.Converter |> isNull then
                None
            else
                typeMapping.Converter |> Some
        else
            pValueConverter |> Some

    let generateAnnotations
        (builderName: string)
        (annotatable: IAnnotatable)
        (annotations: Dictionary<string, IAnnotation>)
        (inChainedCall: bool)
        (leadingNewLine: bool)
        (sb: IndentedStringBuilder)
        =

        let fluentApiCalls =
            annotationCodeGenerator.GenerateFluentApiCalls(annotatable, annotations)

        let mutable (chainedCall: MethodCallCodeFragment option) = None

        let typeQualifiedCalls = ResizeArray<MethodCallCodeFragment>()

        fluentApiCalls
        |> Seq.iter
            (fun call ->
                if notNull call.MethodInfo
                   && call.MethodInfo.IsStatic
                   && (isNull call.MethodInfo.DeclaringType
                       || call.MethodInfo.DeclaringType.Assembly
                          <> typeof<RelationalModelBuilderExtensions>.Assembly) then
                    typeQualifiedCalls.Add call
                else
                    chainedCall <-
                        match chainedCall with
                        | None -> Some call
                        | Some c -> Some(c.Chain(call)))

        // Append remaining raw annotations which did not get generated as Fluent API calls
        annotations.Values
        |> Seq.sortBy (fun a -> a.Name)
        |> Seq.iter
            (fun a ->
                let call =
                    MethodCallCodeFragment(hasAnnotationMethodInfo, a.Name, a.Value)

                chainedCall <-
                    match chainedCall with
                    | None -> Some call
                    | Some c -> Some(c.Chain(call)))

        match chainedCall with
        | Some c ->
            if inChainedCall then
                sb
                    .AppendLine()
                    .AppendLines(code.Fragment(c), skipFinalNewline = true)
                |> ignore
            else
                if leadingNewLine then
                    sb |> appendEmptyLine |> ignore

                sb.AppendLines(code.Fragment(c, builderName), skipFinalNewline = true)
                |> appendLine " |> ignore"
                |> ignore

        | None -> ()

        if inChainedCall then
            sb
            |> appendLine " |> ignore"
            |> unindent
            |> ignore

        if typeQualifiedCalls.Count > 0 then
            if leadingNewLine then
                sb |> appendEmptyLine |> ignore

            typeQualifiedCalls
            |> Seq.iter
                (fun call ->
                    sb
                    |> append (code.Fragment(call, builderName, typeQualified = true))
                    |> appendLine " |> ignore"
                    |> ignore)

        sb

    let generateFluentApiForDefaultValue (property: IProperty) (sb: IndentedStringBuilder) =
        match property.GetDefaultValue() |> Option.ofObj with
        | Some defaultValue ->
            let valueConverter =
                if defaultValue <> (box DBNull.Value) then
                    let valueConverter =
                        property.GetValueConverter() |> Option.ofObj

                    let typeMap =
                        if property.FindTypeMapping()
                           |> Option.ofObj
                           |> Option.isSome then
                            property.FindTypeMapping() |> Option.ofObj
                        else
                            (mappingSource.FindMapping(property) :> CoreTypeMapping)
                            |> Option.ofObj

                    match valueConverter, typeMap with
                    | Some v, _ -> v |> Option.ofObj
                    | None, Some t -> t.Converter |> Option.ofObj
                    | _ -> None
                else
                    None

            let appendValueConverter sb =
                let value =
                    match valueConverter with
                    | Some vc -> vc.ConvertToProvider.Invoke(defaultValue)
                    | None -> defaultValue

                sb |> append (code.UnknownLiteral(value))

            sb
            |> appendEmptyLine
            |> append ".HasDefaultValue("
            |> appendValueConverter
            |> append ")"
        | None -> sb

    let genPropertyAnnotations propertyBuilderName (property: IProperty) (sb: IndentedStringBuilder) =
        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(property.GetAnnotations())
            |> annotationsToDictionary

        let columnType (p: IProperty) =
            let columnType =
                p.GetColumnType()
                ?= mappingSource.GetMapping(p).StoreType

            code.Literal columnType

        let removeAnnotation (annotation: string) sb =
            annotations.Remove(annotation) |> ignore
            sb

        let generateFluentApiForMaxLength sb =
            match property.GetMaxLength() |> Option.ofNullable with
            | Some i ->
                sb
                |> appendEmptyLine
                |> append $".HasMaxLength({code.Literal(i)})"
            | None -> sb

        let generateFluentApiForPrecisionAndScale (sb: IndentedStringBuilder) =
            match property.GetPrecision() |> Option.ofNullable with
            | Some i ->
                sb
                |> appendEmptyLine
                |> append $".HasPrecision({code.UnknownLiteral(i)}"
                |> ignore

                if property.GetScale().HasValue then
                    sb
                    |> append $", {code.UnknownLiteral(property.GetScale().Value)}"
                    |> ignore

                annotations.Remove(CoreAnnotationNames.Precision)
                |> ignore

                sb |> append ")"
            | None -> sb

        let generateFluentApiForUnicode sb =
            match property.IsUnicode() |> Option.ofNullable with
            | Some b ->
                sb
                |> appendEmptyLine
                |> append $".IsUnicode({code.Literal(b)})"
            | None -> sb

        sb
        |> generateFluentApiForMaxLength
        |> generateFluentApiForPrecisionAndScale
        |> generateFluentApiForUnicode
        |> appendEmptyLine
        |> append $".HasColumnType({columnType property})"
        |> removeAnnotation RelationalAnnotationNames.ColumnType
        |> generateFluentApiForDefaultValue property
        |> removeAnnotation RelationalAnnotationNames.DefaultValue
        |> generateAnnotations propertyBuilderName property annotations true true

    let generateBaseType (entityTypeBuilderName: string) (baseType: IEntityType) (sb: IndentedStringBuilder) =

        if (baseType |> notNull) then
            sb
            |> appendEmptyLine
            |> append (sprintf "%s.HasBaseType(%s) |> ignore" entityTypeBuilderName (baseType.Name |> code.Literal))
        else
            sb

    let generateProperty (entityTypeBuilderName: string) (p: IProperty) (sb: IndentedStringBuilder) =

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
                (isOptionType clrType || isNullableType clrType)

            (p.IsPrimaryKey()) || (not isNullable)

        sb
        |> appendEmptyLine
        |> append entityTypeBuilderName
        |> append (sprintf ".Property<%s>(%s)" (code.Reference clrType) (code.Literal p.Name))
        |> indent
        |> appendLineIfTrue p.IsConcurrencyToken ".IsConcurrencyToken()"
        |> appendLineIfTrue true (sprintf ".IsRequired(%b)" isPropertyRequired)
        |> appendLineIfTrue
            (p.ValueGenerated <> ValueGenerated.Never)
            (if p.ValueGenerated = ValueGenerated.OnAdd then
                 ".ValueGeneratedOnAdd()"
             else if p.ValueGenerated = ValueGenerated.OnUpdate then
                 ".ValueGeneratedOnUpdate()"
             else
                 ".ValueGeneratedOnAddOrUpdate()")
        |> genPropertyAnnotations entityTypeBuilderName p

    let generateProperties (entityTypeBuilderName: string) (properties: IProperty seq) (sb: IndentedStringBuilder) =
        properties
        |> Seq.iter
            (fun p ->
                generateProperty entityTypeBuilderName p sb
                |> ignore)

        sb

    let generateKeyAnnotations (keyBuilderName: string) (key: IKey) sb =
        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(key.GetAnnotations())
            |> annotationsToDictionary

        sb
        |> generateAnnotations keyBuilderName key annotations true true

    let generateKey (entityTypeBuilderName: string) (key: IKey) (isPrimary: bool) (sb: IndentedStringBuilder) =

        let keyBuilderName =
            let props =
                key.Properties
                |> Seq.map (fun p -> (p.Name |> code.Literal))
                |> join ", "

            let methodName =
                if isPrimary then
                    "HasKey"
                else
                    "HasAlternateKey"

            sprintf "%s.%s(%s)" entityTypeBuilderName methodName props

        sb
        |> appendEmptyLine
        |> append keyBuilderName
        |> indent
        |> generateKeyAnnotations keyBuilderName key

    let generateKeys (entityTypeBuilderName: string) (keys: IKey seq) (pk: IKey) (sb: IndentedStringBuilder) =

        if notNull pk then
            generateKey entityTypeBuilderName pk true sb
            |> ignore

        if isNull pk || pk.DeclaringEntityType.IsOwned() then
            keys
            |> Seq.filter
                (fun k ->
                    k <> pk
                    && (k.GetReferencingForeignKeys() |> Seq.isEmpty
                        || k.GetAnnotations()
                           |> Seq.exists
                               (fun a ->
                                   a.Name
                                   <> RelationalAnnotationNames.UniqueConstraintMappings)))
            |> Seq.iter
                (fun k ->
                    generateKey entityTypeBuilderName k false sb
                    |> ignore)

        sb

    let generateIndexAnnotations indexBuilderName (idx: IIndex) sb =
        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(idx.GetAnnotations())
            |> annotationsToDictionary

        sb
        |> generateAnnotations indexBuilderName idx annotations true true

    let generateIndex (entityTypeBuilderName: string) (idx: IIndex) (sb: IndentedStringBuilder) =

        let indexParams =
            if isNull idx.Name then
                String.Join(
                    ", ",
                    (idx.Properties
                     |> Seq.map (fun p -> p.Name |> code.Literal))
                )
            else
                sprintf
                    "[| %s |], %s"
                    (String.Join(
                        "; ",
                        (idx.Properties
                         |> Seq.map (fun p -> p.Name |> code.Literal))
                    ))
                    (code.Literal idx.Name)

        let indexBuilderName =
            sprintf "%s.HasIndex(%s)" entityTypeBuilderName indexParams

        sb
        |> appendEmptyLine
        |> append indexBuilderName
        |> indent
        |> appendMethodCall idx.IsUnique ".IsUnique()"
        |> generateIndexAnnotations indexBuilderName idx
        |> ignore

    let generateIndexes (entityTypeBuilderName: string) (indexes: IIndex seq) (sb: IndentedStringBuilder) =

        indexes
        |> Seq.iter
            (fun idx ->
                sb
                |> appendEmptyLine
                |> generateIndex entityTypeBuilderName idx)

        sb

    let processDataItem (props: IProperty list) (sb: IndentedStringBuilder) (data: IDictionary<string, obj>) =

        let writeProperty (p: IProperty) =
            match data.TryGetValue p.Name with
            | true, value ->
                if not (isNull value) then
                    sb
                    |> append (sprintf "%s = %s; " (code.Identifier p.Name) (code.UnknownLiteral value))
                    |> ignore
                else
                    ()
            | _ -> ()


        sb |> append "{| " |> ignore

        props |> Seq.iter writeProperty

        sb |> appendLine "|}" |> ignore

    let processDataItems
        (data: IDictionary<string, obj> seq)
        (propsToOutput: IProperty list)
        (sb: IndentedStringBuilder)
        =
        data
        |> Seq.iter (processDataItem propsToOutput sb)

        sb

    let generateSequence (builderName: string) (sequence: ISequence) sb =
        sb
        |> appendEmptyLine
        |> append builderName
        |> append ".HasSequence"
        |> ignore

        if sequence.Type <> Sequence.DefaultClrType then
            sb
            |> append "<"
            |> append (code.Reference(sequence.Type))
            |> append ">"
            |> ignore

        sb
        |> append "("
        |> append (code.Literal(sequence.Name))
        |> ignore

        if String.IsNullOrEmpty(sequence.Schema) |> not
           && sequence.Model.GetDefaultSchema()
              <> sequence.Schema then
            sb
            |> append ", "
            |> append (code.Literal(sequence.Schema))
            |> ignore

        let appendStartValue sb =
            let condition =
                sequence.StartValue
                <> (Sequence.DefaultStartValue |> int64)

            sb
            |> appendMethodCall condition $".StartsAt({code.Literal(sequence.StartValue)})"

        let appendIncrementBy sb =
            let condition =
                sequence.IncrementBy
                <> Sequence.DefaultIncrementBy

            sb
            |> appendMethodCall condition $".IncrementsBy({code.Literal(sequence.IncrementBy)})"

        let appendMinValue sb =
            let condition =
                sequence.MinValue <> Sequence.DefaultMinValue

            sb
            |> appendMethodCall condition $".HasMin({code.Literal(sequence.MinValue)})"

        let appendMaxValue sb =
            let condition =
                sequence.MaxValue <> Sequence.DefaultMaxValue

            sb
            |> appendMethodCall condition $".HasMax({code.Literal(sequence.MaxValue)})"

        let appendIsCyclic sb =
            let condition =
                sequence.IsCyclic <> Sequence.DefaultIsCyclic

            sb |> appendMethodCall condition ".IsCyclic()"

        sb
        |> append ")"
        |> indent
        |> appendStartValue
        |> appendIncrementBy
        |> appendMinValue
        |> appendMaxValue
        |> appendIsCyclic
        |> append " |> ignore"
        |> ignore

    member this.generatePropertyAnnotations = genPropertyAnnotations

    member this.generateCheckConstraints (builderName: string) (entityType: IEntityType) (sb: IndentedStringBuilder) =
        let generateCheckConstraint (c: ICheckConstraint) sb =
            let name = code.Literal c.Name
            let sql = code.Literal c.Sql

            if c.Name <> (c.GetDefaultName() ?= c.ModelName) then
                sb
                |> append (
                    sprintf
                        "%s.HasCheckConstraint(%s, %s, (fun c -> c.HasName(%s))) |> ignore"
                        builderName
                        name
                        sql
                        c.Name
                )
            else
                sb
                |> append (sprintf "%s.HasCheckConstraint(%s, %s) |> ignore" builderName name sql)

        entityType.GetCheckConstraints()
        |> Seq.iter
            (fun c ->
                sb
                |> appendEmptyLine
                |> generateCheckConstraint c
                |> ignore)

        sb

    member this.generateEntityTypeAnnotations
        (entityTypeBuilderName: string)
        (entityType: IEntityType)
        (sb: IndentedStringBuilder)
        =

        let annotationAndValueNotNull (annotation: IAnnotation) =
            notNull annotation && notNull annotation.Value

        let getAnnotationValue (annotation: IAnnotation) (defaultValue: unit -> string) =
            if annotationAndValueNotNull annotation then
                string annotation.Value
            else
                defaultValue ()

        let annotationList =
            entityType.GetAnnotations() |> Seq.toList

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



        let tableNameAnnotation =
            tryGetAnnotationByName RelationalAnnotationNames.TableName

        if annotationAndValueNotNull tableNameAnnotation
           || (isNull entityType.BaseType) then

            let tableName =
                getAnnotationValue tableNameAnnotation entityType.GetTableName

            if notNull tableName || notNull tableNameAnnotation then
                sb
                |> appendEmptyLine
                |> append entityTypeBuilderName
                |> append ".ToTable("
                |> ignore

                let schemaAnnotation =
                    tryGetAnnotationByName RelationalAnnotationNames.Schema

                let schema =
                    getAnnotationValue schemaAnnotation entityType.GetSchema

                if isNull tableName
                   && (isNull schemaAnnotation || isNull schema) then
                    sb
                    |> append (sprintf "(string %s)" (code.UnknownLiteral tableName))
                    |> ignore
                else
                    sb
                    |> append (code.UnknownLiteral tableName)
                    |> ignore

                if notNull tableNameAnnotation then
                    annotations.Remove(tableNameAnnotation.Name)
                    |> ignore

                let isExcludedAnnotation =
                    tryGetAnnotationByName RelationalAnnotationNames.IsTableExcludedFromMigrations

                if notNull schema
                   || (notNull schemaAnnotation && notNull tableName) then
                    if isNull schema
                       && (notNull isExcludedAnnotation
                           && (isExcludedAnnotation.Value :?> Nullable<bool>)
                               .GetValueOrDefault()
                              <> true) then
                        sb
                        |> append (sprintf ", (string %s)" (code.UnknownLiteral schema))
                        |> ignore
                    elif notNull schema then
                        sb
                        |> append (sprintf ", %s" (code.UnknownLiteral schema))
                        |> ignore

                if notNull isExcludedAnnotation then
                    if (isExcludedAnnotation.Value :?> Nullable<bool>)
                        .GetValueOrDefault() then
                        sb
                        |> append ", (fun t -> t.ExcludeFromMigrations())"
                        |> ignore

                    annotations.Remove(isExcludedAnnotation.Name)
                    |> ignore


                sb |> append ") |> ignore" |> ignore

        annotations.Remove(RelationalAnnotationNames.Schema)
        |> ignore

        let viewNameAnnotation =
            tryGetAnnotationByName RelationalAnnotationNames.ViewName

        if (annotationAndValueNotNull viewNameAnnotation
            || isNull entityType.BaseType) then
            let viewName =
                getAnnotationValue viewNameAnnotation entityType.GetViewName

            if notNull viewName then
                sb
                |> appendEmptyLine
                |> append (sprintf "%s.ToView(%s" entityTypeBuilderName (code.UnknownLiteral viewName))
                |> ignore

                if notNull viewNameAnnotation then
                    annotations.Remove(viewNameAnnotation.Name)
                    |> ignore

                let viewSchemaAnnotation =
                    tryGetAnnotationByName RelationalAnnotationNames.ViewSchema

                if annotationAndValueNotNull viewSchemaAnnotation then
                    let viewSchemaAnnotationValue = viewSchemaAnnotation.Value :?> string

                    sb
                    |> append ", "
                    |> append (code.UnknownLiteral viewSchemaAnnotationValue)
                    |> ignore

                    annotations.Remove(viewSchemaAnnotation.Name)
                    |> ignore

                sb |> append ") |> ignore" |> ignore

        annotations.Remove(RelationalAnnotationNames.ViewSchema)
        |> ignore

        annotations.Remove(RelationalAnnotationNames.ViewDefinitionSql)
        |> ignore

        let functionNameAnnotation =
            tryGetAnnotationByName RelationalAnnotationNames.FunctionName

        if annotationAndValueNotNull functionNameAnnotation
           || isNull entityType.BaseType then
            let functionName =
                getAnnotationValue functionNameAnnotation entityType.GetFunctionName

            if notNull functionName
               || notNull functionNameAnnotation then
                sb
                |> appendEmptyLine
                |> append entityTypeBuilderName
                |> append ".ToFunction("
                |> append (code.UnknownLiteral functionName)
                |> append ") |> ignore"
                |> ignore

                if notNull functionNameAnnotation then
                    annotations.Remove(functionNameAnnotation.Name)
                    |> ignore

        let sqlQueryAnnotation =
            tryGetAnnotationByName RelationalAnnotationNames.SqlQuery

        if annotationAndValueNotNull sqlQueryAnnotation
           || isNull entityType.BaseType then
            let sqlQuery =
                getAnnotationValue sqlQueryAnnotation entityType.GetSqlQuery

            if notNull sqlQuery || notNull sqlQueryAnnotation then
                sb
                |> appendEmptyLine
                |> append entityTypeBuilderName
                |> append ".ToSqlQuery("
                |> append (code.UnknownLiteral sqlQuery)
                |> append ") |> ignore"
                |> ignore

                if notNull sqlQueryAnnotation then
                    annotations.Remove(sqlQueryAnnotation.Name)
                    |> ignore

        if hasDiscriminator then

            sb
            |> appendEmptyLine
            |> append entityTypeBuilderName
            |> append ".HasDiscriminator"
            |> ignore

            if annotationAndValueNotNull discriminatorPropertyAnnotation then
                let discriminatorProperty =
                    entityType.FindProperty(discriminatorPropertyAnnotation.Value :?> string)

                let propertyClrType =
                    match discriminatorProperty |> findValueConverter with
                    | Some c ->
                        c.ProviderClrType
                        |> makeNullable discriminatorProperty.IsNullable
                    | None -> discriminatorProperty.ClrType

                sb
                |> append "<"
                |> append (code.Reference(propertyClrType))
                |> append ">("
                |> append (code.Literal(discriminatorPropertyAnnotation.Value :?> string))
                |> append ")"
                |> ignore
            else
                sb |> append "()" |> ignore

            if annotationAndValueNotNull discriminatorMappingCompleteAnnotation then
                let value =
                    discriminatorMappingCompleteAnnotation.Value

                sb
                |> append ".IsComplete("
                |> append (code.UnknownLiteral(value))
                |> append ")"
                |> ignore

            if annotationAndValueNotNull discriminatorValueAnnotation then
                let discriminatorProperty = entityType.FindDiscriminatorProperty()

                let defaultValue = discriminatorValueAnnotation.Value

                let value =
                    if notNull discriminatorProperty then
                        match discriminatorProperty |> findValueConverter with
                        | Some c -> c.ConvertToProvider.Invoke(defaultValue)
                        | None -> defaultValue
                    else
                        defaultValue

                sb
                |> append ".HasValue("
                |> append (code.UnknownLiteral(value))
                |> append ")"
                |> ignore

            sb |> appendLine " |> ignore" |> ignore

        sb
        |> generateAnnotations entityTypeBuilderName entityType annotations false true
        |> ignore

        sb

    member private this.generateForeignKeyAnnotations entityTypeBuilderName (fk: IForeignKey) sb =

        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(fk.GetAnnotations())
            |> annotationsToDictionary

        sb
        |> generateAnnotations entityTypeBuilderName fk annotations true false

    member private this.generateForeignKey entityTypeBuilderName (fk: IForeignKey) sb =

        let literalPropNames (properties: seq<IProperty>) =
            properties
            |> Seq.map (fun p -> p.Name |> code.Literal)

        if not fk.IsOwnership then
            let dependent =
                if isNull fk.DependentToPrincipal then
                    code.UnknownLiteral null
                else
                    fk.DependentToPrincipal.Name |> code.Literal

            sb
            |> append entityTypeBuilderName
            |> append ".HasOne("
            |> append (fk.PrincipalEntityType.Name |> code.Literal)
            |> append ","
            |> append dependent
            |> ignore
        else
            sb
            |> append entityTypeBuilderName
            |> append ".WithOwner("
            |> ignore

            if notNull fk.DependentToPrincipal then
                sb
                |> append (fk.DependentToPrincipal.Name |> code.Literal)
                |> ignore

        sb
        |> append ")"
        |> appendEmptyLine
        |> indent
        |> ignore

        if fk.IsUnique && (not fk.IsOwnership) then
            sb |> append ".WithOne(" |> ignore

            if notNull fk.PrincipalToDependent then
                sb
                |> append (fk.PrincipalToDependent.Name |> code.Literal)
                |> ignore

            sb
            |> append (")")
            |> append ".HasForeignKey("
            |> append (fk.DeclaringEntityType.Name |> code.Literal)
            |> append ", "
            |> append (String.Join(",", (literalPropNames fk.Properties)))
            |> append ")"
            |> ignore

            if fk.PrincipalKey
               <> fk.PrincipalEntityType.FindPrimaryKey() then
                sb
                |> appendEmptyLine
                |> append ".HasPrincipalKey("
                |> append (fk.PrincipalEntityType.Name |> code.Literal)
                |> append ", "
                |> append (String.Join(", ", (literalPropNames fk.PrincipalKey.Properties)))
                |> append ")"
                |> ignore

        else
            if not fk.IsOwnership then
                sb |> append ".WithMany(" |> ignore

                if notNull fk.PrincipalToDependent then
                    sb
                    |> append (fk.PrincipalToDependent.Name |> code.Literal)
                    |> ignore

                sb |> append ")" |> ignore

            sb
            |> appendEmptyLine
            |> append ".HasForeignKey("
            |> append (String.Join(", ", (literalPropNames fk.Properties)))
            |> append ")"
            |> ignore

            if fk.PrincipalKey
               <> fk.PrincipalEntityType.FindPrimaryKey() then
                sb
                |> appendEmptyLine
                |> append ".HasPrincipalKey("
                |> append (String.Join(", ", (literalPropNames fk.PrincipalKey.Properties)))
                |> append ")"
                |> ignore

        if not fk.IsOwnership then
            if fk.DeleteBehavior <> DeleteBehavior.ClientSetNull then
                sb
                |> appendEmptyLine
                |> append ".OnDelete("
                |> append (fk.DeleteBehavior |> code.Literal)
                |> append ")"
                |> ignore

            if fk.IsRequired then
                sb
                |> appendEmptyLine
                |> append ".IsRequired()"
                |> ignore

        sb
        |> this.generateForeignKeyAnnotations entityTypeBuilderName fk

    member private this.generateForeignKeys entityTypeBuilderName (foreignKeys: IForeignKey seq) sb =
        foreignKeys
        |> Seq.iter
            (fun fk ->
                this.generateForeignKey entityTypeBuilderName fk sb
                |> ignore)

        sb |> unindent

    member private this.generateOwnedType entityTypeBuilderName (ownership: IForeignKey) (sb: IndentedStringBuilder) =
        this.generateEntityType entityTypeBuilderName ownership.DeclaringEntityType sb

    member private this.generateOwnedTypes
        entityTypeBuilderName
        (ownerships: IForeignKey seq)
        (sb: IndentedStringBuilder)
        =
        ownerships
        |> Seq.iter (fun o -> this.generateOwnedType entityTypeBuilderName o sb)

        sb

    member private this.generateRelationships
        (entityTypeBuilderName: string)
        (entityType: IEntityType)
        (sb: IndentedStringBuilder)
        =
        sb
        |> this.generateForeignKeys entityTypeBuilderName (getDeclaredForeignKeys entityType)
        |> this.generateOwnedTypes
            entityTypeBuilderName
            (entityType
             |> getDeclaredReferencingForeignKeys
             |> Seq.filter (fun fk -> fk.IsOwnership))

    member private this.generateNavigationAnnotations
        (navigationBuilderName: string)
        (navigation: INavigation)
        (sb: IndentedStringBuilder)
        =
        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(navigation.GetAnnotations())
            |> annotationsToDictionary

        sb
        |> generateAnnotations navigationBuilderName navigation annotations true false

    member private this.generateNavigations
        (entityTypeBuilderName: string)
        (navigations: INavigation seq)
        (sb: IndentedStringBuilder)
        =

        navigations
        |> Seq.iter
            (fun n ->
                let navigationBuilderName =
                    $"{entityTypeBuilderName}.Navigation({code.Literal(n.Name)})"

                let isRequired =
                    (not n.IsOnDependent)
                    && (not n.IsCollection)
                    && n.ForeignKey.IsRequiredDependent

                sb
                |> appendEmptyLine
                |> append navigationBuilderName
                |> appendLineIfTrue isRequired ""
                |> appendLineIfTrue isRequired ".IsRequired()"
                |> this.generateNavigationAnnotations navigationBuilderName n
                |> ignore)

        sb

    member private this.generateData
        builderName
        (properties: IProperty seq)
        (data: IDictionary<string, obj> seq)
        (sb: IndentedStringBuilder)
        =
        if Seq.isEmpty data then
            sb
        else
            let propsToOutput = properties |> Seq.toList

            sb
            |> appendEmptyLine
            |> appendLine (sprintf "%s.HasData([| " builderName)
            |> indent
            |> processDataItems data propsToOutput
            |> unindent
            |> appendLine " |]) |> ignore"

    member private this.generateEntityType (builderName: string) (entityType: IEntityType) (sb: IndentedStringBuilder) =

        let ownership = entityType.FindOwnership()

        let ownerNav =
            if isNull ownership then
                None
            else
                Some ownership.PrincipalToDependent.Name

        let declaration =
            match ownerNav with
            | None -> sprintf ".Entity(%s" (entityType.Name |> code.Literal)
            | Some o ->
                if ownership.IsUnique then
                    sprintf ".OwnsOne(%s, %s" (entityType.Name |> code.Literal) (o |> code.Literal)
                else
                    sprintf ".OwnsMany(%s, %s" (entityType.Name |> code.Literal) (o |> code.Literal)

        let entityTypeBuilderName =
            if builderName.StartsWith("b", StringComparison.Ordinal) then
                let mutable counter = 1

                match builderName.Length > 1, Int32.TryParse(builderName.[1..]) with
                | true, (true, _) -> counter <- counter + 1
                | _ -> ()

                "b"
                + if counter = 0 then
                      ""
                  else
                      counter.ToString()
            else
                "b"

        sb
        |> appendEmptyLine
        |> append builderName
        |> append declaration
        |> append ", (fun "
        |> append entityTypeBuilderName
        |> appendLine " ->"
        |> indent
        |> generateBaseType entityTypeBuilderName entityType.BaseType
        |> generateProperties entityTypeBuilderName (entityType |> getDeclaredProperties)
        |> generateKeys
            entityTypeBuilderName
            (entityType |> getDeclaredKeys)
            (if isNull entityType.BaseType then
                 entityType.FindPrimaryKey()
             else
                 null)
        |> generateIndexes entityTypeBuilderName (entityType |> getDeclaredIndexes)
        |> this.generateEntityTypeAnnotations entityTypeBuilderName entityType
        |> this.generateCheckConstraints entityTypeBuilderName entityType
        |> match ownerNav with
           | None -> id
           | Some _ -> this.generateRelationships entityTypeBuilderName entityType
        |> this.generateData entityTypeBuilderName (entityType.GetProperties()) (entityType |> getData true)
        |> appendEmptyLine
        |> unindent
        |> appendLine ")) |> ignore"
        |> ignore

    member private this.generateEntityTypeRelationships
        builderName
        (entityType: IEntityType)
        (sb: IndentedStringBuilder)
        =

        sb
        |> appendEmptyLine
        |> append builderName
        |> append ".Entity("
        |> append (entityType.Name |> code.Literal)
        |> appendLine (", (fun b ->")
        |> indent
        |> this.generateRelationships "b" entityType
        |> appendLine ")) |> ignore"
        |> ignore

    member private this.generateEntityTypeNavigations
        builderName
        (entityType: IEntityType)
        (sb: IndentedStringBuilder)
        =

        sb
        |> appendEmptyLine
        |> append builderName
        |> append ".Entity("
        |> append (entityType.Name |> code.Literal)
        |> appendLine (", (fun b ->")
        |> indent
        |> this.generateNavigations
            "b"
            (entityType.GetDeclaredNavigations()
             |> Seq.filter
                 (fun n ->
                     (not n.IsOnDependent)
                     && (not n.ForeignKey.IsOwnership)))
        |> appendLine ")) |> ignore"
        |> ignore

    member private this.generateEntityTypes builderName (entities: IEntityType seq) (sb: IndentedStringBuilder) =

        let entitiesToWrite =
            entities |> Seq.filter (findOwnership >> isNull)

        entitiesToWrite
        |> Seq.iter (fun e -> this.generateEntityType builderName e sb)

        let relationships =
            entitiesToWrite
            |> Seq.filter
                (fun e ->
                    (e |> getDeclaredForeignKeys |> Seq.isEmpty |> not)
                    || (e
                        |> getDeclaredReferencingForeignKeys
                        |> Seq.exists (fun fk -> fk.IsOwnership)))

        relationships
        |> Seq.iter (fun e -> this.generateEntityTypeRelationships builderName e sb)

        let navigations =
            entitiesToWrite
            |> Seq.filter
                (fun e ->
                    e.GetDeclaredNavigations()
                    |> Seq.exists
                        (fun n ->
                            (not n.IsOnDependent)
                            && (not n.ForeignKey.IsOwnership)))

        navigations
        |> Seq.iter (fun e -> this.generateEntityTypeNavigations builderName e sb)

        sb

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

            sb
            |> generateAnnotations builderName model annotations false false
            |> ignore

            for sequence in model.GetSequences() do
                generateSequence builderName sequence sb

            this.generateEntityTypes builderName (model.GetEntityTypesInHierarchicalOrder()) sb
            |> ignore
