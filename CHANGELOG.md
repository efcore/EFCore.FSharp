# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- Generate valid code in HasData method

## [5.0.3-alpha7] - 2021-04-01

### Changed
- Simplify scaffolded classes using `member val`

### Fixed
- Resolved issue with migrations have a missing unit arg to the "Up" method if no changes in model
- Foreign Key constraints now created correctly

- Fix indentation issue when table has constraints - https://github.com/efcore/EFCore.FSharp/pull/75
- Constraints now correctly generated - https://github.com/efcore/EFCore.FSharp/pull/72
- Generated Migrations will now always include the System namespace - https://github.com/efcore/EFCore.FSharp/pull/70
- Removed unneeded dependency
- Link to NuGet badges in README.md

### Added
- DbContextHelpers - curried functions for interacting with DbContext to allow for a more 'native' F# experience
- Initial release
- F# migrations
- F# scaffolding

## [5.0.3-alpha6] - 2021-04-01

### Fixed
- Fix indentation issue when table has constraints - https://github.com/efcore/EFCore.FSharp/pull/75

## [5.0.3-alpha5] - 2021-03-31

### Fixed
- Constraints now correctly generated - https://github.com/efcore/EFCore.FSharp/pull/72

## [5.0.3-alpha3] - 2021-03-31

### Fixed
- Generated Migrations will now always include the System namespace - https://github.com/efcore/EFCore.FSharp/pull/70

## [5.0.3-alpha2] - 2021-03-27

### Added
- DbContextHelpers - curried functions for interacting with DbContext to allow for a more 'native' F# experience

### Fixed
- Removed unneeded dependency
- Link to NuGet badges in README.md

## [5.0.3-alpha1] - 2021-03-21

### Added
- Initial release
- F# migrations
- F# scaffolding
[Unreleased]: https://github.com/efcore/EFCore.FSharp/compare/v5.0.3-alpha7...HEAD
[5.0.3-alpha7]: https://github.com/efcore/EFCore.FSharp/releases/tag/v5.0.3-alpha7
