# Scaffolding code

EF Core allows us to scaffold a model in code, based on an existing database.

Because F# allows multiple ways of representing an object, we are able to provide configuration options for how that generated code is formed.

## Record vs Class type

If we wanted to model a blog post with an Id, Title and Content we could do it as a record type

```fsharp
type BlogPostRecord = {
    Id : int
    Title: string
    Content: string
}
```

Or we could do it as a class type, that would behave more like how a C# class would

```fsharp
type BlogPostClass() =

    [<DefaultValue>] val mutable private _Id : int
    member this.Id with get() = this._Id and set v = this._Id <- v

    [<DefaultValue>] val mutable private _Title : string
    member this.Title with get() = this._Title and set v = this._Title <- v

    [<DefaultValue>] val mutable private _Content : string
    member this.Content with get() = this._Content and set v = this._Content <- v
```

Similarly, if we had a nullable column, for instance of type `Guid`, it might be specified as either `Nullable<Guid>` or in more idiomatic F#, as `guid option`

Again, we provide options for how these should be scaffolded.

## Scaffolding Options

We provide `EntityFramework.FSharp.ScaffoldOptions` to specify how we want to create our scaffolded code.

By default we scaffold record types with nullable columns represented as `option` types, but this can be overridden.

To do this, you'll need your own implementation of `IDesignTimeServices`, where we can declare a `ScaffoldOptions` object.

The `ScaffoldOptions.Default` object is equivalent to `ScaffoldOptions (ScaffoldTypesAs = RecordType, ScaffoldNullableColumnsAs = OptionTypes)`

If you are happy with the default of record-type objects with `option` properties then you will not need to implement this at all, it is already handled automatically for you.

```fsharp
module DesignTimeServices =

    open Microsoft.Extensions.DependencyInjection
    open Microsoft.EntityFrameworkCore.Design
    open EntityFrameworkCore.FSharp

    type DesignTimeServices() =
        interface IDesignTimeServices with
            member __.ConfigureDesignTimeServices(serviceCollection: IServiceCollection) =
                
                // The default behaviour can be specified by calling
                let fSharpServices = EFCoreFSharpServices.Default

                // Or we can define a ScaffoldOptions use that instead
                let scaffoldOptions =
                    ScaffoldOptions (
                        ScaffoldTypesAs = ScaffoldTypesAs.ClassType,
                        ScaffoldNullableColumnsAs = ScaffoldNullableColumnsAs.NullableTypes)

                let fSharpServices = EFCoreFSharpServices.WithScaffoldOptions scaffoldOptions

                fSharpServices.ConfigureDesignTimeServices serviceCollection
                ()
```
