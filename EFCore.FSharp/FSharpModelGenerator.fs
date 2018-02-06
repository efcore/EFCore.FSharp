namespace Bricelam.EntityFrameworkCore.FSharp

open System.IO
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Scaffolding

open Bricelam.EntityFrameworkCore.FSharp.Scaffolding
open Bricelam.EntityFrameworkCore.FSharp.Scaffolding.ScaffoldingTypes

type FSharpModelGenerator(dependencies: ModelCodeGeneratorDependencies) =
    inherit ModelCodeGenerator(dependencies)
 
    let fileExtension = ".fs"

    override this.Language = "F#"

    override this.GenerateModel(model: IModel, ``namespace``: string, contextDir: string, contextName: string, connectionString: string, dataAnnotations: bool) =
        let resultingFiles = ScaffoldedModel()

        let generatedCode = FSharpDbContextGenerator.WriteCode model ``namespace`` contextName connectionString dataAnnotations //options.SuppressConnectionStringWarning

        let dbContextFileName = contextName + fileExtension;

        let contextFile = ScaffoldedFile()
        contextFile.Code <- generatedCode
        contextFile.Path <- Path.Combine(contextDir, dbContextFileName)
        resultingFiles.ContextFile <- contextFile

        let files =
            model.GetEntityTypes()
            |> Seq.map(fun entityType -> 
                let entityCode = FSharpEntityTypeGenerator.WriteCode entityType ``namespace`` dataAnnotations RecordOrType.RecordType OptionOrNullable.OptionTypes

                // output EntityType poco .fs file
                let entityTypeFileName = (entityType |> EntityTypeExtensions.DisplayName) + fileExtension;
                let additionalFile = ScaffoldedFile()
                additionalFile.Path <- entityTypeFileName
                additionalFile.Code <- entityCode

                additionalFile                
            )

        files |> Seq.iter(fun f -> resultingFiles.AdditionalFiles.Add(f))

        resultingFiles        

