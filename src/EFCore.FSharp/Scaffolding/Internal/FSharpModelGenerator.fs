namespace EntityFrameworkCore.FSharp.Scaffolding.Internal

open System.IO
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Scaffolding
open Microsoft.EntityFrameworkCore.Scaffolding.Internal

open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open EntityFrameworkCore.FSharp.SharedTypeExtensions


type FSharpModelGenerator
    (dependencies : ModelCodeGeneratorDependencies,
     contextGenerator : ICSharpDbContextGenerator,
     entityTypeGenerator : ICSharpEntityTypeGenerator) =
    inherit ModelCodeGenerator(dependencies)

    let fileExtension = ".fs"

    let defaultNamespaces = [
        "System";
        "System.Collections.Generic";
    ]

    let annotationNamespaces = [
        "System.ComponentModel.DataAnnotations";
        "System.ComponentModel.DataAnnotations.Schema";
    ]

    let getNamespacesFromModel (model:IModel) =
        model.GetEntityTypes()
        |> Seq.collect (fun e -> e.GetProperties())
        |> Seq.collect (fun p -> getNamespaces p.ClrType)
        |> Seq.filter (fun ns -> defaultNamespaces |> Seq.contains ns |> not)
        |> Seq.distinct
        |> Seq.sort

    let createDomainFileContent (model:IModel) (useDataAnnotations:bool) (``namespace``:string) (moduleName: string) =

        let namespaces =
            if useDataAnnotations then
                defaultNamespaces |> Seq.append annotationNamespaces |> Seq.append (model |> getNamespacesFromModel)
            else
                defaultNamespaces  |> Seq.append (model |> getNamespacesFromModel)

        let writeNamespaces ``namespace`` (sb:IndentedStringBuilder) =

            sb
                |> append "namespace " |> appendLine ``namespace``
                |> appendEmptyLine
                |> writeNamespaces namespaces
                |> appendEmptyLine

        let noEntities =
            model.GetEntityTypes() |> Seq.isEmpty

        IndentedStringBuilder()
                |> writeNamespaces ``namespace``
                |> append "module rec " |> append moduleName |> appendLine " ="
                |> appendEmptyLine
                |> appendIfTrue noEntities "    ()"

    override __.Language = "F#"

    override __.GenerateModel(model: IModel, options: ModelCodeGenerationOptions) =
        let resultingFiles = ScaffoldedModel()

        let generatedCode =
            contextGenerator.WriteCode(
                model,
                options.ContextName,
                options.ConnectionString,
                options.ContextNamespace,
                options.ModelNamespace,
                options.UseDataAnnotations,
                options.UseNullableReferenceTypes,
                options.SuppressConnectionStringWarning,
                options.SuppressOnConfiguring)

        let dbContextFileName = options.ContextName + fileExtension;

        let path =
            if notNull options.ContextDir then
                Path.Combine(options.ContextDir, dbContextFileName)
            else
                dbContextFileName

        let contextFile =
            ScaffoldedFile(
                Code = generatedCode,
                Path = path)

        resultingFiles.ContextFile <- contextFile

        let dbContextFileName = options.ContextName

        let domainFile = ScaffoldedFile()
        let domainFileName = dbContextFileName.Replace("Context", "Domain")
        domainFile.Path <- (domainFileName + fileExtension)

        let domainFileBuilder =
            createDomainFileContent model options.UseDataAnnotations options.ModelNamespace domainFileName

        model.GetEntityTypes()
            |> Seq.iter(fun entityType ->
                domainFileBuilder
                    |> append (entityTypeGenerator.WriteCode(entityType,
                                                                options.ModelNamespace,
                                                                options.UseDataAnnotations,
                                                                options.UseNullableReferenceTypes))
                    |> ignore
            )
        domainFile.Code <- (domainFileBuilder.ToString())

        resultingFiles.AdditionalFiles.Add(domainFile)

        resultingFiles

