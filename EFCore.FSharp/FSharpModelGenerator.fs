namespace Bricelam.EntityFrameworkCore.FSharp

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

    let createDomainFileContent (``namespace``:string) domainFileName =

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

        IndentedStringBuilder()
                |> writeNamespaces ``namespace``
                |> append "module " |> append domainFileName |> appendLine "= "
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

        let domainFileBuilder = createDomainFileContent ``namespace`` domainFileName

        model.GetEntityTypes()
            |> Seq.iter(fun entityType -> 
                domainFileBuilder
                    |> FSharpEntityTypeGenerator.WriteCode entityType dataAnnotations RecordOrType.RecordType OptionOrNullable.OptionTypes
                    |> ignore
            )
        domainFile.Code <- (domainFileBuilder |> string)

        resultingFiles.AdditionalFiles.Add(domainFile)

        resultingFiles

