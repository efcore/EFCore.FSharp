namespace EntityFrameworkCore.FSharp

open System
open EntityFrameworkCore.FSharp.Translations
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Storage.ValueConversion

module Extensions =

    let private genericOptionConverterType = typedefof<OptionConverter<_>>
    let private genericSingleCaseUnionConverterType = typedefof<SingleCaseUnionConverter<_,_>>

    type ModelBuilder with

        member this.UseValueConverterForType<'a>(converter : ValueConverter) =
            this.UseValueConverterForType(typeof<'a>, converter)

        member this.UseValueConverterForType(``type`` : Type, converter : ValueConverter) =

            this.Model.GetEntityTypes()
            |> Seq.iter(fun e ->
                e.ClrType.GetProperties()
                |> Seq.filter(fun p -> p.PropertyType = ``type``)
                |> Seq.iter(fun p ->
                    this.Entity(e.Name).Property(p.Name).HasConversion(converter) |> ignore
                )
            )

            this

        member this.RegisterOptionTypes() =

            let makeOptionConverter t =
                let underlyingType = SharedTypeExtensions.unwrapOptionType t
                let converterType = genericOptionConverterType.MakeGenericType(underlyingType)
                let converter = converterType.GetConstructor([||]).Invoke([||]) :?> ValueConverter
                converter

            let converterDetails =
                this.Model.GetEntityTypes()
                |> Seq.filter (fun p -> not <| SharedTypeExtensions.isOptionType p.ClrType)
                |> Seq.collect (fun e -> e.ClrType.GetProperties())
                |> Seq.filter (fun p -> SharedTypeExtensions.isOptionType p.PropertyType)
                |> Seq.map(fun p -> (p, (makeOptionConverter p.PropertyType)) )

            for (prop, converter) in converterDetails do
                    this.Entity(prop.DeclaringType)
                        .Property(prop.PropertyType,prop.Name)
                        .HasConversion(converter)
                    |> ignore

        member this.RegisterSingleUnionCases() =
            let makeSingleUnionCaseConverter tUnion =
                let underlyingType = SharedTypeExtensions.unwrapSingleCaseUnion tUnion
                let converterType = genericSingleCaseUnionConverterType.MakeGenericType(underlyingType, tUnion)
                let converter = converterType.GetConstructor([||]).Invoke([||]) :?> ValueConverter
                converter

            let converterDetails =
                this.Model.GetEntityTypes()
                |> Seq.filter (fun p -> not <| SharedTypeExtensions.isSingleCaseUnion p.ClrType)
                |> Seq.collect (fun e -> e.ClrType.GetProperties())
                |> Seq.filter (fun p -> SharedTypeExtensions.isSingleCaseUnion p.PropertyType)
                |> Seq.map(fun p -> (p, (makeSingleUnionCaseConverter p.PropertyType)) )

            for (prop, converter) in converterDetails do
                    this.Entity(prop.DeclaringType)
                        .Property(prop.PropertyType,prop.Name)
                        .HasConversion(converter)
                    |> ignore

    let registerOptionTypes (modelBuilder : ModelBuilder) =
        modelBuilder.RegisterOptionTypes()

    let registerSingleCaseUnionTypes (modelBuilder : ModelBuilder) =
        modelBuilder.RegisterSingleUnionCases()

    let useValueConverter<'a> (converter : ValueConverter) (modelBuilder : ModelBuilder) =
        modelBuilder.UseValueConverterForType<'a>(converter)

    let useValueConverterForType (``type`` : Type) (converter : ValueConverter) (modelBuilder : ModelBuilder) =
        modelBuilder.UseValueConverterForType(``type``, converter)


    type DbContextOptionsBuilder with
        member this.UseFSharpTypes() =
            let extension =
                let finded = this.Options.FindExtension<FSharpTypeOptionsExtension>()
                if (box finded) <> null then finded else FSharpTypeOptionsExtension()


            (this :> IDbContextOptionsBuilderInfrastructure).AddOrUpdateExtension(extension)
            this
