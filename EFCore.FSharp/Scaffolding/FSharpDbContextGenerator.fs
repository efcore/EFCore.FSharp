namespace Bricelam.EntityFrameworkCore.FSharp.Scaffolding

open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Metadata.Internal

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities

module FSharpDbContextGenerator =

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

        sb        


    let generateEntityTypeErrors (model:IModel) (sb:IndentedStringBuilder) =    
        sb |> appendLine "// EntityTypeErrors"

    let generateOnConfiguring (connectionString:string) (sb:IndentedStringBuilder) =        
        sb |> appendLine "// OnConfiguring"

    let generateOnModelCreating (model:IModel) (useDataAnnotations:bool) (sb:IndentedStringBuilder) =
        sb.AppendLine("override this.OnModelCreating(modelBuilder: ModelBuilder) =")
            |> appendLineIndent "base.OnModelCreating(modelBuilder)"
            

    let WriteCode (model: IModel) (``namespace``: string) (contextName: string)  (connectionString: string) (useDataAnnotations: bool) =
        IndentedStringBuilder()
            |> writeNamespaces ``namespace``
            |> generateType contextName
            |> generateDbSets model
            |> generateEntityTypeErrors model
            |> generateOnConfiguring connectionString
            |> generateOnModelCreating model useDataAnnotations
            |> string