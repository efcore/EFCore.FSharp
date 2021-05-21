namespace EntityFrameworkCore.FSharp

open System
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Storage.ValueConversion
open System.Runtime.CompilerServices

module Extensions =

    let private genericOptionConverterType = typedefof<OptionConverter<_>>

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

    let registerOptionTypes (modelBuilder : ModelBuilder) =
        modelBuilder.RegisterOptionTypes()

    let useValueConverter<'a> (converter : ValueConverter) (modelBuilder : ModelBuilder) =
        modelBuilder.UseValueConverterForType<'a>(converter)

    let useValueConverterForType (``type`` : Type) (converter : ValueConverter) (modelBuilder : ModelBuilder) =
        modelBuilder.UseValueConverterForType(``type``, converter)
