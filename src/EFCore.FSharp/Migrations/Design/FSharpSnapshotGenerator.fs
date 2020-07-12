namespace EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Internal

open EntityFrameworkCore.FSharp.SharedTypeExtensions
open EntityFrameworkCore.FSharp.EntityFrameworkExtensions
open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Storage.ValueConversion
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.EntityFrameworkCore.ChangeTracking.Internal

type FSharpSnapshotGenerator (code : ICSharpHelper, mappingSource : IRelationalTypeMappingSource) =

    let getAnnotations (o:IAnnotatable) =
        ResizeArray (o.GetAnnotations())

    let appendLineIfTrue truth value b =
        if truth then
            b |> appendEmptyLine |> append value
        else
            b

    let appendIfTrue truth value b =
        if truth then
            b |> append value
        else 
            b

    let findValueConverter (p:IProperty) =
        let mapping = findMapping p

        if isNull mapping then p.GetValueConverter() else mapping.Converter

    let generateFluentApiForAnnotation (annotations: IAnnotation ResizeArray) (annotationName:string) (annotationValueFunc: (IAnnotation -> obj) option) (fluentApiMethodName:string) (genericTypesFunc: (IAnnotation -> IReadOnlyList<Type>)option) (sb:IndentedStringBuilder) =

        let annotationValueFunc' =
            match annotationValueFunc with
            | Some a -> a
            | None -> (fun a -> if isNull a then null else a.Value)

        let annotation = annotations |> Seq.tryFind (fun a -> a.Name = annotationName)
        let annotationValue = annotation |> Option.map annotationValueFunc'

        let genericTypesFunc' =
            match genericTypesFunc with
            | Some a -> a
            | None -> (fun _ -> List<Type>() :> _)

        let genericTypes = annotation |> Option.map genericTypesFunc'
        let hasGenericTypes =
            match genericTypes with
            | Some gt -> ((gt |> Seq.isEmpty |> not) && (gt |> Seq.forall(isNull >> not)))
            | None -> false

        if (annotationValue.IsSome && (annotationValue.Value |> isNull |> not)) || hasGenericTypes then
            sb
            |> appendEmptyLine
            |> append "."
            |> append fluentApiMethodName
            |> ignore

            if hasGenericTypes then
                sb
                    |> append "<"
                    |> append (String.Join(",", (genericTypes.Value |> Seq.map(code.Reference))))
                    |> append ">"
                    |> ignore

            sb
                |> append "("
                |> ignore

            if annotationValue.IsSome && annotationValue.Value |> notNull then
                sb |> append (annotationValue.Value |> code.UnknownLiteral) |> ignore

            sb
                |> append ")"
                |> ignore

            annotation.Value |> annotations.Remove |> ignore
            
        sb

    let sort (entityTypes:IEntityType list) =
        let entityTypeGraph = new Multigraph<IEntityType, int>()
        entityTypeGraph.AddVertices(entityTypes)

        entityTypes
            |> Seq.filter(fun e -> e.BaseType |> isNull |> not)
            |> Seq.iter(fun e -> entityTypeGraph.AddEdge(e.BaseType, e, 0))
        entityTypeGraph.TopologicalSort() |> Seq.toList

    let ignoreAnnotationTypes (annotations:IAnnotation ResizeArray) (annotation:string) (sb:IndentedStringBuilder) =

        let annotationsToRemove =
            annotations |> Seq.filter (fun a -> a.Name.StartsWith(annotation, StringComparison.OrdinalIgnoreCase)) |> Seq.toList

        annotationsToRemove |> Seq.iter (annotations.Remove >> ignore)

        sb

    let generateAnnotation (annotation:IAnnotation) (sb:IndentedStringBuilder) =
        let name = annotation.Name |> code.Literal
        let value = annotation.Value |> code.UnknownLiteral

        sb
            |> append (sprintf ".HasAnnotation(%s, %s)" name value)
            |> appendEmptyLine
            |> ignore

    let generateAnnotations (annotations:IAnnotation ResizeArray) (sb:IndentedStringBuilder) =

        annotations
        |> Seq.iter(fun a ->
            sb
                |> generateAnnotation a)

        sb

    let getMappingHints (hints : ConverterMappingHints) =
        seq {
            if hints.Size.HasValue then
                yield (sprintf "size = Nullable(%s)" (hints.Size.Value |> code.Literal))               
            if hints.Precision.HasValue then
                yield (sprintf "precision = Nullable(%s)" (hints.Precision.Value |> code.Literal))
            if hints.Scale.HasValue then
                yield (sprintf "scale = Nullable(%s)" (hints.Scale.Value |> code.Literal))
            if hints.IsUnicode.HasValue then
                yield (sprintf "unicode = Nullable(%s)" (hints.IsUnicode.Value |> code.Literal))

            if hints :? RelationalConverterMappingHints then
                let relationalHints = hints :?> RelationalConverterMappingHints
                if relationalHints.IsFixedLength.HasValue then
                    yield (sprintf "fixedLength = Nullable(%s)" (relationalHints.IsFixedLength.Value |> code.Literal))
        }

    let genPropertyAnnotations (p:IProperty) (sb:IndentedStringBuilder) =
        let annotations =  getAnnotations p
        let valueConverter = p |> findValueConverter

        if valueConverter |> notNull && valueConverter.MappingHints |> notNull then
            let hints = valueConverter.MappingHints
            let storeType = valueConverter.ProviderClrType |> code.Reference
            let createStoreType = sprintf "(fun v -> Unchecked.defaultof<%s>)" storeType

            let mappingHints = hints |> getMappingHints |> join ", "

            sb
                |> appendEmptyLine
                |> append (sprintf ".HasConversion(ValueConverter<%s,%s>(%s, %s), ConverterMappingHints(%s))" storeType storeType createStoreType createStoreType mappingHints)
                |> ignore
        
        let consumed = 
            annotations 
            |> Seq.filter(fun a -> a.Name = CoreAnnotationNames.ValueConverter || a.Name = CoreAnnotationNames.ProviderClrType) 
            |> Seq.toList

        consumed 
        |> Seq.iter (annotations.Remove >> ignore)

        let getValueFunc (valueConverter: ValueConverter) (a:IAnnotation) =
            if valueConverter |> notNull then
                valueConverter.ConvertToProvider.Invoke(a.Value)
            else
                a.Value

        
        let columnType (p: IProperty) =
            if p.GetColumnType() |> isNull then
                mappingSource.GetMapping(p).StoreType |> code.Literal
            else 
                p.GetColumnType() |> code.Literal

        sb
            |> generateFluentApiForAnnotation annotations RelationalAnnotationNames.ColumnName None "HasColumnName" None            
            |> ignore

        sb
        |> appendEmptyLine
        |> append "."
        |> append "HasColumnType"
        |> append "("
        |> append (columnType p)
        |> append ")"
        |> ignore

        sb
            |> generateFluentApiForAnnotation annotations RelationalAnnotationNames.DefaultValueSql None "HasDefaultValueSql" None
            |> generateFluentApiForAnnotation annotations RelationalAnnotationNames.ComputedColumnSql None "HasComputedColumnSql" None
            |> generateFluentApiForAnnotation annotations RelationalAnnotationNames.IsFixedLength None "IsFixedLength" None
            |> generateFluentApiForAnnotation annotations RelationalAnnotationNames.Comment None "HasComment" None
            |> generateFluentApiForAnnotation annotations CoreAnnotationNames.MaxLength None "HasMaxLength" None
            |> generateFluentApiForAnnotation annotations CoreAnnotationNames.Unicode None "IsUnicode" None

            |> generateFluentApiForAnnotation annotations RelationalAnnotationNames.DefaultValue (getValueFunc valueConverter |> Some) "HasDefaultValue" None
            
            |> ignoreAnnotationTypes annotations RelationalAnnotationNames.ColumnType
            |> ignoreAnnotationTypes annotations CoreAnnotationNames.ValueGeneratorFactory
            |> ignoreAnnotationTypes annotations CoreAnnotationNames.PropertyAccessMode
            |> ignoreAnnotationTypes annotations CoreAnnotationNames.ChangeTrackingStrategy
            |> ignoreAnnotationTypes annotations CoreAnnotationNames.BeforeSaveBehavior
            |> ignoreAnnotationTypes annotations CoreAnnotationNames.AfterSaveBehavior
            |> ignoreAnnotationTypes annotations CoreAnnotationNames.TypeMapping
            |> ignoreAnnotationTypes annotations CoreAnnotationNames.ValueComparer
            |> ignoreAnnotationTypes annotations CoreAnnotationNames.KeyValueComparer
            |> ignoreAnnotationTypes annotations CoreAnnotationNames.StructuralValueComparer
            |> ignoreAnnotationTypes annotations CoreAnnotationNames.ValueConverter
            |> ignoreAnnotationTypes annotations CoreAnnotationNames.ProviderClrType
            
            |> generateAnnotations annotations
            |> append " |> ignore"

    let generateBaseType (funcId: string) (baseType: IEntityType) (sb:IndentedStringBuilder) =

        if (baseType |> notNull) then
            sb
                |> appendEmptyLine
                |> append (sprintf "%s.HasBaseType(%s)" funcId (baseType.Name |> code.Literal))
        else
            sb

    let generateProperty (funcId:string) (p:IProperty) (sb:IndentedStringBuilder) =

        let isPropertyRequired =
            let isNullable = 
                p.IsPrimaryKey() |> not ||
                p.ClrType |> isOptionType ||
                p.ClrType |> isNullableType

            isNullable <> p.IsNullable

        let converter = findValueConverter p
        let clrType =
            if isNull converter then p.ClrType else converter.ProviderClrType

        sb
            |> appendEmptyLine
            |> append funcId
            |> append ".Property<"
            |> append (clrType |> code.Reference)
            |> append ">("
            |> append (p.Name |> code.Literal)
            |> append ")"
            |> indent
            |> appendLineIfTrue p.IsConcurrencyToken ".IsConcurrencyToken()"
            |> appendLineIfTrue isPropertyRequired ".IsRequired()"
            |> appendLineIfTrue (p.ValueGenerated <> ValueGenerated.Never) (if p.ValueGenerated = ValueGenerated.OnAdd then ".ValueGeneratedOnAdd()" else if p.ValueGenerated = ValueGenerated.OnUpdate then ".ValueGeneratedOnUpdate()" else ".ValueGeneratedOnAddOrUpdate()" )
            |> genPropertyAnnotations p
            |> unindent

    let generateProperties (funcId: string) (properties: IProperty seq) (sb:IndentedStringBuilder) =
        properties |> Seq.iter (fun p -> generateProperty funcId p sb |> ignore)
        sb

    let generateKey (funcId: string) (key:IKey) (isPrimary:bool) (sb:IndentedStringBuilder) =

        let annotations = getAnnotations key

        sb
            |> appendEmptyLine
            |> appendEmptyLine
            |> append funcId
            |> append (if isPrimary then ".HasKey(" else ".HasAlternateKey(")
            |> append (key.Properties |> Seq.map (fun p -> (p.Name |> code.Literal)) |> join ", ")
            |> append ")"
            |> indent
            |> generateFluentApiForAnnotation annotations RelationalAnnotationNames.Name Option.None "HasName" Option.None
            |> generateAnnotations annotations
            |> appendLine " |> ignore"
            |> unindent

    let generateKeys (funcId: string) (keys: IKey seq) (pk:IKey) (sb:IndentedStringBuilder) =

        if pk |> notNull then
            generateKey funcId pk true sb |> ignore

        keys
            |> Seq.filter(fun k -> k <> pk && (k.GetReferencingForeignKeys() |> Seq.isEmpty) && (getAnnotations k) |> Seq.isEmpty |> not)
            |> Seq.iter (fun k -> generateKey funcId k false sb |> ignore)

        sb        

    let generateIndex (funcId: string) (idx:IIndex) (sb:IndentedStringBuilder) =

        let annotations = getAnnotations idx

        sb
            |> appendEmptyLine
            |> append funcId
            |> append ".HasIndex("
            |> append (String.Join(", ", (idx.Properties |> Seq.map (fun p -> p.Name |> code.Literal))))
            |> append ")"
            |> indent
            |> appendLineIfTrue idx.IsUnique ".IsUnique()"
            |> generateFluentApiForAnnotation annotations RelationalAnnotationNames.Name Option.None "HasName" Option.None
            |> generateFluentApiForAnnotation annotations RelationalAnnotationNames.Filter Option.None "HasFilter" Option.None
            |> generateAnnotations annotations
            |> appendLine " |> ignore"
            |> unindent
            |> ignore

    let generateIndexes (funcId: string) (indexes:IIndex seq) (sb:IndentedStringBuilder) =

        indexes |> Seq.iter (fun idx -> sb |> appendEmptyLine |> generateIndex funcId idx)
        sb

    let processDataItem (props : IProperty list) (sb:IndentedStringBuilder) (data : IDictionary<string, obj>) =
        
        let writeProperty isFirst (p : IProperty) =
            match data.TryGetValue p.Name with
            | true, value ->
                if not (isNull value) then
                    if isFirst then
                        sb |> appendLine "," |> ignore

                    sb
                    |> append (sprintf "%s = %s" (code.Identifier(p.Name)) (code.UnknownLiteral value) )
                    |> ignore
                else
                    ()
            | _ -> ()

        
        sb |> appendLine "({" |> indent |> ignore

        props |> Seq.head |> writeProperty true
        props |> Seq.tail |> Seq.iter(fun p -> writeProperty false p)

        sb |> appendLine "} :> obj)" |> unindent |> ignore
        ()

    let processDataItems (data : IDictionary<string, obj> seq) (propsToOutput : IProperty list) (sb:IndentedStringBuilder) =
        
        (data |> Seq.head) |> processDataItem propsToOutput sb

        data
        |> Seq.tail
        |> Seq.iter(fun d ->
                sb |> appendLine " |> ignore" |> ignore
                d |> processDataItem propsToOutput sb
            )
        sb

    member this.generatePropertyAnnotations = 
        genPropertyAnnotations

    member this.generateEntityTypeAnnotations (funcId: string) (entityType:IEntityType) (sb:IndentedStringBuilder) =
        let annotations = getAnnotations entityType

        let tryGetAnnotationByName (name:string) =
            let a = annotations |> Seq.tryFind (fun a -> a.Name = name)

            match a with
            | Some a' -> annotations.Remove(a') |> ignore
            | None -> ()

            a

        let tableNameAnnotation = tryGetAnnotationByName RelationalAnnotationNames.TableName
        let schemaAnnotation = tryGetAnnotationByName RelationalAnnotationNames.Schema

        let tableName =
            match tableNameAnnotation with
            | Some t -> t.Value |> string
            | None -> getDisplayName entityType

        let hasSchema, schema =
            match schemaAnnotation with
            | Some s -> true, s.Value
            | None -> false, null

        sb
            |> appendEmptyLine
            |> append funcId
            |> append ".ToTable("
            |> append (tableName |> code.Literal)
            |> appendIfTrue hasSchema (sprintf ",%A" schema)
            |> append ") |> ignore"
            |> appendEmptyLine
            |> ignore

        let discriminatorPropertyAnnotation = 
            tryGetAnnotationByName CoreAnnotationNames.DiscriminatorProperty
        let discriminatorValueAnnotation = 
            tryGetAnnotationByName CoreAnnotationNames.DiscriminatorValue

        if discriminatorPropertyAnnotation.IsSome || discriminatorValueAnnotation.IsSome then
            sb
                |> appendEmptyLine
                |> append funcId
                |> append ".HasDiscriminator"
                |> ignore

            match discriminatorPropertyAnnotation with
            | Some a when notNull a.Value  ->
                let discriminatorProperty = entityType.FindProperty(string a.Value)

                let propertyClrType = 
                    let valueConverter = findValueConverter discriminatorProperty
                    if notNull valueConverter then
                        makeNullable discriminatorProperty.IsNullable valueConverter.ProviderClrType
                    else
                        discriminatorProperty.ClrType

                sb
                    |> append "<"
                    |> append (code.Reference propertyClrType)
                    |> append ">("
                    |> append (code.UnknownLiteral a.Value)
                    |> append ")"
                    |> ignore
            | _ ->
                sb |> append "()" |> ignore
            

            match discriminatorValueAnnotation with
            | Some a ->
                let discriminatorProperty = 
                    entityType.GetRootType().GetDiscriminatorProperty() 

                let value =
                    if discriminatorProperty |> notNull then
                        let valueConverter = discriminatorProperty |> findValueConverter
                        if valueConverter |> notNull then
                            valueConverter.ConvertToProvider.Invoke(a.Value)
                        else
                            a.Value
                    else
                        a.Value

                sb
                    |> append (sprintf ".HasValue(%s) |> ignore" (value |> code.UnknownLiteral))
                    |> appendEmptyLine 
                    |> ignore
            | None -> 
                sb 
                |> append " |> ignore" 
                |> appendEmptyLine 
                |> ignore

        let commentAnnotation = tryGetAnnotationByName RelationalAnnotationNames.Comment

        match commentAnnotation with
        | Some c -> 
            sb
            |> appendEmptyLine
            |> append funcId
            |> append "."
            |> append "HasComment"
            |> append "("
            |> append (c.Value |> code.UnknownLiteral)
            |> appendLine ") |> ignore"
            |> ignore
        | None -> ()

        let annotationsToRemove =
            [|
                CoreAnnotationNames.NavigationCandidates
                CoreAnnotationNames.AmbiguousNavigations
                CoreAnnotationNames.InverseNavigations
                CoreAnnotationNames.NavigationAccessMode
                CoreAnnotationNames.PropertyAccessMode
                CoreAnnotationNames.ChangeTrackingStrategy
                CoreAnnotationNames.ConstructorBinding
                CoreAnnotationNames.DefiningQuery
                CoreAnnotationNames.QueryFilter
                RelationalAnnotationNames.CheckConstraints |]

        annotationsToRemove |> Seq.iter (tryGetAnnotationByName >> ignore)

        annotations
            |> Seq.iter(fun a ->
                sb
                    |> appendEmptyLine
                    |> append funcId
                    |> generateAnnotation a)

        sb

    member private this.generateForeignKeyAnnotations (fk: IForeignKey) sb =

        let annotations = getAnnotations fk

        sb
            |> generateFluentApiForAnnotation annotations RelationalAnnotationNames.Name Option.None "HasConstraintName" Option.None
            |> generateAnnotations annotations       

    member private this.generateForeignKey funcId (fk: IForeignKey) sb =

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
            |> append funcId
            |> append ".HasOne("
            |> append (fk.PrincipalEntityType.Name |> code.Literal)
            |> append ","
            |> append dependent
            |> ignore
        else
            sb
            |> append funcId
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
            sb
            |> append ".WithOne("
            |> ignore

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

            this.generateForeignKeyAnnotations fk sb |> ignore

            if fk.PrincipalKey <> fk.PrincipalEntityType.FindPrimaryKey() then
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
                sb
                |> append ".WithMany("
                |> ignore

                if notNull fk.PrincipalToDependent then
                    sb
                    |> append (fk.PrincipalToDependent.Name |> code.Literal)
                    |> ignore

                sb
                |> append ")"
                |> ignore

            sb
            |> appendEmptyLine
            |> append ".HasForeignKey("
            |> append (String.Join(", ", (literalPropNames fk.Properties)))
            |> append ")"
            |> ignore

            this.generateForeignKeyAnnotations fk sb |> ignore

            if fk.PrincipalKey <> fk.PrincipalEntityType.FindPrimaryKey() then
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
        |> appendLine " |> ignore"
        |> unindent

    member private this.generateForeignKeys funcId (foreignKeys: IForeignKey seq) sb =
        foreignKeys 
        |> Seq.iter (fun fk -> this.generateForeignKey funcId fk sb |> ignore)
        sb

    member private this.generateOwnedType funcId (ownership: IForeignKey) (sb:IndentedStringBuilder) =
        this.generateEntityType funcId ownership.DeclaringEntityType sb

    member private this.generateOwnedTypes funcId (ownerships: IForeignKey seq) (sb:IndentedStringBuilder) =
        ownerships |> Seq.iter (fun o -> this.generateOwnedType funcId o sb)
        sb

    member private this.generateRelationships (funcId: string) (entityType:IEntityType) (sb:IndentedStringBuilder) =
        sb
            |> this.generateForeignKeys funcId (getDeclaredForeignKeys entityType)
            |> this.generateOwnedTypes funcId (entityType |> getDeclaredReferencingForeignKeys |> Seq.filter(fun fk -> fk.IsOwnership))

    member private this.generateData builderName (properties: IProperty seq) (data : IDictionary<string, obj> seq) (sb:IndentedStringBuilder) =
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
            |> appendLine " |])"

    member private this.generateEntityType (builderName:string) (entityType: IEntityType) (sb:IndentedStringBuilder) =

        let ownership = findOwnership entityType

        let ownerNav =
            if isNull ownership then
                None
            else
                Some ownership.PrincipalToDependent.Name

        let declaration =
            match ownerNav with
            | None -> 
                sprintf ".Entity(%s" (entityType.Name |> code.Literal)
            | Some o -> 
                if ownership.IsUnique then                    
                    sprintf ".OwnsOne(%s, %s" (entityType.Name |> code.Literal) (o |> code.Literal)
                else
                    sprintf ".OwnsMany(%s, %s" (entityType.Name |> code.Literal) (o |> code.Literal)

        let funcId = "b"

        sb
            |> appendEmptyLine
            |> append builderName
            |> append declaration
            |> append ", (fun " |> append funcId |> appendLine " ->"
            |> indent
            |> generateBaseType funcId entityType.BaseType
            |> generateProperties funcId (entityType |> getDeclaredProperties)
            |>
                match ownerNav with
                | Some _ -> id
                | None -> generateKeys funcId (entityType |> getDeclaredKeys) (entityType |> findDeclaredPrimaryKey)
            |> generateIndexes funcId (entityType |> getDeclaredIndexes)
            |> this.generateEntityTypeAnnotations funcId entityType
            |>
                match ownerNav with
                | None -> id
                | Some _ -> this.generateRelationships funcId entityType
            |> this.generateData funcId (entityType.GetProperties()) (entityType |> getData true)
            |> unindent
            |> appendEmptyLine
            |> appendLine ")) |> ignore"
            |> ignore

    member private this.generateEntityTypeRelationships builderName (entityType: IEntityType) (sb:IndentedStringBuilder) =

        sb
            |> appendEmptyLine
            |> append builderName
            |> append ".Entity("
            |> append (entityType.Name |> code.Literal)
            |> appendLine(", (fun b ->")
            |> indent
            |> this.generateRelationships "b" entityType
            |> unindent
            |> appendLine ")) |> ignore"
            |> ignore


    member private this.generateEntityTypes builderName (entities: IEntityType list) (sb:IndentedStringBuilder) =

        let entitiesToWrite =
            entities |> Seq.filter (fun e -> (e.HasDefiningNavigation() |> not) && (e |> findOwnership |> isNull))

        entitiesToWrite
            |> Seq.iter(fun e -> this.generateEntityType builderName e sb)

        let relationships =
            entitiesToWrite
            |> Seq.filter(fun e -> (e |> getDeclaredForeignKeys |> Seq.isEmpty |> not) || (e |> getDeclaredReferencingForeignKeys |> Seq.exists(fun fk -> fk.IsOwnership)))

        relationships |> Seq.iter(fun e -> this.generateEntityTypeRelationships builderName e sb)

        sb

    member private this.generate (builderName:string) (model:IModel) (sb:IndentedStringBuilder) =

        let annotations = getAnnotations model

        if annotations |> Seq.isEmpty |> not then
            sb
                |> append builderName
                |> indent
                |> generateFluentApiForAnnotation annotations RelationalAnnotationNames.DefaultSchema Option.None "HasDefaultSchema" Option.None
                |> ignoreAnnotationTypes annotations RelationalAnnotationNames.DbFunction
                |> ignoreAnnotationTypes annotations ChangeDetector.SkipDetectChangesAnnotation
                |> ignoreAnnotationTypes annotations CoreAnnotationNames.ChangeTrackingStrategy
                |> ignoreAnnotationTypes annotations CoreAnnotationNames.OwnedTypes
                |> ignoreAnnotationTypes annotations RelationalAnnotationNames.CheckConstraints
                |> generateAnnotations annotations
                |> appendLine " |> ignore"
                |> unindent
                |> ignore

        let sortedEntities = 
            model.GetEntityTypes() 
            |> Seq.filter(fun et -> not (et.IsIgnoredByMigrations())) 
            |> Seq.toList 
            |> sort

        sb |> this.generateEntityTypes builderName sortedEntities |> ignore

    interface Microsoft.EntityFrameworkCore.Migrations.Design.ICSharpSnapshotGenerator with
        member this.Generate(builderName, model, stringBuilder) =
            this.generate builderName model stringBuilder
