namespace EntityFrameworkCore.FSharp.Scaffolding.Internal

open System.IO
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Scaffolding
open Microsoft.EntityFrameworkCore.Scaffolding.Internal

open EntityFrameworkCore.FSharp
open EntityFrameworkCore.FSharp.EntityFrameworkExtensions
open EntityFrameworkCore.FSharp.SharedTypeExtensions


type FSharpModelGenerator
    (
        dependencies: ModelCodeGeneratorDependencies,
        contextGenerator: ICSharpDbContextGenerator,
        entityTypeGenerator: ICSharpEntityTypeGenerator
    ) =
    inherit ModelCodeGenerator(dependencies)

    let fileExtension = ".fs"

    let defaultNamespaces =
        [ "System"
          "System.Collections.Generic" ]

    let annotationNamespaces =
        [ "System.ComponentModel.DataAnnotations"
          "System.ComponentModel.DataAnnotations.Schema" ]

    let getNamespacesFromModel (model: IModel) =
        model.GetEntityTypes()
        |> Seq.collect (fun e -> e.GetProperties())
        |> Seq.collect (fun p -> getNamespaces p.ClrType)
        |> Seq.filter (fun ns -> defaultNamespaces |> Seq.contains ns |> not)
        |> Seq.distinct
        |> Seq.sort

    let createDomainFileContent
        (model: IModel)
        (useDataAnnotations: bool)
        (``namespace``: string)
        (moduleName: string)
        =

        let namespaces =
            if useDataAnnotations then
                defaultNamespaces
                |> Seq.append annotationNamespaces
                |> Seq.append (model |> getNamespacesFromModel)
            else
                defaultNamespaces
                |> Seq.append (model |> getNamespacesFromModel)

        stringBuilder {
            $"namespace {``namespace``}"
            ""
            writeNamespaces namespaces
            ""
            $"module rec {moduleName} ="
            ""

            if model.GetEntityTypes() |> Seq.isEmpty then
                indent { "()" }
        }

    override __.Language = "F#"

    override __.GenerateModel(model: IModel, options: ModelCodeGenerationOptions) =

        let dbContextFileName = options.ContextName

        let domainFileName =
            dbContextFileName.Replace("Context", "Domain")

        let generatedCode =
            contextGenerator.WriteCode(
                model,
                options.ContextName,
                options.ConnectionString,
                (if isNull options.ContextNamespace then
                     options.ModelNamespace
                 else
                     options.ContextNamespace),
                domainFileName,
                options.UseDataAnnotations,
                options.UseNullableReferenceTypes,
                options.SuppressConnectionStringWarning,
                options.SuppressOnConfiguring
            )

        let dbContextFileName = options.ContextName + fileExtension

        let path =
            if notNull options.ContextDir then
                Path.Combine(options.ContextDir, dbContextFileName)
            else
                dbContextFileName

        let contextFile =
            ScaffoldedFile(Code = generatedCode, Path = path)

        let resultingFiles =
            ScaffoldedModel(ContextFile = contextFile)

        let domainFile = ScaffoldedFile()
        domainFile.Path <- (domainFileName + fileExtension)

        let createEntityCode (entityType: IEntityType) =
            entityTypeGenerator.WriteCode(
                entityType,
                options.ModelNamespace,
                options.UseDataAnnotations,
                options.UseNullableReferenceTypes
            )

        let domainFileBuilder =
            createDomainFileContent model options.UseDataAnnotations options.ModelNamespace domainFileName

        let entityCode =
            model.GetEntityTypes()
            |> Seq.filter (isManyToManyJoinEntityType >> not)
            |> Seq.map createEntityCode

        let domainFileCode =
            stringBuilder {
                domainFileBuilder

                entityCode
            }

        domainFile.Code <- domainFileCode

        resultingFiles.AdditionalFiles.Add(domainFile)

        resultingFiles
