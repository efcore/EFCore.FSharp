namespace EntityFrameworkCore.FSharp.Scaffolding

open System
open System.CodeDom.Compiler
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Conventions
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Scaffolding
open Microsoft.EntityFrameworkCore.Scaffolding.Internal

open EntityFrameworkCore.FSharp.EntityFrameworkExtensions
open EntityFrameworkCore.FSharp.Internal
open EntityFrameworkCore.FSharp.Internal.FSharpUtilities

#nowarn "0044"

open System.Collections.Generic
open EntityFrameworkCore.FSharp

type FSharpDbContextGenerator
    (
        providerCodeGenerator: IProviderConfigurationCodeGenerator,
        annotationCodeGenerator: IAnnotationCodeGenerator,
        code: ICSharpHelper
    ) =

    let errors = CompilerErrorCollection()

    let mutable _entityTypeBuilderInitialized = false

    let namespaces = HashSet<string>()
    let entityLambdaIdentifier = "entity"

    let generateAnnotations
        (annotatable: IAnnotatable)
        (annotations: Dictionary<string, IAnnotation>)
        (lines: ResizeArray<string>)
        =

        annotationCodeGenerator.GenerateFluentApiCalls(annotatable, annotations)
        |> Seq.iter (fun fluentApiCall ->
            lines.Add(code.Fragment(fluentApiCall))

            if notNull fluentApiCall.Namespace then
                namespaces.Add fluentApiCall.Namespace
                |> ignore
        )

        lines.AddRange(
            annotations.Values
            |> Seq.map (fun a ->
                $".HasAnnotation({code.Literal(a.Name)}, {code.UnknownLiteral(a.Value)})"
            )
        )

    let generateForeignKeyConfigurationLines (foreignKey: IForeignKey) targetType identifier =

        let lines = ResizeArray<string>()

        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(foreignKey.GetAnnotations())
            |> annotationsToDictionary

        lines.Add(
            sprintf
                "(fun (%s: Builders.EntityTypeBuilder<_>) -> %s.HasOne<%s>().WithMany()"
                identifier
                identifier
                targetType
        )

        if not (foreignKey.PrincipalKey.IsPrimaryKey()) then
            let principalKeyProps =
                foreignKey.PrincipalKey.Properties
                |> Seq.map (fun e -> code.Literal(e.Name))
                |> join ", "

            lines.Add(sprintf ".HasPrincipalKey(%s)" principalKeyProps)

        let fkProps =
            foreignKey.Properties
            |> Seq.map (fun e -> code.Literal(e.Name))
            |> join ", "

        lines.Add(sprintf ".HasForeignKey(%s)" fkProps)

        let defaultOnDeleteAction =
            if foreignKey.IsRequired then
                DeleteBehavior.Cascade
            else
                DeleteBehavior.ClientSetNull

        if
            foreignKey.DeleteBehavior
            <> defaultOnDeleteAction
        then
            lines.Add $".OnDelete({code.Literal(foreignKey.DeleteBehavior)})"

        generateAnnotations foreignKey annotations lines

        (lines
         |> join "")
        + "),"

    let generateOnConfiguring (connectionString: string) suppressOnConfiguring =

        if suppressOnConfiguring then
            None
        else

            let connStringFragment =
                connectionString
                |> providerCodeGenerator.GenerateUseProvider
                |> code.Fragment

            stringBuffer {
                "override this.OnConfiguring(optionsBuilder: DbContextOptionsBuilder) ="

                indent {
                    "if not optionsBuilder.IsConfigured then"
                    indent { $"optionsBuilder{connStringFragment} |> ignore" }
                    "()"
                    ""
                }
            }
            |> Some

    let generateSequence (s: ISequence) =

        let methodName =
            if s.Type = Sequence.DefaultClrType then
                "HasSequence"
            else
                sprintf "HasSequence<%s>" (FSharpUtilities.getTypeName (s.Type))

        let parameters =
            if
                (s.Schema
                 |> String.IsNullOrEmpty)
                && (s.Model.GetDefaultSchema()
                    <> s.Schema)
            then
                sprintf
                    "%s, %s"
                    (s.Name
                     |> FSharpUtilities.delimitString)
                    (s.Schema
                     |> FSharpUtilities.delimitString)
            else
                s.Name
                |> FSharpUtilities.delimitString

        stringBuffer {
            $"modelBuilder.{methodName}({parameters})"

            indent {
                if
                    s.StartValue
                    <> Sequence.DefaultStartValue
                then
                    $".StartsAt({s.StartValue})"

                if
                    s.IncrementBy
                    <> Sequence.DefaultIncrementBy
                then
                    $".IncrementsBy({s.IncrementBy})"

                if
                    s.MinValue
                    <> Nullable(
                        Sequence.DefaultMinValue
                        |> int64
                    )
                then
                    $".HasMin({s.MinValue})"

                if
                    s.MaxValue
                    <> Nullable(
                        Sequence.DefaultMaxValue
                        |> int64
                    )
                then
                    $".HasMax({s.MaxValue})"

                if s.IsCyclic then
                    $".IsCyclic()"

                ""
            }
        }

    let generateLambdaToKey (properties: IReadOnlyList<IProperty>) lambdaIdentifier =
        match properties.Count with
        | 0 -> ""
        | 1 ->
            sprintf "fun %s -> %s.%s :> obj" lambdaIdentifier lambdaIdentifier (properties.[0].Name)
        | _ ->
            let props =
                properties
                |> Seq.map (fun p -> sprintf "%s.%s" lambdaIdentifier p.Name)
                |> join ", "

            sprintf "fun %s -> (%s) :> obj" lambdaIdentifier props

    let generatePropertyNameArray (properties: IReadOnlyList<IProperty>) =

        let props =
            properties
            |> Seq.map (fun p -> code.Literal p.Name)
            |> join "; "

        sprintf "[| %s |]" props

    let initializeEntityTypeBuilder (entityType: IEntityType) =

        if not _entityTypeBuilderInitialized then

            _entityTypeBuilderInitialized <- true

            stringBuffer {
                ""
                $"modelBuilder.Entity<%s{entityType.Name}>(fun %s{entityLambdaIdentifier} ->"
            }
            |> Some

        else
            None


    let appendMultiLineFluentApi entityType lines =

        if
            lines
            |> Seq.isEmpty
        then
            None
        else
            let head =
                entityLambdaIdentifier
                + (lines
                   |> Seq.head)

            let tail =
                lines
                |> Seq.tail

            stringBuffer {
                initializeEntityTypeBuilder entityType

                indent {
                    ""
                    head

                    indent {
                        tail
                        "|> ignore"

                    }
                }
            }
            |> Some

    let generateKeyGuardClause
        (key: IKey)
        (annotations: IAnnotation seq)
        useDataAnnotations
        explicitName
        =
        if
            key.Properties.Count = 1
            && annotations
               |> Seq.isEmpty
        then
            match key with
            | :? IConventionKey as concreteKey ->
                let keyProperties = key.Properties

                let concreteDeclaringProperties =
                    concreteKey.DeclaringEntityType.GetProperties()
                    |> Seq.cast<IConventionProperty>


                let concreteProperties =
                    KeyDiscoveryConvention.DiscoverKeyProperties(
                        concreteKey.DeclaringEntityType,
                        concreteDeclaringProperties
                    )
                    |> Seq.cast<IProperty>


                System.Linq.Enumerable.SequenceEqual(keyProperties, concreteProperties)
            | _ ->
                if
                    (not explicitName)
                    && useDataAnnotations
                then
                    true
                else
                    false
        else
            false

    let generateKey (key: IKey) (entityType: IEntityType) useDataAnnotations =

        if isNull key then
            if not useDataAnnotations then
                let lines = ResizeArray()
                lines.Add ".HasNoKey()"

                appendMultiLineFluentApi entityType lines
            else
                None
        else

            let annotations =
                annotationCodeGenerator.FilterIgnoredAnnotations(key.GetAnnotations())
                |> annotationsToDictionary

            annotationCodeGenerator.RemoveAnnotationsHandledByConventions(key, annotations)

            let explicitName =
                key.GetName()
                <> key.GetDefaultName()

            annotations.Remove(RelationalAnnotationNames.Name)
            |> ignore

            let earlyExit =
                generateKeyGuardClause key annotations.Values useDataAnnotations explicitName

            if earlyExit then
                None
            else
                let lines = ResizeArray<string>()
                lines.Add(sprintf ".HasKey(%s)" (generateLambdaToKey key.Properties "e"))

                if explicitName then
                    lines.Add(sprintf ".HasName(%s)" (code.Literal(key.GetName())))

                generateAnnotations key annotations lines

                appendMultiLineFluentApi key.DeclaringEntityType lines

    let generateTableName (entityType: IEntityType) =

        let lines = ResizeArray<string>()

        let tableName = entityType.GetTableName()
        let schema = entityType.GetSchema()
        let defaultSchema = entityType.Model.GetDefaultSchema()

        let explicitSchema =
            not (isNull schema)
            && schema
               <> defaultSchema

        let explicitTable =
            explicitSchema
            || not (isNull tableName)
               && tableName
                  <> entityType.GetDbSetName()

        if explicitTable then

            let parameterString =
                if explicitSchema then
                    sprintf "%s, %s" (code.Literal tableName) (code.Literal schema)
                else
                    code.Literal tableName


            lines.Add($".ToTable({parameterString})")

        let viewName = entityType.GetViewName()
        let viewSchema = entityType.GetViewSchema()

        let explicitViewSchema =
            notNull viewSchema
            && viewSchema
               <> defaultSchema

        let explicitViewTable =
            explicitViewSchema
            || notNull viewName

        if explicitViewTable then
            let parameterString =
                if explicitViewSchema then
                    $"{code.Literal(viewName)}, {code.Literal(viewSchema)}"
                else
                    code.Literal(viewName)

            lines.Add($".ToView({parameterString})")


        appendMultiLineFluentApi entityType lines

    let generateIndex useDataAnnotations (index: IIndex) =
        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(index.GetAnnotations())
            |> annotationsToDictionary

        annotationCodeGenerator.RemoveAnnotationsHandledByConventions(index, annotations)

        if
            not useDataAnnotations
            || annotations.Count > 0
        then

            let lines = ResizeArray<string>()

            lines.Add(
                sprintf
                    ".HasIndex((%s), %s)" // Parentheses required for F# implicit conversion to Expression<Func<T, obj>>
                    (generateLambdaToKey index.Properties "e")
                    (code.Literal(index.GetDatabaseName()))
            )

            annotations.Remove(RelationalAnnotationNames.Name)
            |> ignore

            if index.IsUnique then
                lines.Add(".IsUnique()")

            generateAnnotations index annotations lines
            appendMultiLineFluentApi index.DeclaringEntityType lines
        else
            None

    let generateProperty (property: IProperty) useDataAnnotations =

        let lines = ResizeArray<string>()
        lines.Add(sprintf ".Property(fun e -> e.%s)" property.Name)

        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(property.GetAnnotations())
            |> annotationsToDictionary

        annotationCodeGenerator.RemoveAnnotationsHandledByConventions(property, annotations)

        if useDataAnnotations then
            annotations.Remove(RelationalAnnotationNames.ColumnName)
            |> ignore

            annotations.Remove(RelationalAnnotationNames.ColumnType)
            |> ignore

            annotationCodeGenerator.GenerateDataAnnotationAttributes(property, annotations)
            |> ignore
        else if
            property.IsNullable
            |> not
            && property.ClrType
               |> SharedTypeExtensions.isNullableType
            && property.IsPrimaryKey()
               |> not
        then
            lines.Add(".IsRequired()")

        match
            property.GetConfiguredColumnType()
            |> isNull
            |> not
        with
        | true ->
            lines.Add($".HasColumnType({code.Literal(property.GetConfiguredColumnType())})")

            annotations.Remove(RelationalAnnotationNames.ColumnType)
            |> ignore
        | false -> ()

        match
            property.GetMaxLength()
            |> Option.ofNullable
        with
        | Some l -> lines.Add($".HasMaxLength({code.Literal(l)})")
        | None -> ()

        match
            property.GetPrecision()
            |> Option.ofNullable,
            property.GetScale()
            |> Option.ofNullable
        with
        | Some p, Some s when s <> 0 ->
            lines.Add($".HasPrecision({code.Literal(p)}, {code.Literal(s)})")
        | Some p, _ -> lines.Add($".HasPrecision({code.Literal(p)})")
        | _, _ -> ()

        match
            property.IsUnicode()
            |> Option.ofNullable
        with
        | Some b ->
            let arg = if b then "" else "false"
            lines.Add($".IsUnicode({arg})")
        | None -> ()

        match
            property.GetDefaultValue()
            |> Option.ofObj
        with
        | Some d ->
            annotations.Remove(RelationalAnnotationNames.DefaultValue)
            |> ignore

            match d with
            | :? DBNull -> lines.Add(".HasDefaultValue()")
            | _ -> lines.Add($".HasDefaultValue({code.UnknownLiteral(d)})")
        | _ -> ()

        let isRowVersion = false
        let valueGenerated = property.ValueGenerated

        match property with
        | :? IConventionProperty as cp ->
            match
                cp.GetValueGeneratedConfigurationSource()
                |> Option.ofNullable
            with
            | Some valueGeneratedConfigurationSource when
                valueGeneratedConfigurationSource
                <> ConfigurationSource.Convention
                && ValueGenerationConvention.GetValueGenerated(property)
                   <> (valueGenerated
                       |> Nullable)
                ->
                let methodName =
                    match valueGenerated with
                    | ValueGenerated.OnAdd -> "ValueGeneratedOnAdd"
                    | ValueGenerated.OnAddOrUpdate when property.IsConcurrencyToken ->
                        "IsRowVersion"
                    | ValueGenerated.OnAddOrUpdate -> "ValueGeneratedOnAddOrUpdate"
                    | ValueGenerated.OnUpdate -> "ValueGeneratedOnUpdate"
                    | ValueGenerated.Never -> "ValueGeneratedNever"
                    | _ -> invalidOp $"Unhandled enum value ValueGenerated.{valueGenerated}"

                lines.Add($".{methodName}()")
            | _ -> ()
        | _ -> ()

        if
            property.IsConcurrencyToken
            && isRowVersion
               |> not
        then
            lines.Add(".IsConcurrencyToken()")

        generateAnnotations property annotations lines

        if lines.Count = 0 then
            ()
        elif lines.Count = 2 then
            let l1 = lines.[0]
            let l2 = lines.[1]
            lines.Clear()
            lines.Add(l1 + l2)

        appendMultiLineFluentApi property.DeclaringEntityType lines

    let generateRelationship (fk: IForeignKey) useDataAnnotations =

        let mutable canUseDataAnnotations = false

        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(fk.GetAnnotations())
            |> annotationsToDictionary

        annotationCodeGenerator.RemoveAnnotationsHandledByConventions(fk, annotations)
        |> ignore

        let lines = ResizeArray()

        lines.Add(
            sprintf
                ".HasOne(%s)"
                (if isNull fk.DependentToPrincipal then
                     ""
                 else
                     (sprintf "fun d -> d.%s" fk.DependentToPrincipal.Name))
        )

        lines.Add(
            sprintf
                ".%s(%s)"
                (if fk.IsUnique then "WithOne" else "WithMany")
                (if isNull fk.PrincipalToDependent then
                     ""
                 else
                     (sprintf "fun p -> p.%s" fk.PrincipalToDependent.Name))
        )

        if not (fk.PrincipalKey.IsPrimaryKey()) then
            canUseDataAnnotations <- false

            let typeParam =
                if fk.IsUnique then
                    (sprintf "<%s>" ((fk.PrincipalEntityType :> ITypeBase).DisplayName()))
                else
                    ""

            let methodParams = code.Lambda(fk.PrincipalKey.Properties, "p")

            lines.Add(sprintf ".HasPrincipalKey%s(%s)" typeParam methodParams)

        let typeParam =
            if fk.IsUnique then
                (sprintf "<%s>" fk.DeclaringEntityType.Name)
            else
                ""

        let methodParams =
            fk.Properties
            |> Seq.map (fun p ->
                "d."
                + p.Name
            )
            |> join ", "

        lines.Add(
            sprintf
                ".HasForeignKey%s(fun (d:%s) -> (%s) :> obj)"
                typeParam
                fk.DeclaringEntityType.Name
                methodParams
        )

        let defaultOnDeleteAction =
            if fk.IsRequired then
                DeleteBehavior.Cascade
            else
                DeleteBehavior.ClientSetNull

        if
            fk.DeleteBehavior
            <> defaultOnDeleteAction
        then
            canUseDataAnnotations <- false
            lines.Add(sprintf ".OnDelete(%s)" (code.Literal fk.DeleteBehavior))

        if not (String.IsNullOrEmpty(string fk.[RelationalAnnotationNames.Name])) then
            canUseDataAnnotations <- false

        generateAnnotations fk annotations lines

        if
            not useDataAnnotations
            || not canUseDataAnnotations
        then
            appendMultiLineFluentApi fk.DeclaringEntityType lines

        else
            None

    let generateManyToMany (skipNavigation: ISkipNavigation) =

        let writeLines (lines: ResizeArray<string>) terminator =

            let result =
                (lines
                 |> join "")
                + terminator

            lines.Clear()
            result


        let createJoinEntity (joinEntityType: IEntityType) =
            let key = joinEntityType.FindPrimaryKey()
            let lines = ResizeArray<string>()

            let keyAnnotations =
                annotationCodeGenerator.FilterIgnoredAnnotations(key.GetAnnotations())
                |> annotationsToDictionary

            annotationCodeGenerator.RemoveAnnotationsHandledByConventions(key, keyAnnotations)
            |> ignore

            let explicitName =
                key.GetName()
                <> key.GetDefaultName()

            keyAnnotations.Remove(RelationalAnnotationNames.Name)
            |> ignore

            let props =
                key.Properties
                |> Seq.map (fun e -> code.Literal(e.Name))
                |> join ", "

            lines.Add $"j.HasKey({props})"

            if explicitName then
                lines.Add $".HasName({code.Literal(key.GetName())})"

            generateAnnotations key keyAnnotations lines
            let fst = writeLines lines " |> ignore"

            let annotations =
                annotationCodeGenerator.FilterIgnoredAnnotations(joinEntityType.GetAnnotations())
                |> annotationsToDictionary

            annotationCodeGenerator.RemoveAnnotationsHandledByConventions(
                joinEntityType,
                annotations
            )
            |> ignore

            seq {
                RelationalAnnotationNames.TableName
                RelationalAnnotationNames.Schema
                RelationalAnnotationNames.ViewName
                RelationalAnnotationNames.ViewSchema
                ScaffoldingAnnotationNames.DbSetName
                RelationalAnnotationNames.ViewDefinitionSql
            }
            |> Seq.iter (fun a ->
                annotations.Remove(a)
                |> ignore
            )

            let tableName = joinEntityType.GetTableName()
            let schema = joinEntityType.GetSchema()
            let defaultSchema = joinEntityType.Model.GetDefaultSchema()

            let explicitSchema =
                notNull schema
                && schema
                   <> defaultSchema

            let parameterString =
                if explicitSchema then
                    (code.Literal tableName)
                    + ", "
                    + (code.Literal schema)
                else
                    code.Literal tableName

            lines.Add $"j.ToTable({parameterString})"
            generateAnnotations joinEntityType annotations lines
            let snd = writeLines lines " |> ignore"

            let generateManyToManyIndex (index: IIndex) =
                let indexAnnotations =
                    annotationCodeGenerator.FilterIgnoredAnnotations(index.GetAnnotations())
                    |> annotationsToDictionary

                annotationCodeGenerator.RemoveAnnotationsHandledByConventions(
                    index,
                    indexAnnotations
                )
                |> ignore

                let indexProps =
                    index.Properties
                    |> Seq.map (fun e -> e.Name)
                    |> Seq.toArray

                lines.Add
                    $".HasIndex({code.Literal(indexProps)}, {code.Literal(index.GetDatabaseName())}"

                if index.IsUnique then
                    lines.Add ".IsUnique()"

                generateAnnotations index indexAnnotations lines
                writeLines lines " |> ignore"

            let indexes =
                joinEntityType.GetIndexes()
                |> Seq.map generateManyToManyIndex

            let generateManyToManyProperties (property: IProperty) =
                lines.Add
                    $"j.IndexerProperty<{code.Reference(property.ClrType)}>({code.Literal(property.Name)})"

                let propertyAnnotations =
                    annotationCodeGenerator.FilterIgnoredAnnotations(property.GetAnnotations())
                    |> annotationsToDictionary

                propertyAnnotations.Remove RelationalAnnotationNames.ColumnOrder
                |> ignore

                if
                    property.IsNullable
                    |> not
                    && property.ClrType
                       |> SharedTypeExtensions.isNullableType
                    && property.IsPrimaryKey()
                       |> not
                then
                    lines.Add ".IsRequired()"

                let columnType = property.GetConfiguredColumnType()

                if notNull columnType then
                    lines.Add $".HasColumnType({code.Literal(columnType)})"

                    propertyAnnotations.Remove RelationalAnnotationNames.ColumnType
                    |> ignore

                let maxLength = property.GetMaxLength()

                if maxLength.HasValue then
                    lines.Add $".HasMaxLength({code.Literal(maxLength.Value)})"

                let precision = property.GetPrecision()
                let scale = property.GetScale()

                if
                    precision.HasValue
                    && scale.GetValueOrDefault()
                       <> 0
                then
                    lines.Add
                        $".HasPrecision({code.Literal(precision.Value)}, {code.Literal(scale.Value)})"
                elif precision.HasValue then
                    lines.Add $".HasPrecision({code.Literal(precision.Value)})"

                if property.IsUnicode().HasValue then
                    let value = if property.IsUnicode().Value then "" else "false"

                    lines.Add $".IsUnicode({value})"

                let (isSuccess, defaultValue) = property.TryGetDefaultValue()

                if isSuccess then
                    if defaultValue = box DBNull.Value then
                        lines.Add ".HasDefaultValue()"

                        propertyAnnotations.Remove RelationalAnnotationNames.DefaultValue
                        |> ignore
                    elif notNull defaultValue then
                        lines.Add ".HasDefaultValue({code.UnknownLiteral(defaultValue)})"

                        propertyAnnotations.Remove RelationalAnnotationNames.DefaultValue
                        |> ignore

                let valueGenerated = property.ValueGenerated
                let mutable isRowVersion = false

                let valueGeneratedConfigurationSource =
                    ((property :?> IConventionProperty).GetValueGeneratedConfigurationSource())

                if
                    valueGeneratedConfigurationSource.HasValue
                    && valueGeneratedConfigurationSource.Value
                       <> ConfigurationSource.Convention
                    && ValueGenerationConvention.GetValueGenerated(property)
                       <> Nullable(valueGenerated)
                then
                    let methodName =
                        match valueGenerated with
                        | ValueGenerated.OnAdd -> "ValueGeneratedOnAdd"
                        | ValueGenerated.OnAddOrUpdate ->
                            if property.IsConcurrencyToken then
                                "IsRowVersion"
                            else
                                "ValueGeneratedOnAddOrUpdate"
                        | ValueGenerated.OnUpdate -> "ValueGeneratedOnUpdate"
                        | ValueGenerated.Never -> "ValueGeneratedNever"
                        | _ ->
                            invalidOp (
                                Microsoft
                                    .EntityFrameworkCore
                                    .Internal
                                    .DesignStrings
                                    .UnhandledEnumValue($"ValueGenerated.{valueGenerated}")
                            )

                    lines.Add $".{methodName}()"

                if
                    property.IsConcurrencyToken
                    && not isRowVersion
                then
                    lines.Add ".IsConcurrencyToken()"

                generateAnnotations property propertyAnnotations lines

                if lines.Count > 1 then
                    stringBuffer {
                        ""
                        writeLines lines " |> ignore"
                    }
                    |> Some
                else
                    lines.Clear()
                    None

            let properties =
                joinEntityType.GetProperties()
                |> Seq.map generateManyToManyProperties

            stringBuffer {
                fst
                snd
                indexes
                properties
            }


        let inverse = skipNavigation.Inverse
        let joinEntityType = skipNavigation.JoinEntityType

        stringBuffer {

            if not _entityTypeBuilderInitialized then
                initializeEntityTypeBuilder skipNavigation.DeclaringEntityType

            ""

            indent {
                $"{entityLambdaIdentifier}.HasMany(fun d -> d.{skipNavigation.Name})"

                indent {
                    $".WithMany(fun p -> p.{inverse.Name})"
                    $".UsingEntity<{code.Reference(Model.DefaultPropertyBagType)}>("

                    indent {
                        $"{code.Literal joinEntityType.Name},"

                        generateForeignKeyConfigurationLines
                            inverse.ForeignKey
                            inverse.ForeignKey.PrincipalEntityType.Name
                            "l"

                        generateForeignKeyConfigurationLines
                            skipNavigation.ForeignKey
                            skipNavigation.ForeignKey.PrincipalEntityType.Name
                            "r"

                        "fun j ->"
                        indent { createJoinEntity joinEntityType }
                        ") |> ignore"


                    }
                }
            }
        }

    let generateEntityType (entityType: IEntityType) (useDataAnnotations: bool) =

        let key = generateKey (entityType.FindPrimaryKey()) entityType useDataAnnotations

        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(entityType.GetAnnotations())
            |> annotationsToDictionary

        seq {
            RelationalAnnotationNames.TableName
            RelationalAnnotationNames.Schema
            RelationalAnnotationNames.ViewName
            RelationalAnnotationNames.ViewSchema
            ScaffoldingAnnotationNames.DbSetName
            RelationalAnnotationNames.ViewDefinitionSql
        }
        |> Seq.iter (
            annotations.Remove
            >> ignore
        )

        if useDataAnnotations then
            // Strip out any annotations handled as attributes - these are already handled when generating the entity's properties
            annotationCodeGenerator.GenerateDataAnnotationAttributes(entityType, annotations)
            |> ignore

        let tableName =
            if
                not useDataAnnotations
                || notNull (entityType.GetViewName())
            then
                generateTableName entityType
            else
                None

        let lines = ResizeArray<string>()

        generateAnnotations entityType annotations lines


        stringBuffer {
            key
            tableName

            appendMultiLineFluentApi entityType lines

            for index in entityType.GetIndexes() do
                generateIndex useDataAnnotations index

            for p in entityType.GetProperties() do
                generateProperty p useDataAnnotations

            for fk in entityType.GetForeignKeys() do
                generateRelationship fk useDataAnnotations

            entityType.GetSkipNavigations()
            |> Seq.map (fun skip ->
                let containingKey =
                    skip.JoinEntityType.FindPrimaryKey().Properties.[0].GetContainingForeignKeys()
                    |> Seq.head

                if containingKey.PrincipalEntityType = entityType then
                    generateManyToMany skip
                    |> Some
                else
                    None
            )
        }


    let generateOnModelCreating (model: IModel) (useDataAnnotations: bool) =

        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(model.GetAnnotations())
            |> annotationsToDictionary

        annotationCodeGenerator.RemoveAnnotationsHandledByConventions(model, annotations)

        seq {
            CoreAnnotationNames.ProductVersion
            RelationalAnnotationNames.MaxIdentifierLength
            ScaffoldingAnnotationNames.DatabaseName
            ScaffoldingAnnotationNames.EntityTypeErrors
        }
        |> Seq.iter (
            annotations.Remove
            >> ignore
        )

        let lines = ResizeArray<string>()
        generateAnnotations model annotations lines

        let typesToGenerate =
            model.GetEntityTypes()
            |> Seq.filter (
                isManyToManyJoinEntityType
                >> not
            )

        let writeEntityType (e: IEntityType) =

            _entityTypeBuilderInitialized <- false

            stringBuffer {
                generateEntityType e useDataAnnotations

                if _entityTypeBuilderInitialized then
                    ") |> ignore"
            }

        stringBuffer {
            "override this.OnModelCreating(modelBuilder: ModelBuilder) ="

            indent {
                "base.OnModelCreating(modelBuilder)"

                if lines.Count > 0 then
                    "modelBuilder"
                    + (lines
                       |> Seq.head)

                    indent {
                        let lines' =
                            lines
                            |> Seq.tail

                        lines'
                        |> Seq.mapi (fun i line ->
                            if
                                i = ((lines'
                                      |> Seq.length)
                                     - 1)
                            then
                                line
                                + " |> ignore"
                            else
                                line
                        )

                        ""
                    }

                for e in typesToGenerate do
                    writeEntityType e

                for s in model.GetSequences() do
                    generateSequence s

                ""
                "modelBuilder.RegisterOptionTypes()"

            }
        }

    let generateClass
        (model: IModel)
        contextName
        connectionString
        useDataAnnotations
        suppressOnConfiguring
        =

        let typesToGenerate =
            model.GetEntityTypes()
            |> Seq.filter (
                isManyToManyJoinEntityType
                >> not
            )

        stringBuffer {
            $"type %s{contextName} ="

            indent {
                "inherit DbContext"
                ""
                "new() = { inherit DbContext() }"
                $"new(options : DbContextOptions<%s{contextName}>) = {{ inherit DbContext(options) }}"
                ""

                if
                    typesToGenerate
                    |> Seq.isEmpty
                    |> not
                then
                    for entityType in typesToGenerate do
                        let dbSetName = entityType.GetDbSetName()
                        $"[<DefaultValue>] val mutable private _{dbSetName} : DbSet<{entityType.Name}>"
                        $"member this.{dbSetName}"

                        indent {
                            $"with get() = this._{dbSetName}"
                            $"and set v = this._{dbSetName} <- v"
                        }

                        ""

                for e in model.GetEntityTypeErrors() do
                    $"// {e.Value} Please see the warning messages."
                    ""

                generateOnConfiguring connectionString suppressOnConfiguring

                generateOnModelCreating model useDataAnnotations
            }
        }


    interface ICSharpDbContextGenerator with
        member this.WriteCode
            (
                model,
                contextName,
                connectionString,
                contextNamespace,
                modelNamespace,
                useDataAnnotations,
                useNullableReferenceTypes,
                suppressConnectionStringWarning,
                suppressOnConfiguring
            ) =

            namespaces.Clear()

            namespaces.Add "System"
            |> ignore

            namespaces.Add "System.Collections.Generic"
            |> ignore

            namespaces.Add "Microsoft.EntityFrameworkCore"
            |> ignore

            namespaces.Add "Microsoft.EntityFrameworkCore.Metadata"
            |> ignore

            namespaces.Add "EntityFrameworkCore.FSharp.Extensions"
            |> ignore

            let finalContextNamespace =
                if isNull contextNamespace then
                    modelNamespace
                else
                    contextNamespace

            let finalCode =
                generateClass
                    model
                    contextName
                    connectionString
                    useDataAnnotations
                    suppressOnConfiguring

            let mutable finalNamespaces =
                namespaces
                |> Seq.sortBy (fun n ->
                    (match n with
                     | "System" -> 1
                     | x when x.StartsWith("System", StringComparison.Ordinal) -> 2
                     | x when x.StartsWith("Microsoft", StringComparison.Ordinal) -> 3
                     | x when x.StartsWith("EntityFrameworkCore.FSharp", StringComparison.Ordinal) ->
                         4
                     | _ -> 5),

                    n
                )

            if
                finalContextNamespace
                <> modelNamespace
                && not (String.IsNullOrEmpty modelNamespace)
            then
                finalNamespaces <-
                    finalNamespaces
                    |> Seq.append [ modelNamespace ]

            stringBuffer {
                $"namespace {finalContextNamespace}"
                ""

                for ns in finalNamespaces do
                    $"open {ns}"

                ""
                finalCode
            }
