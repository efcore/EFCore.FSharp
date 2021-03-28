namespace EntityFrameworkCore.FSharp

open System
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Storage.ValueConversion
open EntityFrameworkCore.FSharp.ValueConverters

module Extensions =

    let private makeConverter<'a> typeToConvert =
        let converterType' = typeof<'a>.MakeGenericType([|typeToConvert|])
        let converter = converterType'.GetConstructor([||]).Invoke([||]) :?> ValueConverter
        converter

    let private registerTypes findType (makeConverter: Type -> ValueConverter) (mb: ModelBuilder) =
        let converterDetails =
            mb.Model.GetEntityTypes()
            |> Seq.collect (fun e -> e.GetProperties())
            |> Seq.filter (fun p -> findType p.ClrType)
            |> Seq.map(fun p -> (p.Name, p.DeclaringType.Name, (makeConverter p.ClrType)) )
            
        converterDetails
        |> Seq.iter(fun (propName, entityName, converter) ->
            mb.Entity(entityName).Property(propName).HasConversion(converter) |> ignore
        )

        mb

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
            registerTypes SharedTypeExtensions.isOptionType (SharedTypeExtensions.unwrapOptionType >> makeConverter<OptionConverter<_>>) this

        member this.RegisterEnumLikeUnionTypes() =
            registerTypes SharedTypeExtensions.isEnumUnionType makeConverter<EnumLikeUnionConverter<_>> this

    let registerOptionTypes (modelBuilder : ModelBuilder) =
        modelBuilder.RegisterOptionTypes()

    let useValueConverter<'a> (converter : ValueConverter) (modelBuilder : ModelBuilder) =
        modelBuilder.UseValueConverterForType<'a>(converter)

    let useValueConverterForType (``type`` : Type) (converter : ValueConverter) (modelBuilder : ModelBuilder) =
        modelBuilder.UseValueConverterForType(``type``, converter)            

    let registerEnumLikeUnionTypes (modelBuilder : ModelBuilder) =
        modelBuilder.RegisterEnumLikeUnionTypes()
