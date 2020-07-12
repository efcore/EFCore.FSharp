namespace EntityFrameworkCore.FSharp.Scaffolding.Internal

open System.IO
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Scaffolding
open Microsoft.EntityFrameworkCore.Scaffolding.Internal

open EntityFrameworkCore.FSharp.Scaffolding
open EntityFrameworkCore.FSharp.Scaffolding.ScaffoldingTypes
open EntityFrameworkCore.FSharp.EntityFrameworkExtensions
open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Microsoft.EntityFrameworkCore.Internal


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

    let createDomainFileContent (model:IModel) (useDataAnnotations:bool) (``namespace``:string) (domainFileName: string) =

        let ``module`` = 
            domainFileName.Replace("Context", "Domain")

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
                |> append "module rec " |> append ``module`` |> appendLine " ="
                |> appendEmptyLine                
                |> appendIfTrue (noEntities) "    ()"

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
                options.SuppressConnectionStringWarning)

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
        domainFile.Path <- ("TestDomain" + fileExtension)        

        let domainFileBuilder = 
            createDomainFileContent model options.UseDataAnnotations options.ModelNamespace dbContextFileName

        model.GetEntityTypes()
            |> Seq.iter(fun entityType -> 
                domainFileBuilder
                    |> append (entityTypeGenerator.WriteCode(entityType, 
                                                                options.ModelNamespace, 
                                                                options.UseDataAnnotations))
                    |> ignore
            )
        domainFile.Code <- (domainFileBuilder.ToString())

        resultingFiles.AdditionalFiles.Add(domainFile)

        resultingFiles

