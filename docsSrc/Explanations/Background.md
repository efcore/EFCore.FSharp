# Background

Every introduction to .NET Core onwards will usually start by suggesting [EntityFramework Core](https://docs.microsoft.com/en-us/ef/core/) as the go-to choice for data access.

Unfortunately EF Core will output all generated code in C# and C# alone. While it is the most popular .NET language, it it not the *only* .NET language.

This project brings support for F# to EF Core, allowing for database scaffolding, migrations to be created in F#.

## Why not Dapper, or SQLProvider, or .....?

If that's what you want to use, please do! The sheer scale of database access libraries in .NET and in F# is one of the great strengths of the community.

But as EF Core is one of the most widely known ORM solutions in the .NET world, it makes sense that it should support various .NET languages. That's what this project is for.
