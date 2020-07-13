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
    (providerCodeGenerators: IProviderConfigurationCodeGenerator seq,
        annotationCodeGenerator : IAnnotationCodeGenerator,
        code : ICSharpHelper) =

    let providerCodeGenerator =
        if Seq.isEmpty providerCodeGenerators then
            let name = "providerCodeGenerators"
            invalidArg name (AbstractionsStrings.CollectionArgumentIsEmpty name)
        else
            providerCodeGenerators |> Seq.tryLast
         
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

    let generateOnConfiguring (connectionString:string) (sb:IndentedStringBuilder) =      

        let connStringLine =
            match providerCodeGenerator with
            | Some pcg ->
                let contextOptions = pcg.GenerateContextOptions()
                let useProviderCall =
                    if isNull contextOptions then
                        pcg.GenerateUseProvider(connectionString, pcg.GenerateProviderOptions())
                    else
                        pcg.GenerateUseProvider(connectionString, pcg.GenerateProviderOptions()).Chain(contextOptions)

                let fragment = code.Fragment useProviderCall

                sprintf "optionsBuilder%s |> ignore" fragment
            | None ->
                let name = "providerCodeGenerators"
                invalidArg name (AbstractionsStrings.CollectionArgumentIsEmpty name)
            
        sb
            |> appendLine "override this.OnConfiguring(optionsBuilder: DbContextOptionsBuilder) ="
            |> indent
            |> appendLine "if not optionsBuilder.IsConfigured then"
            |> indent
            |> appendLine connStringLine
            |> appendLine "()"
            |> appendEmptyLine
            |> unindent
            |> unindent

    let removeAnnotation (annotationToRemove : string) (annotations : IAnnotation seq) =
        annotations |> Seq.filter (fun a -> a.Name <> annotationToRemove)

    let checkAnnotation (model:IModel) (annotation: IAnnotation) =
        if annotationCodeGenerator.IsHandledByConvention(model, annotation) then
            (annotation |> Some, None)
        else
            let methodCall = annotationCodeGenerator.GenerateFluentApi(model, annotation)
            let line = FSharpUtilities.generate(methodCall)

            if isNull line then
                (None, None)
            else            
                (annotation |> Some, line |> Some)

    let generateAnnotations (annotations: IAnnotation seq) =
        annotations
        |> Seq.map (fun a ->
            let name = FSharpUtilities.delimitString(a.Name)
            let literal = FSharpUtilities.generateLiteral(a.Value)
            sprintf ".HasAnnotation(%s, %s)" name literal)

    let generateEntityTypes (entities: IEntityType seq) useDataAnnotations (sb:IndentedStringBuilder) =
        sb

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

    let isHandledByConvention (annotatable : IAnnotatable) annotation =
        match annotatable with
        | :? IModel as m -> annotationCodeGenerator.IsHandledByConvention(m, annotation)
        | :? IEntityType as e -> annotationCodeGenerator.IsHandledByConvention(e, annotation)
        | :? IKey as k -> annotationCodeGenerator.IsHandledByConvention(k, annotation)
        | :? IForeignKey as fk -> annotationCodeGenerator.IsHandledByConvention(fk, annotation)
        | :? IProperty as p -> annotationCodeGenerator.IsHandledByConvention(p, annotation)
        | :? IIndex as i -> annotationCodeGenerator.IsHandledByConvention(i, annotation)
        | _ -> failwith "Unhandled pattern match in isHandledByConvention"

    let generateFluentApiWithLanguage (annotatable : IAnnotatable) annotation =
        match annotatable with
        | :? IModel as m -> annotationCodeGenerator.GenerateFluentApi(m, annotation)
        | :? IEntityType as e -> annotationCodeGenerator.GenerateFluentApi(e, annotation)
        | :? IKey as k -> annotationCodeGenerator.GenerateFluentApi(k, annotation)
        | :? IForeignKey as fk -> annotationCodeGenerator.GenerateFluentApi(fk, annotation)
        | :? IProperty as p -> annotationCodeGenerator.GenerateFluentApi(p, annotation)
        | :? IIndex as i -> annotationCodeGenerator.GenerateFluentApi(i, annotation)
        | _ -> failwith "Unhandled pattern match in generateFluentApiWithLanguage"

    let generateFluentApi (annotatable : IAnnotatable) annotation =
        match annotatable with
        | :? IModel as m -> annotationCodeGenerator.GenerateFluentApi(m, annotation)
        | :? IEntityType as e -> annotationCodeGenerator.GenerateFluentApi(e, annotation)
        | :? IKey as k -> annotationCodeGenerator.GenerateFluentApi(k, annotation)
        | :? IForeignKey as fk -> annotationCodeGenerator.GenerateFluentApi(fk, annotation)
        | :? IProperty as p -> annotationCodeGenerator.GenerateFluentApi(p, annotation)
        | :? IIndex as i -> annotationCodeGenerator.GenerateFluentApi(i, annotation)
        | _ -> failwith "Unhandled pattern match in generateFluentApi"
            
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

    let getLinesFromAnnotations (annotatable : IAnnotatable) annotations =
        let annotationsToRemove = ResizeArray<IAnnotation>()
        let lines = ResizeArray<string>()
        
        annotations
        |> Seq.iter (fun a ->

            if isHandledByConvention annotatable a then
                annotationsToRemove.Add a
            else
                let methodCall = generateFluentApi annotatable a

                let line =
                    if isNull methodCall then
                        generateFluentApiWithLanguage annotatable a |> code.Fragment
                    else
                        code.Fragment methodCall

                if not (isNull line) then
                    lines.Add line
                    annotationsToRemove.Add a
        )

        annotations |> Seq.except annotationsToRemove |> generateAnnotations |> lines.AddRange
        lines |> Seq.toList
            
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

                lines.AddRange(getLinesFromAnnotations key annotations)

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

        let linesToAdd = getLinesFromAnnotations index annotations
        lines.AddRange linesToAdd

        appendMultiLineFluentApi index.DeclaringEntityType lines sb

    let generateProperty (property : IProperty) useDataAnnotations sb =

        let lines = ResizeArray<string>()
        lines.Add(sprintf ".Property(fun e -> e.%s)" property.Name)

        let annotations =
            property.GetAnnotations()
            |> removeAnnotation RelationalAnnotationNames.ColumnName
            |> removeAnnotation RelationalAnnotationNames.ColumnType
            |> removeAnnotation CoreAnnotationNames.MaxLength
            |> removeAnnotation CoreAnnotationNames.TypeMapping
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

        if property.IsFixedLength() then
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

        let generatedLines = getLinesFromAnnotations property annotations
        lines.AddRange generatedLines

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


        let annotationsToRemove = ResizeArray<IAnnotation>()
        
        annotations
        |> Seq.iter (fun a ->

            if isHandledByConvention fk a then
                annotationsToRemove.Add a
            else
                let methodCall = generateFluentApi fk a

                let line =
                    if isNull methodCall then
                        generateFluentApiWithLanguage fk a
                    else
                         methodCall

                if not (isNull line) then
                    canUseDataAnnotations <- false
                    lines.Add (line |> code.Fragment)
                    annotationsToRemove.Add a
        )
        annotations |> Seq.except annotationsToRemove |> generateAnnotations |> lines.AddRange

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


        let lines = getLinesFromAnnotations entityType annotations
        
        sb |> appendMultiLineFluentApi entityType lines |> ignore

        entityType.GetIndexes() |> Seq.iter(fun i -> generateIndex i sb)
        entityType.GetProperties() |> Seq.iter(fun p -> generateProperty p useDataAnnotations sb)
        entityType.GetForeignKeys() |> Seq.iter(fun fk -> generateRelationship fk useDataAnnotations sb)
        
        sb


    let generateOnModelCreating (model:IModel) (useDataAnnotations:bool) (sb:IndentedStringBuilder) =
        sb.AppendLine("override this.OnModelCreating(modelBuilder: ModelBuilder) =")
            |> appendLineIndent "base.OnModelCreating(modelBuilder)"
            |> ignore

        let annotations =
            model.GetAnnotations()
            |> removeAnnotation CoreAnnotationNames.ProductVersion
            |> removeAnnotation CoreAnnotationNames.ChangeTrackingStrategy
            |> removeAnnotation CoreAnnotationNames.OwnedTypes
            |> removeAnnotation ChangeDetector.SkipDetectChangesAnnotation
            |> removeAnnotation RelationalAnnotationNames.MaxIdentifierLength
            |> removeAnnotation RelationalAnnotationNames.CheckConstraints
            |> removeAnnotation ScaffoldingAnnotationNames.DatabaseName
            |> removeAnnotation ScaffoldingAnnotationNames.EntityTypeErrors
            |> Seq.toList

        let annotationsToRemove =
            annotations
            |> Seq.filter(fun a -> a.Name.StartsWith(RelationalAnnotationNames.SequencePrefix, StringComparison.Ordinal))

        let checkedAnnotations =
            annotations
            |> Seq.map(fun a -> a |> checkAnnotation model)

        let moreAnnotaionsToRemove =
            checkedAnnotations
            |> Seq.map(fun (a, _) -> a)
            |> Seq.choose id

        let toRemove = annotationsToRemove |> Seq.append moreAnnotaionsToRemove

        let lines =
            checkedAnnotations
            |> Seq.map (fun (_, l) -> l)
            |> Seq.choose id
            |> Seq.append ((annotations |> Seq.except toRemove) |> generateAnnotations)

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

    let generateClass model contextName connectionString useDataAnnotations sb =
        sb
            |> generateType contextName
            |> generateDbSets model
            |> generateEntityTypeErrors model
            |> generateOnConfiguring connectionString
            |> generateOnModelCreating model useDataAnnotations

    interface Microsoft.EntityFrameworkCore.Scaffolding.Internal.ICSharpDbContextGenerator with
        member this.WriteCode (model, contextName, connectionString, contextNamespace, modelNamespace, useDataAnnotations, suppressConnectionStringWarning) =

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
            |> generateClass model contextName connectionString useDataAnnotations
            |> ignore

            sb.ToString()
