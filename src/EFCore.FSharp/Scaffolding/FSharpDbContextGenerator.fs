namespace EntityFrameworkCore.FSharp.Scaffolding

open System
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Conventions
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Scaffolding
open Microsoft.EntityFrameworkCore.Scaffolding.Internal

open EntityFrameworkCore.FSharp.EntityFrameworkExtensions
open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open EntityFrameworkCore.FSharp.Internal

#nowarn "0044"

open System.Collections.Generic
open EntityFrameworkCore.FSharp

type FSharpDbContextGenerator
    (
        providerCodeGenerator: IProviderConfigurationCodeGenerator,
        annotationCodeGenerator: IAnnotationCodeGenerator,
        code: ICSharpHelper
    ) =

    let mutable _entityTypeBuilderInitialized = false

    let namespaces = HashSet<string>()
    let entityLambdaIdentifier = "entity"

    let generateConstructors (contextName: string) (sb: IndentedStringBuilder) =
        sb
        |> appendLine (sprintf "type %s =" contextName)
        |> indent
        |> appendLine "inherit DbContext"
        |> appendEmptyLine
        |> appendLine "new() = { inherit DbContext() }"
        |> appendLine (sprintf "new(options : DbContextOptions<%s>) = { inherit DbContext(options) }" contextName)
        |> appendEmptyLine

    let generateDbSet (sb: IndentedStringBuilder) (entityType: IEntityType) =

        let dbSetName = entityDbSetName entityType

        sb
        |> appendLine (sprintf "[<DefaultValue>] val mutable private _%s : DbSet<%s>" dbSetName entityType.Name)
        |> appendLine (sprintf "member this.%s" dbSetName)
        |> appendLineIndent (sprintf "with get() = this._%s"dbSetName)
        |> appendLineIndent (sprintf "and set v = this._%s <- v" dbSetName)
        |> appendEmptyLine
        |> ignore

    let generateDbSets (model: IModel) (sb: IndentedStringBuilder) =

        let typesToGenerate =
            model.GetEntityTypes()
            |> Seq.filter (isManyToManyJoinEntityType >> not)

        typesToGenerate
        |> Seq.iter (fun entityType -> entityType |> generateDbSet sb)

        if typesToGenerate |> Seq.isEmpty then
            sb
        else
            sb |> appendEmptyLine

    let generateEntityTypeErrors (model: IModel) (sb: IndentedStringBuilder) =

        let entityTypeErrors = model.GetEntityTypeErrors()

        entityTypeErrors
        |> Seq.iter
            (fun e ->
                sb
                |> appendLine (sprintf "// %s Please see the warning messages." e.Value)
                |> ignore)

        if entityTypeErrors |> Seq.isEmpty |> not then
            sb |> appendEmptyLine
        else
            sb

    let generateOnConfiguring (connectionString: string) suppressOnConfiguring (sb: IndentedStringBuilder) =

        if suppressOnConfiguring then
            sb
        else
            sb
            |> appendLine "override this.OnConfiguring(optionsBuilder: DbContextOptionsBuilder) ="
            |> indent
            |> appendLine "if not optionsBuilder.IsConfigured then"
            |> indent
            |> appendLine (
                "optionsBuilder"
                + (connectionString
                   |> providerCodeGenerator.GenerateUseProvider
                   |> code.Fragment)
                + " |> ignore"
            )
            |> unindent
            |> appendLine "()"
            |> appendEmptyLine
            |> unindent

    let generateAnnotations
        (annotatable: IAnnotatable)
        (annotations: Dictionary<string, IAnnotation>)
        (lines: ResizeArray<string>)
        =

        annotationCodeGenerator.GenerateFluentApiCalls(annotatable, annotations)
        |> Seq.iter
            (fun fluentApiCall ->
                lines.Add(code.Fragment(fluentApiCall))

                if notNull fluentApiCall.Namespace then
                    namespaces.Add fluentApiCall.Namespace |> ignore)

        lines.AddRange(
            annotations.Values
            |> Seq.map (fun a -> $".HasAnnotation({code.Literal(a.Name)}, {code.UnknownLiteral(a.Value)})")
        )

    let generateSequence (s: ISequence) (sb: IndentedStringBuilder) =

        let writeLineIfTrue truth name parameter (sb: IndentedStringBuilder) =
            if truth then
                sb
                |> appendLine (sprintf ".%s(%A)" name parameter)
            else
                sb

        let methodName =
            if s.ClrType = Sequence.DefaultClrType then
                "HasSequence"
            else
                sprintf "HasSequence<%s>" (FSharpUtilities.getTypeName (s.ClrType))

        let parameters =
            if (s.Schema |> String.IsNullOrEmpty)
               && (s.Model.GetDefaultSchema() <> s.Schema) then
                sprintf "%s, %s" (s.Name |> FSharpUtilities.delimitString) (s.Schema |> FSharpUtilities.delimitString)
            else
                s.Name |> FSharpUtilities.delimitString

        sb
        |> appendLine (sprintf "modelBuilder.%s(%s)" methodName parameters)
        |> indent
        |> writeLineIfTrue
            (s.StartValue
             <> (Sequence.DefaultStartValue |> int64))
            "StartsAt"
            s.StartValue
        |> writeLineIfTrue (s.IncrementBy <> Sequence.DefaultIncrementBy) "IncrementsBy" s.IncrementBy
        |> writeLineIfTrue (s.MinValue <> Sequence.DefaultMinValue) "HasMin" s.MinValue
        |> writeLineIfTrue (s.MaxValue <> Sequence.DefaultMaxValue) "HasMax" s.MaxValue
        |> writeLineIfTrue (s.IsCyclic <> Sequence.DefaultIsCyclic) "IsCyclic" ""
        |> appendEmptyLine
        |> unindent
        |> ignore

    let generateLambdaToKey (properties: IReadOnlyList<IProperty>) lambdaIdentifier =
        match properties.Count with
        | 0 -> ""
        | 1 -> sprintf "fun %s -> %s.%s :> obj" lambdaIdentifier lambdaIdentifier (properties.[0].Name)
        | _ ->
            let props =
                properties
                |> Seq.map (fun p -> sprintf "%s.%s" lambdaIdentifier p.Name)

            sprintf "fun %s -> (%s) :> obj" lambdaIdentifier (String.Join(", ", props))

    let generatePropertyNameArray (properties: IReadOnlyList<IProperty>) =

        let props =
            properties
            |> Seq.map (fun p -> code.Literal p.Name)

        sprintf "[| %s |]" (String.Join("; ", props))

    let initializeEntityTypeBuilder (entityType: IEntityType) sb =

        if not _entityTypeBuilderInitialized then
            sb
            |> appendEmptyLine
            |> appendLine (sprintf "modelBuilder.Entity<%s>(fun %s ->" entityType.Name entityLambdaIdentifier)
            |> ignore

        _entityTypeBuilderInitialized <- true

    let appendMultiLineFluentApi entityType lines sb =

        if lines |> Seq.isEmpty then
            ()
        else

            initializeEntityTypeBuilder entityType sb

            sb
            |> indent
            |> appendEmptyLine
            |> append (entityLambdaIdentifier + (lines |> Seq.head))
            |> indent
            |> appendLines (lines |> Seq.tail) false
            |> appendLine "|> ignore"
            |> unindent
            |> unindent
            |> ignore

    let generateKeyGuardClause (key: IKey) (annotations: IAnnotation seq) useDataAnnotations explicitName =
        if key.Properties.Count = 1
           && annotations |> Seq.isEmpty then
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
                if (not explicitName) && useDataAnnotations then
                    true
                else
                    false
        else
            false

    let generateKey (key: IKey) (entityType: IEntityType) useDataAnnotations sb =

        if isNull key then
            if not useDataAnnotations then
                let lines = ResizeArray()
                lines.Add ".HasNoKey()"
                appendMultiLineFluentApi entityType lines sb

            ()
        else

            let annotations =
                annotationCodeGenerator.FilterIgnoredAnnotations(key.GetAnnotations())
                |> annotationsToDictionary

            annotationCodeGenerator.RemoveAnnotationsHandledByConventions(key, annotations)

            let explicitName = key.GetName() <> key.GetDefaultName()

            annotations.Remove(RelationalAnnotationNames.Name)
            |> ignore

            // TODO: guard clause code
            let earlyExit =
                generateKeyGuardClause key annotations.Values useDataAnnotations explicitName

            if not earlyExit then
                let lines = ResizeArray<string>()
                lines.Add(sprintf ".HasKey(%s)" (generateLambdaToKey key.Properties "e"))

                if explicitName then
                    lines.Add(sprintf ".HasName(%s)" (code.Literal(key.GetName())))

                generateAnnotations key annotations lines
                appendMultiLineFluentApi key.DeclaringEntityType lines sb

    let generateTableName (entityType: IEntityType) sb =

        let tableName = entityType.GetTableName()
        let schema = entityType.GetSchema()
        let defaultSchema = entityType.Model.GetDefaultSchema()

        let explicitSchema =
            not (isNull schema) && schema <> defaultSchema

        let explicitTable =
            explicitSchema
            || not (isNull tableName)
               && tableName <> entityType.GetDbSetName()

        if explicitTable then

            let parameterString =
                if explicitSchema then
                    sprintf "%s, %s" (code.Literal tableName) (code.Literal schema)
                else
                    code.Literal tableName


            let lines = ResizeArray<string>()
            lines.Add($".ToTable({parameterString})")

            appendMultiLineFluentApi entityType lines sb

        let viewName = entityType.GetViewName()
        let viewSchema = entityType.GetViewSchema()

        let explicitViewSchema =
            viewSchema |> isNull |> not
            && viewSchema <> defaultSchema

        let explicitViewTable =
            explicitViewSchema || viewName |> isNull |> not

        if explicitViewTable then
            let parameterString =
                if explicitViewSchema then
                    $"{code.Literal(viewName)}, {code.Literal(viewSchema)}"
                else
                    code.Literal(viewName)

            let lines = ResizeArray<string>()
            lines.Add($".ToView({parameterString})")

            appendMultiLineFluentApi entityType lines sb

    let generateIndex (index: IIndex) sb =
        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(index.GetAnnotations())
            |> annotationsToDictionary

        annotationCodeGenerator.RemoveAnnotationsHandledByConventions(index, annotations)

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
        appendMultiLineFluentApi index.DeclaringEntityType lines sb

    let generateProperty (property: IProperty) useDataAnnotations sb =

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
        else if property.IsNullable |> not
                && property.ClrType
                   |> SharedTypeExtensions.isNullableType
                && property.IsPrimaryKey() |> not then
            lines.Add(".IsRequired()")

        match property.GetConfiguredColumnType()
              |> isNull
              |> not with
        | true ->
            lines.Add($".HasColumnType({code.Literal(property.GetConfiguredColumnType())})")

            annotations.Remove(RelationalAnnotationNames.ColumnType)
            |> ignore
        | false -> ()

        match property.GetMaxLength() |> Option.ofNullable with
        | Some l -> lines.Add($".HasMaxLength({code.Literal(l)})")
        | None -> ()

        match property.GetPrecision() |> Option.ofNullable, property.GetScale() |> Option.ofNullable with
        | Some p, Some s when s <> 0 -> lines.Add($".HasPrecision({code.Literal(p)}, {code.Literal(s)})")
        | Some p, _ -> lines.Add($".HasPrecision({code.Literal(p)})")
        | _, _ -> ()

        match property.IsUnicode() |> Option.ofNullable with
        | Some b ->
            let arg = if b then "" else "false"
            lines.Add($".IsUnicode({arg})")
        | None -> ()

        match property.GetDefaultValue() |> Option.ofObj with
        | Some d ->
            annotations.Remove(RelationalAnnotationNames.DefaultValue)
            |> ignore

            match d with
            | :? DBNull -> lines.Add(".HasValue()")
            | _ -> lines.Add($".HasValue({code.UnknownLiteral(d)})")
        | _ -> ()

        let isRowVersion = false
        let valueGenerated = property.ValueGenerated

        match property with
        | :? IConventionProperty as cp ->
            match cp.GetValueGeneratedConfigurationSource()
                  |> Option.ofNullable with
            | Some valueGeneratedConfigurationSource when
                valueGeneratedConfigurationSource
                <> ConfigurationSource.Convention
                && ValueGenerationConvention.GetValueGenerated(property)
                   <> (valueGenerated |> Nullable)
                ->
                let methodName =
                    match valueGenerated with
                    | ValueGenerated.OnAdd -> "ValueGeneratedOnAdd"
                    | ValueGenerated.OnAddOrUpdate when property.IsConcurrencyToken -> "IsRowVersion"
                    | ValueGenerated.OnAddOrUpdate -> "ValueGeneratedOnAddOrUpdate"
                    | ValueGenerated.OnUpdate -> "ValueGeneratedOnUpdate"
                    | ValueGenerated.Never -> "ValueGeneratedNever"
                    | _ -> invalidOp $"Unhandled enum value ValueGenerated.{valueGenerated}"

                lines.Add($".{methodName}()")
            | _ -> ()
        | _ -> ()

        if property.IsConcurrencyToken && isRowVersion |> not then
            lines.Add(".IsConcurrencyToken()")

        generateAnnotations property annotations lines

        if lines.Count = 0 then
            ()
        elif lines.Count = 2 then
            let l1 = lines.[0]
            let l2 = lines.[1]
            lines.Clear()
            lines.Add(l1 + l2)

        appendMultiLineFluentApi property.DeclaringEntityType lines sb

    let generateRelationship (fk: IForeignKey) useDataAnnotations sb =

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
                (if fk.IsUnique then
                     "WithOne"
                 else
                     "WithMany")
                (if isNull fk.PrincipalToDependent then
                     ""
                 else
                     code.Literal fk.PrincipalToDependent.Name)
        )

        if not (fk.PrincipalKey.IsPrimaryKey()) then
            canUseDataAnnotations <- false

            let typeParam =
                if fk.IsUnique then
                    (sprintf
                        "<%s>"
                        ((fk.PrincipalEntityType :> ITypeBase)
                            .DisplayName()))
                else
                    ""

            let methodParams =
                code.Lambda(fk.PrincipalKey.Properties, "p")

            lines.Add(sprintf ".HasPrincipalKey%s(%s)" typeParam methodParams)

        let typeParam =
            if fk.IsUnique then
                (sprintf "<%s>" fk.DeclaringEntityType.Name)
            else
                ""

        let methodParams = code.Lambda(fk.Properties, "d")
        lines.Add(sprintf ".HasForeignKey%s(%s)" typeParam methodParams)

        let defaultOnDeleteAction =
            if fk.IsRequired then
                DeleteBehavior.Cascade
            else
                DeleteBehavior.ClientSetNull

        if fk.DeleteBehavior <> defaultOnDeleteAction then
            canUseDataAnnotations <- false
            lines.Add(sprintf ".OnDelete(%s)" (code.Literal fk.DeleteBehavior))

        if not (String.IsNullOrEmpty(string fk.[RelationalAnnotationNames.Name])) then
            canUseDataAnnotations <- false

        generateAnnotations fk annotations lines

        if not useDataAnnotations
           || not canUseDataAnnotations then
            appendMultiLineFluentApi fk.DeclaringEntityType lines sb

        ()

    let generateManyToMany (skipNavigation: ISkipNavigation) (sb: IndentedStringBuilder) =

        let lines = ResizeArray<string>()

        let writeLines terminator =
            for line in lines do
                sb |> append line |> ignore

            sb |> appendLine terminator |> ignore
            lines.Clear()

        let generateForeignKeyConfigurationLines (foreignKey: IForeignKey) targetType identifier =
            let annotations =
                annotationCodeGenerator.FilterIgnoredAnnotations(foreignKey.GetAnnotations())
                |> annotationsToDictionary

            lines.Add(sprintf "fun %s -> %s.HasOne<%s>().WithMany()" identifier identifier targetType)

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

            if foreignKey.DeleteBehavior <> defaultOnDeleteAction then
                lines.Add $".OnDelete({code.Literal(foreignKey.DeleteBehavior)})"

            generateAnnotations foreignKey annotations lines
            writeLines ","

        if not _entityTypeBuilderInitialized then
            initializeEntityTypeBuilder skipNavigation.DeclaringEntityType sb


        let inverse = skipNavigation.Inverse
        let joinEntityType = skipNavigation.JoinEntityType

        sb
        |> appendEmptyLine
        |> indent
        |> appendLine $"{entityLambdaIdentifier}.HasMany(fun d -> d.{skipNavigation.Name})"
        |> indent
        |> appendLine $".WithMany(fun p -> p.{inverse.Name})"
        |> appendLine $".UsingEntity<{code.Reference Model.DefaultPropertyBagType}>("
        |> indent
        |> appendLine $"{code.Literal joinEntityType.Name},"
        |> ignore

        generateForeignKeyConfigurationLines inverse.ForeignKey inverse.ForeignKey.PrincipalEntityType.Name "l"

        generateForeignKeyConfigurationLines
            skipNavigation.ForeignKey
            skipNavigation.ForeignKey.PrincipalEntityType.Name
            "r"

        sb |> appendLine "fun j ->" |> indent |> ignore

        let key = joinEntityType.FindPrimaryKey()

        let keyAnnotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(key.GetAnnotations())
            |> annotationsToDictionary

        annotationCodeGenerator.RemoveAnnotationsHandledByConventions(key, keyAnnotations)
        |> ignore

        let explicitName = key.GetName() <> key.GetDefaultName()

        keyAnnotations.Remove(RelationalAnnotationNames.Name)
        |> ignore

        let props =
            key.Properties
            |> Seq.map (fun e -> code.Literal(e.Name))
            |> join ", "

        lines.Add $"j.HasKey({props})"

        if explicitName then
            lines.Add ".HasName({code.Literal(key.GetName())})"

        generateAnnotations key keyAnnotations lines
        writeLines " |> ignore"

        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(joinEntityType.GetAnnotations())
            |> annotationsToDictionary

        annotationCodeGenerator.RemoveAnnotationsHandledByConventions(joinEntityType, annotations)
        |> ignore

        [ RelationalAnnotationNames.TableName
          RelationalAnnotationNames.Schema
          RelationalAnnotationNames.ViewName
          RelationalAnnotationNames.ViewSchema
          ScaffoldingAnnotationNames.DbSetName
          RelationalAnnotationNames.ViewDefinitionSql ]
        |> Seq.iter (annotations.Remove >> ignore)

        let tableName = joinEntityType.GetTableName()
        let schema = joinEntityType.GetSchema()
        let defaultSchema = joinEntityType.Model.GetDefaultSchema()

        let explicitSchema =
            notNull schema && schema <> defaultSchema

        let parameterString =
            if explicitSchema then
                (code.Literal tableName)
                + ", "
                + (code.Literal schema)
            else
                code.Literal tableName

        lines.Add $".ToTable({parameterString})"
        generateAnnotations joinEntityType annotations lines
        writeLines " |> ignore"

        joinEntityType.GetIndexes()
        |> Seq.iter
            (fun index ->
                let indexAnnotations =
                    annotationCodeGenerator.FilterIgnoredAnnotations(index.GetAnnotations())
                    |> annotationsToDictionary

                annotationCodeGenerator.RemoveAnnotationsHandledByConventions(index, indexAnnotations)
                |> ignore

                let indexProps =
                    index.Properties
                    |> Seq.map (fun e -> e.Name)
                    |> Seq.toArray

                lines.Add $".HasIndex({code.Literal(indexProps)}, {code.Literal(index.GetDatabaseName())}"

                if index.IsUnique then
                    lines.Add ".IsUnique()"

                generateAnnotations index indexAnnotations lines
                writeLines " |> ignore")

        joinEntityType.GetProperties()
        |> Seq.iter
            (fun property ->
                lines.Add $"j.IndexerProperty<{code.Reference(property.ClrType)}>({code.Literal(property.Name)})"

                let propertyAnnotations =
                    annotationCodeGenerator.FilterIgnoredAnnotations(property.GetAnnotations())
                    |> annotationsToDictionary

                propertyAnnotations.Remove RelationalAnnotationNames.ColumnOrder
                |> ignore

                if property.IsNullable |> not
                   && property.ClrType
                      |> SharedTypeExtensions.isNullableType
                   && property.IsPrimaryKey() |> not then
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

                if precision.HasValue
                   && scale.GetValueOrDefault() <> 0 then
                    lines.Add $".HasPrecision({code.Literal(precision.Value)}, {code.Literal(scale.Value)})"
                elif precision.HasValue then
                    lines.Add $".HasPrecision({code.Literal(precision.Value)})"

                if property.IsUnicode().HasValue then
                    let value =
                        if property.IsUnicode().Value then
                            ""
                        else
                            "false"

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
                    ((property :?> IConventionProperty)
                        .GetValueGeneratedConfigurationSource())

                if valueGeneratedConfigurationSource.HasValue
                   && valueGeneratedConfigurationSource.Value
                      <> ConfigurationSource.Convention
                   && ValueGenerationConvention.GetValueGenerated(property)
                      <> Nullable(valueGenerated) then
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
                                Microsoft.EntityFrameworkCore.Internal.DesignStrings.UnhandledEnumValue(
                                    $"ValueGenerated.{valueGenerated}"
                                )
                            )

                    lines.Add $".{methodName}()"

                if property.IsConcurrencyToken && not isRowVersion then
                    lines.Add ".IsConcurrencyToken()"

                generateAnnotations property propertyAnnotations lines

                if lines.Count > 1 then
                    sb |> appendEmptyLine |> ignore
                    writeLines " |> ignore"
                else
                    lines.Clear())

        sb
        |> unindent
        |> appendLine ") |> ignore"
        |> unindent
        |> unindent
        |> unindent
        |> ignore

    let generateEntityType (entityType: IEntityType) (useDataAnnotations: bool) (sb: IndentedStringBuilder) =

        generateKey (entityType.FindPrimaryKey()) entityType useDataAnnotations sb

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
        |> Seq.iter (annotations.Remove >> ignore)

        if useDataAnnotations then
            // Strip out any annotations handled as attributes - these are already handled when generating the entity's properties
            annotationCodeGenerator.GenerateDataAnnotationAttributes(entityType, annotations)
            |> ignore

        if
            (not useDataAnnotations)
            || notNull (entityType.GetViewName())
        then
            sb |> generateTableName entityType

        let lines = ResizeArray<string>()

        generateAnnotations entityType annotations lines
        appendMultiLineFluentApi entityType lines sb

        entityType.GetIndexes()
        |> Seq.iter
            (fun i ->
                let indexAnnotations =
                    annotationCodeGenerator.FilterIgnoredAnnotations(i.GetAnnotations())
                    |> annotationsToDictionary

                annotationCodeGenerator.RemoveAnnotationsHandledByConventions(i, indexAnnotations)

                if not useDataAnnotations
                   || indexAnnotations.Count > 0 then
                    generateIndex i sb)

        entityType.GetProperties()
        |> Seq.iter (fun p -> generateProperty p useDataAnnotations sb)

        entityType.GetForeignKeys()
        |> Seq.iter (fun fk -> generateRelationship fk useDataAnnotations sb)

        entityType.GetSkipNavigations()
        |> Seq.iter
            (fun skip ->
                let containingKey =
                    skip.JoinEntityType.FindPrimaryKey().Properties.[0]
                        .GetContainingForeignKeys()
                    |> Seq.head

                if containingKey.PrincipalEntityType = entityType then
                    generateManyToMany skip sb)

        sb


    let generateOnModelCreating (model: IModel) (useDataAnnotations: bool) (sb: IndentedStringBuilder) =
        sb.AppendLine("override this.OnModelCreating(modelBuilder: ModelBuilder) =")
        |> appendLineIndent "base.OnModelCreating(modelBuilder)"
        |> ignore

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
        |> Seq.iter (annotations.Remove >> ignore)

        let lines = ResizeArray<string>()
        generateAnnotations model annotations lines

        if lines |> Seq.isEmpty |> not then
            sb
            |> appendEmptyLine
            |> indent
            |> append ("modelBuilder" + (lines |> Seq.head))
            |> indent
            |> appendLines (lines |> Seq.tail) false
            |> appendLine "|> ignore"
            |> appendEmptyLine
            |> unindent
            |> unindent
            |> ignore

        sb |> indent |> ignore

        let typesToGenerate =
            model.GetEntityTypes()
            |> Seq.filter (isManyToManyJoinEntityType >> not)

        typesToGenerate
        |> Seq.iter
            (fun e ->
                _entityTypeBuilderInitialized <- false

                sb
                |> generateEntityType e useDataAnnotations
                |> ignore

                if _entityTypeBuilderInitialized then
                    sb |> appendLine ") |> ignore" |> ignore

                )

        model.GetSequences()
        |> Seq.iter (fun s -> generateSequence s sb)

        sb
        |> appendEmptyLine
        |> appendLine "modelBuilder.RegisterOptionTypes()"
        |> unindent

    let generateClass model contextName connectionString useDataAnnotations suppressOnConfiguring sb =

        sb
        |> generateConstructors contextName
        |> generateDbSets model
        |> generateEntityTypeErrors model
        |> generateOnConfiguring connectionString suppressOnConfiguring
        |> generateOnModelCreating model useDataAnnotations

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

            let sb = IndentedStringBuilder()
            namespaces.Clear()

            namespaces.Add "System" |> ignore

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

            sb
            |> generateClass model contextName connectionString useDataAnnotations suppressOnConfiguring
            |> ignore

            let finalBuilder = IndentedStringBuilder()

            finalBuilder
            |> appendLine $"namespace {finalContextNamespace}"
            |> appendEmptyLine
            |> ignore

            let mutable finalNamespaces =
                namespaces
                |> Seq.sortBy
                    (fun n ->
                        (match n with
                         | "System" -> 1
                         | x when x.StartsWith("System", StringComparison.Ordinal) -> 2
                         | x when x.StartsWith("Microsoft", StringComparison.Ordinal) -> 3
                         | x when x.StartsWith("EntityFrameworkCore.FSharp", StringComparison.Ordinal) -> 4
                         | _ -> 5),

                        n)

            if
                finalContextNamespace <> modelNamespace
                && not (String.IsNullOrEmpty modelNamespace)
            then
                finalNamespaces <- finalNamespaces |> Seq.append [ modelNamespace ]

            for ns in finalNamespaces do
                finalBuilder |> appendLine $"open {ns}" |> ignore

            finalBuilder |> appendEmptyLine |> ignore

            finalBuilder.ToString() + sb.ToString()
