module EntityFrameworkCore.FSharp.Test.Scaffolding.Internal.FSharpModelGeneratorTests

open System
open System.IO
open Microsoft.EntityFrameworkCore.Design
open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore.Scaffolding
open Microsoft.EntityFrameworkCore.Metadata.Internal
open EntityFrameworkCore.FSharp
open EntityFrameworkCore.FSharp.Test.TestUtilities
open Expecto
open EntityFrameworkCore.FSharp.Test.Migrations.Design
open Microsoft.EntityFrameworkCore.Design.Internal

let _eol = System.Environment.NewLine

let join separator (lines: string seq) = System.String.Join(separator, lines)

let createGenerator options =

    let testAssembly =
        (typeof<ModelCodeGeneratorTestBase.ModelCodeGeneratorTestBase>).Assembly

    let reporter = TestOperationReporter()

    let services =
        DesignTimeServicesBuilder(testAssembly, testAssembly, reporter, [||])
            .CreateServiceCollection("Microsoft.EntityFrameworkCore.SqlServer")
            .AddSingleton<IAnnotationCodeGenerator, AnnotationCodeGenerator>()
            .AddSingleton<IProviderConfigurationCodeGenerator, TestProviderCodeGenerator>()

    let designTimeServices = EFCoreFSharpServices.WithScaffoldOptions options

    designTimeServices.ConfigureDesignTimeServices(services)

    services
        .BuildServiceProvider(validateScopes = true)
        .GetRequiredService<IModelCodeGenerator>()

let getModelBuilder () =
    let modelBuilder = RelationalTestHelpers.Instance.CreateConventionBuilder()

    modelBuilder.Entity("BlogPost").Property<int>("Id")
    |> ignore

    modelBuilder.Entity("BlogPost").Property<string>("Title")
    |> ignore

    modelBuilder.Entity("Comment").Property<int>("Id")
    |> ignore

    modelBuilder.Entity("Comment").Property<int>("BlogPostId")
    |> ignore

    modelBuilder
        .Entity("Comment")
        .Property<Nullable<Guid>>("OptionalGuid")
        .IsRequired(false)
    |> ignore

    modelBuilder
        .Entity("BlogPost")
        .HasMany("Comment", "Comments")
        .WithOne("BlogPost")
    |> ignore

    modelBuilder

let getModelBuilderOptions useDataAnnotations =
    ModelCodeGenerationOptions(
        ModelNamespace = "TestNamespace",
        ContextNamespace = "ContextNameSpace",
        ContextDir =
            Path.Combine("..", (sprintf "%s%c" "TestContextDir" Path.DirectorySeparatorChar)),
        ContextName = "TestContext",
        ConnectionString = "Data Source=Test",
        UseDataAnnotations = useDataAnnotations
    )

