# F# EF Core
Very rough upgrade to dot net core 3.1 of the good work done [here](https://github.com/bricelam/EFCore.FSharp).

## Known upgrade issues
- Generics have and some strings have `+` in names. This needs to be manually removed.
- Code generation failing in some circumstances...

## Getting started
- Add a reference to the `.nuget` package in the release folder.
- Run the usual `dotnet ef migrations...` command to add migrations.
- Add a reference to the generated files from the migration folder in the correct order.
