# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- Removed `DevelopmentDependency=true` as it was setting `IncludeAssets` to undesirable values when restoring package - https://github.com/efcore/EFCore.FSharp/pull/108

## [5.0.3-beta005] - 2021-08-09

### Fixed
- Fix formatting of Nullable parameters - [@LiteracyFanatic](https://github.com/LiteracyFanatic) - https://github.com/efcore/EFCore.FSharp/pull/106
- Handle nullablility correctly in snapshot generation - https://github.com/efcore/EFCore.FSharp/pull/107

- Issues with scaffolded code
- Improved code generation for contexts scaffolded from an existing database - https://github.com/efcore/EFCore.FSharp/pull/89
- Correct issue with InsertData operations and 2D arrays
- Generate valid code in HasData method
- Fix issue with generated DbContext members in scaffolded code
- Resolved issue with migrations have a missing unit arg to the "Up" method if no changes in model
- Foreign Key constraints now created correctly
- Fix indentation issue when table has constraints - https://github.com/efcore/EFCore.FSharp/pull/75
- Constraints now correctly generated - https://github.com/efcore/EFCore.FSharp/pull/72
- Generated Migrations will now always include the System namespace - https://github.com/efcore/EFCore.FSharp/pull/70

### Added
- Single case union support - [@lucasteles](https://github.com/lucasteles) - https://github.com/efcore/EFCore.FSharp/pull/98
- Query translation for Option types - [@lucasteles](https://github.com/lucasteles) - https://github.com/efcore/EFCore.FSharp/pull/93
- DbSet/IQueryable helpers to deal with EF Core async and nullable methods - [@lucasteles](https://github.com/lucasteles) - https://github.com/efcore/EFCore.FSharp/pull/94
- Automatic registration of DesignTimeServices - https://github.com/efcore/EFCore.FSharp/pull/86
- DbContextHelpers - curried functions for interacting with DbContext to allow for a more 'native' F# experience

## [5.0.3-beta004] - 2021-06-16

### Added
- Single case union support - [@lucasteles](https://github.com/lucasteles) - https://github.com/efcore/EFCore.FSharp/pull/98

- Query translation for Option types - [@lucasteles](https://github.com/lucasteles) - https://github.com/efcore/EFCore.FSharp/pull/93
- DbSet/IQueryable helpers to deal with EF Core async and nullable methods - [@lucasteles](https://github.com/lucasteles) - https://github.com/efcore/EFCore.FSharp/pull/94
- Automatic registration of DesignTimeServices - https://github.com/efcore/EFCore.FSharp/pull/86
- DbContextHelpers - curried functions for interacting with DbContext to allow for a more 'native' F# experience

### Fixed
- Issues with scaffolded code

- Improved code generation for contexts scaffolded from an existing database - https://github.com/efcore/EFCore.FSharp/pull/89
- Correct issue with InsertData operations and 2D arrays
- Generate valid code in HasData method
- Fix issue with generated DbContext members in scaffolded code
- Resolved issue with migrations have a missing unit arg to the "Up" method if no changes in model
- Foreign Key constraints now created correctly
- Fix indentation issue when table has constraints - https://github.com/efcore/EFCore.FSharp/pull/75
- Constraints now correctly generated - https://github.com/efcore/EFCore.FSharp/pull/72
- Generated Migrations will now always include the System namespace - https://github.com/efcore/EFCore.FSharp/pull/70
[Unreleased]: https://github.com/efcore/EFCore.FSharp/compare/v5.0.3-beta005...HEAD
[5.0.3-beta005]: https://github.com/efcore/EFCore.FSharp/releases/tag/v5.0.3-beta005