[<Tests>]
let FSharpModelGeneratorTests =

    testList "FSharpModelGeneratorTests" [

        test "Language Works" {
            let generator = createGenerator ScaffoldOptions.Default

            let result = generator.Language

            Expect.equal result "F#" "Should be equal"
        }

        test "Write code works" {
            let generator = createGenerator (ScaffoldOptions(ScaffoldTypesAs = ClassType))

            let modelBuilder = getModelBuilder ()
            let modelBuilderOptions = getModelBuilderOptions false

            let result =
                generator.GenerateModel(
                    (modelBuilder.FinalizeModel(designTime = true)),
                    modelBuilderOptions
                )

            let expectedContextFilePath = Path.Combine("..", "TestContextDir", "TestContext.fs")

            Expect.equal result.ContextFile.Path expectedContextFilePath "Should be equal"
            Expect.isNotEmpty result.ContextFile.Code "Should not be empty"

            Expect.equal result.AdditionalFiles.Count 1 "Should be equal"
            Expect.equal result.AdditionalFiles.[0].Path "TestDomain.fs" "Should be equal"
            Expect.isNotEmpty result.AdditionalFiles.[0].Code "Should not be empty"
        }

        test "Record types created correctly" {
            let generator = createGenerator (ScaffoldOptions(ScaffoldTypesAs = RecordType))

            let modelBuilder = getModelBuilder ()
            let modelBuilderOptions = getModelBuilderOptions false

            let result =
                generator.GenerateModel(
                    (modelBuilder.FinalizeModel(designTime = true)),
                    modelBuilderOptions
                )

            let expectedCode =
                seq {
                    "namespace TestNamespace"
                    ""
                    "open System"
                    "open System.Collections.Generic"
                    ""
                    "module rec TestDomain ="
                    ""
                    "    [<CLIMutable>]"
                    "    type BlogPost = {"
                    "        Id: int"
                    "        Title: string"
                    "        Comments: Comment seq"
                    "    }"
                    ""
                    "    [<CLIMutable>]"
                    "    type Comment = {"
                    "        Id: int"
                    "        BlogPostId: int"
                    "        OptionalGuid: Guid option"
                    "        BlogPost: BlogPost"
                    "    }"
                }
                |> join _eol

            Expect.isNotEmpty result.AdditionalFiles.[0].Code "Should not be empty"
            Expect.equal (result.AdditionalFiles.[0].Code.Trim()) expectedCode "Should be equal"
        }

        test "Record types created correctly with annotations" {
            let generator = createGenerator (ScaffoldOptions(ScaffoldTypesAs = RecordType))

            let modelBuilder = getModelBuilder ()
            let modelBuilderOptions = getModelBuilderOptions true

            let result =
                generator.GenerateModel(
                    (modelBuilder.FinalizeModel(designTime = true)),
                    modelBuilderOptions
                )

            let expectedCode =
                seq {
                    "namespace TestNamespace"
                    ""
                    "open System.ComponentModel.DataAnnotations"
                    "open System.ComponentModel.DataAnnotations.Schema"
                    "open System"
                    "open System.Collections.Generic"
                    ""
                    "module rec TestDomain ="
                    ""
                    "    [<CLIMutable>]"
                    "    type BlogPost = {"
                    "        [<KeyAttribute>]"
                    "        Id: int"
                    "        Title: string"
                    "        [<InversePropertyAttribute(\"Comment.BlogPost\")>]"
                    "        Comments: Comment seq"
                    "    }"
                    ""
                    "    [<CLIMutable>]"
                    "    type Comment = {"
                    "        [<KeyAttribute>]"
                    "        Id: int"
                    "        BlogPostId: int"
                    "        OptionalGuid: Guid option"
                    "        [<ForeignKeyAttribute(\"BlogPostId\")>]"
                    "        [<InversePropertyAttribute(\"Comments\")>]"
                    "        BlogPost: BlogPost"
                    "    }"
                }
                |> join _eol

            Expect.isNotEmpty result.AdditionalFiles.[0].Code "Should not be empty"
            Expect.equal (result.AdditionalFiles.[0].Code.Trim()) expectedCode "Should be equal"
        }

        test "Class types created correctly" {
            let generator = createGenerator (ScaffoldOptions(ScaffoldTypesAs = ClassType))

            let modelBuilder = getModelBuilder ()
            let modelBuilderOptions = getModelBuilderOptions false

            let result =
                generator.GenerateModel(
                    (modelBuilder.FinalizeModel(designTime = true)),
                    modelBuilderOptions
                )

            let expectedCode =
                seq {
                    "namespace TestNamespace"
                    ""
                    "open System"
                    "open System.Collections.Generic"
                    ""
                    "module rec TestDomain ="
                    ""
                    "    type BlogPost() as this ="
                    "        do"
                    "            this.Comments <- HashSet<Comment>() :> ICollection<Comment>"
                    ""
                    "        [<DefaultValue>] val mutable private _Id : int"
                    ""
                    "        member this.Id with get() = this._Id and set v = this._Id <- v"
                    ""
                    "        [<DefaultValue>] val mutable private _Title : string"
                    ""
                    "        member this.Title with get() = this._Title and set v = this._Title <- v"
                    ""
                    ""
                    "        [<DefaultValue>] val mutable private _Comments : ICollection<Comment>"
                    ""
                    "        member this.Comments with get() = this._Comments and set v = this._Comments <- v"
                    ""
                    "    type Comment() as this ="
                    "        [<DefaultValue>] val mutable private _Id : int"
                    ""
                    "        member this.Id with get() = this._Id and set v = this._Id <- v"
                    ""
                    "        [<DefaultValue>] val mutable private _BlogPostId : int"
                    ""
                    "        member this.BlogPostId with get() = this._BlogPostId and set v = this._BlogPostId <- v"
                    ""
                    "        [<DefaultValue>] val mutable private _OptionalGuid : Guid option"
                    ""
                    "        member this.OptionalGuid with get() = this._OptionalGuid and set v = this._OptionalGuid <- v"
                    ""
                    ""
                    "        [<DefaultValue>] val mutable private _BlogPost : BlogPost"
                    ""
                    "        member this.BlogPost with get() = this._BlogPost and set v = this._BlogPost <- v"
                }
                |> join _eol

            Expect.isNotEmpty result.AdditionalFiles.[0].Code "Should not be empty"
            Expect.equal (result.AdditionalFiles.[0].Code.Trim()) expectedCode "Should be equal"

        }

        test "Class types created correctly with annotations" {
            let generator = createGenerator (ScaffoldOptions(ScaffoldTypesAs = ClassType))

            let modelBuilder = getModelBuilder ()
            let modelBuilderOptions = getModelBuilderOptions true

            let result =
                generator.GenerateModel(
                    (modelBuilder.FinalizeModel(designTime = true)),
                    modelBuilderOptions
                )

            let expectedCode =
                seq {
                    "namespace TestNamespace"
                    ""
                    "open System.ComponentModel.DataAnnotations"
                    "open System.ComponentModel.DataAnnotations.Schema"
                    "open System"
                    "open System.Collections.Generic"
                    ""
                    "module rec TestDomain ="
                    ""
                    "    type BlogPost() as this ="
                    "        do"
                    "            this.Comments <- HashSet<Comment>() :> ICollection<Comment>"
                    ""
                    "        [<DefaultValue>] val mutable private _Id : int"
                    ""
                    "        [<KeyAttribute>]"
                    "        member this.Id with get() = this._Id and set v = this._Id <- v"
                    ""
                    "        [<DefaultValue>] val mutable private _Title : string"
                    ""
                    "        member this.Title with get() = this._Title and set v = this._Title <- v"
                    ""
                    ""
                    "        [<DefaultValue>] val mutable private _Comments : ICollection<Comment>"
                    ""
                    "        [<InversePropertyAttribute(\"Comment.BlogPost\")>]"
                    "        member this.Comments with get() = this._Comments and set v = this._Comments <- v"
                    ""
                    "    type Comment() as this ="
                    "        [<DefaultValue>] val mutable private _Id : int"
                    ""
                    "        [<KeyAttribute>]"
                    "        member this.Id with get() = this._Id and set v = this._Id <- v"
                    ""
                    "        [<DefaultValue>] val mutable private _BlogPostId : int"
                    ""
                    "        member this.BlogPostId with get() = this._BlogPostId and set v = this._BlogPostId <- v"
                    ""
                    "        [<DefaultValue>] val mutable private _OptionalGuid : Guid option"
                    ""
                    "        member this.OptionalGuid with get() = this._OptionalGuid and set v = this._OptionalGuid <- v"
                    ""
                    ""
                    "        [<DefaultValue>] val mutable private _BlogPost : BlogPost"
                    ""
                    "        [<ForeignKeyAttribute(\"BlogPostId\")>]"
                    "        [<InversePropertyAttribute(\"Comments\")>]"
                    "        member this.BlogPost with get() = this._BlogPost and set v = this._BlogPost <- v"
                }
                |> join _eol

            Expect.isNotEmpty result.AdditionalFiles.[0].Code "Should not be empty"
            Expect.equal (result.AdditionalFiles.[0].Code.Trim()) expectedCode "Should be equal"

        }

    ]
