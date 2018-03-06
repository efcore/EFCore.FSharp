namespace Bricelam.EntityFrameworkCore.FSharp.Scaffolding.Internal

open System.IO
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Scaffolding
open Microsoft.EntityFrameworkCore.Scaffolding.Internal

open Bricelam.EntityFrameworkCore.FSharp.Scaffolding
open Bricelam.EntityFrameworkCore.FSharp.Scaffolding.ScaffoldingTypes
open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Microsoft.EntityFrameworkCore.Internal

type FSharpModelGenerator(dependencies: ModelCodeGeneratorDependencies, contextGenerator: ICSharpDbContextGenerator) =
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
        |> Seq.collect (fun p -> p.ClrType.GetNamespaces())
        |> Seq.filter (fun ns -> defaultNamespaces |> Seq.contains ns |> not)
        |> Seq.distinct
        |> Seq.sort

    let createDomainFileContent (model:IModel) (useDataAnnotations:bool) (``namespace``:string) domainFileName =

        let namespaces =
            match useDataAnnotations with
            | true -> defaultNamespaces |> Seq.append annotationNamespaces |> Seq.append (model |> getNamespacesFromModel)
            | false -> defaultNamespaces  |> Seq.append (model |> getNamespacesFromModel)

        let writeNamespaces ``namespace`` (sb:IndentedStringBuilder) =
            sb
                |> append "namespace " |> appendLine ``namespace``
                |> appendLine ""
                |> writeNamespaces namespaces
                |> appendLine ""

        IndentedStringBuilder()
                |> writeNamespaces ``namespace``
                |> append "module rec " |> append domainFileName |> appendLine " ="
                |> appendLine ""
                |> indent

    override this.Language = "F#"

    override this.GenerateModel(model: IModel, ``namespace``: string, contextDir: string, contextName: string, connectionString: string, dataAnnotations: bool) =
        let resultingFiles = ScaffoldedModel()

        let generatedCode = contextGenerator.WriteCode(model, ``namespace``, contextName, connectionString, dataAnnotations)

        let dbContextFileName = contextName + fileExtension;

        let contextFile = ScaffoldedFile()
        contextFile.Code <- generatedCode
        contextFile.Path <- Path.Combine(contextDir, dbContextFileName)
        resultingFiles.ContextFile <- contextFile

        let domainFileName = contextName.Replace("Context", "Domain")

        let domainFile = ScaffoldedFile()
        domainFile.Path <- (domainFileName + fileExtension)

        let domainFileBuilder = createDomainFileContent model dataAnnotations ``namespace`` domainFileName

        model.GetEntityTypes()
            |> Seq.iter(fun entityType -> 
                domainFileBuilder
                    |> FSharpEntityTypeGenerator.WriteCode entityType dataAnnotations RecordOrType.RecordType OptionOrNullable.OptionTypes
                    |> ignore
            )
        domainFile.Code <- (domainFileBuilder |> string)

        resultingFiles.AdditionalFiles.Add(domainFile)

        resultingFiles

