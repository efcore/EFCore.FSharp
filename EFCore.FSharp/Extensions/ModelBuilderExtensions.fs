namespace Bricelam.EntityFrameworkCore.FSharp

open System
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Storage.ValueConversion

module Extensions =

    let private genericOptionConverterType = typedefof<OptionConverter<_>>

    type ModelBuilder with

        member this.UseValueConverterForType<'a>(converter : ValueConverter) =
            this.UseValueConverterForType(typeof<'a>, converter)

        member this.UseValueConverterForType(``type`` : Type, converter : ValueConverter) =
            
            this.Model.GetEntityTypes()
            |> Seq.iter(fun e ->
                let properties =
                    e.ClrType.GetProperties()
                    |> Seq.filter(fun p ->                        
                        if SharedTypeExtensions.isOptionType p.PropertyType then
                            let underlyingType = SharedTypeExtensions.unwrapOptionType p.PropertyType
                            ``type`` = underlyingType
                        else
                            false)

                properties
                |> Seq.iter(fun p ->
                    this.Entity(e.Name).Property(p.Name).HasConversion(converter) |> ignore
                )
            )

            this

        member this.UseFSharp () =
            
            let registerOptionTypes () =
            
                let types =
                    this.Model.GetEntityTypes()
                    |> Seq.collect (fun e -> e.ClrType.GetProperties())
                    |> Seq.map (fun p -> p.PropertyType)
                    |> Seq.filter SharedTypeExtensions.isOptionType
                    
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
        modelBuilder.UseValueConverterForType<'a>(converter)

    let useValueConverterForType (``type`` : Type) (converter : ValueConverter) (modelBuilder : ModelBuilder) =
        modelBuilder.UseValueConverterForType(``type``, converter)            