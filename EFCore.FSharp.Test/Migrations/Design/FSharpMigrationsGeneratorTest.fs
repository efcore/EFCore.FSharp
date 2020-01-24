namespace Bricelam.EntityFrameworkCore.FSharp.Test.Migrations.Design

open System

open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.EntityFrameworkCore.TestUtilities

open Bricelam.EntityFrameworkCore.FSharp.Internal
open Bricelam.EntityFrameworkCore.FSharp.Migrations.Design
open Bricelam.EntityFrameworkCore.FSharp.Test.TestUtilities

open FsUnit.Xunit
open Microsoft.EntityFrameworkCore

type TestFSharpSnapshotGenerator (dependencies, mappingSource : IRelationalTypeMappingSource) =
    inherit FSharpSnapshotGenerator(dependencies, mappingSource)

    member this.TestGenerateEntityTypeAnnotations builderName entityType stringBuilder =
        this.generateEntityTypeAnnotations builderName entityType stringBuilder

module FSharpMigrationsGeneratorTest =
    open System.Collections.Generic
    
    [<CLIMutable>]
    type WithAnnotations =
        { Id: int }

    let nl = Environment.NewLine
    let toTable = nl + nl + """modelBuilder.ToTable("WithAnnotations") |> ignore"""

    let missingAnnotationCheck (createMetadataItem: ModelBuilder -> IMutableAnnotatable) (invalidAnnotations:HashSet<string>) (validAnnotations:IDictionary<string, (obj * string)>) (generationDefault : string) (test: TestFSharpSnapshotGenerator -> IMutableAnnotatable -> IndentedStringBuilder -> unit) =
        
        let typeMappingSource = 
            SqlServerTypeMappingSource(
                TestServiceFactory.Instance.Create<TypeMappingSourceDependencies>(),
                TestServiceFactory.Instance.Create<RelationalTypeMappingSourceDependencies>())

        let codeHelper =
            new FSharpHelper(typeMappingSource)

        let generator = TestFSharpSnapshotGenerator(codeHelper, typeMappingSource);
        
        let caNames = 
            (typeof<CoreAnnotationNames>).GetFields() 
            |> Seq.filter(fun f -> f.FieldType = typeof<string>) 
            |> Seq.toList

        let rlNames = (typeof<RelationalAnnotationNames>).GetFields() |> Seq.toList

        let fields = (caNames @ rlNames) |> Seq.filter (fun f -> f.Name <> "Prefix")
        
        fields
        |> Seq.iter(fun f ->
            let modelBuilder = RelationalTestHelpers.Instance.CreateConventionBuilder()
            let metadataItem = createMetadataItem modelBuilder

            let annotationName = f.GetValue(null) |> string

            if not (invalidAnnotations.Contains(annotationName)) then
                
                let value, expected =
                    if validAnnotations.ContainsKey(annotationName) then
                        validAnnotations.[annotationName]
                    else
                        null, generationDefault

                metadataItem.[annotationName] <- value
                //modelBuilder.FinalizeModel() |> ignore
                
                let sb = IndentedStringBuilder()

                try
                    test generator metadataItem sb
                with
                    | exn ->
                        let msg = sprintf "Annotation '%s' was not handled by the code generator: {%s}" annotationName exn.Message
                        Xunit.Assert.False(true, msg)


                let actual = sb.ToString()

                actual |> should equal expected

                metadataItem.[annotationName] <- null            
            )
        
        ()

    //[<Xunit.Fact>] TODO: Fix this test
    let ``Test new annotations handled for entity types`` () =
        let notForEntityType =
            [
                CoreAnnotationNames.MaxLength
                CoreAnnotationNames.Unicode
                CoreAnnotationNames.ProductVersion
                CoreAnnotationNames.ValueGeneratorFactory
                CoreAnnotationNames.OwnedTypes
                CoreAnnotationNames.TypeMapping
                CoreAnnotationNames.ValueConverter
                CoreAnnotationNames.ValueComparer
                CoreAnnotationNames.KeyValueComparer
                CoreAnnotationNames.StructuralValueComparer
                CoreAnnotationNames.BeforeSaveBehavior
                CoreAnnotationNames.AfterSaveBehavior
                CoreAnnotationNames.ProviderClrType
                CoreAnnotationNames.EagerLoaded
                CoreAnnotationNames.DuplicateServiceProperties
                RelationalAnnotationNames.ColumnName
                RelationalAnnotationNames.ColumnType
                RelationalAnnotationNames.DefaultValueSql
                RelationalAnnotationNames.ComputedColumnSql
                RelationalAnnotationNames.DefaultValue
                RelationalAnnotationNames.Name
                RelationalAnnotationNames.SequencePrefix
                RelationalAnnotationNames.CheckConstraints
                RelationalAnnotationNames.DefaultSchema
                RelationalAnnotationNames.Filter
                RelationalAnnotationNames.DbFunction
                RelationalAnnotationNames.MaxIdentifierLength
                RelationalAnnotationNames.IsFixedLength
            ] |> HashSet

        let forEntityType =
            [
                (
                    RelationalAnnotationNames.TableName,
                    ("MyTable" :> obj, nl + "modelBuilder.ToTable" + @"(""MyTable"");" + nl)
                )
                (
                    RelationalAnnotationNames.Schema,
                    ("MySchema" :> obj, nl + "modelBuilder..ToTable" + @"(""WithAnnotations"",""MySchema"");" + nl)
                )
                (
                    CoreAnnotationNames.DiscriminatorProperty,
                    ("Id" :> obj, toTable + nl + "modelBuilder.HasDiscriminator" + @"<int>(""Id"");" + nl)
                )
                (
                    CoreAnnotationNames.DiscriminatorValue,
                    ("MyDiscriminatorValue" :> obj,
                        toTable + nl + "modelBuilder.HasDiscriminator"
                        + "().HasValue" + @"(""MyDiscriminatorValue"");" + nl)
                )
            ] |> dict       

        missingAnnotationCheck
                (fun b -> (b.Entity<WithAnnotations>().Metadata :> IMutableAnnotatable))
                notForEntityType
                forEntityType
                toTable
                (fun g m b -> g.generateEntityTypeAnnotations "modelBuilder" (m :> obj :?> _) b |> ignore)