# EFCore.FSharp
Adds F# design-time support to EF Core

[![AppVeyor build status ](https://ci.appveyor.com/api/projects/status/joy15u99gu69fg1l/branch/master?svg=true)](https://ci.appveyor.com/project/bricelam/efcore-fsharp)
[![Travis CI build status](https://travis-ci.org/bricelam/EFCore.FSharp.svg?branch=master)](https://travis-ci.org/bricelam/EFCore.FSharp)

## Install
Note: Only supports EF Core 2.2. Later versions of EF Core will fail with error: 
`System.MissingMethodException: Method not found: 'System.String Microsoft.EntityFrameworkCore.Design.ICSharpHelper.Literal(Byte[])'.`

- Install [Fake](http://fake.build/fake-gettingstarted.html).
- Run `build.cmd`/`build.sh`.
- Add reference to nuget package in EFCore.FSharp/bin/Release.

Migrations must be manually added to the solution in the correct order.
