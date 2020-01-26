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
open Microsoft.EntityFrameworkCore.Storage.ValueConversion

type TestFSharpSnapshotGenerator (dependencies, mappingSource : IRelationalTypeMappingSource) =
    inherit FSharpSnapshotGenerator(dependencies, mappingSource)

    member this.TestGenerateEntityTypeAnnotations builderName entityType stringBuilder =
        this.generateEntityTypeAnnotations builderName entityType stringBuilder

    member this.TestGeneratePropertyAnnotations property sb = 
        this.generatePropertyAnnotations property sb

module FSharpMigrationsGeneratorTest =
    open System.Collections.Generic
    
    [<CLIMutable>]
    type WithAnnotations =
        { Id: int }
        
    let nl = Environment.NewLine

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

        let allAnnotations = (caNames @ rlNames) |> Seq.filter (fun f -> f.Name <> "Prefix")
        
        allAnnotations
        |> Seq.iter(fun f ->
            let annotationName = f.GetValue(null) |> string

            if not (invalidAnnotations.Contains(annotationName)) then                    
                let modelBuilder = RelationalTestHelpers.Instance.CreateConventionBuilder()
                let metadataItem = createMetadataItem modelBuilder

                metadataItem.[annotationName] <- 
                    if validAnnotations.ContainsKey(annotationName) then 
                        fst validAnnotations.[annotationName]
                    else 
                        null

                modelBuilder.FinalizeModel() |> ignore
                
                let sb = IndentedStringBuilder()

                try
                    test generator metadataItem sb
                with
                    | exn ->
                        let msg = sprintf "Annotation '%s' was not handled by the code generator: {%s}" annotationName exn.Message
                        Xunit.Assert.False(true, msg)

                let actual = sb.ToString()

                let expected = 
                    if validAnnotations.ContainsKey(annotationName) then
                        snd validAnnotations.[annotationName]
                    else 
                        generationDefault

                actual |> should equal expected       
            )
        
        ()

    [<Xunit.Fact>]
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
                    
        let toTable = nl + @"modelBuilder.ToTable(""WithAnnotations"") |> ignore" + nl 

        let forEntityType =
            [
                (
                    RelationalAnnotationNames.TableName, ("MyTable" :> obj, 
                        nl + "modelBuilder.ToTable" + @"(""MyTable"") |> ignore" + nl)
                )
                (
                    RelationalAnnotationNames.Schema, ("MySchema" :> obj,
                        nl
                        + "modelBuilder."
                        + "ToTable"
                        + @"(""WithAnnotations"",""MySchema"") |> ignore"
                        + nl)
                )
                (
                    CoreAnnotationNames.DiscriminatorProperty, ("Id" :> obj,
                        toTable
                        + nl
                        + "modelBuilder.HasDiscriminator"
                        + @"<int>(""Id"") |> ignore"
                        + nl)
                )
                (
                    CoreAnnotationNames.DiscriminatorValue, ("MyDiscriminatorValue" :> obj,
                        toTable
                        + nl
                        + "modelBuilder.HasDiscriminator"
                        + "()."
                        + "HasValue"
                        + @"(""MyDiscriminatorValue"") |> ignore"
                        + nl)
                )
                (
                    RelationalAnnotationNames.Comment, ("My Comment" :> obj,
                        toTable
                        + nl
                        + "modelBuilder.HasComment"
                        + @"(""My Comment"") |> ignore"
                        + nl)
                )
            ] |> dict

        missingAnnotationCheck
                (fun b -> (b.Entity<WithAnnotations>().Metadata :> IMutableAnnotatable))
                notForEntityType
                forEntityType
                toTable
                (fun g m b -> g.generateEntityTypeAnnotations "modelBuilder" (m :> obj :?> _) b |> ignore)


    [<Xunit.Fact>]
    let ``Test new annotations handled for property types`` () =
        let notForProperty =
            [
                CoreAnnotationNames.ProductVersion
                CoreAnnotationNames.OwnedTypes
                CoreAnnotationNames.ConstructorBinding
                CoreAnnotationNames.NavigationAccessMode
                CoreAnnotationNames.EagerLoaded
                CoreAnnotationNames.QueryFilter
                CoreAnnotationNames.DefiningQuery
                CoreAnnotationNames.DiscriminatorProperty
                CoreAnnotationNames.DiscriminatorValue
                CoreAnnotationNames.InverseNavigations
                CoreAnnotationNames.NavigationCandidates
                CoreAnnotationNames.AmbiguousNavigations
                CoreAnnotationNames.DuplicateServiceProperties
                RelationalAnnotationNames.TableName
                RelationalAnnotationNames.Schema
                RelationalAnnotationNames.DefaultSchema
                RelationalAnnotationNames.Name
                RelationalAnnotationNames.SequencePrefix
                RelationalAnnotationNames.CheckConstraints
                RelationalAnnotationNames.Filter
                RelationalAnnotationNames.DbFunction
                RelationalAnnotationNames.MaxIdentifierLength
            ] |> HashSet

        let columnMapping = 
            nl + @".HasColumnType(""default_int_mapping"")"

        let forProperty = 
            [
                ( CoreAnnotationNames.MaxLength, (256 :> obj, columnMapping + nl + ".HasMaxLength(256) |> ignore"))
                ( CoreAnnotationNames.Unicode, (false :> obj, columnMapping + nl + ".IsUnicode(false) |> ignore"))
                (
                    CoreAnnotationNames.ValueConverter, (new ValueConverter<int, int64>((fun v -> v |> int64), (fun v -> v |> int), null) :> obj,
                        nl+ @".HasColumnType(""default_long_mapping"") |> ignore")
                )
                (
                    CoreAnnotationNames.ProviderClrType,
                    (typeof<int64> :> obj, nl + @".HasColumnType(""default_long_mapping"") |> ignore")
                )
                (
                    RelationalAnnotationNames.ColumnName,
                    ("MyColumn" :> obj, nl + @".HasColumnName(""MyColumn"")" + columnMapping + " |> ignore")
                )
                (
                    RelationalAnnotationNames.ColumnType,
                    ("int" :> obj, nl + @".HasColumnType(""int"") |> ignore")
                )
                (
                    RelationalAnnotationNames.DefaultValueSql,
                    ("some SQL" :> obj, columnMapping + nl + @".HasDefaultValueSql(""some SQL"") |> ignore")
                )
                (
                    RelationalAnnotationNames.ComputedColumnSql,
                    ("some SQL" :> obj, columnMapping + nl + @".HasComputedColumnSql(""some SQL"") |> ignore")
                )
                (
                    RelationalAnnotationNames.DefaultValue,
                    ("1" :> obj, columnMapping + nl + @".HasDefaultValue(""1"") |> ignore")
                )
                (
                    CoreAnnotationNames.TypeMapping,
                    (new LongTypeMapping("bigint") :> obj, nl + @".HasColumnType(""bigint"") |> ignore")
                )
                (
                    RelationalAnnotationNames.IsFixedLength,
                    (true :> obj, columnMapping + nl + @".IsFixedLength(true) |> ignore")
                )
                (
                    RelationalAnnotationNames.Comment,
                    ("My Comment" :> obj, columnMapping + nl + @".HasComment(""My Comment"") |> ignore")
                )
            ] |> dict

        missingAnnotationCheck
            (fun b -> (b.Entity<WithAnnotations>().Property(fun e -> e.Id).Metadata :> IMutableAnnotatable))
            notForProperty
            forProperty
            (columnMapping + " |> ignore")
            (fun g m b -> g.generatePropertyAnnotations (m :> obj :?> _) b |> ignore)
