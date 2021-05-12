# Getting Started

## Prerequisites
This guide assumes:

* You have the .NET 5.0 SDK installed
* You have created a project and added the `dotnet-ef` tool

## Installing the package
paket

    [lang=bash]
    paket install EntityFrameworkCore.FSharp

dotnet CLI

    [lang=bash]
    dotnet add package EntityFrameworkCore.FSharp

## That's it
Yes, really. Standard EF Core commands such as `dotnet ef migrations add` and `dotnet ef dbcontext scaffold` will now work as usual but the generated code will be in F#

## Advanced Options - Configure Design Time Services

The necessary design time services are automatically registered by EntityFrameworkCore.FSharp. Unless you have a specific need to do so, you should not need to register your own implementation of `IDesignTimeServices` just to add F# support.

An example of where you would need to, would be if you were scaffolding a new context from an existing database.

By default, scaffolded objects will be created as Record types objects where nullable columns of type `'a` are represented as types of `'a option`.

If you want to change the deault behaviour to generate nullable columns as properties of type `Nullable<'a>` or to crete your entities as a C#-type class object, check out <a href="{{siteBaseUrl}}/How_Tos/Scaffold_As_Types.html" class="">Scaffolding Types</a>
