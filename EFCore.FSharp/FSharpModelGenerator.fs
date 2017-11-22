namespace Bricelam.EntityFrameworkCore.FSharp

open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Scaffolding

type FSharpModelGenerator(dependencies: ModelCodeGeneratorDependencies) =
    inherit ModelCodeGenerator(dependencies)

    override this.Language = "F#"

    override this.GenerateModel(model: IModel, ``namespace``: string, contextDir: string, contextName: string, connectionString: string, dataAnnotations: bool) = null
