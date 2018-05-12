namespace Bricelam.EntityFrameworkCore.FSharp.Scaffolding

open System
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Scaffolding

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Bricelam.EntityFrameworkCore.FSharp.Internal
open Microsoft.EntityFrameworkCore.ChangeTracking.Internal
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Design

#nowarn "0044"
type FSharpDbContextGenerator
    (providerCodeGenerator: ProviderCodeGenerator,
        legacyProviderCodeGenerator: IScaffoldingProviderCodeGenerator,
        annotationCodeGenerator : IAnnotationCodeGenerator) =

    let entityLambdaIdentifier = "entity";
    let language = "FSharp";

    let defaultNamespaces = [
        "System";
        "System.Collections.Generic";
        "Microsoft.EntityFrameworkCore";
        "Microsoft.EntityFrameworkCore.Metadata";
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

        let scaffolding = entityType |> ScaffoldingMetadataExtensions.Scaffolding
        let mutableName = "_" + scaffolding.DbSetName;

        sb
            |> appendLine "[<DefaultValue>]"
            |> append "val mutable private " |> append mutableName |> append " : DbSet<" |> append entityType.Name |> appendLine ">"
            |> append "member this." |> appendLine scaffolding.DbSetName
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

        let scaffolding = model |> ScaffoldingMetadataExtensions.Scaffolding

        scaffolding.EntityTypeErrors
            |> Seq.iter (fun e -> sb |> appendLine (sprintf "// %s Please see the warning messages." e.Value) |> ignore)

        if scaffolding.EntityTypeErrors |> Seq.isEmpty |> not then
            sb |> appendEmptyLine |> ignore

        sb

    let generateOnConfiguring (connectionString:string) (sb:IndentedStringBuilder) =      

        let connStringLine = //sprintf "optionsBuilder%s" (legacyProviderCodeGenerator.GenerateUseProvider(connectionString, language))
            match isNull providerCodeGenerator with
                | true -> sprintf "optionsBuilder%s" (legacyProviderCodeGenerator.GenerateUseProvider(connectionString, language))
                | false -> sprintf "optionsBuilder%s" (connectionString |> providerCodeGenerator.GenerateUseProvider |> FSharpHelper.Fragment)
            
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
        match annotationCodeGenerator.IsHandledByConvention(model, annotation) with
        | true -> (annotation |> Some, None)
        | false ->
            let methodCall = annotationCodeGenerator.GenerateFluentApi(model, annotation)
            let line =
                match isNull methodCall with
                | true -> annotationCodeGenerator.GenerateFluentApi(model, annotation, language)
                | false -> FSharpUtilities.generate(methodCall)

            match isNull line with
                | false -> (annotation |> Some, line |> Some)
                | _ -> (None, None)

    let generateAnnotations (annotations: IAnnotation seq) =
        annotations
        |> Seq.map (fun a ->
            let name = FSharpUtilities.delimitString(a.Name)
            let literal = FSharpUtilities.generateLiteral(a.Value)
            sprintf ".HasAnnotation(%s, %s,)" name literal)

    let generateEntityTypes (entities: IEntityType seq) useDataAnnotations (sb:IndentedStringBuilder) =
        sb
    let generateSequence (s: ISequence) (sb:IndentedStringBuilder) =

        let writeLineIfTrue truth name parameter (sb:IndentedStringBuilder) =
            match truth with
            | true -> sb |> appendLine (sprintf ".%s(%A)" name parameter)
            | false -> sb

        let methodName =
            match s.ClrType = Sequence.DefaultClrType with
            | true -> "HasSequence"
            | false -> sprintf "HasSequence<%s>" (FSharpUtilities.getTypeName(s.ClrType))

        let parameters =
            match (s.Schema |> String.IsNullOrEmpty) && (s.Model.Relational().DefaultSchema <> s.Schema) with
            | true -> sprintf "%s, %s" (s.Name |> FSharpUtilities.delimitString) (s.Schema |> FSharpUtilities.delimitString)
            | false -> s.Name |> FSharpUtilities.delimitString

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
            
    let generateSequences (sequences: ISequence seq) (sb:IndentedStringBuilder) =
        sequences |> Seq.iter(fun s -> sb |> generateSequence s)
        sb

    let generateEntityType (entityType : IEntityType) (useDataAnnotations : bool) (sb:IndentedStringBuilder) =
        sb


    let generateOnModelCreating (model:IModel) (useDataAnnotations:bool) (sb:IndentedStringBuilder) =
        sb.AppendLine("override this.OnModelCreating(modelBuilder: ModelBuilder) =")
            |> appendLineIndent "base.OnModelCreating(modelBuilder)"
            |> ignore

        let annotations =
            model.GetAnnotations()
            |> removeAnnotation ChangeDetector.SkipDetectChangesAnnotation
            |> removeAnnotation RelationalAnnotationNames.MaxIdentifierLength
            |> removeAnnotation ScaffoldingAnnotationNames.DatabaseName
            |> removeAnnotation ScaffoldingAnnotationNames.EntityTypeErrors
            |> Seq.toList

        let annotationsToRemove =
            annotations
            |> Seq.filter(fun a -> a.Name.StartsWith(RelationalAnnotationNames.SequencePrefix, StringComparison.Ordinal))

        let checkedAnnotations =
            annotations
            |> Seq.map(fun a -> a |> checkAnnotation model)

        let moreAnnotaionsToRemove = checkedAnnotations |> Seq.map(fun (a, _) -> a) |> Seq.filter(fun x -> x.IsSome) |> Seq.map(fun x -> x.Value)
        let lines = checkedAnnotations |> Seq.map(fun (_, l) -> l) |> Seq.filter(fun x -> x.IsSome) |> Seq.map(fun x -> x.Value)

        let toRemove = annotationsToRemove |> Seq.append moreAnnotaionsToRemove

        
        let lines' = lines |> Seq.append ((annotations |> Seq.except toRemove) |> generateAnnotations)

        if lines' |> Seq.isEmpty |> not then
            sb
                |> appendEmptyLine
                |> indent
                |> append (sprintf "modelBuilder%s" (lines' |> Seq.head))
                |> indent
                |> appendLines (lines' |> Seq.tail) false
                |> unindent
                |> unindent
                |> ignore

        // TODO: https://github.com/aspnet/EntityFrameworkCore/blob/dev/src/EFCore.Design/Scaffolding/Internal/CSharpDbContextGenerator.cs#L301

        sb            

    let generateClass model contextName connectionString useDataAnnotations sb =
        sb
            |> generateType contextName
            |> generateDbSets model
            |> generateEntityTypeErrors model
            |> generateOnConfiguring connectionString
            |> generateOnModelCreating model useDataAnnotations

    interface Microsoft.EntityFrameworkCore.Scaffolding.Internal.ICSharpDbContextGenerator with
        member this.WriteCode (model, ``namespace``, contextName, connectionString, useDataAnnotations, suppressConnectionStringWarning) =
            let sb = 
                IndentedStringBuilder()
                |> writeNamespaces ``namespace``
                |> indent
                |> generateClass model contextName connectionString useDataAnnotations
            sb.ToString()
