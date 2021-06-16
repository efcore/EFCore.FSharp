# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Single case union support - [@lucasteles](https://github.com/lucasteles) - https://github.com/efcore/EFCore.FSharp/pull/98

### Fixed
- Issues with scaffolded code

## [5.0.3-beta003] - 2021-05-27

### Added
- Query translation for Option types - [@lucasteles](https://github.com/lucasteles) - https://github.com/efcore/EFCore.FSharp/pull/93
- DbSet/IQueryable helpers to deal with EF Core async and nullable methods - [@lucasteles](https://github.com/lucasteles) - https://github.com/efcore/EFCore.FSharp/pull/94

- Automatic registration of DesignTimeServices - https://github.com/efcore/EFCore.FSharp/pull/86
- DbContextHelpers - curried functions for interacting with DbContext to allow for a more 'native' F# experience
[Unreleased]: https://github.com/efcore/EFCore.FSharp/compare/v5.0.3-beta002...HEAD
[5.0.3-beta002]: https://github.com/efcore/EFCore.FSharp/releases/tag/v5.0.3-beta002
[5.0.3-beta001]: https://github.com/efcore/EFCore.FSharp/releases/tag/v5.0.3-beta001

### Fixed
- Improved code generation for contexts scaffolded from an existing database - https://github.com/efcore/EFCore.FSharp/pull/89
- Correct issue with InsertData operations and 2D arrays
- Generate valid code in HasData method
- Fix issue with generated DbContext members in scaffolded code
- Resolved issue with migrations have a missing unit arg to the "Up" method if no changes in model
- Foreign Key constraints now created correctly
- Fix indentation issue when table has constraints - https://github.com/efcore/EFCore.FSharp/pull/75
- Constraints now correctly generated - https://github.com/efcore/EFCore.FSharp/pull/72
- Generated Migrations will now always include the System namespace - https://github.com/efcore/EFCore.FSharp/pull/70

## [5.0.3-beta002] - 2021-05-12

### Fixed
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
- Automatic registration of DesignTimeServices - https://github.com/efcore/EFCore.FSharp/pull/86
- DbContextHelpers - curried functions for interacting with DbContext to allow for a more 'native' F# experience
[Unreleased]: https://github.com/efcore/EFCore.FSharp/compare/v5.0.3-beta001...HEAD
[5.0.3-beta001]: https://github.com/efcore/EFCore.FSharp/releases/tag/v5.0.3-beta001

## [5.0.3-beta001] - 2021-05-08

### Added
- Automatic registration of DesignTimeServices - https://github.com/efcore/EFCore.FSharp/pull/86

- DbContextHelpers - curried functions for interacting with DbContext to allow for a more 'native' F# experience
[Unreleased]: https://github.com/efcore/EFCore.FSharp/compare/v5.0.3-alpha10...HEAD
[5.0.3-alpha10]: https://github.com/efcore/EFCore.FSharp/releases/tag/v5.0.3-alpha10

### Fixed
- Correct issue with InsertData operations and 2D arrays
- Generate valid code in HasData method
- Fix issue with generated DbContext members in scaffolded code
- Resolved issue with migrations have a missing unit arg to the "Up" method if no changes in model
- Foreign Key constraints now created correctly
- Fix indentation issue when table has constraints - https://github.com/efcore/EFCore.FSharp/pull/75
- Constraints now correctly generated - https://github.com/efcore/EFCore.FSharp/pull/72
- Generated Migrations will now always include the System namespace - https://github.com/efcore/EFCore.FSharp/pull/70
