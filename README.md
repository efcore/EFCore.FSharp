# F# EF Core
Very rough upgrade to dot net core 3.1 of the good work done [here](https://github.com/bricelam/EFCore.FSharp).

## Known upgrade issues
- Generics have a `+` in names. This needs to be manually removed.
- Failing unit test in `FSharpMigrationsGeneratorTest`. There was significant change from EF 2.2 -> 3.1 in the MigrationsGenerator that made it non trivial to update. This may cause some odd behaviour. 

## Getting started
- Add a reference to the `.nuget` package in the release folder.
- Run the usual `dotnet ef migrations...` command to add migrations.
- Add a reference to the generated files from the migration folder in the correct order.
