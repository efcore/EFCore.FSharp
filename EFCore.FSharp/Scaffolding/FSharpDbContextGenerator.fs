namespace Bricelam.EntityFrameworkCore.FSharp.Scaffolding

open System
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Scaffolding

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities

type IFSharpDbContextGenerator =
    inherit Microsoft.EntityFrameworkCore.Scaffolding.Internal.ICSharpDbContextGenerator


type FSharpDbContextGenerator(providerCodeGenerator: IProviderCodeGenerator, legacyProviderCodeGenerator: IScaffoldingProviderCodeGenerator) =

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

    let generateOnModelCreating (model:IModel) (useDataAnnotations:bool) (sb:IndentedStringBuilder) =
        sb.AppendLine("override this.OnModelCreating(modelBuilder: ModelBuilder) =")
            |> appendLineIndent "base.OnModelCreating(modelBuilder)"
            

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