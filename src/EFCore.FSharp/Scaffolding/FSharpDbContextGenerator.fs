namespace EntityFrameworkCore.FSharp.Scaffolding

open System
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Scaffolding

open EntityFrameworkCore.FSharp.EntityFrameworkExtensions
open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open EntityFrameworkCore.FSharp.Internal
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Diagnostics
open Microsoft.EntityFrameworkCore.Metadata.Conventions
open Microsoft.EntityFrameworkCore.ChangeTracking.Internal

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
            |> append "type " |> append contextName |> appendLine " ="
            |> indent
            |> appendLine "inherit DbContext"
            |> appendEmptyLine
            |> appendLine "new() = { inherit DbContext() }"
            |> append "new(options : DbContextOptions<" |> append contextName |> appendLine ">) ="
            |> appendLineIndent "{ inherit DbContext(options) }"
            |> appendEmptyLine

    let generateDbSet (sb:IndentedStringBuilder) (entityType : IEntityType) =

        let dbSetName = entityDbSetName entityType
        let mutableName = "_" + dbSetName

        sb
            |> appendLine "[<DefaultValue>]"
            |> append "val mutable private " |> append mutableName |> append " : DbSet<" |> append entityType.Name |> appendLine ">"
            |> append "member this." |> appendLine dbSetName
            |> indent
            |> append "with get() = this." |> appendLine mutableName
            |> append "and set v = this." |> append mutableName |> appendLine " <- v"
            |> unindent
            |> ignore

        sb.AppendLine() |> ignore

    let generateDbSets (model:IModel) (sb:IndentedStringBuilder) =

        model.GetEntityTypes()
            |> Seq.iter(fun entityType -> entityType |> generateDbSet sb)

        if model.GetEntityTypes() |> Seq.isEmpty |> not then
            sb |> appendEmptyLine |> ignore

        sb

    let generateEntityTypeErrors (model:IModel) (sb:IndentedStringBuilder) =

        let entityTypeErrors = modelEntityTypeErrors model

        entityTypeErrors
            |> Seq.iter (fun e -> sb |> appendLine (sprintf "// %s Please see the warning messages." e.Value) |> ignore)

        if entityTypeErrors |> Seq.isEmpty |> not then
            sb |> appendEmptyLine |> ignore

        sb

    let generateOnConfiguring (connectionString:string) suppressOnConfiguring suppressConnectionStringWarning (sb:IndentedStringBuilder) =

        let writeWarning suppressWarning connString (isb:IndentedStringBuilder) =
            if suppressWarning then
                isb
                    else
                isb
                |> unindent
                |> unindent
                |> unindent
                |> unindent
                |> appendLine "#warning: To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263."
                |> indent
                |> indent
                |> indent

        if suppressOnConfiguring then
            sb
        else
        sb
            |> appendLine "override this.OnConfiguring(optionsBuilder: DbContextOptionsBuilder) ="
            |> indent
            |> appendLine "if not optionsBuilder.IsConfigured then"
            |> indent
                |> writeWarning suppressConnectionStringWarning connectionString
                |> appendLine ("optionsBuilder" + (connectionString |> providerCodeGenerator.GenerateUseProvider |> code.Fragment) + " |> ignore")
            |> appendLine "()"
            |> appendEmptyLine
            |> unindent
            |> unindent

    let removeAnnotation (annotationToRemove : string) (annotations : IAnnotation seq) =
        annotations |> Seq.filter (fun a -> a.Name <> annotationToRemove)

    let generateAnnotations (annotations: IAnnotation seq) =
        annotations
        |> Seq.map (fun a ->
            let name = FSharpUtilities.delimitString(a.Name)
            let literal = FSharpUtilities.generateLiteral(a.Value)
            sprintf ".HasAnnotation(%s, %s)" name literal)

    let linesFromAnnotations (annotations: IAnnotation seq) (annotatable: IAnnotatable) =
        let annotations =
            annotations
            |> Seq.map (fun a -> a.Name, a)
            |> readOnlyDict
            |> Dictionary

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
            |> indent
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
            |> appendLine entityLambdaIdentifier
            |> indent
            |> ignore

            lines
            |> Seq.iter(fun l -> sb |> appendLine l |> ignore)

            sb
            |> appendLine "|> ignore"
            |> unindent
            |> unindent
            |> ignore

    let generateKeyGuardClause (key : IKey) (annotations : IAnnotation list) useDataAnnotations explicitName =
        if key.Properties.Count = 1 && annotations.IsEmpty then
            (*
            let ck = key :?> Key
            let kvc = KeyDiscoveryConvention.
            let props =
                KeyDiscoveryConvention.DiscoverKeyProperties(
                    ck.DeclaringEntityType,
                    (key.Properties |> Seq.map(fun p -> p :> IConventionProperty) |> Seq.toList))

            if key.Properties.StructuralSequenceEqual(props |> Seq.cast) then
                true    *)
            if (not explicitName) && useDataAnnotations then
                true
            else false
        else
            false

    let generateKey (key : IKey) useDataAnnotations sb =

        if isNull key then
            ()
        else

            let annotations =
                key.GetAnnotations()
                |> removeAnnotation RelationalAnnotationNames.Name
                |> Seq.toList

            let explicitName = key.GetName() <> key.GetDefaultName()

            let shouldExitEarly = generateKeyGuardClause key annotations useDataAnnotations explicitName

            if shouldExitEarly then
                ()
            else

                let lines = ResizeArray<string>()
                lines.Add(sprintf ".HasKey(%s)" (generateLambdaToKey key.Properties "e"))

                if explicitName then
                    lines.Add(sprintf ".HasName(%s)" (code.Literal (key.GetName())))

                linesFromAnnotations annotations key |> lines.AddRange

                sb |> appendMultiLineFluentApi key.DeclaringEntityType lines

    let generateTableName (entityType : IEntityType) sb =

        let tableName = entityType.GetTableName()
        let schema = entityType.GetSchema()
        let defaultSchema = entityType.Model.GetDefaultSchema()

        let explicitSchema = not (isNull schema) && schema <> defaultSchema
        let explicitTable = explicitSchema || (not (isNull tableName) && tableName <> entityType.GetDbSetName())

        if explicitTable then

            let parameterString =
                if explicitSchema then
                    sprintf "%s, %s" (code.Literal tableName) (code.Literal schema)
                else
                    code.Literal tableName


            let lines = ResizeArray<string>()
            lines.Add(sprintf ".ToTable(%s)" parameterString)

            appendMultiLineFluentApi entityType lines sb

    let generateIndex (index : IIndex) sb =
        let lines = ResizeArray<string>()
        lines.Add(sprintf ".HasIndex(%s)" (generateLambdaToKey index.Properties "e"))

        let annotations = ResizeArray<IAnnotation>()
        annotations.AddRange(index.GetAnnotations())

        if not (String.IsNullOrEmpty(string index.[RelationalAnnotationNames.Name])) then
            lines.Add(sprintf ".HasName(%s)" (code.Literal (index.GetName())))
            annotations.RemoveAt(annotations.FindIndex(fun i -> i.Name = RelationalAnnotationNames.Name))

        if index.IsUnique then
            lines.Add(".IsUnique()")

        if not (isNull (index.GetFilter())) then
            lines.Add(sprintf ".HasFilter(%s)" (code.Literal (index.GetFilter())))
            annotations.RemoveAt(annotations.FindIndex(fun i -> i.Name = RelationalAnnotationNames.Filter))

        linesFromAnnotations annotations index |> lines.AddRange

        appendMultiLineFluentApi index.DeclaringEntityType lines sb

    let generateProperty (property : IProperty) useDataAnnotations sb =

        let lines = ResizeArray<string>()
        lines.Add(sprintf ".Property(fun e -> e.%s)" property.Name)

        let annotations =
            property.GetAnnotations()
            |> removeAnnotation RelationalAnnotationNames.ColumnName
            |> removeAnnotation RelationalAnnotationNames.ColumnType
            |> removeAnnotation CoreAnnotationNames.MaxLength
            |> removeAnnotation CoreAnnotationNames.Unicode
            |> removeAnnotation RelationalAnnotationNames.DefaultValue
            |> removeAnnotation RelationalAnnotationNames.DefaultValueSql
            |> removeAnnotation RelationalAnnotationNames.ComputedColumnSql
            |> removeAnnotation RelationalAnnotationNames.IsFixedLength
            |> removeAnnotation ScaffoldingAnnotationNames.ColumnOrdinal
            |> Seq.toList

        if useDataAnnotations then
            if not property.IsNullable &&
                (SharedTypeExtensions.isNullableType property.ClrType ||
                 SharedTypeExtensions.isOptionType property.ClrType) &&
                not (property.IsPrimaryKey()) then
                    lines.Add ".IsRequired()"

            let columnName = property.GetColumnName()

            if not (isNull columnName) && columnName <> property.Name then
                lines.Add(sprintf ".HasColumnName(%s)" (code.Literal columnName))

            let columnType = property.GetConfiguredColumnType()

            if not (isNull columnName) then
                lines.Add(sprintf ".HasColumnType(%s)" (code.Literal columnType))

            let maxLength = property.GetMaxLength()

            if maxLength.HasValue then
                lines.Add(sprintf ".HasMaxLength(%s)" (code.Literal maxLength.Value))

        if property.IsUnicode().HasValue then
            lines.Add(sprintf ".IsUnicode(%s)" (if property.IsUnicode().Value then "" else "false"))

        if property.IsFixedLength().GetValueOrDefault() then
            lines.Add ".IsFixedLength()"

        if not (property.GetDefaultValue() |> isNull) then
            lines.Add(sprintf ".HasDefaultValue(%s)" (property.GetDefaultValue() |> code.UnknownLiteral))

        if not (property.GetDefaultValueSql() |> isNull) then
            lines.Add(sprintf ".HasDefaultValueSql(%s)" (property.GetDefaultValueSql() |> code.Literal))

        if not (property.GetComputedColumnSql() |> isNull) then
            lines.Add(sprintf ".HasComputedColumnSql(%s)" (property.GetComputedColumnSql() |> code.Literal))

        let valueGenerated = property.ValueGenerated
        let mutable isRowVersion = false

        let concreteProp = property :?> Property
        if concreteProp.GetValueGeneratedConfigurationSource().HasValue
            && RelationalValueGenerationConvention.GetValueGenerated(concreteProp) <> Nullable(valueGenerated) then

                let methodName =
                    match valueGenerated with
                    | ValueGenerated.OnAdd -> "ValueGeneratedOnAdd"
                    | ValueGenerated.OnAddOrUpdate ->
                        isRowVersion <- property.IsConcurrencyToken
                        if isRowVersion then "IsRowVersion" else "ValueGeneratedOnAddOrUpdate"
                    | ValueGenerated.Never -> "ValueGeneratedNever"
                    | _ -> ""

                lines.Add(sprintf ".%s()" methodName)

        if property.IsConcurrencyToken && not isRowVersion then
            lines.Add ".IsConcurrencyToken()"

        linesFromAnnotations annotations property |> lines.AddRange

        match lines.Count with
        | 2 ->
            let concatLines =
                seq {
                    yield (lines.[0] + lines.[1])
                }

            appendMultiLineFluentApi property.DeclaringEntityType concatLines sb
        | _ -> appendMultiLineFluentApi property.DeclaringEntityType lines sb


    let generateRelationship (fk : IForeignKey) useDataAnnotations sb =

        let mutable canUseDataAnnotations = false
        let annotations = fk.GetAnnotations() |> ResizeArray

        let lines = ResizeArray<string>()

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
            lines.Add(sprintf ".HasConstraintName(%s)" (fk.GetConstraintName() |> code.Literal))
            annotations.RemoveAt(annotations.FindIndex(fun a -> a.Name = RelationalAnnotationNames.Name))

        linesFromAnnotations annotations fk |> lines.AddRange

        if not useDataAnnotations || not canUseDataAnnotations then
            appendMultiLineFluentApi fk.DeclaringEntityType lines sb

        ()

    let generateEntityType (entityType : IEntityType) (useDataAnnotations : bool) (sb:IndentedStringBuilder) =

        sb |> generateKey (entityType.FindPrimaryKey()) useDataAnnotations

        let annotations =
            entityType.GetAnnotations()
            |> removeAnnotation CoreAnnotationNames.ConstructorBinding
            |> removeAnnotation RelationalAnnotationNames.TableName
            |> removeAnnotation RelationalAnnotationNames.Schema
            |> removeAnnotation ScaffoldingAnnotationNames.DbSetName
            |> Seq.toList

        if not useDataAnnotations then
            sb |> generateTableName entityType |> ignore

        sb |> appendMultiLineFluentApi entityType (linesFromAnnotations annotations entityType)

        entityType.GetIndexes() |> Seq.iter(fun i -> generateIndex i sb)
        entityType.GetProperties() |> Seq.iter(fun p -> generateProperty p useDataAnnotations sb)
        entityType.GetForeignKeys() |> Seq.iter(fun fk -> generateRelationship fk useDataAnnotations sb)

        sb


    let generateOnModelCreating (model:IModel) (useDataAnnotations:bool) (sb:IndentedStringBuilder) =
        sb.AppendLine("override this.OnModelCreating(modelBuilder: ModelBuilder) =")
            |> appendLineIndent "base.OnModelCreating(modelBuilder)"
            |> ignore

        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(model.GetAnnotations())
            |> Seq.map (fun a -> a.Name, a)
            |> readOnlyDict
            |> Dictionary

        annotationCodeGenerator.RemoveAnnotationsHandledByConventions(model, annotations)

        annotations.Remove(CoreAnnotationNames.ProductVersion) |> ignore
        annotations.Remove(RelationalAnnotationNames.MaxIdentifierLength) |> ignore
        annotations.Remove(ScaffoldingAnnotationNames.DatabaseName) |> ignore
        annotations.Remove(ScaffoldingAnnotationNames.EntityTypeErrors) |> ignore

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
                |> append "modelBuilder"
                |> indent
                |> appendLines lines false
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
                sb |> unindent |> appendLine ") |> ignore" |> ignore

        )

        model.GetSequences() |> Seq.iter(fun s -> generateSequence s sb |> ignore)

        sb
        |> appendEmptyLine
        |> appendLine "modelBuilder.RegisterOptionTypes()"
        |> unindent

    let generateClass model
                      contextName
                      connectionString
                      useDataAnnotations
                      suppressOnConfiguring
                      suppressConnectionStringWarning
                      sb =

        sb
            |> generateType contextName
            |> generateDbSets model
            |> generateEntityTypeErrors model
            |> generateOnConfiguring connectionString suppressOnConfiguring suppressConnectionStringWarning
            |> generateOnModelCreating model useDataAnnotations

    interface Microsoft.EntityFrameworkCore.Scaffolding.Internal.ICSharpDbContextGenerator with
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
                   suppressConnectionStringWarning

            |> ignore

            sb.ToString()
