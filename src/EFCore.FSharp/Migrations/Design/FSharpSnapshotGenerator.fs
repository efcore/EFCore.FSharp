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

type FSharpSnapshotGenerator (code : ICSharpHelper,
                              mappingSource : IRelationalTypeMappingSource,
                              annotationCodeGenerator: IAnnotationCodeGenerator) =

    let getAnnotations (o: IAnnotatable) =
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
        let pValueConverter = p.GetValueConverter()
        if p.GetValueConverter() |> isNull then
            let typeMapping = p.FindTypeMapping()

            if typeMapping |> isNull then
                let mapping = mappingSource.FindMapping(p)
                if mapping |> isNull then
                    None
                elif mapping.Converter |> isNull then
                    None
                else mapping.Converter |> Some
            elif typeMapping.Converter |> isNull then None
            else typeMapping.Converter |> Some
        else pValueConverter |> Some

    let sort (entityTypes:IEntityType seq) =
        let entityTypeGraph = Multigraph()
        entityTypeGraph.AddVertices(entityTypes)
        entityTypes
        |> Seq.filter(fun e -> e.BaseType |> isNull |> not)
        |> Seq.iter (fun e -> entityTypeGraph.AddEdge(e.BaseType, e, 0))

        entityTypeGraph.TopologicalSort() |> Seq.toList

    let generateAnnotation (annotation:IAnnotation) (sb:IndentedStringBuilder) =
        let name = annotation.Name |> code.Literal
        let value = annotation.Value |> code.UnknownLiteral

        sb
            |> append (sprintf ".HasAnnotation(%s, %s)" name value)
            |> appendEmptyLine
            |> ignore

    let generateAnnotations (annotations: IAnnotation seq) (sb:IndentedStringBuilder) =

        annotations
        |> Seq.iter(fun a -> sb |> generateAnnotation a)

        sb

    let generateFluentApiForDefaultValue (property: IProperty) (sb: IndentedStringBuilder) =
        match property.GetDefaultValue() |> Option.ofObj with
        | Some defaultValue ->
            let valueConverter =
                if defaultValue <> (box DBNull.Value) then
                    let valueConverter = property.GetValueConverter() |> Option.ofObj
                    let typeMap =
                        if property.FindTypeMapping() |> Option.ofObj |> Option.isSome then
                            property.FindTypeMapping() |> Option.ofObj
                        else (mappingSource.FindMapping(property) :> CoreTypeMapping) |> Option.ofObj

                    match valueConverter, typeMap with
                    | Some v, _ -> v |> Option.ofObj
                    | None, Some t -> t.Converter |> Option.ofObj
                    | _ -> None
                else None

            let appendValueConverter sb =
                let value =
                    match valueConverter with
                    | Some vc -> vc.ConvertToProvider.Invoke(defaultValue)
                    | None -> defaultValue

                sb
                |> append (code.UnknownLiteral(value))

            sb
                |> appendEmptyLine
                |> append ".HasDefaultValue("
                |> appendValueConverter
                |> append ")"
        | None -> sb

    let genPropertyAnnotations (p:IProperty) (sb:IndentedStringBuilder) =
        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(p.GetAnnotations())
            |> annotationsToDictionary

        let columnType (p: IProperty) =
            if p.GetColumnType() |> isNull then
                mappingSource.GetMapping(p).StoreType |> code.Literal
            else
                p.GetColumnType() |> code.Literal

        let removeAnnotation (annotation: string) sb =
            annotations.Remove(annotation) |> ignore
            sb

        let generateFluentApiForMaxLength sb =
            match p.GetMaxLength() |> Option.ofNullable with
            | Some i ->
                sb
                    |> appendEmptyLine
                    |> append ".HasMaxLength("
                    |> append (code.Literal(i))
                    |> append ")"
            | None -> sb

        let generateFluentApiForPrecisionAndScale (sb: IndentedStringBuilder) =
            match p.GetPrecision() |> Option.ofNullable with
            | Some i ->
                sb
                |> appendEmptyLine
                |> append $".HasPrecision({code.UnknownLiteral(i)}"
                |> ignore

                if p.GetScale().HasValue then
                    sb
                    |> append $", {code.UnknownLiteral(p.GetScale().Value)}"
                    |> ignore

                annotations.Remove(CoreAnnotationNames.Precision) |> ignore

                sb
                |> append ")"
            | None -> sb

        let generateFluentApiForUnicode sb =
            match p.IsUnicode() |> Option.ofNullable with
            | Some b ->
                sb
                    |> appendEmptyLine
                    |> append ".IsUnicode("
                    |> append (code.Literal(b))
                    |> append ")"
            | None -> sb

        sb
            |> generateFluentApiForMaxLength
            |> generateFluentApiForPrecisionAndScale
            |> generateFluentApiForUnicode
            |> appendEmptyLine
            |> append "."
            |> append "HasColumnType"
            |> append "("
            |> append (columnType p)
            |> append ")"
            |> removeAnnotation RelationalAnnotationNames.ColumnType
            |> generateFluentApiForDefaultValue p
            |> removeAnnotation RelationalAnnotationNames.DefaultValue
            |> ignore

        annotationCodeGenerator.GenerateFluentApiCalls(p, annotations)
        |> Seq.map code.Fragment
        |> Seq.iter (fun m ->
            sb
                |> appendEmptyLine
                |> append m
                |> ignore)

        sb
            |> generateAnnotations annotations.Values
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
                not (p.IsPrimaryKey()) ||
                isOptionType p.ClrType ||
                isNullableType p.ClrType

            isNullable <> p.IsNullable

        let converter = findValueConverter p
        let clrType =
            match converter with
            | Some c -> c.ProviderClrType
            | None -> p.ClrType

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
            |> appendLineIfTrue (p.ValueGenerated <> ValueGenerated.Never)
                   (if p.ValueGenerated = ValueGenerated.OnAdd then ".ValueGeneratedOnAdd()"
                    else if p.ValueGenerated = ValueGenerated.OnUpdate then ".ValueGeneratedOnUpdate()"
                    else ".ValueGeneratedOnAddOrUpdate()" )
            |> genPropertyAnnotations p
            |> unindent

    let generateProperties (funcId: string) (properties: IProperty seq) (sb:IndentedStringBuilder) =
        properties |> Seq.iter (fun p -> generateProperty funcId p sb |> ignore)
        sb

    let generateKey (funcId: string) (key:IKey) (isPrimary:bool) (sb:IndentedStringBuilder) =

        let generateKeyAnnotations sb =
            let annotations =
                annotationCodeGenerator.FilterIgnoredAnnotations(key.GetAnnotations())
                |> annotationsToDictionary

            annotationCodeGenerator.GenerateFluentApiCalls(key, annotations)
            |> Seq.map code.Fragment
            |> Seq.iter (fun m ->
                sb
                    |> appendEmptyLine
                    |> append m
                    |> ignore)

            generateAnnotations annotations.Values sb

        sb
            |> appendEmptyLine
            |> appendEmptyLine
            |> append funcId
            |> append (if isPrimary then ".HasKey(" else ".HasAlternateKey(")
            |> append (key.Properties |> Seq.map (fun p -> (p.Name |> code.Literal)) |> join ", ")
            |> append ")"
            |> indent
            |> generateKeyAnnotations
            |> unindent
            |> appendLine " |> ignore"

    let generateKeys (funcId: string) (keys: IKey seq) (pk: IKey) (sb: IndentedStringBuilder) =

        if pk |> notNull then
            generateKey funcId pk true sb |> ignore

        keys
            |> Seq.filter(fun k -> k <> pk && (k.GetReferencingForeignKeys() |> Seq.isEmpty) && (getAnnotations k) |> Seq.isEmpty |> not)
            |> Seq.iter (fun k -> generateKey funcId k false sb |> ignore)

        sb

    let generateIndex (funcId: string) (idx:IIndex) (sb:IndentedStringBuilder) =

        let generateIndexAnnotations sb =
            let annotations =
                annotationCodeGenerator.FilterIgnoredAnnotations(idx.GetAnnotations())
                |> annotationsToDictionary

            annotationCodeGenerator.GenerateFluentApiCalls(idx, annotations)
            |> Seq.map code.Fragment
            |> Seq.iter (fun m ->
                sb
                    |> appendEmptyLine
                    |> append m
                    |> ignore)

            generateAnnotations annotations.Values sb

        sb
            |> appendEmptyLine
            |> append funcId
            |> append ".HasIndex("
            |> append (String.Join(", ", (idx.Properties |> Seq.map (fun p -> p.Name |> code.Literal))))
            |> append ")"
            |> indent
            |> appendLineIfTrue idx.IsUnique ".IsUnique()"
            |> generateIndexAnnotations
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
           && sequence.Model.GetDefaultSchema() <> sequence.Schema then
            sb
                |> append ", "
                |> append (code.Literal(sequence.Schema))
                |> ignore

        let appendStartValue sb =
            if sequence.StartValue <> (Sequence.DefaultStartValue |> int64) then
                sb
                    |> appendEmptyLine
                    |> append ".StartsAt("
                    |> append (code.Literal(sequence.StartValue))
                    |> append ")"
                    |> ignore
            sb

        let appendIncrementBy sb =
            if sequence.IncrementBy <> Sequence.DefaultIncrementBy then
                sb
                    |> appendEmptyLine
                    |> append ".IncrementsBy("
                    |> append (code.Literal(sequence.IncrementBy))
                    |> append ")"
                    |> ignore
            sb

        let appendMinValue sb =
            if sequence.MinValue <> Sequence.DefaultMinValue then
                sb
                    |> appendEmptyLine
                    |> append ".HasMin("
                    |> append (code.Literal(sequence.MinValue))
                    |> append ")"
                    |> ignore
            sb

        let appendMaxValue sb =
            if sequence.MaxValue <> Sequence.DefaultMaxValue then
                sb
                    |> appendEmptyLine
                    |> append ".HasMax("
                    |> append (code.Literal(sequence.MaxValue))
                    |> append ")"
                    |> ignore
            sb

        let appendIsCyclic sb =
            if sequence.IsCyclic <> Sequence.DefaultIsCyclic then
                sb
                    |> appendEmptyLine
                    |> append ".IsCyclic()"
                    |> ignore
            sb

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

    let removeAmbiguousAnnotations (annotations: Dictionary<string, IAnnotation>) annotationNameMatcher =
        annotations
        |> Seq.filter (fun kvp ->
            match annotationNameMatcher kvp.Key with
            | true ->
                annotations.Remove(kvp.Key) |> ignore
                true
            | false -> false)
        |> Seq.map (fun kvp -> kvp.Value)



    member this.generatePropertyAnnotations =
        genPropertyAnnotations

    member this.generateCheckConstraints (builderName: string) (entityType:IEntityType) (sb:IndentedStringBuilder) =
        let generateCheckConstraint (c: ICheckConstraint) sb =
            let name = code.Literal c.Name
            let sql = code.Literal c.Sql

            sb |> append (sprintf "%s.HasCheckConstraint(%s, %s) |> ignore" builderName name sql)

        entityType.GetCheckConstraints()
        |> Seq.iter (fun c ->
            sb
                |> appendEmptyLine
                |> generateCheckConstraint c
                |> ignore)

        sb

    member this.generateEntityTypeAnnotations (builderName: string) (entityType:IEntityType) (sb:IndentedStringBuilder) =
        let annotationList = entityType.GetAnnotations() |> ResizeArray

        let annotations =
            annotationCodeGenerator
                .FilterIgnoredAnnotations(entityType.GetAnnotations())
                |> annotationsToDictionary

        let tryGetAnnotationByName (name:string) =
            match annotations.TryGetValue name with
            | true, a ->
                annotations.Remove(name) |> ignore
                Some a
            | _ -> None

        let tableName =
            match tryGetAnnotationByName RelationalAnnotationNames.TableName with
            | Some t ->
                if t.Value |> isNull then None else t.Value |> string |> Some
            | None when entityType.BaseType |> isNull ->
                let tableName = entityType.GetTableName()
                if tableName |> isNull then None else tableName |> string |> Some
            | _ -> None

        let hasSchema, schema =
            match tryGetAnnotationByName RelationalAnnotationNames.Schema with
            | Some s -> true, s.Value
            | None -> false, null

        tableName
        |> Option.iter (fun tableName ->
            sb
                |> appendEmptyLine
                |> append builderName
                |> append ".ToTable("
                |> append (tableName |> code.Literal)
                |> appendIfTrue hasSchema (sprintf ",%A" schema)
                |> append ") |> ignore"
                |> appendEmptyLine
                |> ignore
            )

        let viewName =
            match tryGetAnnotationByName RelationalAnnotationNames.ViewName with
            | Some t ->
                t.Value |> Option.ofObj |> Option.map string
            | None when entityType.BaseType |> Option.ofObj |> Option.isNone ->
                entityType.GetViewName() |> Option.ofObj
            | _ -> None

        let hasViewSchema, viewSchema =
            match tryGetAnnotationByName RelationalAnnotationNames.ViewSchema with
            | Some s -> true, s.Value
            | None -> false, null

        viewName
        |> Option.iter (fun viewName ->
            sb
                |> appendEmptyLine
                |> append builderName
                |> append ".ToView("
                |> append (code.Literal(viewName))
                |> appendIfTrue hasViewSchema $", {viewSchema}"
                |> appendLine ") |> ignore"
                |> ignore
            )

        let functionName =
            match tryGetAnnotationByName RelationalAnnotationNames.FunctionName with
            | Some f ->
                f.Value |> Option.ofObj |> Option.map string
            | None when entityType.BaseType |> Option.ofObj |> Option.isNone ->
                entityType.GetFunctionName() |> Option.ofObj
            | _ -> None

        functionName
        |> Option.iter (fun functionName ->
            sb
                |> appendEmptyLine
                |> append builderName
                |> append ".ToFunction("
                |> append functionName
                |> append ") |> ignore"
                |> ignore
            )

        let discriminatorPropertyAnnotation =
            annotationList |> Seq.tryFind (fun a -> a.Name = CoreAnnotationNames.DiscriminatorProperty)

        let discriminatorMappingCompleteAnnotation =
            annotationList |> Seq.tryFind (fun a -> a.Name = CoreAnnotationNames.DiscriminatorMappingComplete)

        let discriminatorValueAnnotation =
            annotationList |> Seq.tryFind (fun a -> a.Name = CoreAnnotationNames.DiscriminatorValue)

        let annotationAndValueNotNull (anno: IAnnotation option) =
            match anno with
            | Some a when a.Value |> isNull |> not -> true
            | _ -> false

        if annotationAndValueNotNull discriminatorPropertyAnnotation
            || annotationAndValueNotNull discriminatorMappingCompleteAnnotation
            || annotationAndValueNotNull discriminatorValueAnnotation then

            sb
                |> appendEmptyLine
                |> append builderName
                |> append ".HasDiscriminator"
                |> ignore

            if annotationAndValueNotNull discriminatorPropertyAnnotation then
                let discriminatorPropertyAnnotation = discriminatorPropertyAnnotation.Value
                let discriminatorProperty =
                    entityType.FindProperty(discriminatorPropertyAnnotation.Value |> string)

                let propertyClrType =
                    match discriminatorProperty |> findValueConverter with
                    | Some c -> c.ProviderClrType |> makeNullable discriminatorProperty.IsNullable
                    | None -> discriminatorProperty.ClrType

                sb
                    |> append "<"
                    |> append (code.Reference(propertyClrType))
                    |> append ">("
                    |> append (code.Literal(discriminatorPropertyAnnotation.Value |> string))
                    |> append ")"
                    |> ignore
            else
                sb
                    |> append "()"
                    |> ignore

            if annotationAndValueNotNull discriminatorMappingCompleteAnnotation then
                let value = discriminatorMappingCompleteAnnotation.Value
                sb
                    |> append ".IsComplete("
                    |> append (code.UnknownLiteral(value))
                    |> append ")"
                    |> ignore

            if annotationAndValueNotNull discriminatorValueAnnotation then
                let discriminatorValueAnnotation = discriminatorValueAnnotation.Value
                let discriminatorProperty = entityType.GetDiscriminatorProperty()

                let defaultValue = discriminatorValueAnnotation.Value

                let value =
                    if discriminatorProperty |> isNull |> not then
                        match discriminatorProperty |> findValueConverter with
                        | Some c -> c.ConvertToProvider.Invoke(defaultValue)
                        | None -> defaultValue
                    else defaultValue

                sb
                    |> append ".HasValue("
                    |> append (code.UnknownLiteral(value))
                    |> append ")"
                    |> ignore

            sb
                |> appendLine " |> ignore"
                |> ignore

        let fluentApiCalls =
            annotationCodeGenerator.GenerateFluentApiCalls(entityType, annotations)

        if fluentApiCalls.Count > 0 || annotations.Count > 0 then
            sb
                |> appendEmptyLine
                |> append builderName
                |> indent
                |> ignore

            fluentApiCalls
            |> Seq.map code.Fragment
            |> Seq.iter (fun m ->
                sb
                    |> appendEmptyLine
                    |> append m
                    |> ignore)

            generateAnnotations annotations.Values sb
            |> appendLine " |> ignore"
            |> unindent
            |> ignore

        sb

    member private this.generateForeignKeyAnnotations (fk: IForeignKey) sb =

        let annotations =
            annotationCodeGenerator.FilterIgnoredAnnotations(fk.GetAnnotations())
            |> annotationsToDictionary

        annotationCodeGenerator.GenerateFluentApiCalls(fk, annotations)
        |> Seq.map code.Fragment
        |> Seq.iter (fun m ->
            sb
                |> appendEmptyLine
                |> append m
                |> ignore)

        generateAnnotations annotations.Values sb

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

        let funcId =
            if builderName.StartsWith("b", StringComparison.Ordinal) then
                let mutable counter = 1
                match builderName.Length > 1, Int32.TryParse(builderName.[1..]) with
                | true, (true, _) -> counter <- counter + 1
                | _ -> ()

                "b" + if counter = 0 then "" else counter.ToString()
            else "b"

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
            |> this.generateCheckConstraints funcId entityType
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
            |> Seq.filter(fun e ->
                (e
                 |> getDeclaredForeignKeys
                 |> Seq.isEmpty
                 |> not) || (e |> getDeclaredReferencingForeignKeys |> Seq.exists(fun fk -> fk.IsOwnership)))

        relationships |> Seq.iter(fun e -> this.generateEntityTypeRelationships builderName e sb)

        sb

    interface Microsoft.EntityFrameworkCore.Migrations.Design.ICSharpSnapshotGenerator with
        member this.Generate(builderName, model, sb) =
            let annotations =
                annotationCodeGenerator.FilterIgnoredAnnotations(model.GetAnnotations())
                |> annotationsToDictionary

            let productVersion = model.GetProductVersion()

            if annotations |> Seq.isEmpty |> not || productVersion |> isNull |> not then
                sb
                    |> appendLine builderName
                    |> indent
                    |> ignore

                let useOldBehavior =
                    match AppContext.TryGetSwitch("Microsoft.EntityFrameworkCore.Issue23456") with
                    | true, enabled -> enabled
                    | false, _ -> false

                let annotationsToMatch = [
                    ":ValueGenerationStrategy"
                    ":IdentityIncrement"
                    ":IdentitySeed"
                    ":HiLoSequenceName"
                    ":HiLoSequenceSchema"
                ]

                let ambiguousAnnotations =
                    if useOldBehavior then Seq.empty
                    else
                        removeAmbiguousAnnotations
                            annotations
                            (fun n -> annotationsToMatch
                                      |> Seq.exists (fun x -> n.EndsWith(x, StringComparison.Ordinal)))

                annotationCodeGenerator.GenerateFluentApiCalls(model, annotations)
                |> Seq.map code.Fragment
                |> Seq.iter (fun m ->
                    sb
                        |> appendEmptyLine
                        |> append m
                        |> ignore)

                let remainingAnnotations =
                    seq {
                        yield! annotations.Values
                        if productVersion |> isNull |> not then
                            yield Annotation(CoreAnnotationNames.ProductVersion, productVersion) :> _
                    }

                sb
                    |> generateAnnotations (remainingAnnotations |> Seq.append ambiguousAnnotations)
                    |> appendLine "|> ignore"
                    |> unindent
                    |> ignore

            for sequence in model.GetSequences() do
                generateSequence builderName sequence sb

            this.generateEntityTypes builderName (model.GetEntityTypes() |> sort) sb |> ignore
