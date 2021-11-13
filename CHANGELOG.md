# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [6.0.2] - 2021-11-13

### Fixed
- Fix compilation errors in scaffolded Many-to-Many joins - https://github.com/efcore/EFCore.FSharp/pull/119

## [6.0.1] - 2021-11-10

### Fixed
- Issue with generated records using Many-to-Many joins

## [6.0.0] - 2021-11-09

### Added
- Support EF Core 6.0
- Many-to-Many relationships now scaffolded without joining table
- Temporal tables in SQL Server supported

## [5.0.3] - 2021-10-16

### Fixed
- Fix formatting of Nullable parameters - [@LiteracyFanatic](https://github.com/LiteracyFanatic) - https://github.com/efcore/EFCore.FSharp/pull/106

### Added
- Translations for `isNull` to be evaluated in-database - https://github.com/efcore/EFCore.FSharp/pull/114
- Single case union support - [@lucasteles](https://github.com/lucasteles) - https://github.com/efcore/EFCore.FSharp/pull/98
- Query translation for Option types - [@lucasteles](https://github.com/lucasteles) - https://github.com/efcore/EFCore.FSharp/pull/93
- DbSet/IQueryable helpers to deal with EF Core async and nullable methods - [@lucasteles](https://github.com/lucasteles) - https://github.com/efcore/EFCore.FSharp/pull/94
- Automatic registration of DesignTimeServices - https://github.com/efcore/EFCore.FSharp/pull/86
- DbContextHelpers - curried functions for interacting with DbContext to allow for a more 'native' F# experience
[Unreleased]: https://github.com/efcore/EFCore.FSharp/compare/v6.0.2...HEAD
[6.0.2]: https://github.com/efcore/EFCore.FSharp/compare/v6.0.1...v6.0.2
[6.0.1]: https://github.com/efcore/EFCore.FSharp/compare/v6.0.0...v6.0.1
[6.0.0]: https://github.com/efcore/EFCore.FSharp/compare/v5.0.3...v6.0.0
