namespace Bricelam.EntityFrameworkCore.FSharp.Test.Migrations.Design

open System

open Microsoft.EntityFrameworkCore.ChangeTracking
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Design.Internal
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Metadata.Builders
open Microsoft.EntityFrameworkCore.Metadata.Internal
open Microsoft.EntityFrameworkCore.Migrations.Internal
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.EntityFrameworkCore.Storage.ValueConversion
open Microsoft.EntityFrameworkCore.TestUtilities
open Microsoft.EntityFrameworkCore.ValueGeneration

open Bricelam.EntityFrameworkCore.FSharp
open Bricelam.EntityFrameworkCore.FSharp.Internal
open Bricelam.EntityFrameworkCore.FSharp.Migrations.Design
open Bricelam.EntityFrameworkCore.FSharp.Test.TestUtilities

open FsUnit.Xunit

type TestFSharpSnapshotGenerator (dependencies) =
    inherit FSharpSnapshotGenerator(dependencies)

    member this.TestGenerateEntityTypeAnnotations builderName entityType stringBuilder =
        this.generateEntityTypeAnnotations builderName entityType stringBuilder

module FSharpMigrationsGeneratorTest =
    open System.Collections.Generic
    
    type WithAnnotations =
        { Id: int }

    let nl = Environment.NewLine
    let toTable = nl + nl + """modelBuilder.ToTable("WithAnnotations") |> ignore"""

    let missingAnnotationCheck (metadataItem:IMutableAnnotatable) (invalidAnnotations:HashSet<string>) (validAnnotations:IDictionary<string, (obj * string)>) (generationDefault : string) (test: TestFSharpSnapshotGenerator -> IMutableAnnotatable -> IndentedStringBuilder -> unit) =
        
        let codeHelper =
            new FSharpHelper(
                SqlServerTypeMappingSource(
                    TestServiceFactory.Instance.Create<TypeMappingSourceDependencies>(),
                    TestServiceFactory.Instance.Create<RelationalTypeMappingSourceDependencies>()))

        let generator = TestFSharpSnapshotGenerator(codeHelper);


        let caNames = (typeof<CoreAnnotationNames>).GetFields() |> Seq.toList
        let rlNames = (typeof<CoreAnnotationNames>).GetFields() |> Seq.toList

        let fields = (caNames @ rlNames) |> Seq.filter (fun f -> f.Name <> "Prefix")
        
        fields
        |> Seq.iter(fun f ->
            
            let annotationName = (f.GetValue(null)) |> string

            if not (invalidAnnotations.Contains(annotationName)) then
                
                let value, expected =
                    if validAnnotations.ContainsKey(annotationName) then
                        validAnnotations.[annotationName]
                    else
                        Random() :> obj, generationDefault

                metadataItem.[annotationName] <- value

                let sb = IndentedStringBuilder()

                try
                    test generator metadataItem sb
                with
                    | _ ->
                        let msg = sprintf "Annotation '%s' was not handled by the code generator: {e.Message}" annotationName
                        Xunit.Assert.False(true, msg)


                let actual = sb.ToString()

                actual |> should equal expected

                metadataItem.[annotationName] <- null            
            )
        
        ()

    [<Xunit.Fact>]
    let ``Test new annotations handled for entity types`` () =
        let model = RelationalTestHelpers.Instance.CreateConventionBuilder()
        let entityType = model.Entity<WithAnnotations>().Metadata

        let notForEntityType =
            [
                CoreAnnotationNames.MaxLengthAnnotation
                CoreAnnotationNames.UnicodeAnnotation
                CoreAnnotationNames.ProductVersionAnnotation
                CoreAnnotationNames.ValueGeneratorFactoryAnnotation
                CoreAnnotationNames.OwnedTypesAnnotation
                CoreAnnotationNames.TypeMapping
                CoreAnnotationNames.ValueConverter
                CoreAnnotationNames.ValueComparer
                CoreAnnotationNames.KeyValueComparer
                CoreAnnotationNames.StructuralValueComparer
                CoreAnnotationNames.ProviderClrType
                RelationalAnnotationNames.ColumnName
                RelationalAnnotationNames.ColumnType
                RelationalAnnotationNames.DefaultValueSql
                RelationalAnnotationNames.ComputedColumnSql
                RelationalAnnotationNames.DefaultValue
                RelationalAnnotationNames.Name
                RelationalAnnotationNames.SequencePrefix
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
                    ("MyTable" :> obj, nl + "modelBuilder.ToTable)" + @"(""MyTable"");" + nl)
                )
                (
                    RelationalAnnotationNames.Schema,
                    ("MySchema" :> obj, nl + "modelBuilder..ToTable)" + @"(""WithAnnotations"",""MySchema"");" + nl)
                )
                (
                    RelationalAnnotationNames.DiscriminatorProperty,
                    ("Id" :> obj, toTable + nl + "modelBuilder.HasDiscriminator" + @"<int>(""Id"");" + nl)
                )
                (
                    RelationalAnnotationNames.DiscriminatorValue,
                    ("MyDiscriminatorValue" :> obj,
                        toTable + nl + "modelBuilder.HasDiscriminator"
                        + "().HasValue" + @"(""MyDiscriminatorValue"");" + nl)
                )
            ] |> dict

        missingAnnotationCheck
                entityType
                notForEntityType
                forEntityType
                toTable
                (fun g m b -> g.generateEntityTypeAnnotations "modelBuilder" (m :> obj :?> _) b |> ignore)