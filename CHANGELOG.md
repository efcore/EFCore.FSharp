# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

## [5.0.3-alpha10] - 2021-04-18

### Fixed
- Correct issue with InsertData operations and 2D arrays
- Generate valid code in HasData method
- Fix issue with generated DbContext members in scaffolded code
- Resolved issue with migrations have a missing unit arg to the "Up" method if no changes in model
- Foreign Key constraints now created correctly
- Fix indentation issue when table has constraints - https://github.com/efcore/EFCore.FSharp/pull/75
- Constraints now correctly generated - https://github.com/efcore/EFCore.FSharp/pull/72
- Generated Migrations will now always include the System namespace - https://github.com/efcore/EFCore.FSharp/pull/70

### Added
- DbContextHelpers - curried functions for interacting with DbContext to allow for a more 'native' F# experience
[Unreleased]: https://github.com/efcore/EFCore.FSharp/compare/v5.0.3-beta001...HEAD
[5.0.3-beta001]: https://github.com/efcore/EFCore.FSharp/releases/tag/v5.0.3-beta001
[5.0.3-alpha10]: https://github.com/efcore/EFCore.FSharp/releases/tag/v5.0.3-alpha10
