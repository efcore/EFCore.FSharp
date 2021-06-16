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
    (providerCodeGenerator: IProviderConfigurationCodeGenerator,
        annotationCodeGenerator : IAnnotationCodeGenerator,
        code : ICSharpHelper) =

    let mutable _entityTypeBuilderInitialized = false

    let entityLambdaIdentifier = "entity"

    let defaultNamespaces =
        [
            "System"
            "System.Collections.Generic"
            "Microsoft.EntityFrameworkCore"
            "Microsoft.EntityFrameworkCore.Metadata"
            "EntityFrameworkCore.FSharp.Extensions"
        ]

    let writeNamespaces ``namespace`` (sb:IndentedStringBuilder) =
        sb
            |> append "namespace " |> appendLine ``namespace``
            |> appendEmptyLine
            |> writeNamespaces defaultNamespaces
            |> appendEmptyLine

    let generateType (contextName:string) (sb:IndentedStringBuilder) =
        sb
            |> append "open " |> appendLine (contextName.Replace("Context", "Domain"))
            |> appendEmptyLine
            |> appendLine (sprintf "type %s =" contextName)
            |> indent
            |> appendLine "inherit DbContext"
            |> appendEmptyLine
            |> appendLine "new() = { inherit DbContext() }"
            |> appendLine (sprintf "new(options : DbContextOptions<%s>) =" contextName)
            |> appendLineIndent "{ inherit DbContext(options) }"
            |> appendEmptyLine

    let generateDbSet (sb:IndentedStringBuilder) (entityType : IEntityType) =

        let dbSetName = entityDbSetName entityType

        sb
            |> appendLine (sprintf "[<DefaultValue>] val mutable private _%s : DbSet<%s>" dbSetName entityType.Name)
            |> appendLine (sprintf "member this.%s with get() = this._%s and set v = this._%s <- v" dbSetName dbSetName dbSetName)
            |> appendEmptyLine
            |> ignore

    let generateDbSets (model:IModel) (sb:IndentedStringBuilder) =

        model.GetEntityTypes()
            |> Seq.iter(fun entityType -> entityType |> generateDbSet sb)

        if model.GetEntityTypes() |> Seq.isEmpty |> not then
            sb |> appendEmptyLine
        else
            sb

    let generateEntityTypeErrors (model:IModel) (sb:IndentedStringBuilder) =

        let entityTypeErrors = modelEntityTypeErrors model

        entityTypeErrors
            |> Seq.iter (fun e -> sb |> appendLine (sprintf "// %s Please see the warning messages." e.Value) |> ignore)

        if entityTypeErrors |> Seq.isEmpty |> not then
            sb |> appendEmptyLine
        else
            sb

    let generateOnConfiguring (connectionString:string) suppressOnConfiguring (sb:IndentedStringBuilder) =

        if suppressOnConfiguring then
            sb
        else
        sb
            |> appendLine "override this.OnConfiguring(optionsBuilder: DbContextOptionsBuilder) ="
            |> indent
            |> appendLine "if not optionsBuilder.IsConfigured then"
            |> indent
            |> appendLine ("optionsBuilder" + (connectionString |> providerCodeGenerator.GenerateUseProvider |> code.Fragment) + " |> ignore")
            |> appendLine "()"
            |> appendEmptyLine
            |> unindent
            |> unindent

    let generateAnnotations (annotations: IAnnotation seq) =
        annotations
        |> Seq.map (fun a ->
            let name = FSharpUtilities.delimitString(a.Name)
            let literal = FSharpUtilities.generateLiteral(a.Value)
            sprintf ".HasAnnotation(%s, %s)" name literal)

    let linesFromAnnotations (annotations: IAnnotation seq) (annotatable: IAnnotatable) =
        let annotations =
            annotations
            |> annotationsToDictionary

        let fluentApiCalls =
            match annotatable with
            | :? IModel as m -> annotationCodeGenerator.GenerateFluentApiCalls(m, annotations)
            | :? IEntityType as e -> annotationCodeGenerator.GenerateFluentApiCalls(e, annotations)
            | :? IKey as k -> annotationCodeGenerator.GenerateFluentApiCalls(k, annotations)
            | :? IForeignKey as fk -> annotationCodeGenerator.GenerateFluentApiCalls(fk, annotations)
            | :? IProperty as p -> annotationCodeGenerator.GenerateFluentApiCalls(p, annotations)
            | :? IIndex as i -> annotationCodeGenerator.GenerateFluentApiCalls(i, annotations)
            | _ -> failwith "Unhandled pattern match in isHandledByConvention"

        fluentApiCalls
            |> Seq.map code.Fragment
            |> Seq.append (generateAnnotations annotations.Values)

    let generateSequence (s: ISequence) (sb:IndentedStringBuilder) =

        let writeLineIfTrue truth name parameter (sb:IndentedStringBuilder) =
            if truth then
                sb |> appendLine (sprintf ".%s(%A)" name parameter)
            else sb

        let methodName =
            if s.ClrType = Sequence.DefaultClrType then
                "HasSequence"
            else
                sprintf "HasSequence<%s>" (FSharpUtilities.getTypeName(s.ClrType))

        let parameters =
            if (s.Schema |> String.IsNullOrEmpty) && (s.Model.GetDefaultSchema() <> s.Schema) then
                sprintf "%s, %s" (s.Name |> FSharpUtilities.delimitString) (s.Schema |> FSharpUtilities.delimitString)
            else
                s.Name |> FSharpUtilities.delimitString

        sb
            |> appendLine (sprintf "modelBuilder.%s(%s)" methodName parameters)
            |> indent
            |> writeLineIfTrue (s.StartValue <> (Sequence.DefaultStartValue |> int64)) "StartsAt" s.StartValue
            |> writeLineIfTrue (s.IncrementBy <> Sequence.DefaultIncrementBy) "IncrementsBy" s.IncrementBy
            |> writeLineIfTrue (s.MinValue <> Sequence.DefaultMinValue) "HasMin" s.MinValue
            |> writeLineIfTrue (s.MaxValue <> Sequence.DefaultMaxValue) "HasMax" s.MaxValue
            |> writeLineIfTrue (s.IsCyclic <> Sequence.DefaultIsCyclic) "IsCyclic" ""
            |> appendEmptyLine
            |> unindent
            |> ignore

    let generateLambdaToKey (properties : IReadOnlyList<IProperty>) lambdaIdentifier =
        match properties.Count with
        | 0 -> ""
        | 1 -> sprintf "fun %s -> %s.%s :> obj" lambdaIdentifier lambdaIdentifier (properties.[0].Name)
        | _ ->
            let props =
                properties |> Seq.map (fun p -> sprintf "%s.%s" lambdaIdentifier p.Name)

            sprintf "fun %s -> (%s) :> obj" lambdaIdentifier (String.Join(", ", props))

    let generatePropertyNameArray (properties : IReadOnlyList<IProperty>) =

        let props =
            properties |> Seq.map (fun p -> code.Literal p.Name)

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
            |> append entityLambdaIdentifier
            |> indent
            |> appendLines lines false
            |> appendLine "|> ignore"
            |> unindent
            |> unindent
            |> ignore

    let generateKeyGuardClause (key : IKey) (annotations : IAnnotation seq) useDataAnnotations explicitName =
        if key.Properties.Count = 1 && annotations |> Seq.isEmpty then
            match key with
            | :? IConventionKey as concreteKey ->
                let keyProperties = key.Properties
                let concreteDeclaringProperties =
                    concreteKey.DeclaringEntityType.GetProperties()
                    |> Seq.cast<IConventionProperty>


                let concreteProperties =
                    KeyDiscoveryConvention.DiscoverKeyProperties(
                        concreteKey.DeclaringEntityType, concreteDeclaringProperties)
                    |> Seq.cast<IProperty>


                System.Linq.Enumerable.SequenceEqual(keyProperties, concreteProperties)
            | _ ->
                if (not explicitName) && useDataAnnotations then
                    true
                else false
        else
            false

    let generateKey (key : IKey) (entityType: IEntityType) useDataAnnotations sb =

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

            annotationCodeGenerator.RemoveAnnotationsHandledByConventions(key, annotations);

            let explicitName = key.GetName() <> key.GetDefaultName()
            annotations.Remove(RelationalAnnotationNames.Name) |> ignore

            // TODO: guard clause code
            let earlyExit = generateKeyGuardClause key annotations.Values useDataAnnotations explicitName

            if not earlyExit then
                let lines = ResizeArray<string>()
                lines.Add(sprintf ".HasKey(%s)" (generateLambdaToKey key.Properties "e"))

                if explicitName then
                    lines.Add(sprintf ".HasName(%s)" (code.Literal (key.GetName())))

                annotationCodeGenerator.GenerateFluentApiCalls(key, annotations)
                |> Seq.map code.Fragment
                |> Seq.append (generateAnnotations annotations.Values)
                |> lines.AddRange

                appendMultiLineFluentApi key.DeclaringEntityType lines sb

    let generateTableName (entityType : IEntityType) sb =

        let tableName = entityType.GetTableName()
        let schema = entityType.GetSchema()
        let defaultSchema = entityType.Model.GetDefaultSchema()

        let explicitSchema = not (isNull schema) && schema <> defaultSchema
        let explicitTable = explicitSchema || not (isNull tableName) && tableName <> entityType.GetDbSetName()

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

        let explicitViewSchema = viewSchema |> isNull |> not && viewSchema <> defaultSchema
        let explicitViewTable = explicitViewSchema || viewName |> isNull |> not

        if explicitViewTable then
            let parameterString =
                if explicitViewSchema then $"{code.Literal(viewName)}, {code.Literal(viewSchema)}" else code.Literal(viewName)

            let lines = ResizeArray<string>()
            lines.Add($".ToView({parameterString})")

            appendMultiLineFluentApi entityType lines sb

    let generateIndex (index : IIndex) sb =
        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(index.GetAnnotations())
            |> annotationsToDictionary

        annotationCodeGenerator.RemoveAnnotationsHandledByConventions(index, annotations)

        let lines = ResizeArray<string>()
        lines.Add(
            sprintf ".HasIndex((%s), %s)" // Parentheses required for F# implicit conversion to Expression<Func<T, obj>>
                (generateLambdaToKey index.Properties "e")
                (code.Literal(index.GetDatabaseName())))

        annotations.Remove(RelationalAnnotationNames.Name) |> ignore

        if index.IsUnique then
            lines.Add(".IsUnique()")

        annotationCodeGenerator
            .GenerateFluentApiCalls(index, annotations)
            |> Seq.map (fun m -> code.Fragment(m))
            |> Seq.append (generateAnnotations annotations.Values)
            |> lines.AddRange

        appendMultiLineFluentApi index.DeclaringEntityType lines sb

    let generateProperty (property : IProperty) useDataAnnotations sb =

        let lines = ResizeArray<string>()
        lines.Add(sprintf ".Property(fun e -> e.%s)" property.Name)

        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(property.GetAnnotations())
            |> annotationsToDictionary

        annotationCodeGenerator.RemoveAnnotationsHandledByConventions(property, annotations)
        annotations.Remove(ScaffoldingAnnotationNames.ColumnOrdinal) |> ignore

        if useDataAnnotations then
            annotations.Remove(RelationalAnnotationNames.ColumnName) |> ignore
            annotations.Remove(RelationalAnnotationNames.ColumnType) |> ignore

            annotationCodeGenerator.GenerateDataAnnotationAttributes(property, annotations) |> ignore
        else
            if property.IsNullable |> not
               && property.ClrType |> SharedTypeExtensions.isNullableType
               && property.IsPrimaryKey() |> not then
                lines.Add(".IsRequired()")

        match property.GetConfiguredColumnType() |> isNull |> not with
        | true ->
            lines.Add($".HasColumnType({code.Literal(property.GetConfiguredColumnType())})")
            annotations.Remove(RelationalAnnotationNames.ColumnType) |> ignore
        | false -> ()

        match property.GetMaxLength() |> Option.ofNullable with
        | Some l ->
            lines.Add($".HasMaxLength({code.Literal(l)})")
        | None -> ()

        match property.GetPrecision() |> Option.ofNullable, property.GetScale() |> Option.ofNullable with
        | Some p, Some s when s <> 0 ->
            lines.Add($".HasPrecision({code.Literal(p)}, {code.Literal(s)})")
        | Some p, _ ->
            lines.Add($".HasPrecision({code.Literal(p)})")
        | _, _ -> ()

        match property.IsUnicode() |> Option.ofNullable with
        | Some b ->
            let arg = if b then "" else "false"
            lines.Add($".IsUnicode({arg})")
        | None -> ()

        match property.GetDefaultValue() |> Option.ofObj with
        | Some d ->
            annotations.Remove(RelationalAnnotationNames.DefaultValue) |> ignore
            match d with
            | :? DBNull -> lines.Add(".HasValue()")
            | _ -> lines.Add($".HasValue({code.UnknownLiteral(d)})")
        | _ -> ()

        let isRowVersion = false
        let valueGenerated = property.ValueGenerated

        match property with
        | :? IConventionProperty as cp ->
            match cp.GetValueGeneratedConfigurationSource() |> Option.ofNullable with
            | Some valueGeneratedConfigurationSource when
                valueGeneratedConfigurationSource <> ConfigurationSource.Convention
                && ValueGenerationConvention.GetValueGenerated(property) <> (valueGenerated |> Nullable) ->
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

        annotationCodeGenerator.GenerateFluentApiCalls(property, annotations)
        |> Seq.map code.Fragment
        |> Seq.append (generateAnnotations annotations.Values)
        |> lines.AddRange

        if lines.Count = 0 then
            ()
        elif lines.Count = 2 then
            let l1 = lines.[0] //Why?
            let l2 = lines.[1]
            lines.Clear()
            seq { l1; l2 } |> lines.AddRange

        appendMultiLineFluentApi property.DeclaringEntityType lines sb

    let generateRelationship (fk : IForeignKey) useDataAnnotations sb =

        let mutable canUseDataAnnotations = false
        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(fk.GetAnnotations())
            |> annotationsToDictionary

        annotationCodeGenerator.RemoveAnnotationsHandledByConventions(fk, annotations)

        let lines = ResizeArray()

        lines.Add(sprintf ".HasOne(%s)" (if isNull fk.DependentToPrincipal then "" else (sprintf "fun d -> d.%s" fk.DependentToPrincipal.Name)))
        lines.Add(sprintf ".%s(%s)" (if fk.IsUnique then "WithOne" else "WithMany") (if isNull fk.PrincipalToDependent then "" else code.Literal fk.PrincipalToDependent.Name))

        if not (fk.PrincipalKey.IsPrimaryKey()) then
            canUseDataAnnotations <- false
            lines.Add(sprintf ".HasPrincipalKey%s(%s)" (if fk.IsUnique then (sprintf "<%s>" ((fk.PrincipalEntityType :> ITypeBase).DisplayName())) else "") (generatePropertyNameArray fk.PrincipalKey.Properties) )

        lines.Add(sprintf ".HasForeignKey%s(%s)" (if fk.IsUnique then (sprintf "<%s>" ((fk.DeclaringEntityType :> ITypeBase).DisplayName())) else "") (generatePropertyNameArray fk.Properties) )

        let defaultOnDeleteAction = if fk.IsRequired then DeleteBehavior.Cascade else DeleteBehavior.ClientSetNull

        if fk.DeleteBehavior <> defaultOnDeleteAction then
            canUseDataAnnotations <- false
            lines.Add(sprintf ".OnDelete(%s)" (code.Literal fk.DeleteBehavior))

        if not (String.IsNullOrEmpty(string fk.[RelationalAnnotationNames.Name])) then
            canUseDataAnnotations <- false

        annotationCodeGenerator
            .GenerateFluentApiCalls(fk, annotations)
            |> Seq.map code.Fragment
            |> Seq.append (generateAnnotations annotations.Values)
            |> lines.AddRange

        if not useDataAnnotations || not canUseDataAnnotations then
            appendMultiLineFluentApi fk.DeclaringEntityType lines sb

        ()

    let generateEntityType (entityType : IEntityType) (useDataAnnotations : bool) (sb:IndentedStringBuilder) =

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
        } |> Seq.iter (annotations.Remove >> ignore)

        if useDataAnnotations then
            annotationCodeGenerator.GenerateDataAnnotationAttributes(entityType, annotations)
            |> ignore

        if (not useDataAnnotations) || not (isNull (entityType.GetViewName())) then
            sb |> generateTableName entityType

        sb |> appendMultiLineFluentApi entityType (linesFromAnnotations annotations.Values entityType)

        let lines = ResizeArray()

        annotationCodeGenerator.GenerateFluentApiCalls(entityType, annotations)
        |> Seq.map code.Fragment
        |> Seq.append (generateAnnotations annotations.Values)
        |> lines.AddRange

        appendMultiLineFluentApi entityType lines sb

        entityType.GetIndexes()
        |> Seq.iter(fun i ->
            let indexAnnotations =
                annotationCodeGenerator.FilterIgnoredAnnotations(i.GetAnnotations())
                |> annotationsToDictionary

            annotationCodeGenerator.RemoveAnnotationsHandledByConventions(i, indexAnnotations)
            if useDataAnnotations |> not || indexAnnotations.Count > 0 then
                generateIndex i sb)

        entityType.GetProperties() |> Seq.iter(fun p ->
            generateProperty p useDataAnnotations sb)
        entityType.GetForeignKeys() |> Seq.iter(fun fk ->
            generateRelationship fk useDataAnnotations sb)

        sb


    let generateOnModelCreating (model:IModel) (useDataAnnotations:bool) (sb:IndentedStringBuilder) =
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
        } |> Seq.iter (annotations.Remove >> ignore)

        let generateAnnotations (a: IAnnotation seq) =
            a
            |> Seq.map (fun a ->
                sprintf ".HasAnnotation(%s, %s)" (code.Literal(a.Name)) (code.UnknownLiteral(a.Value)) )

        let lines =
            annotationCodeGenerator.GenerateFluentApiCalls(model, annotations)
            |> Seq.map code.Fragment
            |> Seq.append (generateAnnotations annotations.Values)


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

        model.GetEntityTypes()
        |> Seq.iter(fun e ->
            _entityTypeBuilderInitialized <- false

            sb
            |> generateEntityType e useDataAnnotations
            |> ignore

            if _entityTypeBuilderInitialized then
                sb |> appendLine ") |> ignore" |> ignore

        )

        model.GetSequences() |> Seq.iter(fun s -> generateSequence s sb)

        sb
        |> appendEmptyLine
        |> appendLine "modelBuilder.RegisterOptionTypes()"
        |> unindent

    let generateClass model
                      contextName
                      connectionString
                      useDataAnnotations
                      suppressOnConfiguring
                      sb =

        sb
            |> generateType contextName
            |> generateDbSets model
            |> generateEntityTypeErrors model
            |> generateOnConfiguring connectionString suppressOnConfiguring
            |> generateOnModelCreating model useDataAnnotations

    interface ICSharpDbContextGenerator with
        member this.WriteCode (model,
                                contextName,
                                connectionString,
                                contextNamespace,
                                modelNamespace,
                                useDataAnnotations,
                                suppressConnectionStringWarning,
                                suppressOnConfiguring) =

            let sb = IndentedStringBuilder()

            let finalContextNamespace =
                if contextNamespace |> isNull then
                    modelNamespace
                else
                    contextNamespace

            sb
            |> writeNamespaces (finalContextNamespace)
            |> ignore

            if finalContextNamespace <> modelNamespace then
                sb
                |> appendLine (sprintf "open %s" modelNamespace)
                |> ignore

            sb
            |> generateClass
                   model
                   contextName
                   connectionString
                   useDataAnnotations
                   suppressOnConfiguring

            |> ignore

            sb.ToString()
