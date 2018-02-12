namespace Bricelam.EntityFrameworkCore.FSharp.Scaffolding

open System
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Scaffolding

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Bricelam.EntityFrameworkCore.FSharp.Internal
open Microsoft.EntityFrameworkCore.ChangeTracking.Internal
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Design

type IFSharpDbContextGenerator =
    inherit Microsoft.EntityFrameworkCore.Scaffolding.Internal.ICSharpDbContextGenerator


type FSharpDbContextGenerator
    (providerCodeGenerator: IProviderCodeGenerator,
        legacyProviderCodeGenerator: IScaffoldingProviderCodeGenerator,
        annotationCodeGenerator : IAnnotationCodeGenerator) =

    let entityLambdaIdentifier = "entity";
    let language = "FSharp";

    let defaultNamespaces = [
        "System";
        "Microsoft.EntityFrameworkCore";
        "Microsoft.EntityFrameworkCore.Metadata";
    ]

    let writeNamespaces ``namespace`` (sb:IndentedStringBuilder) =
        sb
            |> append "namespace " |> appendLine ``namespace``
            |> appendLine ""
            |> writeNamespaces defaultNamespaces
            |> appendLine ""

    let generateType contextName (sb:IndentedStringBuilder) =
        sb
            |> append "type " |> append contextName |> appendLine " ="
            |> indent
            |> appendLine "inherit DbContext"
            |> appendLine ""
            |> appendLine "new() = { inherit DbContext() }"
            |> appendLine "new(options : DbContextOptions<DatabaseContext>) = { inherit DbContext(options) }"
            |> appendLine ""

    let generateDbSet (sb:IndentedStringBuilder) (entityType : IEntityType) =

        let scaffolding = entityType |> ScaffoldingMetadataExtensions.Scaffolding
        let mutableName = "_" + scaffolding.DbSetName;

        sb
            |> appendLine "[<DefaultValue>]"
            |> append "val mutable " |> append mutableName |> append " : DbSet<" |> append entityType.Name |> appendLine ">"
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
            sb |> appendLine "" |> ignore

        sb

    let generateEntityTypeErrors (model:IModel) (sb:IndentedStringBuilder) =    

        let scaffolding = model |> ScaffoldingMetadataExtensions.Scaffolding

        scaffolding.EntityTypeErrors
            |> Seq.iter (fun e -> sb |> appendLine (sprintf "// %s Please see the warning messages." e.Value) |> ignore)

        if scaffolding.EntityTypeErrors |> Seq.isEmpty |> not then
            sb |> appendLine "" |> ignore

        sb

    let generateOnConfiguring (connectionString:string) (sb:IndentedStringBuilder) =      

        let connStringLine =
            match isNull providerCodeGenerator with
                | true -> sprintf "optionsBuilder%s" (legacyProviderCodeGenerator.GenerateUseProvider(connectionString, language))
                | false -> sprintf "optionsBuilder.%s(%s)" providerCodeGenerator.UseProviderMethod connectionString
            
        sb
            |> appendLine "override this.OnConfiguring(optionsBuilder: DbContextOptionsBuilder) ="
            |> indent
            |> appendLine "if not optionsBuilder.IsConfigured then"
            |> indent
            |> append "#warning" |> appendLine DesignStrings.SensitiveInformationWarning
            |> appendLine connStringLine
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


        sb
            

    let generateClass (model:IModel) (contextName: string)  (connectionString: string) (useDataAnnotations: bool) (sb:IndentedStringBuilder) =
        sb
            |> generateType contextName
            |> generateDbSets model
            |> generateEntityTypeErrors model
            |> generateOnConfiguring connectionString
            |> generateOnModelCreating model useDataAnnotations

    interface IFSharpDbContextGenerator with
        member this.WriteCode (model: IModel, ``namespace``: string, contextName: string, connectionString: string, useDataAnnotations: bool) =
            IndentedStringBuilder()
                |> writeNamespaces ``namespace``
                |> indent
                |> generateClass model contextName connectionString useDataAnnotations
                |> string