# Getting Started

## Prerequisites
This guide assumes:

* You have the .NET 5.0 SDK installed
* You have created a project and add the `dotnet-ef` tool

## Installing the package
paket

    [lang=bash]
    paket install EntityFrameworkCore.FSharp

dotnet CLI

    [lang=bash]
    dotnet add package EntityFrameworkCore.FSharp

## Note
This guide is for a simple single-project setup rather than a production-ready topology, however it should hopefully assist with the basics.

## Configure Design Time Services

To override the default Design Time services in EF Core, you will need to add the following file to your project

```fsharp
module DesignTimeServices =

    open Microsoft.Extensions.DependencyInjection
    open Microsoft.EntityFrameworkCore.Design
    open EntityFrameworkCore.FSharp

    type DesignTimeServices() =
        interface IDesignTimeServices with
            member __.ConfigureDesignTimeServices(serviceCollection: IServiceCollection) =
                let fSharpServices = EFCoreFSharpServices.Default
                fSharpServices.ConfigureDesignTimeServices serviceCollection
                ()
```

At this point you can create your model, or scaffold one from an existing database as per usual

By default, scaffolded objects will be created as Record types objects where nullable columns of type `'a` are represented as types of `'a option`

To read more about this configuration, check out <a href="{{siteBaseUrl}}/How_Tos/Scaffold_As_Types.html" class="">Scaffolding Types</a>
