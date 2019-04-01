namespace Bricelam.EntityFrameworkCore.FSharp

open System
open System.Collections.Generic
open System.Reflection
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Storage.ValueConversion

module Extensions =

    let private genericOptionType = typedefof<Option<_>>
    let private genericOptionConverterType = typedefof<OptionConverter<_>>

    type ModelBuilder with

        member this.UseValueConverterForType<'a>(converter : ValueConverter) =
            this.UseValueConverterForType(typeof<'a>, converter)

        member this.UseValueConverterForType(``type`` : Type, converter : ValueConverter) =
            
            let underlyingType = ``type`` |> SharedTypeExtensions.unwrapOptionType
            
            this.Model.GetEntityTypes()
            |> Seq.iter(fun e ->
                let properties =
                    e.ClrType.GetProperties()
                    |> Seq.filter(fun p ->
                        let t = p.PropertyType
                        SharedTypeExtensions.isOptionType t && SharedTypeExtensions.unwrapOptionType t = underlyingType)

                properties
                |> Seq.iter(fun p ->
                    this.Entity(e.Name).Property(p.Name).HasConversion(converter) |> ignore
                )
            )

            this

        member this.UseFSharp () =
            
            let registerOptionTypes () =
            
                let filterOptionalProperties (p : PropertyInfo) =
                    SharedTypeExtensions.isOptionType p.PropertyType

                let types =
                    this.Model.GetEntityTypes()
                    |> Seq.collect (fun e -> e.ClrType.GetProperties())
                    |> Seq.filter filterOptionalProperties
                    |> Seq.map (fun p -> p.PropertyType)

                types
                |> Seq.iter(fun t ->

                    let underlyingType = SharedTypeExtensions.unwrapOptionType t

                    let converterType = genericOptionConverterType.MakeGenericType(underlyingType)
                    let converter = converterType.GetConstructor([||]).Invoke([||]) :?> ValueConverter

                    this.UseValueConverterForType(t, converter) |> ignore
                )

            registerOptionTypes ()            

    let useFSharp (modelBuilder : ModelBuilder) =
        modelBuilder.UseFSharp()

    let useValueConverter<'a> (converter : ValueConverter) (modelBuilder : ModelBuilder) =
        modelBuilder.UseValueConverterForType(converter)

    let useValueConverterForType (``type`` : Type) (converter : ValueConverter) (modelBuilder : ModelBuilder) =
        modelBuilder.UseValueConverterForType(``type``, converter)            